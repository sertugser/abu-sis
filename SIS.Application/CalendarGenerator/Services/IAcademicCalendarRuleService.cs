using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Application.CalendarGenerator.Services;

public interface IAcademicCalendarRuleService
{
    Task ValidateCalendarEventAsync(AcademicTerm term, CalendarEventType eventType, DateTime startDate, DateTime? endDate, CancellationToken ct = default);
    Task<DateTime?> GetGradePublicationDateAsync(int termId, CancellationToken ct = default);
    Task<bool> CanStudentSeeLetterGradeAsync(int termId, bool isFinalized, bool hasGrade, DateTime nowUtc, CancellationToken ct = default);
    Task ValidateTermCanCloseAsync(AcademicTerm term, CancellationToken ct = default);
}
