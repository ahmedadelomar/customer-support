using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Files;
using CustomerSupport.Application.Files.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Controllers;

/// <summary>
/// The shared attachment store (Customer Management / Notes and attachments). Every action here is
/// gated by <see cref="IAttachmentOwnerAuthorizer"/> inside the handler rather than a single
/// blanket permission — access genuinely "depends on owner".
/// </summary>
public class AttachmentsController : ApiControllerBase
{
    /// <summary>Uploads a file against a polymorphic owner. Multipart form data.</summary>
    [HttpPost]
    [RequestSizeLimit(long.MaxValue)] // the configured Attachments:MaxBytes check is the real limit — see UploadAttachmentCommandHandler
    [ProducesResponseType(typeof(AttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AttachmentDto>> Upload(
        [FromForm] string ownerType,
        [FromForm] Guid ownerId,
        IFormFile file,
        CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();

        var result = await Sender.Send(new UploadAttachmentCommand
        {
            OwnerType = ownerType,
            OwnerId = ownerId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            Content = stream,
        }, ct);

        return CreatedAtAction(nameof(Download), new { id = result.Id }, result);
    }

    /// <summary>Streams the file. The original filename is preserved, correctly encoded for non-ASCII names.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var result = await Sender.Send(new DownloadAttachmentQuery(id), ct);

        // ASP.NET Core's File() sets both the ASCII-safe `filename=` and the RFC 5987
        // `filename*=UTF-8''...` Content-Disposition parameters automatically when the name isn't
        // pure ASCII — an Arabic filename downloads with its real name in every modern browser.
        return File(result.Content, result.ContentType, result.FileName);
    }

    /// <summary>Soft-deletes an attachment. The file itself remains on disk for audit.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Sender.Send(new DeleteAttachmentCommand(id), ct);
        return NoContent();
    }

    /// <summary>The current size cap and allowed extensions, for client-side validation before an upload starts.</summary>
    [HttpGet("policy")]
    [ProducesResponseType(typeof(AttachmentPolicy), StatusCodes.Status200OK)]
    public async Task<ActionResult<AttachmentPolicy>> GetPolicy(
        [FromServices] IAttachmentPolicyProvider policyProvider, CancellationToken ct)
        => Ok(await policyProvider.GetPolicyAsync(ct));
}
