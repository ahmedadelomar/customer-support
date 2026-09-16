using CustomerSupport.Application.Automation.AssignmentRules;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Automation;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Automation.EscalationRules;

[RequirePermission(Permissions.Sla.ManageEscalationRules)]
public record CreateEscalationRuleCommand : IRequest<Guid>
{
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int EvaluationOrder { get; init; }
    public EscalationTrigger Trigger { get; init; }
    public int? ThresholdPercent { get; init; }
    public int? ThresholdMinutes { get; init; }
    public int? ThresholdCount { get; init; }
    public SlaTargetType? TargetType { get; init; }
    public List<AssignmentRuleConditionInput> Conditions { get; init; } = [];
    public EscalationActionType Action { get; init; }
    public Guid? ActionTargetUserId { get; init; }
    public Guid? ActionTargetTeamId { get; init; }
    public Guid? ActionTargetDepartmentId { get; init; }
    public Guid? ActionTargetPriorityId { get; init; }
    public Guid? ActionNotifyRoleId { get; init; }
    public int CooldownMinutes { get; init; } = 60;
    public int MaxFiresPerTicket { get; init; } = 3;
}

public class CreateEscalationRuleCommandValidator : AbstractValidator<CreateEscalationRuleCommand>
{
    public CreateEscalationRuleCommandValidator()
    {
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CooldownMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxFiresPerTicket).GreaterThanOrEqualTo(0);

        RuleFor(x => x.ThresholdPercent).InclusiveBetween(1, 100)
            .When(x => x.Trigger == EscalationTrigger.ApproachingBreach)
            .WithMessage("Approaching-breach rules need a threshold percentage.");
        RuleFor(x => x.ThresholdMinutes).GreaterThan(0)
            .When(x => x.Trigger == EscalationTrigger.NoAgentResponse)
            .WithMessage("No-agent-response rules need a threshold in minutes.");
        RuleFor(x => x.ThresholdCount).GreaterThan(0)
            .When(x => x.Trigger is EscalationTrigger.CustomerReplyCount or EscalationTrigger.ReopenCount)
            .WithMessage("This trigger needs a threshold count.");

        RuleFor(x => x.ActionTargetDepartmentId).NotEmpty()
            .When(x => x.Action == EscalationActionType.ChangeDepartment)
            .WithMessage("ChangeDepartment needs a target department.");
        RuleFor(x => x).Must(x => x.ActionTargetUserId is not null)
            .When(x => x.Action == EscalationActionType.AddWatcher)
            .WithMessage("AddWatcher needs a target user.")
            .OverridePropertyName("actionTargetUserId");
        RuleFor(x => x).Must(x => x.ActionTargetUserId is not null || x.ActionTargetTeamId is not null)
            .When(x => x.Action == EscalationActionType.Reassign)
            .WithMessage("Reassign needs a target user or team.")
            .OverridePropertyName("actionTargetUserId");
    }
}

public class CreateEscalationRuleCommandHandler(IAppDbContext db) : IRequestHandler<CreateEscalationRuleCommand, Guid>
{
    public async Task<Guid> Handle(CreateEscalationRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = new EscalationRule { IsActive = true };
        EscalationRuleMapping.Apply(rule, request.NameEn, request.NameAr, request.Description, request.EvaluationOrder,
            request.Trigger, request.ThresholdPercent, request.ThresholdMinutes, request.ThresholdCount, request.TargetType,
            request.Conditions, request.Action, request.ActionTargetUserId, request.ActionTargetTeamId,
            request.ActionTargetDepartmentId, request.ActionTargetPriorityId, request.ActionNotifyRoleId,
            request.CooldownMinutes, request.MaxFiresPerTicket);

        db.EscalationRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);
        return rule.Id;
    }
}

/// <summary>Standalone from <see cref="CreateEscalationRuleCommand"/> deliberately — the two commands
/// return different MediatR response types (<c>Guid</c> vs <c>Unit</c>), and a record cannot inherit
/// from another that already closes <c>IRequest&lt;T&gt;</c> without MediatR's handler resolution
/// becoming ambiguous between the two implemented <c>IRequest&lt;T&gt;</c> interfaces.</summary>
[RequirePermission(Permissions.Sla.ManageEscalationRules)]
public record UpdateEscalationRuleCommand : IRequest
{
    public Guid Id { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int EvaluationOrder { get; init; }
    public bool IsActive { get; init; } = true;
    public EscalationTrigger Trigger { get; init; }
    public int? ThresholdPercent { get; init; }
    public int? ThresholdMinutes { get; init; }
    public int? ThresholdCount { get; init; }
    public SlaTargetType? TargetType { get; init; }
    public List<AssignmentRuleConditionInput> Conditions { get; init; } = [];
    public EscalationActionType Action { get; init; }
    public Guid? ActionTargetUserId { get; init; }
    public Guid? ActionTargetTeamId { get; init; }
    public Guid? ActionTargetDepartmentId { get; init; }
    public Guid? ActionTargetPriorityId { get; init; }
    public Guid? ActionNotifyRoleId { get; init; }
    public int CooldownMinutes { get; init; } = 60;
    public int MaxFiresPerTicket { get; init; } = 3;
}

public class UpdateEscalationRuleCommandValidator : AbstractValidator<UpdateEscalationRuleCommand>
{
    public UpdateEscalationRuleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CooldownMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxFiresPerTicket).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ThresholdPercent).InclusiveBetween(1, 100).When(x => x.Trigger == EscalationTrigger.ApproachingBreach);
        RuleFor(x => x.ThresholdMinutes).GreaterThan(0).When(x => x.Trigger == EscalationTrigger.NoAgentResponse);
        RuleFor(x => x.ThresholdCount).GreaterThan(0).When(x => x.Trigger is EscalationTrigger.CustomerReplyCount or EscalationTrigger.ReopenCount);
        RuleFor(x => x.ActionTargetDepartmentId).NotEmpty().When(x => x.Action == EscalationActionType.ChangeDepartment);
        RuleFor(x => x).Must(x => x.ActionTargetUserId is not null).When(x => x.Action == EscalationActionType.AddWatcher)
            .WithMessage("AddWatcher needs a target user.").OverridePropertyName("actionTargetUserId");
        RuleFor(x => x).Must(x => x.ActionTargetUserId is not null || x.ActionTargetTeamId is not null)
            .When(x => x.Action == EscalationActionType.Reassign)
            .WithMessage("Reassign needs a target user or team.").OverridePropertyName("actionTargetUserId");
    }
}

