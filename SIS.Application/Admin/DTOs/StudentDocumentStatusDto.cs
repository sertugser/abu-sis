namespace SIS.Application.Admin.DTOs;

/// <summary>
/// Per-catalog-type upload status for a given student. One row is returned for every
/// <c>DocumentType</c> in the catalog; <see cref="Uploaded"/> is false (and the
/// document fields null) when the student has no document of that type yet.
/// </summary>
public record StudentDocumentStatusDto(
    int DocumentTypeId,
    string Code,
    string Name,
    bool IsRequired,
    int SortOrder,
    bool Uploaded,
    int? DocumentId,
    string? OriginalFileName,
    DateTime? UploadedAt
);

/// <summary>
/// Minimal file-locating projection of a single uploaded student document.
/// Used by the serve endpoint to locate and stream the stored file without
/// leaking the <c>SIS.Domain.Entities.StudentDocument</c> entity through the
/// Application boundary.
/// </summary>
public record StudentDocumentFileDto(
    int Id,
    int StudentId,
    int DocumentTypeId,
    string FileExtension,
    string? OriginalFileName
);
