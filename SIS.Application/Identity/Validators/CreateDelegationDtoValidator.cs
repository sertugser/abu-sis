using FluentValidation;
using SIS.Application.Identity.DTOs;

namespace SIS.Application.Identity.Validators;

public class CreateDelegationDtoValidator : AbstractValidator<CreateDelegationDto>
{
    public CreateDelegationDtoValidator()
    {
        RuleFor(x => x.DelegatorUserId).GreaterThan(0);
        RuleFor(x => x.DelegateeUserId).GreaterThan(0);
        RuleFor(x => x.DelegateeUserId)
            .NotEqual(x => x.DelegatorUserId)
            .WithMessage("Bir kullanıcı kendisine vekâlet veremez.");
        RuleFor(x => x.EndsAtUtc)
            .GreaterThan(x => x.StartsAtUtc)
            .WithMessage("Bitiş zamanı başlangıçtan sonra olmalı.");
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(500);
    }
}
