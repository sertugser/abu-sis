using SIS.Domain.Entities;

namespace SIS.Application.CourseRegistration.Services;

/// <summary>Bir section'ın o an kayıt/kesinleştirme/onay için uygun olup olmadığını tek
/// kaynaktan değerlendirir: Active mi + (bölüme bağlıysa) fakültesi onaylı mı + enrollment
/// suspend değil mi. "Kayıt penceresi açık mı" (term-phase) AYRI sorumluluktur, burada DEĞİL.</summary>
public interface IRegistrationGate
{
    Task<RegistrationGateResult> EvaluateSectionAsync(CourseSection section, CancellationToken ct = default);

    /// <summary>
    /// Birden çok section'ı tek toplu sorguyla değerlendirir (katalog/şube-picker N+1'i önler).
    /// Section.Course yüklenmiş olmalı (DepartmentId okunur). Anahtar: SectionId.
    /// </summary>
    Task<IReadOnlyDictionary<int, RegistrationGateResult>> EvaluateSectionsAsync(
        IReadOnlyCollection<CourseSection> sections, CancellationToken ct = default);
}

public sealed record RegistrationGateResult(bool IsOpen, string? Rule = null, string? Message = null)
{
    public static readonly RegistrationGateResult Open = new(true);
}
