namespace SIS.Domain.Entities;

/// <summary>
/// A single uploaded document belonging to a student, of a given <see cref="DocumentType"/>.
/// At most one row per (StudentId, DocumentTypeId) — re-uploading replaces the existing row.
/// </summary>
public class StudentDocument
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int DocumentTypeId { get; set; }

    /// <summary>File extension of the stored file (e.g. <c>pdf</c>, <c>jpg</c>).</summary>
    public string FileExtension { get; set; } = string.Empty;

    /// <summary>Original file name as uploaded (optional, for display/download).</summary>
    public string? OriginalFileName { get; set; }

    public DateTime UploadedAt { get; set; }

    /// <summary>User who recorded the upload (staff), if known.</summary>
    public int? UploadedByUserId { get; set; }
}
