using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Channels.WebForms;

[RequirePermission(Permissions.Channels.ManageWebForms)]
public record CreateWebFormCommand(WebFormDefinitionRequest Request) : IRequest<Guid>;

public class CreateWebFormCommandValidator : AbstractValidator<CreateWebFormCommand>
{
    public CreateWebFormCommandValidator()
    {
        RuleFor(x => x.Request.Key).NotEmpty().MaximumLength(64).Matches("^[a-z0-9-]+$")
            .WithMessage("Key must be lowercase letters, digits and hyphens only.");
        RuleFor(x => x.Request.TitleEn).NotEmpty();
        RuleFor(x => x.Request.TitleAr).NotEmpty();
        RuleFor(x => x.Request.Fields).NotEmpty();
    }
}

public class CreateWebFormCommandHandler(IAppDbContext db) : IRequestHandler<CreateWebFormCommand, Guid>
{
    public async Task<Guid> Handle(CreateWebFormCommand command, CancellationToken ct)
    {
        var request = command.Request;

        var keyTaken = await db.WebFormDefinitions.AnyAsync(f => f.Key == request.Key, ct);
        if (keyTaken)
        {
            throw new ConflictException($"A form with key \"{request.Key}\" already exists.");
        }

        var form = new WebFormDefinition
        {
            Key = request.Key,
            Title = new LocalizedText(request.TitleEn, request.TitleAr),
            Description = new LocalizedText(request.DescriptionEn, request.DescriptionAr),
            SubmitButtonLabel = new LocalizedText(request.SubmitButtonLabelEn, request.SubmitButtonLabelAr),
            ThankYouMessage = new LocalizedText(request.ThankYouMessageEn, request.ThankYouMessageAr),
            FieldsJson = WebFormFieldSchemaSerializer.Serialize(request.Fields),
            DefaultCategoryId = request.DefaultCategoryId,
            DefaultPriorityId = request.DefaultPriorityId,
            DefaultDepartmentId = request.DefaultDepartmentId,
            RequireCaptcha = request.RequireCaptcha,
            RateLimitPerHour = request.RateLimitPerHour,
            IsActive = request.IsActive,
        };

        db.WebFormDefinitions.Add(form);
        await db.SaveChangesAsync(ct);
        return form.Id;
    }
}

[RequirePermission(Permissions.Channels.ManageWebForms)]
public record UpdateWebFormCommand(Guid Id, WebFormDefinitionRequest Request) : IRequest;

public class UpdateWebFormCommandValidator : AbstractValidator<UpdateWebFormCommand>
{
    public UpdateWebFormCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request.Key).NotEmpty().MaximumLength(64).Matches("^[a-z0-9-]+$");
        RuleFor(x => x.Request.TitleEn).NotEmpty();
        RuleFor(x => x.Request.TitleAr).NotEmpty();
        RuleFor(x => x.Request.Fields).NotEmpty();
    }
}

public class UpdateWebFormCommandHandler(IAppDbContext db) : IRequestHandler<UpdateWebFormCommand>
{
    public async Task Handle(UpdateWebFormCommand command, CancellationToken ct)
    {
        var form = await db.WebFormDefinitions.FirstOrDefaultAsync(f => f.Id == command.Id, ct)
            ?? throw new NotFoundException(nameof(WebFormDefinition), command.Id);

        var request = command.Request;

        var keyTaken = await db.WebFormDefinitions.AnyAsync(f => f.Key == request.Key && f.Id != command.Id, ct);
        if (keyTaken)
        {
            throw new ConflictException($"A form with key \"{request.Key}\" already exists.");
        }

        form.Key = request.Key;
        form.Title = new LocalizedText(request.TitleEn, request.TitleAr);
        form.Description = new LocalizedText(request.DescriptionEn, request.DescriptionAr);
        form.SubmitButtonLabel = new LocalizedText(request.SubmitButtonLabelEn, request.SubmitButtonLabelAr);
        form.ThankYouMessage = new LocalizedText(request.ThankYouMessageEn, request.ThankYouMessageAr);
        form.FieldsJson = WebFormFieldSchemaSerializer.Serialize(request.Fields);
        form.DefaultCategoryId = request.DefaultCategoryId;
        form.DefaultPriorityId = request.DefaultPriorityId;
        form.DefaultDepartmentId = request.DefaultDepartmentId;
        form.RequireCaptcha = request.RequireCaptcha;
        form.RateLimitPerHour = request.RateLimitPerHour;
        form.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);
    }
}
