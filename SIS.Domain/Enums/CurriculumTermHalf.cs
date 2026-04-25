namespace SIS.Domain.Enums;

/// <summary>
/// Müfredat dönem kurallarında kullanılan dönem değeri (Güz/Bahar).
/// TermType enum'undan ayrı; sadece Güz/Bahar ikili ayrımı.
/// </summary>
public enum CurriculumTermHalf
{
    Fall = 0,    // Güz
    Spring = 1,  // Bahar
}
