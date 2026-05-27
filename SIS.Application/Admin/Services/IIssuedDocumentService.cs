using SIS.Application.Admin.DTOs;
using SIS.Domain.Enums;

namespace SIS.Application.Admin.Services;

public interface IIssuedDocumentService
{
    /// <summary>
    /// Records a new issuance for the student, assigning a sequential
    /// <c>DocumentNumber</c> scoped to the kind and current year, and returns the new row Id.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The student does not exist.</exception>
    Task<int> RecordAsync(
        int studentId,
        IssuedDocumentKind kind,
        int? issuedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the student's issuance history (newest first), or null when the
    /// student does not exist.
    /// </summary>
    Task<List<IssuedDocumentDto>?> GetHistoryAsync(int studentId, CancellationToken cancellationToken = default);
}
