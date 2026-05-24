using FluentValidation;
using SIS.Application.AcademicProgression.DTOs;

namespace SIS.Application.AcademicProgression.Validators;

public class PromoteStudentRequestValidator : AbstractValidator<PromoteStudentRequest>
{
    public PromoteStudentRequestValidator()
    {
        // TargetLevel null → kurallara göre terfi (geçerli). Dolu ise makul bir aralıkta olmalı.
        RuleFor(x => x.TargetLevel)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Hedef sınıf seviyesi 1'den küçük olamaz.")
            .LessThanOrEqualTo(12)
            .WithMessage("Hedef sınıf seviyesi 12'den büyük olamaz.")
            .When(x => x.TargetLevel.HasValue);
    }
}