public class UpdateEscalationRuleCommandHandler(IAppDbContext db) : IRequestHandler<UpdateEscalationRuleCommand>
{
    public async Task Handle(UpdateEscalationRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await db.EscalationRules.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(EscalationRule), request.Id);

        EscalationRuleMapping.Apply(rule, request.NameEn, request.NameAr, request.Description, request.EvaluationOrder,
            request.Trigger, request.ThresholdPercent, request.ThresholdMinutes, request.ThresholdCount, request.TargetType,
            request.Conditions, request.Action, request.ActionTargetUserId, request.ActionTargetTeamId,
            request.ActionTargetDepartmentId, request.ActionTargetPriorityId, request.ActionNotifyRoleId,
            request.CooldownMinutes, request.MaxFiresPerTicket);
        rule.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}

internal static class EscalationRuleMapping
{
    public static void Apply(
        EscalationRule rule, string nameEn, string nameAr, string? description, int evaluationOrder,
        EscalationTrigger trigger, int? thresholdPercent, int? thresholdMinutes, int? thresholdCount, SlaTargetType? targetType,
        List<AssignmentRuleConditionInput> conditions, EscalationActionType action, Guid? actionTargetUserId,
        Guid? actionTargetTeamId, Guid? actionTargetDepartmentId, Guid? actionTargetPriorityId, Guid? actionNotifyRoleId,
        int cooldownMinutes, int maxFiresPerTicket)
    {
        rule.Name = new LocalizedText(nameEn, nameAr);
        rule.Description = description;
        rule.EvaluationOrder = evaluationOrder;
        rule.Trigger = trigger;
        rule.ThresholdPercent = thresholdPercent;
        rule.ThresholdMinutes = thresholdMinutes;
        rule.ThresholdCount = thresholdCount;
        rule.TargetType = targetType;
        rule.ConditionsJson = ConditionJson.Write(conditions);
        rule.Action = action;
        rule.ActionTargetUserId = actionTargetUserId;
        rule.ActionTargetTeamId = actionTargetTeamId;
        rule.ActionTargetDepartmentId = actionTargetDepartmentId;
        rule.ActionTargetPriorityId = actionTargetPriorityId;
        rule.ActionNotifyRoleId = actionNotifyRoleId;
        rule.CooldownMinutes = cooldownMinutes;
        rule.MaxFiresPerTicket = maxFiresPerTicket;
    }
}

[RequirePermission(Permissions.Sla.ManageEscalationRules)]
public record DeleteEscalationRuleCommand(Guid Id) : IRequest;

public class DeleteEscalationRuleCommandHandler(IAppDbContext db) : IRequestHandler<DeleteEscalationRuleCommand>
{
    public async Task Handle(DeleteEscalationRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await db.EscalationRules.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(EscalationRule), request.Id);

        // Deactivating stops a rule immediately while keeping its firing history; deleting removes
        // it outright — both are offered, but the decision log rows always survive the rule itself
        // (AutomationRunLog only references RuleId, it does not cascade).
        db.EscalationRules.Remove(rule);
        await db.SaveChangesAsync(cancellationToken);
    }
}

[RequirePermission(Permissions.Sla.ManageEscalationRules)]
public record ReorderEscalationRulesCommand : IRequest
{
    public IReadOnlyList<Guid> OrderedIds { get; init; } = [];
}

public class ReorderEscalationRulesCommandValidator : AbstractValidator<ReorderEscalationRulesCommand>
{
    public ReorderEscalationRulesCommandValidator() => RuleFor(x => x.OrderedIds).NotEmpty();
}

public class ReorderEscalationRulesCommandHandler(IAppDbContext db) : IRequestHandler<ReorderEscalationRulesCommand>
{
    public async Task Handle(ReorderEscalationRulesCommand request, CancellationToken cancellationToken)
    {
        var rules = await db.EscalationRules
            .Where(r => request.OrderedIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        for (var i = 0; i < request.OrderedIds.Count; i++)
        {
            if (rules.TryGetValue(request.OrderedIds[i], out var rule))
            {
                rule.EvaluationOrder = i;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
