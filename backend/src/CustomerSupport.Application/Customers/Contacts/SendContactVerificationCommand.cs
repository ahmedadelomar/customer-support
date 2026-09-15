using System.Security.Cryptography;
using System.Text;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Customers.Dtos;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Customers.Contacts;

/// <summary>
/// Generates and sends a 6-digit verification code for a contact. Only email and mobile numbers can
/// be verified — matching the UI, which hides the action for every other contact type — because
/// those are the two channels a code can actually be delivered on today.
/// </summary>
[RequirePermission(Permissions.Customers.ManageContacts)]
public record SendContactVerificationCommand(Guid CustomerId, Guid ContactId)
    : IRequest<SendContactVerificationResultDto>;

public class SendContactVerificationCommandHandler(
    IAppDbContext db,
    IContactVerificationSender sender,
    IDateTimeProvider clock)
    : IRequestHandler<SendContactVerificationCommand, SendContactVerificationResultDto>
{
    private static readonly ContactType[] VerifiableTypes = [ContactType.Email, ContactType.Mobile];
    private const int MaxSendsPerHour = 3;

    public async Task<SendContactVerificationResultDto> Handle(
        SendContactVerificationCommand request, CancellationToken cancellationToken)
    {
        var contact = await db.CustomerContacts.FirstOrDefaultAsync(
            c => c.Id == request.ContactId && c.CustomerId == request.CustomerId && !c.IsDeleted,
            cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerContact), request.ContactId);

        if (!VerifiableTypes.Contains(contact.Type))
        {
            throw new ConflictException("Only email and mobile contacts can be verified.");
        }

        var now = clock.UtcNow;
        var hourAgo = now.AddHours(-1);

        // Without this, the endpoint is a way to send someone unlimited SMS.
        var sentInLastHour = await db.ContactVerifications.CountAsync(
            v => v.CustomerContactId == request.ContactId && v.CreatedAt >= hourAgo, cancellationToken);

        if (sentInLastHour >= MaxSendsPerHour)
        {
            throw new ConflictException(
                $"Too many verification codes requested. Try again after {hourAgo.AddHours(1):t}.");
        }

        // At most one code is ever active: expire any earlier unconfirmed one immediately, so a
        // stale code lying around cannot be confirmed after a fresh one was requested.
        await db.ContactVerifications
            .Where(v => v.CustomerContactId == request.ContactId && v.ConfirmedAt == null && v.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.ExpiresAt, now), cancellationToken);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        db.ContactVerifications.Add(new ContactVerification
        {
            CustomerContactId = contact.Id,
            CodeHash = Hash(code),
            ExpiresAt = now.AddMinutes(10),
            CreatedAt = now,
        });

        await db.SaveChangesAsync(cancellationToken);
        await sender.SendAsync(contact, code, cancellationToken);

        return new SendContactVerificationResultDto
        {
            ExpiresAt = now.AddMinutes(10),
            DevCode = sender.IsPlaceholder ? code : null,
        };
    }

    private static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
