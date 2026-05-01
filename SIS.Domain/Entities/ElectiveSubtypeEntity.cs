namespace SIS.Domain.Entities;

/// <summary>
/// Seçmeli ders tipi (TE/NDE/OC vb.) — DB-driven lookup.
/// Code immutable, sadece Name editable (Karar #14).
/// IsActive=false ise soft-deleted (Karar #2).
/// Faz B B3a: Yeni infrastructure; eski <c>SIS.Domain.Enums.ElectiveSubtype</c> enum'u
/// B3c'ye kadar yan yana yaşar.
/// </summary>
public class ElectiveSubtypeEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;          // "UniversityElective", "TechnicalElective" — immutable
    public string Name { get; set; } = string.Empty;          // "Üniversite Seçmeli" — editable, Türkçe
    public bool RequiresFaculty { get; set; }                 // TE: true
    public bool ForbidsFaculty { get; set; }                  // OC: true
    public bool RequiresDepartment { get; set; }              // genelde false
    public bool ForbidsDepartment { get; set; }               // OC: true
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
