using FluentValidation;
using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Validators;

public class ChangeStudentNumberDtoValidator : AbstractValidator<ChangeStudentNumberDto>
{
    public ChangeStudentNumberDtoValidator()
    {
        RuleFor(x => x.NewStudentNumber)
            .NotEmpty()
            .WithMessage("Öğrenci numarası boş olamaz.")
            .MaximumLength(50)
            .WithMessage("Öğrenci numarası en fazla 50 karakter olabilir.");
    }
}
