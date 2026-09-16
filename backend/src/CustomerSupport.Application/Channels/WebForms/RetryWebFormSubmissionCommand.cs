using System.Text.Json;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Channels;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Channels.WebForms;

/// <summary>Reprocesses a submission stuck as <c>Failed</c> — either an admin clicking Retry, or <c>WebFormSubmissionRetryJob</c>.</summary>
[RequirePermission(Permissions.Channels.ManageWebForms)]
public record RetryWebFormSubmissionCommand(Guid SubmissionId) : IRequest<Guid?>;

public class RetryWebFormSubmissionCommandHandler(IAppDbContext db, IWebFormSubmissionRetryService retry)
    : IRequestHandler<RetryWebFormSubmissionCommand, Guid?>
{
    public async Task<Guid?> Handle(RetryWebFormSubmissionCommand command, CancellationToken ct)
    {
        var submission = await db.WebFormSubmissions.FirstOrDefaultAsync(s => s.Id == command.SubmissionId, ct)
            ?? throw new NotFoundException(nameof(WebFormSubmission), command.SubmissionId);

        if (submission.Status != "Failed")
        {
            throw new ConflictException("Only a failed submission can be retried.");
        }

        return await retry.RetryAsync(submission, ct);
    }
}

/// <summary>
/// The retry logic shared by the manual retry command and <c>WebFormSubmissionRetryJob</c> —
/// re-runs the same ticket-creation path <see cref="SubmitWebFormCommand"/> uses, from the
/// already-stored payload, so a fixed root cause (a deactivated category, say) resolves cleanly.
/// </summary>
public interface IWebFormSubmissionRetryService
{
    Task<Guid?> RetryAsync(WebFormSubmission submission, CancellationToken ct);
}

public class WebFormSubmissionRetryService(
    IAppDbContext db, IWebFormTicketFactory ticketFactory, IDateTimeProvider clock,
    ILogger<WebFormSubmissionRetryService> logger) : IWebFormSubmissionRetryService
{
    public async Task<Guid?> RetryAsync(WebFormSubmission submission, CancellationToken ct)
    {
        var form = await db.WebFormDefinitions.FirstOrDefaultAsync(f => f.Id == submission.WebFormDefinitionId, ct)
            ?? throw new NotFoundException(nameof(WebFormDefinition), submission.WebFormDefinitionId);

        var fields = WebFormFieldSchemaSerializer.Deserialize(form.FieldsJson);
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(submission.PayloadJson) ?? [];

        try
        {
            var (ticketId, _) = await ticketFactory.CreateAsync(form, fields, values, ct);
            submission.TicketId = ticketId;
            submission.Status = "Processed";
            submission.ProcessedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            return ticketId;
        }
        catch (Exception ex)
        {
            submission.FailureReason = ex.Message;
            await db.SaveChangesAsync(ct);
            logger.LogWarning(ex, "Retry of web form submission {Id} failed again.", submission.Id);
            return null;
        }
    }
}
