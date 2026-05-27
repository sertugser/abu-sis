using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Services;

public interface IStudentScholarshipService
{
    /// <summary>
    /// Returns the student's scholarship records, ordered active-first then by
    /// StartDate descending. Returns null only when the student does not exist.
    /// </summary>
    Task<List<ScholarshipDetailDto>?> GetForStudentAsync(int studentId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new scholarship record and returns its id.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Student does not exist.</exception>
    /// <exception cref="InvalidOperationException">
    /// Type is unparseable, RatePercent is outside [0,100], or EndDate &lt; StartDate.
    /// </exception>
    Task<int> CreateAsync(int studentId, UpsertScholarshipDetailDto dto, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing scholarship record belonging to the student.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    /// Record does not exist or belongs to a different student.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Type is unparseable, RatePercent is outside [0,100], or EndDate &lt; StartDate.
    /// </exception>
    Task UpdateAsync(int studentId, int id, UpsertScholarshipDetailDto dto, CancellationToken ct = default);

    /// <summary>
    /// Deletes a scholarship record belonging to the student.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    /// Record does not exist or belongs to a different student.
    /// </exception>
    Task DeleteAsync(int studentId, int id, CancellationToken ct = default);
}
