namespace SIS.Domain.Enums;

/// <summary>
/// Dönem tipi — Ankara Bilim OBS pattern'i.
/// Normal dönemler (Fall/Spring/Summer) + Sınav Hakkı (Tekders) + Diğer (Erasmus/Muafiyet) + Muafiyet.
/// </summary>
public enum TermType
{
    Fall         = 0,   // Güz
    Spring       = 1,   // Bahar
    Summer       = 2,   // Yaz
    FallResit    = 3,   // Güz Tekders (Sınav Hakkı)
    SpringResit  = 4,   // Bahar Tekders
    SummerResit  = 5,   // Yaz Tekders
    SpringOther  = 6,   // Bahar Diğer (Erasmus)
    SummerOther  = 7,   // Yaz Diğer (Muafiyet alt tipi)
    Exemption    = 8    // Muafiyet
}
