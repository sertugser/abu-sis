using SIS.Application.AcademicProgression.DTOs;

namespace SIS.Application.AcademicProgression.Services;

public interface IAcademicProgressionService
{
    /// <summary>
    /// Bir bölümdeki tüm aktif öğrencileri analiz ederek
    /// CompletedSemesters → AKTS bazlı sınıf atlama işlemini uygular.
    /// </summary>
    Task<BatchProgressionResultDto> PromoteDepartmentStudentsAsync(
        int departmentId,
        bool dryRun = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bir bölümdeki tüm aktif öğrencilerin CGPA ve SGPA'sını yeniden hesaplar ve günceller.
    /// </summary>
    Task<GpaRecalculationResultDto> RecalculateDepartmentGpaAsync(
        int departmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dönem sonu not girişi eksikliği kontrolü.
    /// </summary>
    Task<GradeLockResultDto> CheckGradeLockReadinessAsync(
        int departmentId,
        int? termId = null,
        CancellationToken cancellationToken = default);
}
