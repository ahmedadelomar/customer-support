using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Channels;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Channels.ChannelAccounts;

/// <summary>
/// The admin screen's "Test connection" action. No real mail provider exists in this environment, so
/// this validates configuration completeness (a plausible mailbox identifier, a webhook secret
/// configured) rather than opening a real network connection — still honest about what it checked,
/// and still what makes a broken mailbox visible on the admin screen instead of discovered days
/// later through missing tickets. Updates <see cref="ChannelAccount.LastPolledAt"/>/<see cref="ChannelAccount.LastPollError"/>
/// exactly as a real connectivity check would.
/// </summary>
[RequirePermission(Permissions.Channels.Manage)]
public record TestChannelAccountConnectionCommand(Guid Id) : IRequest<TestChannelAccountConnectionResult>;

public class TestChannelAccountConnectionCommandHandler(
    IAppDbContext db, IChannelWebhookSecrets secrets, IDateTimeProvider clock)
    : IRequestHandler<TestChannelAccountConnectionCommand, TestChannelAccountConnectionResult>
{
    public async Task<TestChannelAccountConnectionResult> Handle(TestChannelAccountConnectionCommand command, CancellationToken ct)
    {
        var account = await db.ChannelAccounts.Include(a => a.Channel)
            .FirstOrDefaultAsync(a => a.Id == command.Id, ct)
            ?? throw new NotFoundException(nameof(ChannelAccount), command.Id);

        var error = await ValidateAsync(account, ct);

        account.LastPolledAt = clock.UtcNow;
        account.LastPollError = error;
        await db.SaveChangesAsync(ct);

        return new TestChannelAccountConnectionResult(error is null, error);
    }

    private async Task<string?> ValidateAsync(ChannelAccount account, CancellationToken ct)
    {
        if (!account.IsActive)
        {
            return "This account is inactive.";
        }

        if (account.Channel.Key == Domain.Enums.ChannelKey.Email && !account.Identifier.Contains('@'))
        {
            return "The identifier does not look like a mailbox address.";
        }

        var hasSecret = await secrets.GetSecretAsync(account.Id, ct) is not null;
        if (!hasSecret)
        {
            return "No webhook secret is configured — inbound webhook calls will be refused until one is set.";
        }

        return null;
    }
}
