using CustomerSupport.Application.Common.Interfaces;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Placeholder until CS-501 (SLA automation) lands. Ticket creation and reply already call every
/// hook this interface defines, so that story only has to implement the arithmetic — no call site
/// changes anywhere else.
/// </summary>
public class NoOpSlaEngine : ISlaEngine
{
    public Task ApplyPolicyAsync(Guid ticketId, CancellationToken ct = default) => Task.CompletedTask;
    public Task OnFirstAgentReplyAsync(Guid ticketId, CancellationToken ct = default) => Task.CompletedTask;
    public Task OnStatusChangedAsync(Guid ticketId, CancellationToken ct = default) => Task.CompletedTask;
    public Task OnResolvedAsync(Guid ticketId, CancellationToken ct = default) => Task.CompletedTask;
}
