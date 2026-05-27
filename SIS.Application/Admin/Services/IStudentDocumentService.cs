using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Services;

public interface IStudentDocumentService
{
    /// <summary>
    /// Returns one status row per catalog <c>DocumentType</c> (ordered by SortOrder),
    /// left-joined with the student's uploaded documents. Returns null only when the
    /// student itself does not exist.
    /// </summary>
    Task<List<StudentDocumentStatusDto>?> GetStatusAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts the (StudentId, DocumentTypeId) row — creates it or updates the file
    /// metadata — and returns the StudentDocument.Id.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The document type does not exist.</exception>
    Task<int> RecordUploadAsync(
        int studentId,
        int documentTypeId,
        string extension,
        string? originalFileName,
        int? uploadedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the file-locating projection of the document if it belongs to the
    /// given student, otherwise null.
    /// </summary>
    Task<StudentDocumentFileDto?> GetDocumentAsync(int studentId, int documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the document if it belongs to the given student; no-op otherwise.
    /// </summary>
    Task RemoveAsync(int studentId, int documentId, CancellationToken cancellationToken = default);
}
