using FluentValidation;
using SIS.Application.CourseRegistration.DTOs;

namespace SIS.Application.CourseRegistration.Validators;

/// <summary>
/// Sistem sınırı doğrulaması: kayıt gönderimi. Boş/tekrarlı/negatif/aşırı büyük ders listesi
/// servise hiç ulaşmadan reddedilir (DoS + çift sayım yüzeyini kapatır).
/// </summary>
public sealed class RegistrationSubmitDtoValidator : AbstractValidator<RegistrationSubmitDto>
{
    // Bir öğrencinin tek seferde gönderebileceği makul üst sınır (AKTS sınırı zaten daha düşük).
    public const int MaxCourseSections = 20;

    public RegistrationSubmitDtoValidator()
    {
        RuleFor(dto => dto.CourseSectionIds)
            .NotNull().WithMessage("Ders listesi boş olamaz.")
            .Must(ids => ids is { Count: > 0 }).WithMessage("Kayıt için en az bir ders seçmelisiniz.")
            .Must(ids => ids.Count <= MaxCourseSections).WithMessage($"En fazla {MaxCourseSections} ders gönderilebilir.")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Aynı şube birden fazla kez gönderilemez.")
            .Must(ids => ids.All(id => id > 0)).WithMessage("Geçersiz şube kimliği.");
    }
}

/// <summary>Sistem sınırı doğrulaması: plana tek ders ekleme.</summary>
public sealed class RegistrationPlanItemDtoValidator : AbstractValidator<RegistrationPlanItemDto>
{
    public RegistrationPlanItemDtoValidator()
    {
        RuleFor(dto => dto.CourseSectionId)
            .GreaterThan(0).WithMessage("Geçersiz şube kimliği.");
    }
}
