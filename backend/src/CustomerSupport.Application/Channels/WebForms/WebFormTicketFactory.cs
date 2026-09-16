using System.Globalization;
using CustomerSupport.Application.Common;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Channels.WebForms;

internal record MappedWebFormValues(
    string? Email, string? Phone, string? Name, string? Subject, string? Description, string? CategoryCode, string? PriorityCode);

/// <summary>
/// Builds a ticket from a web-form submission's stored payload. Shared by
/// <see cref="SubmitWebFormCommandHandler"/> (the first attempt) and
/// <see cref="WebFormSubmissionRetryService"/> (a later retry from the same stored payload) — the
/// same logic either way, so a fixed root cause resolves identically whichever path re-runs it.
/// </summary>
public interface IWebFormTicketFactory
{
    Task<(Guid TicketId, string TicketNumber)> CreateAsync(
        WebFormDefinition form, IReadOnlyList<WebFormField> fields, IReadOnlyDictionary<string, string> values, CancellationToken ct);
}

public class WebFormTicketFactory(
    IAppDbContext db,
    IReferenceNumberGenerator numbers,
    ITicketEventRecorder events,
    IInteractionRecorder interactions,
    ISlaEngine sla,
    IAssignmentEngine assignment,
    IDateTimeProvider clock,
    ILogger<WebFormTicketFactory> logger) : IWebFormTicketFactory
{
    public async Task<(Guid TicketId, string TicketNumber)> CreateAsync(
        WebFormDefinition form, IReadOnlyList<WebFormField> fields, IReadOnlyDictionary<string, string> values, CancellationToken ct)
    {
        var mapped = ExtractMapped(fields, values);
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        var customer = await ResolveCustomerAsync(mapped.Email, mapped.Phone, mapped.Name, language, ct);
        var category = await ResolveCategoryAsync(form, mapped.CategoryCode, ct);
        var priorityId = await ResolvePriorityIdAsync(form, category, mapped.PriorityCode, ct);
        var departmentId = await ResolveDepartmentIdAsync(form, category, customer.BranchId ?? form.BranchId, ct);

        var description = BuildDescription(fields, values, mapped.Description, language);
        var subject = !string.IsNullOrWhiteSpace(mapped.Subject)
            ? mapped.Subject!
            : (string.IsNullOrWhiteSpace(form.Title.En) && string.IsNullOrWhiteSpace(form.Title.Ar)
                ? "Web form submission"
                : form.Title.For(language));

        var status = await db.TicketStatuses.FirstOrDefaultAsync(s => s.IsDefault, ct)
            ?? throw new InvalidOperationException("No default ticket status is configured.");

        var ticket = new Ticket
        {
            Number = await numbers.NextTicketNumberAsync(ct),
            CustomerId = customer.Id,
            BranchId = customer.BranchId ?? form.BranchId,
            Subject = subject,
            Description = description,
            Language = customer.PreferredLanguage,
            CategoryId = category.Id,
            PriorityId = priorityId,
            StatusId = status.Id,
            Channel = ChannelKey.WebForm,
            DepartmentId = departmentId,
        };

        db.Tickets.Add(ticket);

        ticket.Messages.Add(new TicketMessage
        {
            TicketId = ticket.Id,
            Channel = ChannelKey.WebForm,
            Direction = MessageDirection.Inbound,
            AuthorType = MessageAuthorType.Customer,
            AuthorId = customer.Id,
            AuthorDisplayName = customer.DisplayName.For(customer.PreferredLanguage),
            Subject = subject,
            BodyText = description,
            SentAt = clock.UtcNow,
        });

        events.Record(ticket.Id, TicketEventType.Created);
        interactions.Record(
            customer.Id, ChannelKey.WebForm, MessageDirection.Inbound,
            subject, Truncate(description), ticket.Id, nameof(Ticket), ticket.Id);

        await db.SaveChangesAsync(ct);

        // After the save, so the engines read committed state — same ordering every other
        // ticket-creation path (CreateTicketCommand, the inbound pipeline, chat promotion) uses.
        await sla.ApplyPolicyAsync(ticket.Id, ct);

        try
        {
            await assignment.AssignAsync(ticket.Id, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Automatic assignment failed for web-form ticket {TicketId}.", ticket.Id);
        }

        return (ticket.Id, ticket.Number);
    }

    private static MappedWebFormValues ExtractMapped(IReadOnlyList<WebFormField> fields, IReadOnlyDictionary<string, string> values)
    {
        string? Get(string mapTo) => fields
            .Where(f => f.MapTo == mapTo)
            .Select(f => values.GetValueOrDefault(f.Key))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        return new MappedWebFormValues(
            Get("customerEmail"), Get("customerPhone"), Get("customerName"),
            Get("subject"), Get("description"), Get("categoryCode"), Get("priorityCode"));
    }

    private static string BuildDescription(
        IReadOnlyList<WebFormField> fields, IReadOnlyDictionary<string, string> values, string? mappedDescription, string language)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(mappedDescription))
        {
            lines.Add(mappedDescription);
        }

        // Unmapped fields become a labelled list. Values are untrusted, but the description is
        // always rendered as plain text (never innerHTML) downstream — the same defence CS-301's
        // inbound pipeline and CS-303's chat both rely on, so no escaping is needed here either.
        foreach (var field in fields)
        {
            if (field.MapTo is not null)
            {
                continue;
            }

            if (!values.TryGetValue(field.Key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var label = language.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? field.LabelAr : field.LabelEn;
            lines.Add($"**{label}:** {value}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Matches an existing customer by email or phone (normalised), creating a minimal profile
    /// otherwise — the same "never drop the request" rule <c>InboundMessagePipeline.ResolveCustomerAsync</c>
    /// follows, adapted for explicit mapped fields instead of a single "from address".
    /// </summary>
    private async Task<Customer> ResolveCustomerAsync(string? email, string? phone, string? name, string language, CancellationToken ct)
    {
        var normalizedEmail = ContactNormalizer.Normalize(email);
        var normalizedPhone = ContactNormalizer.Normalize(phone);

        CustomerContact? existing = null;
        if (normalizedEmail is not null)
        {
            existing = await db.CustomerContacts.Include(c => c.Customer)
                .FirstOrDefaultAsync(c => c.NormalizedValue == normalizedEmail, ct);
        }

        if (existing is null && normalizedPhone is not null)
        {
            existing = await db.CustomerContacts.Include(c => c.Customer)
                .FirstOrDefaultAsync(c => c.NormalizedValue == normalizedPhone, ct);
        }

        if (existing is not null)
        {
            return existing.Customer;
        }

        var displayName = !string.IsNullOrWhiteSpace(name) ? name : (email ?? phone ?? "Web form visitor");
        var customer = new Customer
        {
            Code = await numbers.NextCustomerCodeAsync(ct),
            DisplayName = new LocalizedText(displayName, displayName),
            PreferredLanguage = language,
            PreferredChannel = ChannelKey.WebForm,
            PrimaryEmail = email,
            PrimaryPhone = phone,
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            customer.Contacts.Add(new CustomerContact
            {
                Type = ContactType.Email, Value = email, NormalizedValue = normalizedEmail ?? email.ToLowerInvariant(),
                Label = displayName, IsPrimary = true, IsVerified = false,
            });
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            customer.Contacts.Add(new CustomerContact
            {
                Type = ContactType.Mobile, Value = phone, NormalizedValue = normalizedPhone ?? phone,
                Label = displayName, IsPrimary = string.IsNullOrWhiteSpace(email), IsVerified = false,
            });
        }

        db.Customers.Add(customer);
        return customer;
    }

    /// <summary>
    /// A mapped code must resolve to a portal-visible, active category — never trust the client's
    /// declared `select` options alone, since the public endpoint can be called directly with any
    /// code. Falls back to the form's configured default, then the oldest active portal-visible one.
    /// </summary>
    private async Task<TicketCategory> ResolveCategoryAsync(WebFormDefinition form, string? categoryCode, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(categoryCode))
        {
            var mapped = await db.TicketCategories
                .FirstOrDefaultAsync(c => c.Code == categoryCode && c.IsActive && c.IsVisibleInPortal, ct);
            if (mapped is not null)
            {
                return mapped;
            }
        }

        if (form.DefaultCategoryId is { } defaultId)
        {
            var configured = await db.TicketCategories.FirstOrDefaultAsync(c => c.Id == defaultId && c.IsActive, ct);
            if (configured is not null)
            {
                return configured;
            }
        }

        return await db.TicketCategories.Where(c => c.IsActive && c.IsVisibleInPortal).OrderBy(c => c.CreatedAt).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("No active, portal-visible ticket category is configured.");
    }

    private async Task<Guid> ResolvePriorityIdAsync(WebFormDefinition form, TicketCategory category, string? priorityCode, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(priorityCode))
        {
            var mapped = await db.TicketPriorities.FirstOrDefaultAsync(p => p.Code == priorityCode && p.IsActive, ct);
            if (mapped is not null)
            {
                return mapped.Id;
            }
        }

        if (form.DefaultPriorityId is { } formDefault)
        {
            return formDefault;
        }

        if (category.DefaultPriorityId is { } categoryDefault)
        {
            return categoryDefault;
        }

        var defaultPriority = await db.TicketPriorities.FirstOrDefaultAsync(p => p.IsDefault, ct)
            ?? throw new InvalidOperationException("No default ticket priority is configured.");
        return defaultPriority.Id;
    }

    /// <summary>The same cascading department default every other anonymous ticket-creation path uses.</summary>
    private async Task<Guid?> ResolveDepartmentIdAsync(WebFormDefinition form, TicketCategory category, Guid? branchId, CancellationToken ct)
    {
        if (form.DefaultDepartmentId is { } formDept)
        {
            return formDept;
        }

        if (category.DefaultDepartmentId is { } categoryDept)
        {
            return categoryDept;
        }

        var department = await db.Departments.Where(d => d.IsActive && d.BranchId == branchId)
            .OrderBy(d => d.CreatedAt).FirstOrDefaultAsync(ct);
        department ??= await db.Departments.Where(d => d.IsActive).OrderBy(d => d.CreatedAt).FirstOrDefaultAsync(ct);
        return department?.Id;
    }

    private static string Truncate(string text, int maxLength = 280) =>
        text.Length <= maxLength ? text : text[..maxLength] + "…";
}
