using SIS.Application.AcademicCalendar.DTOs;

namespace SIS.Application.AcademicCalendar.Services;

public interface IAcademicCalendarService
{
    // Admin
    Task<AcademicCalendarDto?> GetByTermIdAsync(int termId, CancellationToken ct = default);
    Task<AcademicCalendarDto> UpsertAsync(int termId, UpsertAcademicCalendarDto dto, CancellationToken ct = default);
    Task UpdatePhaseOverrideAsync(int termId, UpdateCalendarPhaseOverrideDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<AcademicCalendarTermSummaryDto>> ListTermsAsync(CancellationToken ct = default);

    // Public (öğrenci/hoca)
    Task<StudentAcademicCalendarDto?> GetStudentViewAsync(int termId, CancellationToken ct = default);
    Task<StudentAcademicCalendarDto?> GetActiveStudentViewAsync(CancellationToken ct = default);
}
