using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.Scheduling.Services;

/// <summary>
/// Öğrencinin kendi programının (program × sınıf × dönem) haftalık ders programını
/// sunar. RegistrationGate ile aynı yayın gate'inden geçer:
/// section Active + IsEnrollmentSuspended==false + FacultyScheduleApproval gerekir.
/// </summary>
public interface IDepartmentScheduleService
{
    /// <summary>Sayfa açılışında dropdown'ları ve default değerleri döner.</summary>
    Task<DepartmentScheduleContextDto> GetContextAsync(
        int studentId,
        CancellationToken cancellationToken);

    /// <summary>Belirli (term, program, classYear) için haftalık ders programı.</summary>
    Task<DepartmentScheduleDto> GetScheduleAsync(
        int studentId,
        int termId,
        int programId,
        int classYear,
        CancellationToken cancellationToken);

    /// <summary>PDF render — aynı GetScheduleAsync sonucu QuestPDF ile.</summary>
    Task<byte[]> GeneratePdfAsync(
        int studentId,
        int termId,
        int programId,
        int classYear,
        CancellationToken cancellationToken);
}
