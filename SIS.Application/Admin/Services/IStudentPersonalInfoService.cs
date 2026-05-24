using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Services;

public interface IStudentPersonalInfoService
{
    /// <summary>
    /// Returns the student's personal info. If the student exists but has no
    /// personal-info row yet, returns an "empty" DTO with all-null fields.
    /// Returns null only when the student itself does not exist.
    /// </summary>
    Task<StudentPersonalInfoDto?> GetAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the personal-info row when absent, otherwise updates it.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Student does not exist.</exception>
    /// <exception cref="InvalidOperationException">An enum string value is invalid.</exception>
    Task UpsertAsync(int studentId, UpdateStudentPersonalInfoDto dto, CancellationToken cancellationToken = default);
}
