namespace SIS.Domain.Enums;

/// <summary>
/// SectionSchedule kayıtlarında bir slotun teorik mi uygulama mı olduğunu belirtir.
/// Devamsızlık eşikleri, e-yoklama entegrasyonu ve derslik tipi kontrolü için kullanılır.
/// </summary>
public enum ScheduleBlockType
{
    /// <summary>Teorik ders (amfi/normal derslik).</summary>
    Theory = 0,

    /// <summary>Uygulama / laboratuvar (lab/stüdyo derslik).</summary>
    Lab = 1,
}
