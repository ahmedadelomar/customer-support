using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Channels;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Channels.WebForms;

/// <summary>Anonymous — no <see cref="Common.Security.RequirePermissionAttribute"/>. This is what the public page fetches to render the form.</summary>
public record GetPublicWebFormSchemaQuery(string Key) : IRequest<PublicWebFormSchemaDto>;

public class GetPublicWebFormSchemaQueryHandler(IAppDbContext db)
    : IRequestHandler<GetPublicWebFormSchemaQuery, PublicWebFormSchemaDto>
{
    public async Task<PublicWebFormSchemaDto> Handle(GetPublicWebFormSchemaQuery request, CancellationToken ct)
    {
        var form = await db.WebFormDefinitions
            .FirstOrDefaultAsync(f => f.Key == request.Key && f.IsActive, ct)
            ?? throw new NotFoundException(nameof(WebFormDefinition), request.Key);

        return new PublicWebFormSchemaDto(
            form.Key,
            form.Title.En, form.Title.Ar,
            form.Description.En, form.Description.Ar,
            form.SubmitButtonLabel.En, form.SubmitButtonLabel.Ar,
            WebFormFieldSchemaSerializer.Deserialize(form.FieldsJson),
            form.RequireCaptcha);
    }
}
