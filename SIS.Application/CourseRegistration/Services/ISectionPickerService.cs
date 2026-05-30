using SIS.Application.CourseRegistration.DTOs;

namespace SIS.Application.CourseRegistration.Services;

public sealed record SectionPickerQuery(
    int CourseId, int? InstructorId, int? FacultyId, int? ProgramId, string? Search, int Page, int PageSize);

public interface ISectionPickerService
{
    Task<SectionPickerResultDto> QueryAsync(int studentId, SectionPickerQuery query, CancellationToken ct = default);
}
