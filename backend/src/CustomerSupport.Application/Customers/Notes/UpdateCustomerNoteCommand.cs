using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Customers;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Customers.Notes;

/// <summary>
/// Edits a note's body. Deliberately carries no <c>[RequirePermission]</c>: the API contract is
/// "author or <c>customers.notes.manage</c>", which is an either/or the attribute-based pipeline
/// cannot express — a single required permission would incorrectly block an author who lacks it.
/// The rule is enforced inline in the handler instead.
/// </summary>
public record UpdateCustomerNoteCommand : IRequest
{
    public Guid CustomerId { get; init; }
    public Guid NoteId { get; init; }
    public string Body { get; init; } = string.Empty;
}

public class UpdateCustomerNoteCommandValidator : AbstractValidator<UpdateCustomerNoteCommand>
{
    public UpdateCustomerNoteCommandValidator() =>
        RuleFor(x => x.Body).NotEmpty().MaximumLength(8000);
}

public class UpdateCustomerNoteCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UpdateCustomerNoteCommand>
{
    public async Task Handle(UpdateCustomerNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await db.CustomerNotes.FirstOrDefaultAsync(
            n => n.Id == request.NoteId && n.CustomerId == request.CustomerId && !n.IsDeleted,
            cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerNote), request.NoteId);

        if (note.CreatedById != currentUser.UserId && !currentUser.HasPermission(Permissions.Customers.ManageNotes))
        {
            throw new ForbiddenException("You can only edit notes you created.");
        }

        note.Body = request.Body;
        await db.SaveChangesAsync(cancellationToken);
    }
}
