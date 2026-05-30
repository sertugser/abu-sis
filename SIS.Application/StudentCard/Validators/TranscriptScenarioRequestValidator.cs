using FluentValidation;
using SIS.Application.StudentCard.DTOs;

namespace SIS.Application.StudentCard.Validators;

/// <summary>
/// Transkript Senaryosu (what-if) istek doğrulaması. Tüm veri öğrencinin ekranındaki
/// simülasyondan gelir; sistemde kalıcı değişiklik yapılmasa da girdi sınırları (AKTS/kredi
/// negatif olamaz, ortalama 0-100, ders kodu zorunlu) ASVS V2/V4 gereği burada kapılanır.
/// </summary>
public sealed class TranscriptScenarioRequestValidator : AbstractValidator<TranscriptScenarioRequestDto>
{
    public TranscriptScenarioRequestValidator()
    {
        RuleFor(request => request.Terms).NotNull();
        RuleFor(request => request.Courses).NotNull();

        RuleForEach(request => request.Courses).SetValidator(new ScenarioCourseInputValidator());
    }
}

public sealed class ScenarioCourseInputValidator : AbstractValidator<ScenarioCourseInputDto>
{
    public ScenarioCourseInputValidator()
    {
        RuleFor(course => course.ScenarioId).NotEmpty();
        RuleFor(course => course.CourseCode).NotEmpty().MaximumLength(50);
        RuleFor(course => course.CourseName).NotEmpty().MaximumLength(500);
        // Grade boş olabilir: "not henüz girilmedi" (projeksiyon/yeni ders) → ortalamaya
        // katılmaz. Yalnızca uzunluk sınırı; NotEmpty YOK.
        RuleFor(course => course.Grade).MaximumLength(10);
        RuleFor(course => course.Credits).InclusiveBetween(0, 100);
        RuleFor(course => course.Ects).InclusiveBetween(0, 100);
        RuleFor(course => course.AverageScore)
            .InclusiveBetween(0m, 100m)
            .When(course => course.AverageScore.HasValue);
    }
}
