namespace SIS.Domain.Entities;

/// <summary>
/// Reference catalog of student document kinds (e.g. Lise Diploması, Vesikalık Fotoğraf).
/// Seeded only — there is no admin CRUD for this catalog. Not audited (reference data).
/// </summary>
public class DocumentType
{
    public int Id { get; set; }

    /// <summary>Stable machine code used by seeds/SQL (e.g. <c>LISE_DIPLOMA</c>).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name (Turkish).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether the document is mandatory for enrollment.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Ordering hint for catalog display.</summary>
    public int SortOrder { get; set; }
}
