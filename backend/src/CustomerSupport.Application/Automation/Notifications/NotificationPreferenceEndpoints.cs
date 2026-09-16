using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Automation;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Automation.Notifications;

public record NotificationPreferenceDto
{
    public string EventType { get; init; } = string.Empty;
    public NotificationArea Area { get; init; }
    public bool ViaInApp { get; init; } = true;
    public bool ViaEmail { get; init; }
    public bool ViaSms { get; init; }
    public bool ViaPush { get; init; }
    /// <summary>True when the caller has no saved row for this event — the UI marks these "default" rather than showing them as a deliberate off.</summary>
    public bool IsUsingDefault { get; init; }
    public TimeOnly? QuietHoursStart { get; init; }
    public TimeOnly? QuietHoursEnd { get; init; }
}

/// <summary>
/// The FULL matrix for the caller — one row per <see cref="NotificationEventTypes.All"/> entry,
/// whether or not a preference row exists — so the screen can show every event, with unsaved rows
/// visibly marked as using the system default per that story's own rule.
/// </summary>
public record GetNotificationPreferencesQuery : IRequest<IReadOnlyList<NotificationPreferenceDto>>;

public class GetNotificationPreferencesQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetNotificationPreferencesQuery, IReadOnlyList<NotificationPreferenceDto>>
{
    public async Task<IReadOnlyList<NotificationPreferenceDto>> Handle(GetNotificationPreferencesQuery request, CancellationToken cancellationToken)
    {
        var saved = await db.NotificationPreferences.AsNoTracking()
            .Where(p => p.UserId == currentUser.UserId)
            .ToDictionaryAsync(p => p.EventType, cancellationToken);

        return NotificationEventTypes.All.Select(info =>
        {
            if (saved.TryGetValue(info.Key, out var pref))
            {
                return new NotificationPreferenceDto
                {
                    EventType = info.Key,
                    Area = info.Area,
                    ViaInApp = pref.ViaInApp,
                    ViaEmail = pref.ViaEmail,
                    ViaSms = pref.ViaSms,
                    ViaPush = pref.ViaPush,
                    IsUsingDefault = false,
                    QuietHoursStart = pref.QuietHoursStart,
                    QuietHoursEnd = pref.QuietHoursEnd,
                };
            }

            return new NotificationPreferenceDto
            {
                EventType = info.Key,
                Area = info.Area,
                ViaInApp = true,
                ViaEmail = info.DefaultViaEmail,
                ViaSms = info.DefaultViaSms,
                ViaPush = info.DefaultViaPush,
                IsUsingDefault = true,
            };
        }).ToList();
    }
}

public record NotificationPreferenceInput(
    string EventType, bool ViaInApp, bool ViaEmail, bool ViaSms, bool ViaPush,
    TimeOnly? QuietHoursStart, TimeOnly? QuietHoursEnd);

/// <summary>Replaces the caller's whole matrix in one write — the screen submits every row together.</summary>
public record UpdateNotificationPreferencesCommand(List<NotificationPreferenceInput> Preferences) : IRequest;

public class UpdateNotificationPreferencesCommandValidator : AbstractValidator<UpdateNotificationPreferencesCommand>
{
    public UpdateNotificationPreferencesCommandValidator()
    {
        RuleForEach(x => x.Preferences).ChildRules(p =>
        {
            p.RuleFor(x => x.EventType).NotEmpty();
            p.RuleFor(x => x).Must(x => (x.QuietHoursStart is null) == (x.QuietHoursEnd is null))
                .WithMessage("Quiet hours need both a start and an end, or neither.")
                .OverridePropertyName("quietHoursStart");
        });
    }
}

public class UpdateNotificationPreferencesCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UpdateNotificationPreferencesCommand>
{
    public async Task Handle(UpdateNotificationPreferencesCommand request, CancellationToken cancellationToken)
    {
        var existing = await db.NotificationPreferences
            .Where(p => p.UserId == currentUser.UserId)
            .ToDictionaryAsync(p => p.EventType, cancellationToken);

        var knownKeys = NotificationEventTypes.All.Select(e => e.Key).ToHashSet();

        foreach (var input in request.Preferences)
        {
            if (!knownKeys.Contains(input.EventType))
            {
                continue; // ignore an event type the server no longer defines
            }

            if (existing.TryGetValue(input.EventType, out var pref))
            {
                Apply(pref, input);
            }
            else
            {
                var created = new NotificationPreference { UserId = currentUser.UserId!.Value, EventType = input.EventType };
                Apply(created, input);
                db.NotificationPreferences.Add(created);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void Apply(NotificationPreference pref, NotificationPreferenceInput input)
    {
        pref.ViaInApp = input.ViaInApp;
        pref.ViaEmail = input.ViaEmail;
        pref.ViaSms = input.ViaSms;
        pref.ViaPush = input.ViaPush;
        pref.QuietHoursStart = input.QuietHoursStart;
        pref.QuietHoursEnd = input.QuietHoursEnd;
    }
}
