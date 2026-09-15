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
/// Edits names, description, code, portal visibility and defaults. Reparenting is
/// <see cref="MoveTicketCategoryCommand"/>'s job — it carries its own subtree-rewrite rules and
/// self-descendant guard, so it must not be duplicated here.
/// </summary>
[RequirePermission(Permissions.Tickets.ManageCategories)]
public record UpdateTicketCategoryCommand : IRequest
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int DisplayOrder { get; init; }
    public Guid? DefaultPriorityId { get; init; }
    public Guid? DefaultDepartmentId { get; init; }
    public Guid? DefaultSlaPolicyId { get; init; }
    public bool IsVisibleInPortal { get; init; }
}

public class UpdateTicketCategoryCommandValidator : AbstractValidator<UpdateTicketCategoryCommand>
{
    public UpdateTicketCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64).Matches("^[a-z0-9-]+$")
            .WithMessage("Code must be lowercase letters, digits and hyphens only.");
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
    }
}

public class UpdateTicketCategoryCommandHandler(IAppDbContext db) : IRequestHandler<UpdateTicketCategoryCommand>
{
    public async Task Handle(UpdateTicketCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await db.TicketCategories.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TicketCategory), request.Id);

        if (request.Code != category.Code)
        {
            if (await db.TicketCategories.AnyAsync(c => c.Id != request.Id && c.Code == request.Code, cancellationToken))
            {
                throw new ConflictException($"A category with code '{request.Code}' already exists.");
            }

            // The node's own path segment is its code — renaming it moves every descendant's path
            // too, exactly like a reparent. Compute the new segment from the (unchanged) parent path.
            var oldPath = category.Path;
            var parentPath = oldPath[..^(category.Code.Length + 1)];
            var newPath = $"{parentPath}{request.Code}/";

            await db.TicketCategories
                .Where(c => c.Path.StartsWith(oldPath))
                .ExecuteUpdateAsync(s => s.SetProperty(
                    c => c.Path,
                    c => newPath + c.Path.Substring(oldPath.Length)), cancellationToken);

            category.Code = request.Code;
            category.Path = newPath;
        }

        category.Name = new LocalizedText(request.NameEn, request.NameAr);
        category.Description = request.Description;
        category.DisplayOrder = request.DisplayOrder;
        category.DefaultPriorityId = request.DefaultPriorityId;
        category.DefaultDepartmentId = request.DefaultDepartmentId;
        category.DefaultSlaPolicyId = request.DefaultSlaPolicyId;
        category.IsVisibleInPortal = request.IsVisibleInPortal;

        await db.SaveChangesAsync(cancellationToken);
    }
}
