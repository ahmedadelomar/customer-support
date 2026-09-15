using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Customers.Dtos;
using CustomerSupport.Domain.Customers;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Customers.Notes;

/// <summary>Adds a note to a customer profile. Defaults to internal — see the class remark on <see cref="CustomerNote"/>.</summary>
[RequirePermission(Permissions.Customers.ManageNotes)]
public record CreateCustomerNoteCommand : IRequest<CustomerNoteDto>
{
    public Guid CustomerId { get; init; }
    public string Body { get; init; } = string.Empty;
    public bool IsInternal { get; init; } = true;
    public Guid? TicketId { get; init; }
}

public class CreateCustomerNoteCommandValidator : AbstractValidator<CreateCustomerNoteCommand>
{
    public CreateCustomerNoteCommandValidator() =>
        RuleFor(x => x.Body).NotEmpty().MaximumLength(8000);
}

public class CreateCustomerNoteCommandHandler(IAppDbContext db)
    : IRequestHandler<CreateCustomerNoteCommand, CustomerNoteDto>
{
    public async Task<CustomerNoteDto> Handle(CreateCustomerNoteCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
        {
            throw new NotFoundException(nameof(Domain.Customers.Customer), request.CustomerId);
        }

        var note = new CustomerNote
        {
            CustomerId = request.CustomerId,
            TicketId = request.TicketId,
            Body = request.Body,
            IsInternal = request.IsInternal,
        };

        db.CustomerNotes.Add(note);
        await db.SaveChangesAsync(cancellationToken);

        return new CustomerNoteDto
        {
            Id = note.Id,
            CustomerId = note.CustomerId,
            TicketId = note.TicketId,
            Body = note.Body,
            IsPinned = note.IsPinned,
            IsInternal = note.IsInternal,
            CreatedById = note.CreatedById,
            CreatedAt = note.CreatedAt,
            CanEdit = true, // the author, moments after creating it
            Attachments = [],
        };
    }
}
