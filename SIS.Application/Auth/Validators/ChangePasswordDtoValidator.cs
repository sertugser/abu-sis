using FluentValidation;
using SIS.Application.Auth.DTOs;

namespace SIS.Application.Auth.Validators;

public class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordDtoValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MinimumLength(1);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}
