namespace SIS.Domain.Enums;

/// <summary>
/// Zaman slotu gün pattern'ı.
/// MWF = Pazartesi-Çarşamba-Cuma (3×50dk), TR = Salı-Perşembe (2×75dk),
/// MW = Pazartesi-Çarşamba (2×75dk), SingleDay = Tek gün (lab blokları).
/// </summary>
public enum TimeSlotPattern
{
    MWF,        // Monday-Wednesday-Friday
    TR,         // Tuesday-Thursday
    MW,         // Monday-Wednesday
    SingleDay   // Single day (lab blocks)
}
