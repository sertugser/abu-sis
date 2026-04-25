namespace SIS.Domain.Enums;

/// <summary>
/// Müfredat dönem kurallarında kullanılan dönem değeri.
/// Proliz "Müfredat Sınıf Dönem Tanımları" sayfasında Güz/Bahar/Yaz olarak gösterilir.
/// TermType enum'undan ayrı; sadece kural tanımları için.
/// </summary>
public enum CurriculumTermHalf
{
    Fall = 0,    // Güz
    Spring = 1,  // Bahar
    Summer = 2,  // Yaz
}
