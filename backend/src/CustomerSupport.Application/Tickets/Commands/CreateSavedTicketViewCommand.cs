using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Tickets;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Tickets.Commands;

/// <summary>Saves the current ticket-list query params as a named, reusable view.</summary>
[RequirePermission(Permissions.Tickets.View)]
public record CreateSavedTicketViewCommand : IRequest<Guid>
{
    public string NameEn { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string FiltersJson { get; init; } = "{}";
    public bool IsShared { get; init; }
}

public class CreateSavedTicketViewCommandValidator : AbstractValidator<CreateSavedTicketViewCommand>
{
    public CreateSavedTicketViewCommandValidator()
    {
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FiltersJson).NotEmpty();
    }
}

public class CreateSavedTicketViewCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<CreateSavedTicketViewCommand, Guid>
{
    public async Task<Guid> Handle(CreateSavedTicketViewCommand request, CancellationToken cancellationToken)
    {
        var nextOrder = await db.SavedTicketViews
            .Where(v => v.OwnerId == currentUser.UserId)
            .Select(v => (int?)v.DisplayOrder)
            .MaxAsync(cancellationToken) ?? -1;

        var view = new SavedTicketView
        {
            BranchId = currentUser.BranchId,
            OwnerId = currentUser.UserId!.Value,
            Name = new LocalizedText(request.NameEn, request.NameAr),
            FiltersJson = request.FiltersJson,
            DisplayOrder = nextOrder + 1,
            IsShared = request.IsShared,
        };

        db.SavedTicketViews.Add(view);
        await db.SaveChangesAsync(cancellationToken);
        return view.Id;
    }
}
