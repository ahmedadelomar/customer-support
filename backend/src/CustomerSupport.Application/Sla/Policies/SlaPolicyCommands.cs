using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Sla;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ValidationException = CustomerSupport.Application.Common.Exceptions.ValidationException;

namespace CustomerSupport.Application.Sla.Policies;

/// <summary>Creates an SLA policy with its targets and conditions in one write.</summary>
[RequirePermission(Permissions.Sla.ManagePolicies)]
public record CreateSlaPolicyCommand : IRequest<Guid>
{
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid BusinessCalendarId { get; init; }
    public int EvaluationOrder { get; init; }
    public bool IsDefault { get; init; }
    public int WarningThresholdPercent { get; init; } = 80;
    public bool PauseOnPendingCustomer { get; init; } = true;
    public List<SlaTargetInput> Targets { get; init; } = [];
    public List<SlaConditionInput> Conditions { get; init; } = [];
}

public class CreateSlaPolicyCommandValidator : AbstractValidator<CreateSlaPolicyCommand>
{
    public CreateSlaPolicyCommandValidator()
    {
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BusinessCalendarId).NotEmpty();
        RuleFor(x => x.WarningThresholdPercent).InclusiveBetween(1, 99);
        RuleForEach(x => x.Targets).ChildRules(t =>
        {
            t.RuleFor(x => x.PriorityId).NotEmpty();
            t.RuleFor(x => x.FirstResponseMinutes).GreaterThan(0);
            t.RuleFor(x => x.ResolutionMinutes).GreaterThan(0);
        });
    }
}

public class CreateSlaPolicyCommandHandler(IAppDbContext db) : IRequestHandler<CreateSlaPolicyCommand, Guid>
{
    public async Task<Guid> Handle(CreateSlaPolicyCommand request, CancellationToken cancellationToken)
    {
        await SlaPolicyGuards.EnsureCalendarExistsAsync(db, request.BusinessCalendarId, cancellationToken);
        if (!request.IsDefault)
        {
            await SlaPolicyGuards.EnsureCoverageOrThrowAsync(db, request.Targets, cancellationToken);
        }

        if (request.IsDefault)
        {
            await db.SlaPolicies.Where(p => p.IsDefault)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDefault, false), cancellationToken);
        }

        var policy = new SlaPolicy
        {
            Name = new LocalizedText(request.NameEn, request.NameAr),
            Description = request.Description,
            BusinessCalendarId = request.BusinessCalendarId,
            EvaluationOrder = request.EvaluationOrder,
            IsDefault = request.IsDefault,
            IsActive = true,
            WarningThresholdPercent = request.WarningThresholdPercent,
            PauseOnPendingCustomer = request.PauseOnPendingCustomer,
        };

        foreach (var target in request.Targets)
        {
            policy.Targets.Add(new SlaTarget
            {
                PriorityId = target.PriorityId,
                FirstResponseMinutes = target.FirstResponseMinutes,
                ResolutionMinutes = target.ResolutionMinutes,
            });
        }

        foreach (var condition in request.Conditions)
        {
            policy.Conditions.Add(new SlaPolicyCondition
            {
                Field = condition.Field,
                Operator = condition.Operator,
                Value = condition.Value,
            });
        }

        db.SlaPolicies.Add(policy);
        await db.SaveChangesAsync(cancellationToken);
        return policy.Id;
    }
}

/// <summary>Replaces a policy's fields, targets and conditions.</summary>
[RequirePermission(Permissions.Sla.ManagePolicies)]
public record UpdateSlaPolicyCommand : IRequest
{
    public Guid Id { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid BusinessCalendarId { get; init; }
    public int EvaluationOrder { get; init; }
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; } = true;
    public int WarningThresholdPercent { get; init; } = 80;
    public bool PauseOnPendingCustomer { get; init; } = true;
    public List<SlaTargetInput> Targets { get; init; } = [];
    public List<SlaConditionInput> Conditions { get; init; } = [];
}

public class UpdateSlaPolicyCommandValidator : AbstractValidator<UpdateSlaPolicyCommand>
{
    public UpdateSlaPolicyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BusinessCalendarId).NotEmpty();
        RuleFor(x => x.WarningThresholdPercent).InclusiveBetween(1, 99);
    }
}

