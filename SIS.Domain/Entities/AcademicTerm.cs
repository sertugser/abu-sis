using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class AcademicTerm
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;
    public string ShortEnglishName { get; set; } = string.Empty;
    public string? OtherName { get; set; }

    public int Year { get; set; }
    public TermType TermType { get; set; } = TermType.Fall;

    // Dönem kimliğini tanımlayan 2 tarih burada kalır
    public DateTime TermStart { get; set; }
    public DateTime TermEnd { get; set; }

    // Admin override alanları (Faz 4 — manuel kontrol):
    // OverrideStart: admin dönemi TermStart'tan önce aktif etmek istiyorsa (erken aktif).
    // OverrideEnd:   admin dönemi TermEnd'den sonra aktif tutmak istiyorsa (uzatma).
    // Null ise dönem tarihi kullanılır; dolu ise override tarih kazanır (effStart/effEnd).
    public DateTime? OverrideStart { get; set; }
    public DateTime? OverrideEnd { get; set; }

    // SİLİNDİ (Faz 2): RegistrationStart/End, AddDropStart/End, WithdrawalDeadline,
    //                  GradeEntryStart/End + 4 override — TermAcademicCalendar'a taşındı

    public bool IsCancelled { get; set; } = false;
    public YoksisStatus YoksisStatus { get; set; } = YoksisStatus.Inactive;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation'lar
    public TermAcademicCalendar? AcademicCalendar { get; set; }
    public ICollection<TermCalendarEvent> CalendarEvents { get; set; } = new List<TermCalendarEvent>();

    // ── Computed ─────────────────────────────────────────────────────

    /// <summary>
    /// Override tarihleri ile efektif başlangıç. OverrideStart varsa onu, yoksa TermStart döner.
    /// </summary>
    public DateTime EffectiveStart => OverrideStart ?? TermStart;

    /// <summary>
    /// Override tarihleri ile efektif bitiş. OverrideEnd varsa onu, yoksa TermEnd döner.
    /// </summary>
    public DateTime EffectiveEnd => OverrideEnd ?? TermEnd;

    public TermStatus GetStatus(DateTime now) =>
        IsCancelled ? TermStatus.Cancelled
        : (now >= EffectiveStart && now <= EffectiveEnd) ? TermStatus.Active
        : TermStatus.Inactive;

    public bool IsActive(DateTime now) => GetStatus(now) == TermStatus.Active;

    // Phase method'ları: AcademicCalendar null ise false döner (null-safe, mevcut davranışla uyumlu).
    // Çağıran tarafın Include'ı şart; aksi EF lazy olmadığından AcademicCalendar null görünür
    // ve false dönüşü kullanıcıya "kapalı" olarak yansır — veri tutarsızlığı sessiz kalmaz.
    public bool IsRegistrationOpen(DateTime now) =>
        IsActive(now) && AcademicCalendar is { } cal &&
        (cal.RegistrationOverride ?? (now >= cal.RegistrationStart && now <= cal.RegistrationEnd));

    public bool IsAddDropOpen(DateTime now) =>
        IsActive(now) && AcademicCalendar is { } cal &&
        (cal.AddDropOverride ?? (now >= cal.AddDropStart && now <= cal.AddDropEnd));

    public bool IsWithdrawalOpen(DateTime now) =>
        IsActive(now) && AcademicCalendar is { } cal &&
        (cal.WithdrawalOverride ?? (now >= TermStart && now <= cal.WithdrawalDeadline));

    public bool IsGradeEntryOpen(DateTime now) =>
        IsActive(now) && AcademicCalendar is { } cal &&
        (cal.GradeEntryOverride ?? (now >= cal.GradeEntryStart && now <= cal.GradeEntryEnd));
}
