using System.Security.Cryptography;
using System.Text;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Customers.Dtos;
using CustomerSupport.Domain.Customers;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Customers.Contacts;

/// <summary>
/// Confirms a previously sent code. A wrong guess is reported back with the attempts remaining
/// rather than thrown as an error — only running out of attempts, or having no active code at all,
/// is treated as a failure the caller cannot recover from without requesting a new code.
/// </summary>
[RequirePermission(Permissions.Customers.ManageContacts)]
public record ConfirmContactVerificationCommand : IRequest<ConfirmContactVerificationResultDto>
{
    public Guid CustomerId { get; init; }
    public Guid ContactId { get; init; }
    public string Code { get; init; } = string.Empty;
}

public class ConfirmContactVerificationCommandValidator : AbstractValidator<ConfirmContactVerificationCommand>
{
    public ConfirmContactVerificationCommandValidator() =>
        RuleFor(x => x.Code).NotEmpty().Length(6).Matches(@"^\d{6}$")
            .WithMessage("The code must be 6 digits.");
}

public class ConfirmContactVerificationCommandHandler(IAppDbContext db, IDateTimeProvider clock)
    : IRequestHandler<ConfirmContactVerificationCommand, ConfirmContactVerificationResultDto>
{
    private const int MaxAttempts = 5;

    public async Task<ConfirmContactVerificationResultDto> Handle(
        ConfirmContactVerificationCommand request, CancellationToken cancellationToken)
    {
        var contact = await db.CustomerContacts.FirstOrDefaultAsync(
            c => c.Id == request.ContactId && c.CustomerId == request.CustomerId && !c.IsDeleted,
            cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerContact), request.ContactId);

        var verification = await db.ContactVerifications
            .Where(v => v.CustomerContactId == request.ContactId && v.ConfirmedAt == null)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (verification is null || verification.ExpiresAt <= clock.UtcNow)
        {
            throw new ConflictException("This code has expired or does not exist. Request a new one.");
        }

        if (verification.Attempts >= MaxAttempts)
        {
            throw new ConflictException("No attempts remaining for this code. Request a new one.");
        }

        if (Hash(request.Code) != verification.CodeHash)
        {
            verification.Attempts += 1;
            await db.SaveChangesAsync(cancellationToken);

            return new ConfirmContactVerificationResultDto
            {
                Success = false,
                RemainingAttempts = MaxAttempts - verification.Attempts,
            };
        }

        verification.ConfirmedAt = clock.UtcNow;
        contact.IsVerified = true;
        contact.VerifiedAt = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return new ConfirmContactVerificationResultDto { Success = true, RemainingAttempts = MaxAttempts };
    }

    private static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
