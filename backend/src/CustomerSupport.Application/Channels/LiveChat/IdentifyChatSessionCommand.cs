using CustomerSupport.Application.Common;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Channels.LiveChat;

/// <summary>
/// Links an anonymous session to a customer, by email or phone match, creating a minimal customer
/// profile when neither is on file — the same "never drop the visitor" rule
/// <c>InboundMessagePipeline.ResolveCustomerAsync</c> follows for a channel's first message,
/// duplicated here rather than shared because chat identification takes explicit name/email/phone
/// fields the visitor typed, not a single "from address" to parse.
/// </summary>
public record IdentifyChatSessionCommand(Guid SessionId, IdentifyChatSessionRequest Request) : IRequest<ChatSessionDto>;

public class IdentifyChatSessionCommandValidator : AbstractValidator<IdentifyChatSessionCommand>
{
    public IdentifyChatSessionCommandValidator()
    {
        RuleFor(x => x.Request).Must(r => !string.IsNullOrWhiteSpace(r.Email) || !string.IsNullOrWhiteSpace(r.Phone))
            .WithMessage("Provide an email or a phone number.");
    }
}

public class IdentifyChatSessionCommandHandler(
    IAppDbContext db, IReferenceNumberGenerator numbers, IChatRealtimeNotifier realtime)
    : IRequestHandler<IdentifyChatSessionCommand, ChatSessionDto>
{
    public async Task<ChatSessionDto> Handle(IdentifyChatSessionCommand command, CancellationToken ct)
    {
        var session = await db.ChatSessions.FirstOrDefaultAsync(s => s.Id == command.SessionId, ct)
            ?? throw new NotFoundException(nameof(ChatSession), command.SessionId);

        var request = command.Request;
        var contactValue = request.Email ?? request.Phone!;
        var normalized = ContactNormalizer.Normalize(contactValue);

        var existing = await db.CustomerContacts
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.NormalizedValue == normalized, ct);

        Customer customer;
        if (existing is not null)
        {
            customer = existing.Customer;
        }
        else
        {
            var displayName = request.Name ?? contactValue;
            customer = new Customer
            {
                Code = await numbers.NextCustomerCodeAsync(ct),
                DisplayName = new LocalizedText(displayName, displayName),
                PreferredLanguage = session.Language,
                PreferredChannel = ChannelKey.LiveChat,
            };

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                customer.PrimaryEmail = request.Email;
            }
            else
            {
                customer.PrimaryPhone = request.Phone;
            }

            customer.Contacts.Add(new CustomerContact
            {
                Type = !string.IsNullOrWhiteSpace(request.Email) ? ContactType.Email : ContactType.Mobile,
                Value = contactValue,
                NormalizedValue = normalized ?? contactValue.ToLowerInvariant(),
                Label = displayName,
                IsPrimary = true,
                IsVerified = false,
            });

            db.Customers.Add(customer);
        }

        session.CustomerId = customer.Id;
        session.VisitorName = request.Name ?? session.VisitorName;
        session.VisitorEmail = request.Email ?? session.VisitorEmail;

        await db.SaveChangesAsync(ct);

        var dto = ChatMapper.ToDto(session, null, 0);
        await realtime.PushToSessionAsync(session.Id, "session.updated", dto, ct);
        return dto;
    }
}
