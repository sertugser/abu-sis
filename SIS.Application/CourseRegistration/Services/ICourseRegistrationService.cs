using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Enums;

namespace SIS.Application.CourseRegistration.Services;

public interface ICourseRegistrationService
{
    /// <summary>
    /// Plana ders ekler. Öğrenci akışı varsayılan <see cref="RegistrationStatus.Draft"/> oluşturur
    /// (öğrenci sonradan Submit eder). Danışman öğrenci adına eklerken
    /// <see cref="RegistrationStatus.PendingAdvisorApproval"/> geçer → "Danışman Onay" akışı kaydı
    /// doğrudan Active'e çevirir (Proliz modeli). Başka değerler desteklenmez.
    /// </summary>
    Task<RegistrationResponseDto> AddToPlanAsync(int studentId, int courseSectionId, RegistrationStatus initialStatus = RegistrationStatus.Draft);
    Task<RegistrationResponseDto> SubmitRegistrationAsync(int studentId, RegistrationSubmitDto dto);

    /// <summary>
    /// Öğrencinin başarısız (FF/NA/U, sonradan geçilmemiş) derslerini, aktif dönemde uygun bir
    /// şube bularak planına otomatik (Draft) ekler. Idempotent — zaten planda olanlar atlanır,
    /// uygun şube yoksa (kapasite/yayın kapısı) sessizce atlanır. Öğrenci ders kayıt ekranını
    /// açtığında çağrılır; alttan dersler Proliz'deki gibi otomatik plana düşer.
    /// </summary>
    Task<RegistrationResponseDto> EnsureFailedCoursesInPlanAsync(int studentId);
    Task<List<StudentRegistrationDto>> GetStudentRegistrationsAsync(int studentId);
    Task<StudentAttendanceStatusDto?> GetStudentAttendanceStatusAsync(int studentId, int? termId = null);
    Task<List<CourseHistoryTermDto>> GetCourseHistoryAsync(int studentId);
    /// <summary>
    /// "Alınan Dersler" PDF raporu. <paramref name="termId"/> verilirse yalnız o dönemi
    /// basar ve üst bloktaki "Dönemi" alanını o döneme göre yazar; null ise tüm dönemler.
    /// </summary>
    Task<CourseHistoryReportDto?> GetCourseHistoryReportAsync(int studentId, int? termId = null);
    /// <summary>
    /// Plandan/kayıttan ders çıkarır. <paramref name="asAdvisor"/> = true iken danışman öğrenci
    /// adına çıkarır: kesinleştirilmiş (PendingAdvisorApproval) ve onaylı (Active) kayıtlar kayıt
    /// VEYA ekle/bırak penceresinde çıkarılabilir; öğrenci kilidi (kesinleştirme + alttan ders)
    /// uygulanmaz. asAdvisor = false (öğrenci) iken kesinleştirilmiş kayıt ve alttan ders çıkarılamaz.
    /// </summary>
    Task<RegistrationResponseDto> DropCourseAsync(int studentId, int registrationId, bool asAdvisor = false);
    Task<RegistrationResponseDto> WithdrawCourseAsync(int studentId, int registrationId);

    // Advisor approval methods
    Task<List<PendingApprovalDto>> GetPendingApprovalsAsync(int advisorInstructorId);
    Task<ApprovalResponseDto> ApproveStudentRegistrationsAsync(int advisorInstructorId, ApprovalActionDto dto);
    Task<ApprovalResponseDto> RejectStudentRegistrationsAsync(int advisorInstructorId, ApprovalActionDto dto);
}
