using FluentValidation;
using SIS.Application.Identity.DTOs;

namespace SIS.Application.Identity.Validators;

public class CreateMenuModuleDtoValidator : AbstractValidator<CreateMenuModuleDto>
{
    public CreateMenuModuleDtoValidator()
    {
        RuleFor(x => x.ParentId)
            .GreaterThan(0)
            .When(x => x.ParentId.HasValue);
        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[a-z0-9-]+$")
            .WithMessage("Slug sadece küçük harf, rakam ve tire içerebilir.");
        RuleFor(x => x.Label)
            .NotEmpty()
            .MaximumLength(200);
        RuleFor(x => x.Icon)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Icon));
        RuleFor(x => x.AllowedRolesCsv)
            .MaximumLength(300)
            .When(x => !string.IsNullOrWhiteSpace(x.AllowedRolesCsv));
    }
}
