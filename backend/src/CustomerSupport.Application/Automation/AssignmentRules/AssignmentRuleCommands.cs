using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Automation;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Automation.AssignmentRules;

[RequirePermission(Permissions.Sla.ManageAssignmentRules)]
public record CreateAssignmentRuleCommand : IRequest<Guid>
{
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int EvaluationOrder { get; init; }
    public List<AssignmentRuleConditionInput> Conditions { get; init; } = [];
    public AssignmentStrategy Strategy { get; init; }
    public Guid? TargetDepartmentId { get; init; }
    public Guid? TargetTeamId { get; init; }
    public Guid? TargetUserId { get; init; }
    public bool RespectAgentAvailability { get; init; } = true;
    public bool StopProcessing { get; init; } = true;
}

public class CreateAssignmentRuleCommandValidator : AbstractValidator<CreateAssignmentRuleCommand>
{
    public CreateAssignmentRuleCommandValidator()
    {
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TargetUserId).NotEmpty().When(x => x.Strategy == AssignmentStrategy.Direct)
            .WithMessage("The Direct strategy requires a target user.");
        RuleFor(x => x).Must(x => x.TargetTeamId is not null || x.TargetDepartmentId is not null)
            .When(x => x.Strategy is AssignmentStrategy.RoundRobin or AssignmentStrategy.LoadBalanced or AssignmentStrategy.SkillBased)
            .WithMessage("This strategy requires a target team or department.")
            .OverridePropertyName("targetTeamId");
        RuleFor(x => x.TargetTeamId).NotEmpty()
            .When(x => x.Strategy == AssignmentStrategy.RoundRobin)
            .WithMessage("Round robin requires a target team (it advances that team's rotation cursor).");
    }
}

public class CreateAssignmentRuleCommandHandler(IAppDbContext db) : IRequestHandler<CreateAssignmentRuleCommand, Guid>
{
    public async Task<Guid> Handle(CreateAssignmentRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = new AssignmentRule
        {
            Name = new LocalizedText(request.NameEn, request.NameAr),
            Description = request.Description,
            EvaluationOrder = request.EvaluationOrder,
            IsActive = true,
            ConditionsJson = ConditionJson.Write(request.Conditions),
            Strategy = request.Strategy,
            TargetDepartmentId = request.TargetDepartmentId,
            TargetTeamId = request.TargetTeamId,
            TargetUserId = request.TargetUserId,
            RespectAgentAvailability = request.RespectAgentAvailability,
            StopProcessing = request.StopProcessing,
        };

        db.AssignmentRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);
        return rule.Id;
    }
}

[RequirePermission(Permissions.Sla.ManageAssignmentRules)]
public record UpdateAssignmentRuleCommand : IRequest
{
    public Guid Id { get; init; }
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
    public List<AssignmentRuleConditionInput> Conditions { get; init; } = [];
    public AssignmentStrategy Strategy { get; init; }
    public Guid? TargetDepartmentId { get; init; }
    public Guid? TargetTeamId { get; init; }
    public Guid? TargetUserId { get; init; }
    public bool RespectAgentAvailability { get; init; } = true;
    public bool StopProcessing { get; init; } = true;
}

public class UpdateAssignmentRuleCommandValidator : AbstractValidator<UpdateAssignmentRuleCommand>
{
    public UpdateAssignmentRuleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TargetUserId).NotEmpty().When(x => x.Strategy == AssignmentStrategy.Direct);
        RuleFor(x => x.TargetTeamId).NotEmpty().When(x => x.Strategy == AssignmentStrategy.RoundRobin);
    }
}

public class UpdateAssignmentRuleCommandHandler(IAppDbContext db) : IRequestHandler<UpdateAssignmentRuleCommand>
{
    public async Task Handle(UpdateAssignmentRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await db.AssignmentRules.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(AssignmentRule), request.Id);

        rule.Name = new LocalizedText(request.NameEn, request.NameAr);
        rule.Description = request.Description;
        rule.IsActive = request.IsActive;
        rule.ConditionsJson = ConditionJson.Write(request.Conditions);
        rule.Strategy = request.Strategy;
        rule.TargetDepartmentId = request.TargetDepartmentId;
        rule.TargetTeamId = request.TargetTeamId;
        rule.TargetUserId = request.TargetUserId;
        rule.RespectAgentAvailability = request.RespectAgentAvailability;
        rule.StopProcessing = request.StopProcessing;

        await db.SaveChangesAsync(cancellationToken);
    }
}

[RequirePermission(Permissions.Sla.ManageAssignmentRules)]
public record DeleteAssignmentRuleCommand(Guid Id) : IRequest;

public class DeleteAssignmentRuleCommandHandler(IAppDbContext db) : IRequestHandler<DeleteAssignmentRuleCommand>
{
    public async Task Handle(DeleteAssignmentRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await db.AssignmentRules.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(AssignmentRule), request.Id);

        db.AssignmentRules.Remove(rule);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Rewrites <c>EvaluationOrder</c> for every rule from the submitted order — dragging a row in the admin list.</summary>
[RequirePermission(Permissions.Sla.ManageAssignmentRules)]
public record ReorderAssignmentRulesCommand : IRequest
{
    public IReadOnlyList<Guid> OrderedIds { get; init; } = [];
}

public class ReorderAssignmentRulesCommandValidator : AbstractValidator<ReorderAssignmentRulesCommand>
{
    public ReorderAssignmentRulesCommandValidator() => RuleFor(x => x.OrderedIds).NotEmpty();
}

public class ReorderAssignmentRulesCommandHandler(IAppDbContext db) : IRequestHandler<ReorderAssignmentRulesCommand>
{
    public async Task Handle(ReorderAssignmentRulesCommand request, CancellationToken cancellationToken)
    {
        var rules = await db.AssignmentRules
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
