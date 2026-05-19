using SIS.Application.CourseOpening.DTOs;

namespace SIS.Application.CourseOpening.Services;

/// <summary>
/// Section yaşam döngüsü: aç / toplu aç / güncelle / kapat / yeniden aç.
/// Faz 3.C: Eski ICourseOpeningService'ten ayrıldı.
/// </summary>
public interface ICourseSectionLifecycleService
{
    Task<List<OpenedCourseSectionDto>> OpenCourseAsync(int departmentId, int termId, OpenCourseRequest request, CancellationToken cancellationToken = default);
    Task<BulkOpenCoursesResultDto> BulkOpenCoursesAsync(int departmentId, int termId, BulkOpenCoursesRequest request, CancellationToken cancellationToken = default);
    Task<OpenedCourseSectionDto> UpdateSectionAsync(UpdateOpenedSectionRequest request, CancellationToken cancellationToken = default);
    Task CloseSectionAsync(int sectionId, CancellationToken cancellationToken = default);
    Task<CloseAllSectionsResultDto> CloseAllSectionsAsync(int courseId, int termId, CancellationToken cancellationToken = default);
    Task ReactivateSectionAsync(int sectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bir dersin tüm şubelerini (verilen dönemde) DB'den fiziksel olarak siler.
    /// CloseAllSectionsAsync'in aksine geri alınamaz.
    /// Reddedilir: aktif kayıtlı öğrenci varsa, ders değerlendirmesi yapılmışsa.
    /// Cascade silinir: SectionSchedule, AttendanceSession.
    /// SetNull: ConstraintEvaluation, DistributionConstraintCourse.
    /// Manuel silinir: CourseRegistration (Draft/Pending), CourseAccessPolicy.
    /// </summary>
    Task<DeleteAllSectionsResultDto> DeleteAllSectionsHardAsync(int courseId, int termId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aynı dersin iki şubesi arasında verilen öğrencileri taşır (CourseRegistration.CourseSectionId update).
    /// Sadece Active statusündeki kayıtlar taşınır. Source ve target aynı Course + Term olmak zorunda.
    /// Target Active olmalı, kapasitesi yeterli olmalı (mevcut aktif + taşınanlar &lt;= capacity).
    /// </summary>
    Task<TransferStudentsToSectionResultDto> TransferStudentsBetweenSectionsAsync(TransferStudentsToSectionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dersin tüm şubelerini hedef sayıya getirir ve mevcut Active öğrencileri verilen stratejiye göre yeniden dağıtır.
    /// Mevcut N şube vs hedef K:
    ///   K &gt; N → (K - N) yeni şube oluşturulur (kapasite default veya request.DefaultCapacityPerSection)
    ///   K &lt; N → fazla şubeler boşaltılıp Closed yapılır
    ///   K = N → sadece yeniden dağıtım
    /// İlk N şube mevcut hoca/derslik/program bilgilerini korur, yeni şubeler boş gelir.
    /// </summary>
    Task<AutoSplitCourseResultDto> AutoSplitCourseAsync(AutoSplitCourseRequest request, CancellationToken cancellationToken = default);
}
