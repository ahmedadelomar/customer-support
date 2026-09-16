using System.Text.RegularExpressions;
using CustomerSupport.Application.Channels.Outbound;
using CustomerSupport.Application.Common;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Files;
using CustomerSupport.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Channels.Inbound;

/// <summary>
/// The shared inbound pipeline every channel adapter funnels through (Communication Channels /
/// Email channel, story CS-301 — built general enough for WhatsApp, SMS and web forms to reuse
/// unchanged). Lives in Application, not Infrastructure: everything it needs (<see cref="IAppDbContext"/>,
/// the recorders, the SLA/assignment engines) is already an Application-layer abstraction, and it
/// runs from an anonymous webhook request with no <c>ICurrentUser</c> — the same reason
/// <c>EscalationEngine</c> mutates tickets directly rather than dispatching the permission-gated
/// <c>CreateTicketCommand</c>/<c>RecordInboundCustomerMessageCommand</c> through MediatR.
/// </summary>
public partial class InboundMessagePipeline(
    IAppDbContext db,
    IReferenceNumberGenerator numbers,
    ITicketEventRecorder events,
    IInteractionRecorder interactions,
    ISlaEngine sla,
    IAssignmentEngine assignment,
    IAttachmentPolicyProvider attachmentPolicy,
    IFileStorage fileStorage,
    IDateTimeProvider clock,
    ILogger<InboundMessagePipeline> logger) : IInboundMessagePipeline
{
    [GeneratedRegex(@"\bTCK-\d{4}-\d{6}\b")]
    private static partial Regex TicketNumberPattern();

    public async Task<Guid?> IngestAsync(InboundMessage message, CancellationToken ct = default)
    {
        var account = await db.ChannelAccounts.FirstOrDefaultAsync(a => a.Id == message.ChannelAccountId, ct)
            ?? throw new NotFoundException(nameof(ChannelAccount), message.ChannelAccountId);

        var customer = await ResolveCustomerAsync(message, ct);
        var (ticket, isNewTicket) = await ResolveTicketAsync(message, account, customer, ct);

        var ticketMessage = new TicketMessage
        {
            TicketId = ticket.Id,
            Channel = message.Channel,
            Direction = MessageDirection.Inbound,
            AuthorType = MessageAuthorType.Customer,
            AuthorId = customer.Id,
            AuthorDisplayName = message.FromDisplayName ?? customer.DisplayName.For(customer.PreferredLanguage),
            Subject = message.Subject,
            BodyText = message.BodyText,
            BodyHtml = message.BodyHtml,
            ExternalMessageId = message.ExternalMessageId,
            InReplyToExternalId = message.InReplyToExternalId,
            SentAt = message.SentAt,
        };
        db.TicketMessages.Add(ticketMessage);

        await AttachAttachmentsAsync(ticketMessage, message.Attachments, ct);

        if (isNewTicket)
        {
            events.Record(ticket.Id, TicketEventType.Created);
        }
        else
        {
            events.Record(ticket.Id, TicketEventType.MessageAdded,
                field: nameof(TicketMessage.Direction), newValue: nameof(MessageDirection.Inbound));
            ticket.CustomerReplyCount += 1;
        }

        ticket.LastCustomerReplyAt = message.SentAt;

        interactions.Record(
            customer.Id, message.Channel, MessageDirection.Inbound,
            message.Subject ?? ticket.Subject, Truncate(message.BodyText), ticket.Id, nameof(Ticket), ticket.Id);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Insert-and-catch, not check-then-insert: a concurrent redelivery of the same provider
            // message races a check, but the unique index on ExternalMessageId cannot be raced.
            var alreadyIngested = await db.TicketMessages.AsNoTracking()
                .AnyAsync(m => m.ExternalMessageId == message.ExternalMessageId, ct);

            if (alreadyIngested)
            {
                logger.LogInformation("Duplicate inbound message {ExternalMessageId} ignored.", message.ExternalMessageId);
                return null;
            }

            throw;
        }

        if (isNewTicket)
        {
            // After the save, so the engines read committed state — same ordering CreateTicketCommand uses.
            await sla.ApplyPolicyAsync(ticket.Id, ct);

            try
            {
                await assignment.AssignAsync(ticket.Id, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Automatic assignment failed for ticket {TicketId}.", ticket.Id);
            }

            if (account.SendAutoReply && !IsAutomated(message.Headers))
            {
                QueueAutoAcknowledgement(ticket, message, account, customer);
                await db.SaveChangesAsync(ct);
            }
        }

        return ticket.Id;
    }

    /// <summary>
    /// Mail-loop protection. Without this, replying to an automated sender (another ticketing
    /// system, a mailing list, a bounce processor) can trigger an infinite exchange of
    /// auto-responders — this is the one check that stops it.
    /// </summary>
    private static bool IsAutomated(IReadOnlyDictionary<string, string> headers) =>
        (headers.TryGetValue("Auto-Submitted", out var auto) && !auto.Equals("no", StringComparison.OrdinalIgnoreCase))
        || (headers.TryGetValue("Precedence", out var precedence) && precedence is "bulk" or "list" or "junk")
        || headers.ContainsKey("List-Id")
        || headers.ContainsKey("X-Auto-Response-Suppress");

    private async Task<Customer> ResolveCustomerAsync(InboundMessage message, CancellationToken ct)
    {
        var normalized = ContactNormalizer.Normalize(message.FromAddress);

        var existing = await db.CustomerContacts
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.NormalizedValue == normalized, ct);

        if (existing is not null)
        {
            return existing.Customer;
        }

        // An unknown sender is never a reason to drop a message — create a minimal profile instead.
        var displayName = message.FromDisplayName ?? message.FromAddress;
        var customer = new Customer
        {
            Code = await numbers.NextCustomerCodeAsync(ct),
            DisplayName = new LocalizedText(displayName, displayName),
            PreferredLanguage = "ar",
            PreferredChannel = message.Channel,
        };

        if (message.Channel == ChannelKey.Email)
        {
            customer.PrimaryEmail = message.FromAddress;
        }
        else
        {
            customer.PrimaryPhone = message.FromAddress;
        }

        customer.Contacts.Add(new CustomerContact
        {
            Type = ContactTypeFor(message.Channel),
            Value = message.FromAddress,
            NormalizedValue = normalized ?? message.FromAddress.ToLowerInvariant(),
            Label = displayName,
            IsPrimary = true,
            // An inbound message proves the address is reachable, but that is not the same as the
            // deliberate verification flow CS-102 defines — leave it unverified.
            IsVerified = false,
        });

        db.Customers.Add(customer);
        return customer;
    }

    private static ContactType ContactTypeFor(ChannelKey channel) => channel switch
    {
        ChannelKey.Email => ContactType.Email,
        ChannelKey.WhatsApp => ContactType.WhatsApp,
        ChannelKey.Sms => ContactType.Mobile,
        _ => ContactType.Other,
    };

    private async Task<(Ticket Ticket, bool IsNewTicket)> ResolveTicketAsync(
        InboundMessage message, ChannelAccount account, Customer customer, CancellationToken ct)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(message.InReplyToExternalId))
        {
            candidates.Add(message.InReplyToExternalId);
        }
        candidates.AddRange(message.ReferenceExternalIds.Where(r => !string.IsNullOrWhiteSpace(r)));

        Guid? ticketId = null;

        // 1. RFC 5322 threading headers — the reliable signal.
        if (candidates.Count > 0)
        {
            ticketId = await db.TicketMessages
                .Where(m => m.ExternalMessageId != null && candidates.Contains(m.ExternalMessageId))
                .Select(m => (Guid?)m.TicketId)
                .FirstOrDefaultAsync(ct);
        }

        // 2. Fall back to a ticket number quoted in the subject. Never the subject text alone —
        //    "Re: Question" from a customer with two open tickets must not merge them.
        if (ticketId is null && message.Subject is { } subject)
        {
            var match = TicketNumberPattern().Match(subject);
            if (match.Success)
            {
                ticketId = await db.Tickets
                    .Where(t => t.Number == match.Value)
                    .Select(t => (Guid?)t.Id)
                    .FirstOrDefaultAsync(ct);
            }
        }

        if (ticketId is { } id)
        {
            var existing = await db.Tickets.Include(t => t.Status).FirstAsync(t => t.Id == id, ct);

            if (existing.Status.IsTerminal)
            {
                // Closed/cancelled: reopening weeks later would distort resolution-time reporting.
                var followUp = await CreateFollowUpTicketAsync(existing, message, account, ct);
                events.Record(existing.Id, TicketEventType.FollowUpCreated,
                    field: nameof(Ticket.Id), newValue: followUp.Id.ToString(), newDisplay: followUp.Number);
                return (followUp, true);
            }

            if (existing.Status.Kind == TicketStatusKind.Resolved)
            {
                await ReopenAsync(existing, ct);
            }

            return (existing, false);
        }

        return (await CreateNewTicketAsync(message, account, customer, ct), true);
    }

    private async Task ReopenAsync(Ticket ticket, CancellationToken ct)
    {
        var openStatus = await db.TicketStatuses
            .Where(s => s.Kind == TicketStatusKind.Open)
            .OrderBy(s => s.DisplayOrder)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("No Open-kind ticket status is configured.");

        var oldStatus = ticket.Status;

        ticket.StatusId = openStatus.Id;
        ticket.ReopenCount += 1;
        ticket.ResolvedAt = null;
        ticket.ResolvedById = null;

        events.Record(ticket.Id, TicketEventType.Reopened,
            field: nameof(Ticket.StatusId),
            oldValue: oldStatus.Id.ToString(), newValue: openStatus.Id.ToString(),
            oldDisplay: oldStatus.Name.For(ticket.Language), newDisplay: openStatus.Name.For(ticket.Language),
            triggeredByRule: "inbound-message");
    }

    private async Task<Ticket> CreateNewTicketAsync(InboundMessage message, ChannelAccount account, Customer customer, CancellationToken ct)
    {
        var categoryId = account.DefaultCategoryId ?? await DefaultCategoryIdAsync(ct);
        var category = await db.TicketCategories.FirstAsync(c => c.Id == categoryId, ct);
        var status = await db.TicketStatuses.FirstOrDefaultAsync(s => s.IsDefault, ct)
            ?? throw new InvalidOperationException("No default ticket status is configured.");

        var priorityId = account.DefaultPriorityId ?? category.DefaultPriorityId ?? await DefaultPriorityIdAsync(ct);
        var branchId = customer.BranchId ?? account.BranchId;
        var departmentId = account.DefaultDepartmentId ?? category.DefaultDepartmentId ?? await DefaultDepartmentIdAsync(branchId, ct);

        var ticket = new Ticket
        {
            Number = await numbers.NextTicketNumberAsync(ct),
            CustomerId = customer.Id,
            BranchId = branchId,
            Subject = string.IsNullOrWhiteSpace(message.Subject) ? "(no subject)" : message.Subject,
            Description = message.BodyText,
            Language = customer.PreferredLanguage,
            CategoryId = category.Id,
            PriorityId = priorityId,
            StatusId = status.Id,
            Channel = message.Channel,
            ChannelAccountId = account.Id,
            DepartmentId = departmentId,
        };

        db.Tickets.Add(ticket);
        return ticket;
    }

    private async Task<Ticket> CreateFollowUpTicketAsync(Ticket original, InboundMessage message, ChannelAccount account, CancellationToken ct)
    {
        var defaultStatus = await db.TicketStatuses.FirstAsync(s => s.IsDefault, ct);
        var truncatedSubject = Truncate(message.Subject ?? original.Subject, 490);
        var followUpSubject = truncatedSubject.StartsWith("Re: ", StringComparison.OrdinalIgnoreCase)
            ? truncatedSubject
            : $"Re: {truncatedSubject}";

        var followUp = new Ticket
        {
            Number = await numbers.NextTicketNumberAsync(ct),
            CustomerId = original.CustomerId,
            BranchId = original.BranchId,
            Subject = followUpSubject,
            Description = message.BodyText,
            Language = original.Language,
            CategoryId = original.CategoryId,
            PriorityId = original.PriorityId,
            StatusId = defaultStatus.Id,
            Channel = message.Channel,
            ChannelAccountId = account.Id,
            DepartmentId = original.DepartmentId,
        };

        db.Tickets.Add(followUp);
        return followUp;
    }

    /// <summary>
    /// The "system default department" stand-in CreateTicketCommand also uses: the oldest active
    /// department for the branch, falling back to the oldest active department in any branch.
    /// Department has no explicit default flag yet (CS-1203's own scope).
    /// </summary>
    private async Task<Guid?> DefaultDepartmentIdAsync(Guid? branchId, CancellationToken ct)
    {
        var department = await db.Departments
            .Where(d => d.IsActive && d.BranchId == branchId)
            .OrderBy(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);

        department ??= await db.Departments
            .Where(d => d.IsActive)
            .OrderBy(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return department?.Id;
    }

    private async Task<Guid> DefaultPriorityIdAsync(CancellationToken ct)
    {
        var priority = await db.TicketPriorities.FirstOrDefaultAsync(p => p.IsDefault, ct)
            ?? throw new InvalidOperationException("No default ticket priority is configured.");
        return priority.Id;
    }

    private async Task<Guid> DefaultCategoryIdAsync(CancellationToken ct)
    {
        var category = await db.TicketCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("No active ticket category is configured, and the channel account sets no default.");
        return category.Id;
    }

    /// <summary>
    /// Extracts attachments up to the configured policy. An oversized or disallowed attachment is
    /// skipped with a note rather than failing the whole ingestion — losing the message body over
    /// one bad attachment is worse than losing that one file.
    /// </summary>
    private async Task AttachAttachmentsAsync(TicketMessage message, IReadOnlyList<InboundAttachment> attachments, CancellationToken ct)
    {
        if (attachments.Count == 0)
        {
            return;
        }

        var policy = await attachmentPolicy.GetPolicyAsync(ct);
        var skippedNotes = new List<string>();
        var savedCount = 0;

        foreach (var attachment in attachments)
        {
            // AttachmentPolicyProvider normalises its configured list to include the leading dot,
            // matching Path.GetExtension()'s own output — comparing against a stripped version here
            // would silently reject every attachment, allow-list or not.
            var extension = Path.GetExtension(attachment.FileName).ToLowerInvariant();

            if (attachment.Content.LongLength > policy.MaxBytes)
            {
                skippedNotes.Add($"{attachment.FileName} ({FormatSize(attachment.Content.LongLength)}) — exceeds the {FormatSize(policy.MaxBytes)} limit");
                continue;
            }

            if (policy.AllowedExtensions.Count > 0 && !policy.AllowedExtensions.Contains(extension))
            {
                skippedNotes.Add($"{attachment.FileName} — file type not allowed");
                continue;
            }

            using var stream = new MemoryStream(attachment.Content);
            var storageKey = await fileStorage.SaveAsync(stream, attachment.FileName, attachment.ContentType, ct);

            db.Attachments.Add(new Attachment
            {
                BranchId = null,
                OwnerType = nameof(TicketMessage),
                OwnerId = message.Id,
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                SizeBytes = attachment.Content.LongLength,
                StorageKey = storageKey,
                ScanResult = "skipped",
            });
            savedCount++;
        }

        message.AttachmentCount = savedCount;

        if (skippedNotes.Count > 0)
        {
            message.BodyText += $"\n\n[{skippedNotes.Count} attachment(s) skipped: {string.Join("; ", skippedNotes)}]";
        }
    }

    /// <summary>
    /// Queues the mailbox's configured auto-acknowledgement, appended to the ticket as an outbound
    /// System message so it appears in the thread exactly like any other reply. Email-only for now —
    /// CS-302/304 add their own auto-ack shape (a WhatsApp template message, an SMS) when they land.
    /// </summary>
    private void QueueAutoAcknowledgement(Ticket ticket, InboundMessage message, ChannelAccount account, Customer customer)
    {
        if (message.Channel != ChannelKey.Email || string.IsNullOrWhiteSpace(customer.PrimaryEmail))
        {
            return;
        }

        var messageId = EmailChannelOutbox.NewMessageId(account.Identifier);
        var bodyText = account.AutoReplyBody.For(customer.PreferredLanguage);
        if (string.IsNullOrWhiteSpace(bodyText))
        {
            bodyText = customer.PreferredLanguage.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
                ? $"تم استلام تذكرتك {ticket.Number} وسيتواصل معك أحد الوكلاء قريباً."
                : $"Your ticket {ticket.Number} has been received. An agent will be in touch shortly.";
        }

        var subject = $"[{ticket.Number}] {ticket.Subject}";

        var ackMessage = new TicketMessage
        {
            TicketId = ticket.Id,
            Channel = ChannelKey.Email,
            Direction = MessageDirection.Outbound,
            AuthorType = MessageAuthorType.System,
            AuthorDisplayName = account.Name,
            Subject = subject,
            BodyText = bodyText,
            ExternalMessageId = messageId,
            InReplyToExternalId = message.ExternalMessageId,
            SentAt = clock.UtcNow,
        };
        db.TicketMessages.Add(ackMessage);

        EmailChannelOutbox.Queue(db, clock, new EmailOutboundPayload(
            ackMessage.Id, ticket.Id, account.Id, ticket.BranchId,
            customer.PrimaryEmail, account.Identifier, subject, bodyText, null,
            customer.PreferredLanguage, messageId, message.ExternalMessageId));
    }

    private static string FormatSize(long bytes) => bytes >= 1024 * 1024
        ? $"{bytes / (1024.0 * 1024):0.#} MB"
        : $"{bytes / 1024.0:0.#} KB";

    private static string Truncate(string text, int maxLength = 280) =>
        text.Length <= maxLength ? text : text[..maxLength] + "…";
}
