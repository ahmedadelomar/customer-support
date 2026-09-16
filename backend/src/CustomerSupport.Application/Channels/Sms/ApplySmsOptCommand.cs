using CustomerSupport.Application.Channels.Outbound;
using CustomerSupport.Application.Common;
using CustomerSupport.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Channels.Sms;

/// <summary>
/// Applies STOP/START on every contact row matching an inbound phone number and sends one
/// confirmation (Communication Channels / SMS channel, CS-304). No
/// <see cref="Common.Security.RequirePermissionAttribute"/> — reached only from
/// <c>SmsWebhooksController</c>, already gated by HMAC verification, the same reasoning as every
/// other anonymous-webhook-only command in this codebase.
/// </summary>
public record ApplySmsOptCommand(Guid ChannelAccountId, string FromPhone, bool AllowNotifications) : IRequest<Unit>;

public class ApplySmsOptCommandHandler(IAppDbContext db, ISmsChannelSender sender)
    : IRequestHandler<ApplySmsOptCommand, Unit>
{
    public async Task<Unit> Handle(ApplySmsOptCommand request, CancellationToken ct)
    {
        var normalized = ContactNormalizer.Normalize(request.FromPhone);
        var contacts = await db.CustomerContacts
            .Include(c => c.Customer)
            .Where(c => c.NormalizedValue == normalized)
            .ToListAsync(ct);

        foreach (var contact in contacts)
        {
            contact.AllowNotifications = request.AllowNotifications;
        }

        await db.SaveChangesAsync(ct);

        var language = contacts.FirstOrDefault()?.Customer.PreferredLanguage ?? "ar";
        var isArabic = language.StartsWith("ar", StringComparison.OrdinalIgnoreCase);

        var confirmation = request.AllowNotifications
            ? (isArabic ? "تم تفعيل اشتراكك في الرسائل النصية مرة أخرى." : "You have been resubscribed to SMS notifications.")
            : (isArabic ? "تم إلغاء اشتراكك في الرسائل النصية. أرسل START للاشتراك مرة أخرى." : "You have been unsubscribed from SMS notifications. Reply START to opt back in.");

        var account = await db.ChannelAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.ChannelAccountId, ct);
        if (account is not null)
        {
            await sender.SendAsync(request.FromPhone, account.Identifier, confirmation, ct);
        }

        return Unit.Value;
    }
}
