using FluentValidation;
using SIS.Application.Identity.DTOs;

namespace SIS.Application.Identity.Validators;

public class CreateMenuItemDtoValidator : AbstractValidator<CreateMenuItemDto>
{
    public CreateMenuItemDtoValidator()
    {
        RuleFor(x => x.ModuleId).GreaterThan(0);
        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[a-z0-9-]+$")
            .WithMessage("Slug sadece küçük harf, rakam ve tire içerebilir.");
        RuleFor(x => x.Label)
            .NotEmpty()
            .MaximumLength(200);
        RuleFor(x => x.Route)
            .NotEmpty()
            .MaximumLength(300)
            .Must(route => route.StartsWith('/'))
            .WithMessage("Route '/' ile başlamalıdır.");
        RuleFor(x => x.PermissionKeys)
            .NotNull();
        RuleForEach(x => x.PermissionKeys)
            .NotEmpty()
            .MaximumLength(200);
    }
}
