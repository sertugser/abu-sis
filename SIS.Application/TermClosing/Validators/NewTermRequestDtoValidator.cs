using FluentValidation;
using SIS.Application.TermClosing.DTOs;

namespace SIS.Application.TermClosing.Validators;

public class NewTermRequestDtoValidator : AbstractValidator<NewTermRequestDto>
{
    public NewTermRequestDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Donem adi zorunludur.")
            .MaximumLength(100).WithMessage("Donem adi en fazla 100 karakter olabilir.");

        RuleFor(x => x.Year)
            .InclusiveBetween(2020, 2100).WithMessage("Yil 2020-2100 arasinda olmalidir.");

        RuleFor(x => x.Semester)
            .IsInEnum().WithMessage("Gecersiz donem tipi.");

        RuleFor(x => x.RegistrationStart)
            .NotEmpty().WithMessage("Kayit baslangic tarihi zorunludur.");

        RuleFor(x => x.RegistrationEnd)
            .NotEmpty().WithMessage("Kayit bitis tarihi zorunludur.")
            .GreaterThan(x => x.RegistrationStart).WithMessage("Kayit bitis tarihi baslangictan sonra olmalidir.");

        RuleFor(x => x.TermStart)
            .NotEmpty().WithMessage("Donem baslangic tarihi zorunludur.");

        RuleFor(x => x.TermEnd)
            .NotEmpty().WithMessage("Donem bitis tarihi zorunludur.")
            .GreaterThan(x => x.TermStart).WithMessage("Donem bitis tarihi baslangictan sonra olmalidir.");
    }
}
