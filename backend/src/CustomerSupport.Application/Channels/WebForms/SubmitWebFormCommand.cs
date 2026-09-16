using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Channels;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ValidationException = CustomerSupport.Application.Common.Exceptions.ValidationException;

namespace CustomerSupport.Application.Channels.WebForms;

/// <summary>
/// Handles a public form submission (Communication Channels / Web forms, CS-305). Anonymous — no
/// <see cref="Common.Security.RequirePermissionAttribute"/> — this is the most exposed endpoint in
/// the product, so every check here (rate limit, schema validation, captcha) runs before anything
/// is written, and the raw payload is stored before a ticket is ever attempted.
/// </summary>
public record SubmitWebFormCommand(string Key, SubmitWebFormRequest Request, string? IpAddress, string? UserAgent)
    : IRequest<SubmitWebFormResult>;

public class SubmitWebFormCommandHandler(
    IAppDbContext db,
    IWebFormValidator validator,
    ICaptchaVerifier captcha,
    IWebFormTicketFactory ticketFactory,
    IDateTimeProvider clock,
    ILogger<SubmitWebFormCommandHandler> logger)
    : IRequestHandler<SubmitWebFormCommand, SubmitWebFormResult>
{
    public async Task<SubmitWebFormResult> Handle(SubmitWebFormCommand command, CancellationToken ct)
    {
        var form = await db.WebFormDefinitions.FirstOrDefaultAsync(f => f.Key == command.Key && f.IsActive, ct)
            ?? throw new NotFoundException(nameof(WebFormDefinition), command.Key);

        var fields = WebFormFieldSchemaSerializer.Deserialize(form.FieldsJson);
        var values = command.Request.Values;

        if (form.RateLimitPerHour > 0)
        {
            var since = clock.UtcNow.AddHours(-1);
            var recent = await db.WebFormSubmissions.CountAsync(
                s => s.WebFormDefinitionId == form.Id && s.IpAddress == command.IpAddress && s.SubmittedAt >= since, ct);

            if (recent >= form.RateLimitPerHour)
            {
                throw new ConflictException("Too many submissions from this address. Please try again later.");
            }
        }

        var errors = validator.Validate(fields, values);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        if (form.RequireCaptcha && !await captcha.VerifyAsync(command.Request.CaptchaToken, ct))
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["captcha"] = ["Captcha verification failed."] });
        }

        var mapped = ExtractSubmitterFields(fields, values);

        // 1. Persist the raw payload first, so nothing is lost if ticket creation below fails.
        var submission = new WebFormSubmission
        {
            WebFormDefinitionId = form.Id,
            BranchId = form.BranchId,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(values),
            SubmitterName = mapped.Name,
            SubmitterEmail = mapped.Email,
            SubmitterPhone = mapped.Phone,
            IpAddress = command.IpAddress,
            UserAgent = command.UserAgent,
            Status = "Received",
            SubmittedAt = clock.UtcNow,
        };
        db.WebFormSubmissions.Add(submission);
        form.SubmissionCount += 1;
        await db.SaveChangesAsync(ct);

        // 2. Then attempt the ticket, recording failure rather than losing the submission.
        Guid? ticketId = null;
        string? ticketNumber = null;
        try
        {
            (ticketId, ticketNumber) = await ticketFactory.CreateAsync(form, fields, values, ct);
            submission.TicketId = ticketId;
            submission.Status = "Processed";
            submission.ProcessedAt = clock.UtcNow;
        }
        catch (Exception ex)
        {
            submission.Status = "Failed";
            submission.FailureReason = ex.Message;
            logger.LogError(ex, "Web form submission {Id} failed to create a ticket.", submission.Id);
        }

        await db.SaveChangesAsync(ct);

        return new SubmitWebFormResult(submission.Id, ticketId, ticketNumber, form.ThankYouMessage.En, form.ThankYouMessage.Ar);
    }

    /// <summary>Just enough to fill in <see cref="WebFormSubmission"/>'s own submitter columns — the full mapping lives in <see cref="WebFormTicketFactory"/>.</summary>
    private static (string? Name, string? Email, string? Phone) ExtractSubmitterFields(
        IReadOnlyList<WebFormField> fields, IReadOnlyDictionary<string, string> values)
    {
        string? Get(string mapTo) => fields
            .Where(f => f.MapTo == mapTo)
            .Select(f => values.GetValueOrDefault(f.Key))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        return (Get("customerName"), Get("customerEmail"), Get("customerPhone"));
    }
}
