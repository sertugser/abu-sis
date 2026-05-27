namespace SIS.Application.Admin.DTOs;

/// <summary>
/// A single issuance-log entry for a student: which document was generated,
/// when, its issuance number, and who produced it.
/// </summary>
public record IssuedDocumentDto(
    int Id,
    string Kind,
    string KindLabel,
    DateTime IssuedAt,
    string? DocumentNumber,
    string? IssuedByName
);
