using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Channels.WebForms;

[RequirePermission(Permissions.Channels.ManageWebForms)]
public record GetWebFormsQuery : IRequest<IReadOnlyList<WebFormDefinitionDto>>;

public class GetWebFormsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetWebFormsQuery, IReadOnlyList<WebFormDefinitionDto>>
{
    public async Task<IReadOnlyList<WebFormDefinitionDto>> Handle(GetWebFormsQuery request, CancellationToken ct)
    {
        var forms = await db.WebFormDefinitions.OrderBy(f => f.CreatedAt).ToListAsync(ct);

        return forms.Select(f => new WebFormDefinitionDto
        {
            Id = f.Id,
            Key = f.Key,
            TitleEn = f.Title.En,
            TitleAr = f.Title.Ar,
            DescriptionEn = f.Description.En,
            DescriptionAr = f.Description.Ar,
            SubmitButtonLabelEn = f.SubmitButtonLabel.En,
            SubmitButtonLabelAr = f.SubmitButtonLabel.Ar,
            ThankYouMessageEn = f.ThankYouMessage.En,
            ThankYouMessageAr = f.ThankYouMessage.Ar,
            Fields = WebFormFieldSchemaSerializer.Deserialize(f.FieldsJson),
            DefaultCategoryId = f.DefaultCategoryId,
            DefaultPriorityId = f.DefaultPriorityId,
            DefaultDepartmentId = f.DefaultDepartmentId,
            RequireCaptcha = f.RequireCaptcha,
            RateLimitPerHour = f.RateLimitPerHour,
            IsActive = f.IsActive,
            SubmissionCount = f.SubmissionCount,
        }).ToList();
    }
}
