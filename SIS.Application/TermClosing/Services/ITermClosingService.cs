using SIS.Application.AcademicProgression.DTOs;
using SIS.Application.TermClosing.DTOs;

namespace SIS.Application.TermClosing.Services;

public interface ITermClosingService
{
    Task<TermClosingStatusDto> GetStatusAsync(int departmentId, CancellationToken ct = default);
    Task<TermClosingStepResultDto> LockGradesAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<TermClosingStepResultDto> UnlockGradesAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<GpaRecalculationResultDto> CalculateGpaAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<BatchProgressionResultDto> PreviewPromotionAsync(int departmentId, CancellationToken ct = default);
    Task<BatchProgressionResultDto> PromoteAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<TermClosingStepResultDto> RollbackPromotionAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<NewTermResultDto> OpenNewTermAsync(int departmentId, int userId, NewTermRequestDto request, CancellationToken ct = default);

    /// <summary>Şube bazlı not giriş detayını döner</summary>
    Task<GradeLockDetailResultDto> GetGradeReadinessDetailAsync(int departmentId, CancellationToken ct = default);

    /// <summary>Kilitlenmiş notların harf notu dağılımını döner</summary>
    Task<GradeDistributionResultDto> GetGradeDistributionAsync(int departmentId, CancellationToken ct = default);

    /// <summary>Fakülte bazlı tüm bölümlerin dönem kapama durumunu döner (dekan/admin)</summary>
    Task<List<DepartmentClosingOverviewDto>> GetDepartmentOverviewAsync(int? facultyId, CancellationToken ct = default);

    /// <summary>Sınıf atlatma sonuç raporunu Excel olarak döner</summary>
    Task<byte[]> ExportPromotionReportAsync(int departmentId, CancellationToken ct = default);
}
