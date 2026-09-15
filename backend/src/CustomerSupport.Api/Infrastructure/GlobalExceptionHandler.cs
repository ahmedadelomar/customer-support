using CustomerSupport.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Infrastructure;

/// <summary>
/// Maps application exceptions to RFC 7807 problem details, so the Angular error interceptor sees one
/// consistent shape and internal messages never leak to clients.
/// </summary>
public class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed", exception.Message),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found", exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", exception.Message),
            DuplicateContactException => (StatusCodes.Status409Conflict, "Duplicate contact", exception.Message),
            AssignmentWarningException => (StatusCodes.Status409Conflict, "Assignment warning", exception.Message),
            StatusKindChangeWarningException => (StatusCodes.Status409Conflict, "Status kind change warning", exception.Message),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden", exception.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", "Authentication is required."),
            // 499 is a de-facto standard for a client-cancelled request and has no StatusCodes constant.
            OperationCanceledException => (499, "Client closed request", "The request was cancelled."),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error",
                "An unexpected error occurred. Reference the trace id when reporting this."),
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

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
