using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

/// <summary>
/// Append-only log of an officially generated document for a student
/// (e.g. an Öğrenci Bilgi Formu or Öğrenci Belgesi). One row is written
/// each time a document is produced; rows are never updated.
/// </summary>
public class IssuedDocument
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public IssuedDocumentKind Kind { get; set; }
    public DateTime IssuedAt { get; set; }

    /// <summary>User who generated the document (staff), if known.</summary>
    public int? IssuedByUserId { get; set; }

    /// <summary>Human-readable issuance number (e.g. <c>OBF-2026-000001</c>).</summary>
    public string? DocumentNumber { get; set; }
}
