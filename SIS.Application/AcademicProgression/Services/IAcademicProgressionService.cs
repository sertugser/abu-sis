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

    /// <summary>
    /// Tek bir öğrencinin sınıf atlatma uygunluğunu (PromotionRule bazlı) değerlendirir.
    /// Toplu işlemle aynı kural mantığını kullanır. Öğrenci yoksa null döner.
    /// </summary>
    Task<StudentPromotionStatusDto?> EvaluateStudentPromotionAsync(int studentId, CancellationToken ct = default);

    /// <summary>
    /// Öğrencinin sınıf seviyesini doğrudan ayarlar (CompletedSemesters = (targetLevel-1)*2).
    /// Manuel override ve geri alma için temel mutasyon primitifi.
    /// </summary>
    Task SetStudentLevelAsync(int studentId, int targetLevel, CancellationToken ct = default);
}
