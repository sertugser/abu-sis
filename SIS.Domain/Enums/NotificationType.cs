namespace SIS.Domain.Enums;

/// <summary>
/// Kişisel (sistem üretimi) bildirim türleri. Duyurulardan (Announcement) ayrıdır.
/// Yeni olaylar eklendikçe faz faz büyür; frontend tür → ikon/renk eşlemesi yapar.
/// </summary>
public enum NotificationType
{
    // ── Faz 1: Onay akışları + ders kayıt ──
    /// <summary>Öğrenci kaydını gönderdi → danışmana.</summary>
    RegistrationSubmittedForApproval = 100,
    /// <summary>Danışman kaydı onayladı → öğrenciye.</summary>
    RegistrationApproved = 101,
    /// <summary>Danışman kaydı reddetti → öğrenciye.</summary>
    RegistrationRejected = 102,

    // ── Faz 2+ (yer tutucu — ileride doldurulacak) ──
    RegistrationWindowOpen = 110,
    RegistrationWindowClosingSoon = 111,
    GradesPublished = 200,
    AttendanceWarning = 210,
    AcademicCalendarMissing = 300,
    TermPhaseChanged = 301,
    ScheduleApprovalRequested = 400,
    /// <summary>Onaylı (yayında) bir programda section yeniden düzenlenip kayda açıldı → kayıtlı öğrencilere.</summary>
    ScheduleChanged = 410,

    /// <summary>Eşlenmeyen/diğer.</summary>
    Other = 999,
}
