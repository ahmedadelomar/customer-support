using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Localization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Infrastructure;

/// <summary>
/// Maps application exceptions to RFC 7807 problem details, so the Angular error interceptor sees one
/// consistent shape and internal messages never leak to clients.
/// </summary>
/// <remarks>
/// Titles are localised from the request's culture, which <c>AddRequestLocalization</c> resolves from
/// the <c>Accept-Language</c> header the client sends on every call. The detail is the exception's own
/// message: those come from handlers and validators, which own their own wording.
/// </remarks>
public class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    IMessageLocalizer localizer,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, localizer[MessageKeys.ValidationFailed], exception.Message),
            NotFoundException => (StatusCodes.Status404NotFound, localizer[MessageKeys.NotFound], exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, localizer[MessageKeys.Conflict], exception.Message),
            DuplicateContactException => (StatusCodes.Status409Conflict, localizer[MessageKeys.DuplicateContact], exception.Message),
            AssignmentWarningException => (StatusCodes.Status409Conflict, localizer[MessageKeys.AssignmentWarning], exception.Message),
            StatusKindChangeWarningException => (StatusCodes.Status409Conflict, localizer[MessageKeys.StatusKindChangeWarning], exception.Message),
            OpenTasksWarningException => (StatusCodes.Status409Conflict, localizer[MessageKeys.OpenTasksWarning], exception.Message),
            ForbiddenException => (StatusCodes.Status403Forbidden, localizer[MessageKeys.Forbidden], exception.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, localizer[MessageKeys.Unauthorized], localizer[MessageKeys.UnauthorizedDetail]),
            // 499 is a de-facto standard for a client-cancelled request and has no StatusCodes constant.
            OperationCanceledException => (499, localizer[MessageKeys.ClientClosedRequest], localizer[MessageKeys.ClientClosedRequestDetail]),
            _ => (StatusCodes.Status500InternalServerError, localizer[MessageKeys.Unexpected],
                localizer[MessageKeys.UnexpectedDetail]),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            logger.LogInformation("Request failed with {Status}: {Message}", status, exception.Message);
        }

        context.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = $"{context.Request.Method} {context.Request.Path}",
        };

        problem.Extensions["traceId"] = context.TraceIdentifier;

        // Field-level errors let the Angular forms highlight the offending controls.
        if (exception is ValidationException validation)
        {
            problem.Extensions["errors"] = validation.Errors;
        }

        // Lets the client show "already used by {name}" and offer a confirm-and-resubmit step,
        // rather than only a generic conflict message.
        if (exception is DuplicateContactException duplicate)
        {
            problem.Extensions["duplicateCustomerId"] = duplicate.DuplicateCustomerId;
            problem.Extensions["duplicateCustomerName"] = duplicate.DuplicateCustomerName;
        }

        // Lets the client offer "proceed anyway" only when the warning is actually overridable —
        // a deactivated agent (IsHardBlock) never gets a force-retry option.
        if (exception is AssignmentWarningException assignmentWarning)
        {
            problem.Extensions["isHardBlock"] = assignmentWarning.IsHardBlock;
            problem.Extensions["openTickets"] = assignmentWarning.OpenTickets;
            problem.Extensions["cap"] = assignmentWarning.Cap;
        }

        if (exception is StatusKindChangeWarningException kindChangeWarning)
        {
            problem.Extensions["ticketCount"] = kindChangeWarning.TicketCount;
        }

        // Lets the client list the open tasks and offer either "close anyway" (Force) or
        // "close and complete them" (CompleteLinkedTasks) as the resubmit options.
        if (exception is OpenTasksWarningException openTasksWarning)
        {
            problem.Extensions["openTasks"] = openTasksWarning.OpenTasks;
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
