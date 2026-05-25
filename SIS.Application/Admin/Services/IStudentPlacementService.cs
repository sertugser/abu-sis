using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Services;

public interface IStudentPlacementService
{
    /// <summary>
    /// Returns the student's placement info. If the student exists but has no
    /// placement row yet, returns an "empty" DTO with Id and all fields null.
    /// Returns null only when the student itself does not exist.
    /// </summary>
    Task<StudentPlacementDto?> GetAsync(int studentId, CancellationToken ct = default);

    /// <summary>
    /// Creates the placement row when absent, otherwise updates it.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Student does not exist.</exception>
    /// <exception cref="InvalidOperationException">
    /// EntranceType is a non-empty but invalid enum name, or PlacementYear is set
    /// and falls outside [1980, currentYear + 1].
    /// </exception>
    Task UpsertAsync(int studentId, UpsertStudentPlacementDto dto, CancellationToken ct = default);
}
