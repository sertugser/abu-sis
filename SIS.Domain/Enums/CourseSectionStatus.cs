namespace SIS.Domain.Enums;

/// <summary>
/// CourseSection lifecycle durumu. Otomatik hesaplanır — manuel geçiş yok.
/// Draft (eksik) ↔ Active (tam) — atama değişikliklerinde otomatik.
/// Cancelled — admin'in tek manuel aksiyonu, runtime kontroller kilitlenir.
/// </summary>
public enum CourseSectionStatus
{
    /// <summary>Eksik: hoca, derslik veya program dilimi atanmamış. Otomatik.</summary>
    Draft = 0,

    /// <summary>Tam: hoca + derslik + slot atanmış, kayıt açık. Otomatik.</summary>
    Active = 1,

    /// <summary>İptal edildi — admin'in manuel aksiyonu. Kayıtlar Dropped, yazma kilitli.</summary>
    Cancelled = 2
}
