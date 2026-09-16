using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Channels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Channels.LiveChat;

/// <summary>Post-chat rating. Session token only, so no permission attribute — see <c>EndChatSessionCommand</c>.</summary>
public record RateChatSessionCommand(Guid SessionId, RateChatSessionRequest Request) : IRequest;

public class RateChatSessionCommandValidator : AbstractValidator<RateChatSessionCommand>
{
    public RateChatSessionCommandValidator()
    {
        RuleFor(x => x.Request.Rating).InclusiveBetween(1, 5);
    }
}

public class RateChatSessionCommandHandler(IAppDbContext db) : IRequestHandler<RateChatSessionCommand>
{
    public async Task Handle(RateChatSessionCommand command, CancellationToken ct)
    {
        var session = await db.ChatSessions.FirstOrDefaultAsync(s => s.Id == command.SessionId, ct)
            ?? throw new NotFoundException(nameof(ChatSession), command.SessionId);

        if (session.Status is not ("Ended" or "Abandoned"))
        {
            throw new ConflictException("Only an ended chat can be rated.");
        }

        session.Rating = command.Request.Rating;
        await db.SaveChangesAsync(ct);
    }
}