public class UpdateSlaPolicyCommandHandler(IAppDbContext db) : IRequestHandler<UpdateSlaPolicyCommand>
{
    public async Task Handle(UpdateSlaPolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = await db.SlaPolicies
            .Include(p => p.Targets)
            .Include(p => p.Conditions)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SlaPolicy), request.Id);

        await SlaPolicyGuards.EnsureCalendarExistsAsync(db, request.BusinessCalendarId, cancellationToken);

        if (policy.IsDefault && !request.IsDefault)
        {
            var anotherDefaultExists = await db.SlaPolicies
                .AnyAsync(p => p.Id != request.Id && p.IsDefault, cancellationToken);
            if (!anotherDefaultExists)
            {
                throw new ConflictException("At least one policy must remain the default.");
            }
        }

        if (!request.IsDefault)
        {
            // A non-default policy with no matching conditions falls through to the default, so
            // gaps are tolerable there — only the coverage-for-every-priority guard applies.
            await SlaPolicyGuards.EnsureCoverageOrThrowAsync(db, request.Targets, cancellationToken);
        }

        if (request.IsDefault && !policy.IsDefault)
        {
            await db.SlaPolicies.Where(p => p.Id != request.Id && p.IsDefault)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDefault, false), cancellationToken);
        }

        policy.Name = new LocalizedText(request.NameEn, request.NameAr);
        policy.Description = request.Description;
        policy.BusinessCalendarId = request.BusinessCalendarId;
        policy.EvaluationOrder = request.EvaluationOrder;
        policy.IsDefault = request.IsDefault;
        policy.IsActive = request.IsActive;
        policy.WarningThresholdPercent = request.WarningThresholdPercent;
        policy.PauseOnPendingCustomer = request.PauseOnPendingCustomer;

        db.SlaTargets.RemoveRange(policy.Targets);
        policy.Targets.Clear();
        foreach (var target in request.Targets)
        {
            policy.Targets.Add(new SlaTarget
            {
                PriorityId = target.PriorityId,
                FirstResponseMinutes = target.FirstResponseMinutes,
                ResolutionMinutes = target.ResolutionMinutes,
            });
        }

        db.SlaPolicyConditions.RemoveRange(policy.Conditions);
        policy.Conditions.Clear();
        foreach (var condition in request.Conditions)
        {
            policy.Conditions.Add(new SlaPolicyCondition
            {
                Field = condition.Field,
                Operator = condition.Operator,
                Value = condition.Value,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Deletes a policy. Refused for the default policy — there must always be a fallback.</summary>
[RequirePermission(Permissions.Sla.ManagePolicies)]
public record DeleteSlaPolicyCommand(Guid Id) : IRequest;

public class DeleteSlaPolicyCommandHandler(IAppDbContext db) : IRequestHandler<DeleteSlaPolicyCommand>
{
    public async Task Handle(DeleteSlaPolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = await db.SlaPolicies.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SlaPolicy), request.Id);

        if (policy.IsDefault)
        {
            throw new ConflictException("The default policy cannot be deleted — set another policy as default first.");
        }

        db.SlaPolicies.Remove(policy);
        await db.SaveChangesAsync(cancellationToken);
    }
}

internal static class SlaPolicyGuards
{
    public static async Task EnsureCalendarExistsAsync(IAppDbContext db, Guid calendarId, CancellationToken ct)
    {
        if (!await db.BusinessCalendars.AnyAsync(c => c.Id == calendarId, ct))
        {
            throw new NotFoundException(nameof(BusinessCalendar), calendarId);
        }
    }

    /// <summary>
    /// A policy must cover every ACTIVE priority, or a ticket at an uncovered priority silently
    /// gets no clock. The default policy is exempt from this check by its callers — it is itself
    /// the fallback, so a gap there is the "no clocks" product rule working as designed rather
    /// than a configuration mistake.
    /// </summary>
    public static async Task EnsureCoverageOrThrowAsync(IAppDbContext db, List<SlaTargetInput> targets, CancellationToken ct)
    {
        var activePriorityIds = await db.TicketPriorities
            .Where(p => p.IsActive)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var coveredIds = targets.Select(t => t.PriorityId).ToHashSet();
        var missing = activePriorityIds.Where(id => !coveredIds.Contains(id)).ToList();

        if (missing.Count > 0)
        {
            var names = await db.TicketPriorities
                .Where(p => missing.Contains(p.Id))
                .Select(p => p.Name.En)
                .ToListAsync(ct);

            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["targets"] = [$"A target is required for every active priority. Missing: {string.Join(", ", names)}."],
            });
        }
    }
}
