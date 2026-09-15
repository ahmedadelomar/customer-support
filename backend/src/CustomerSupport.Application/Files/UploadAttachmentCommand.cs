using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Files.Dtos;
using CustomerSupport.Domain.Files;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Files;

/// <summary>
/// Uploads a file against a polymorphic owner. Deliberately carries no <c>[RequirePermission]</c> —
/// access is "depends on owner" per the API contract, decided entirely by
/// <see cref="IAttachmentOwnerAuthorizer"/> rather than a single blanket permission.
/// </summary>
public record UploadAttachmentCommand : IRequest<AttachmentDto>
{
    public string OwnerType { get; init; } = string.Empty;
    public Guid OwnerId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }

    /// <summary>
    /// The controller's multipart-file stream. MediatR is in-process, so carrying a live stream
    /// through the command is safe here (there is no serialisation boundary to cross) — this is
    /// the same trade-off Clean Architecture's own reference template makes for file upload.
    /// </summary>
    public Stream Content { get; init; } = Stream.Null;
}

public class UploadAttachmentCommandValidator : AbstractValidator<UploadAttachmentCommand>
{
    public UploadAttachmentCommandValidator()
    {
        RuleFor(x => x.OwnerType).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(400);
        RuleFor(x => x.SizeBytes).GreaterThan(0);
    }
}

public class UploadAttachmentCommandHandler(
    IAppDbContext db,
    IAttachmentOwnerAuthorizer authorizer,
    IAttachmentPolicyProvider policyProvider,
    IFileStorage storage,
    IVirusScanner scanner)
    : IRequestHandler<UploadAttachmentCommand, AttachmentDto>
{
    public async Task<AttachmentDto> Handle(UploadAttachmentCommand request, CancellationToken cancellationToken)
    {
        // Validation happens before any bytes are persisted — extension and size first, then
        // authorisation, then (and only then) does anything touch the file system.
        var policy = await policyProvider.GetPolicyAsync(cancellationToken);
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();

        if (string.IsNullOrEmpty(extension) || !policy.AllowedExtensions.Contains(extension))
        {
            throw new CustomerSupport.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                ["File"] = [$"Files of type '{extension}' are not allowed."],
            });
        }

        if (request.SizeBytes > policy.MaxBytes)
        {
            throw new CustomerSupport.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                ["File"] = [$"The file exceeds the {policy.MaxBytes / (1024 * 1024)} MB limit."],
            });
        }

        if (!await authorizer.CanAccessAsync(request.OwnerType, request.OwnerId, cancellationToken))
        {
            throw new ForbiddenException("You do not have access to attach files to this record.");
        }

        var key = await storage.SaveAsync(request.Content, request.FileName, request.ContentType, cancellationToken);

        // Scanned before the row exists: an infected file never gets an id, so it can never be
        // referenced or downloaded — "before the file is downloadable" means before it exists at all.
        var scanResult = await scanner.ScanAsync(key, cancellationToken);
        if (scanResult == "infected")
        {
            await storage.DeleteAsync(key, cancellationToken);
            throw new ConflictException("This file failed a virus scan and was not saved.");
        }

        var attachment = new Attachment
        {
            OwnerType = request.OwnerType,
            OwnerId = request.OwnerId,
            FileName = request.FileName,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            StorageKey = key,
            ScanResult = scanResult,
        };

        db.Attachments.Add(attachment);

        // Only CustomerNote tracks a denormalised count today; the other owner types listed in
        // IAttachmentOwnerAuthorizer don't have an equivalent field yet.
        if (request.OwnerType == "CustomerNote")
        {
            var note = await db.CustomerNotes.FirstOrDefaultAsync(n => n.Id == request.OwnerId, cancellationToken);
            if (note is not null)
            {
                note.AttachmentCount += 1;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return new AttachmentDto
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            SizeBytes = attachment.SizeBytes,
            CreatedAt = attachment.CreatedAt,
        };
    }
}
