namespace SIS.Domain.Entities;

using SIS.Domain.Enums;

/// <summary>
/// Müfredat × Sınıf × Dönem × Kural Tipi kombinasyonu için tanımlanmış değer.
/// Proliz "Müfredat Dönem Kuralları" muadili. 14+ kural tipi desteklenir.
/// Aynı (CurriculumId, ClassYear, TermHalf, RuleType) kombinasyonu benzersizdir.
/// </summary>
public class CurriculumTermRule
{
    public int Id { get; set; }
    public int CurriculumId { get; set; }
    public Curriculum Curriculum { get; set; } = null!;
    public CurriculumClassYear ClassYear { get; set; }
    public CurriculumTermHalf TermHalf { get; set; }
    public CurriculumTermRuleType RuleType { get; set; }
    /// <summary>Kural değeri (AKTS/Kredi/Ders Saati için tam sayı, ortalama için ondalıklı).</summary>
    public decimal Value { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
