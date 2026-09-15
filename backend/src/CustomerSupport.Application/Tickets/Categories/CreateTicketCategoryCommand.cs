using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Tickets;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Categories;

/// <summary>
/// Creates a category node. <c>Path</c> and <c>Depth</c> are computed from the parent, never
/// supplied by the caller, so the materialised path invariant can never be typed incorrectly.
/// </summary>
[RequirePermission(Permissions.Tickets.ManageCategories)]
public record CreateTicketCategoryCommand : IRequest<Guid>
{
    public Guid? ParentId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int DisplayOrder { get; init; }
    public Guid? DefaultPriorityId { get; init; }
    public Guid? DefaultDepartmentId { get; init; }
    public Guid? DefaultSlaPolicyId { get; init; }
    public bool IsVisibleInPortal { get; init; } = true;
}

public class CreateTicketCategoryCommandValidator : AbstractValidator<CreateTicketCategoryCommand>
{
    public CreateTicketCategoryCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64).Matches("^[a-z0-9-]+$")
            .WithMessage("Code must be lowercase letters, digits and hyphens only.");
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
    }
}

public class CreateTicketCategoryCommandHandler(IAppDbContext db)
    : IRequestHandler<CreateTicketCategoryCommand, Guid>
{
    public async Task<Guid> Handle(CreateTicketCategoryCommand request, CancellationToken cancellationToken)
    {
        if (await db.TicketCategories.AnyAsync(c => c.Code == request.Code, cancellationToken))
        {
            throw new ConflictException($"A category with code '{request.Code}' already exists.");
        }

        TicketCategory? parent = null;
        if (request.ParentId is { } parentId)
        {
            parent = await db.TicketCategories.FirstOrDefaultAsync(c => c.Id == parentId, cancellationToken)
                ?? throw new NotFoundException(nameof(TicketCategory), parentId);
        }

        var category = new TicketCategory
        {
            ParentId = request.ParentId,
            Code = request.Code,
            Name = new LocalizedText(request.NameEn, request.NameAr),
            Description = request.Description,
            Path = $"{parent?.Path ?? "/"}{request.Code}/",
            Depth = (parent?.Depth ?? -1) + 1,
            DisplayOrder = request.DisplayOrder,
            DefaultPriorityId = request.DefaultPriorityId,
            DefaultDepartmentId = request.DefaultDepartmentId,
            DefaultSlaPolicyId = request.DefaultSlaPolicyId,
            IsVisibleInPortal = request.IsVisibleInPortal,
            IsActive = true,
        };

        db.TicketCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return category.Id;
    }
}
