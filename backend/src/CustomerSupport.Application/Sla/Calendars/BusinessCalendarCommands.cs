using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Sla;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Sla.Calendars;

/// <summary>Creates a working-hours calendar with its hours and holidays.</summary>
[RequirePermission(Permissions.Sla.ManageCalendars)]
public record CreateBusinessCalendarCommand : IRequest<Guid>
{
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string TimeZoneId { get; init; } = "Asia/Riyadh";
    public bool IsTwentyFourSeven { get; init; }
    public bool IsDefault { get; init; }
    public List<BusinessHourInput> BusinessHours { get; init; } = [];
    public List<HolidayInput> Holidays { get; init; } = [];
}

public class CreateBusinessCalendarCommandValidator : AbstractValidator<CreateBusinessCalendarCommand>
{
    public CreateBusinessCalendarCommandValidator()
    {
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TimeZoneId).NotEmpty().Must(BeAKnownTimeZone).WithMessage("Unknown IANA time zone id.");
        RuleForEach(x => x.BusinessHours).ChildRules(h => h.RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime));

        // Guarded here rather than left to the calculator, so a bad calendar is refused at save
        // time instead of throwing the first time a ticket tries to use it.
        RuleFor(x => x).Must(x => x.IsTwentyFourSeven || x.BusinessHours.Count > 0)
            .WithMessage("A calendar needs at least one working window, or must be marked 24/7.")
            .OverridePropertyName("businessHours");
    }

    private static bool BeAKnownTimeZone(string id)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}

public class CreateBusinessCalendarCommandHandler(IAppDbContext db)
    : IRequestHandler<CreateBusinessCalendarCommand, Guid>
{
    public async Task<Guid> Handle(CreateBusinessCalendarCommand request, CancellationToken cancellationToken)
    {
        if (request.IsDefault)
        {
            await db.BusinessCalendars.Where(c => c.IsDefault)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDefault, false), cancellationToken);
        }

        var calendar = new BusinessCalendar
        {
            Name = new LocalizedText(request.NameEn, request.NameAr),
            TimeZoneId = request.TimeZoneId,
            IsTwentyFourSeven = request.IsTwentyFourSeven,
            IsDefault = request.IsDefault,
        };

        foreach (var hour in request.BusinessHours)
        {
            calendar.BusinessHours.Add(new BusinessHour { DayOfWeek = hour.DayOfWeek, StartTime = hour.StartTime, EndTime = hour.EndTime });
        }

        foreach (var holiday in request.Holidays)
        {
            calendar.Holidays.Add(new Holiday
            {
                Date = holiday.Date,
                Name = new LocalizedText(holiday.NameEn, holiday.NameAr),
                IsRecurringAnnually = holiday.IsRecurringAnnually,
            });
        }

        db.BusinessCalendars.Add(calendar);
        await db.SaveChangesAsync(cancellationToken);
        return calendar.Id;
    }
}

/// <summary>Replaces a calendar's fields, hours and holidays.</summary>
[RequirePermission(Permissions.Sla.ManageCalendars)]
public record UpdateBusinessCalendarCommand : IRequest
{
    public Guid Id { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string TimeZoneId { get; init; } = "Asia/Riyadh";
    public bool IsTwentyFourSeven { get; init; }
    public bool IsDefault { get; init; }
    public List<BusinessHourInput> BusinessHours { get; init; } = [];
    public List<HolidayInput> Holidays { get; init; } = [];
}

public class UpdateBusinessCalendarCommandValidator : AbstractValidator<UpdateBusinessCalendarCommand>
{
    public UpdateBusinessCalendarCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TimeZoneId).NotEmpty();
        RuleFor(x => x).Must(x => x.IsTwentyFourSeven || x.BusinessHours.Count > 0)
            .WithMessage("A calendar needs at least one working window, or must be marked 24/7.")
            .OverridePropertyName("businessHours");
    }
}

public class UpdateBusinessCalendarCommandHandler(IAppDbContext db, IBusinessCalendarCacheInvalidator cacheInvalidator)
    : IRequestHandler<UpdateBusinessCalendarCommand>
{
    public async Task Handle(UpdateBusinessCalendarCommand request, CancellationToken cancellationToken)
    {
        var calendar = await db.BusinessCalendars
            .Include(c => c.BusinessHours)
            .Include(c => c.Holidays)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(BusinessCalendar), request.Id);

        if (calendar.IsDefault && !request.IsDefault)
        {
            var anotherDefaultExists = await db.BusinessCalendars
                .AnyAsync(c => c.Id != request.Id && c.IsDefault, cancellationToken);
            if (!anotherDefaultExists)
            {
                throw new ConflictException("At least one calendar must remain the default.");
            }
        }

        if (request.IsDefault && !calendar.IsDefault)
        {
            await db.BusinessCalendars.Where(c => c.Id != request.Id && c.IsDefault)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDefault, false), cancellationToken);
        }

        calendar.Name = new LocalizedText(request.NameEn, request.NameAr);
        calendar.TimeZoneId = request.TimeZoneId;
        calendar.IsTwentyFourSeven = request.IsTwentyFourSeven;
        calendar.IsDefault = request.IsDefault;

        db.BusinessHours.RemoveRange(calendar.BusinessHours);
        calendar.BusinessHours.Clear();
        foreach (var hour in request.BusinessHours)
        {
            calendar.BusinessHours.Add(new BusinessHour { DayOfWeek = hour.DayOfWeek, StartTime = hour.StartTime, EndTime = hour.EndTime });
        }

        db.Holidays.RemoveRange(calendar.Holidays);
        calendar.Holidays.Clear();
        foreach (var holiday in request.Holidays)
        {
            calendar.Holidays.Add(new Holiday
            {
                Date = holiday.Date,
                Name = new LocalizedText(holiday.NameEn, holiday.NameAr),
                IsRecurringAnnually = holiday.IsRecurringAnnually,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        // Every ticket create, status change and breach-sweep tick reads the cached snapshot —
        // without this, an edited calendar would keep computing against its old hours for up to
        // the cache's lifetime.
        cacheInvalidator.Invalidate(calendar.Id);
    }
}

/// <summary>Deletes a calendar. Refused while any SLA policy still references it.</summary>
[RequirePermission(Permissions.Sla.ManageCalendars)]
public record DeleteBusinessCalendarCommand(Guid Id) : IRequest;

public class DeleteBusinessCalendarCommandHandler(IAppDbContext db, IBusinessCalendarCacheInvalidator cacheInvalidator)
    : IRequestHandler<DeleteBusinessCalendarCommand>
{
    public async Task Handle(DeleteBusinessCalendarCommand request, CancellationToken cancellationToken)
    {
        var calendar = await db.BusinessCalendars.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(BusinessCalendar), request.Id);

        var inUse = await db.SlaPolicies.AnyAsync(p => p.BusinessCalendarId == request.Id, cancellationToken);
        if (inUse)
        {
            throw new ConflictException("This calendar is used by at least one SLA policy and cannot be deleted.");
        }

        db.BusinessCalendars.Remove(calendar);
        await db.SaveChangesAsync(cancellationToken);
        cacheInvalidator.Invalidate(calendar.Id);
    }
}
