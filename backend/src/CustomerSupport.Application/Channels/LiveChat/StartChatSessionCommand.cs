using System.Text.Json;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Organization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Channels.LiveChat;

/// <summary>
/// Starts an anonymous chat session from the public widget. No <see cref="Common.Security.RequirePermissionAttribute"/>
/// — there is no caller identity yet, that is exactly what this issues.
/// </summary>
public record StartChatSessionCommand(StartChatSessionRequest Request, string? IpAddress, string? UserAgent)
    : IRequest<StartChatSessionResult>;

public class StartChatSessionCommandValidator : AbstractValidator<StartChatSessionCommand>
{
    public StartChatSessionCommandValidator()
    {
        RuleFor(x => x.Request.ChannelAccountId).NotEmpty();
        RuleFor(x => x.Request.Language).NotEmpty();
    }
}

public class StartChatSessionCommandHandler(
    IAppDbContext db,
    IChatVisitorTokenService tokens,
    IChatRealtimeNotifier realtime,
    IDateTimeProvider clock)
    : IRequestHandler<StartChatSessionCommand, StartChatSessionResult>
{
    public async Task<StartChatSessionResult> Handle(StartChatSessionCommand command, CancellationToken ct)
    {
        var request = command.Request;

        var account = await db.ChannelAccounts
            .FirstOrDefaultAsync(a => a.Id == request.ChannelAccountId && a.IsActive, ct)
            ?? throw new NotFoundException(nameof(ChannelAccount), request.ChannelAccountId);

        var teamId = await ResolveTeamIdAsync(account, ct);

        var session = new ChatSession
        {
            BranchId = account.BranchId,
            ChannelAccountId = account.Id,
            VisitorKey = string.IsNullOrWhiteSpace(request.VisitorKey) ? Guid.NewGuid().ToString("N") : request.VisitorKey,
            VisitorName = request.VisitorName,
            Language = request.Language,
            PageUrl = request.PageUrl,
            IpAddress = command.IpAddress,
            UserAgent = command.UserAgent,
            QueuedForTeamId = teamId,
            Status = "Waiting",
            StartedAt = clock.UtcNow,
        };

        db.ChatSessions.Add(session);

        if (!string.IsNullOrWhiteSpace(request.InitialMessage))
        {
            session.Messages.Add(new ChatMessage
            {
                ChatSessionId = session.Id,
                AuthorType = MessageAuthorType.Customer,
                AuthorDisplayName = request.VisitorName,
                Body = request.InitialMessage,
                SentAt = clock.UtcNow,
            });
            session.MessageCount = 1;
        }

        await db.SaveChangesAsync(ct);

        var dto = ChatMapper.ToDto(session, null, 0);

        if (teamId is { } tid)
        {
            await realtime.PushToTeamQueueAsync(tid, "session.queued", dto, ct);
        }

        return new StartChatSessionResult(session.Id, session.VisitorKey, tokens.IssueSessionToken(session.Id), dto);
    }

    /// <summary>
    /// The team a new session queues to. A live-chat account's <c>SettingsJson</c> may pin one
    /// explicitly (<c>{"teamId": "..."}</c>); otherwise the oldest active team under the account's
    /// default department stands in — the same cascading-default shape <c>CreateTicketCommand</c> and
    /// the inbound pipeline already use for department/priority/category, applied here to team.
    /// </summary>
    private async Task<Guid?> ResolveTeamIdAsync(ChannelAccount account, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(account.SettingsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(account.SettingsJson);
                if (doc.RootElement.TryGetProperty("teamId", out var teamIdProp) &&
                    Guid.TryParse(teamIdProp.GetString(), out var pinnedTeamId))
                {
                    var pinnedExists = await db.Teams.AnyAsync(t => t.Id == pinnedTeamId && t.IsActive, ct);
                    if (pinnedExists)
                    {
                        return pinnedTeamId;
                    }
                }
            }
            catch (JsonException)
            {
                // Malformed settings JSON falls through to the department-based default below.
            }
        }

        var team = await db.Teams
            .Where(t => t.IsActive && t.DepartmentId == account.DefaultDepartmentId)
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

        team ??= await db.Teams.Where(t => t.IsActive).OrderBy(t => t.CreatedAt).FirstOrDefaultAsync(ct);

        return team?.Id;
    }
}
