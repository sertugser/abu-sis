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
