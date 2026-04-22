using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class AcademicTerm
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;

    // YENİ — çok dilli ad desteği
    public string ShortEnglishName { get; set; } = string.Empty;
    public string? OtherName { get; set; }

    public int Year { get; set; }

    // SİLİNEN: SemesterType Semester (kullanımdan çıktı)

    // DEĞİŞEN: 9 değerli, default Fall
    public TermType TermType { get; set; } = TermType.Fall;

    public DateTime RegistrationStart { get; set; }
    public DateTime RegistrationEnd { get; set; }
    public DateTime AddDropStart { get; set; }
    public DateTime AddDropEnd { get; set; }
    public DateTime WithdrawalDeadline { get; set; }
    public DateTime TermStart { get; set; }
    public DateTime TermEnd { get; set; }
    public DateTime GradeEntryStart { get; set; }
    public DateTime GradeEntryEnd { get; set; }

    public bool? RegistrationOverride { get; set; }
    public bool? AddDropOverride { get; set; }
    public bool? WithdrawalOverride { get; set; }
    public bool? GradeEntryOverride { get; set; }

    // SİLİNEN: TermStatus Status (stored)
    // YENİ: tek manuel state
    public bool IsCancelled { get; set; } = false;

    // YENİ
    public YoksisStatus YoksisStatus { get; set; } = YoksisStatus.Inactive;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<TermCalendarEvent> CalendarEvents { get; set; } = new List<TermCalendarEvent>();

    /// <summary>
    /// Dönemin gösterim durumunu hesaplar. IsCancelled → Cancelled; tarih aralığındaysak Active; aksi Inactive.
    /// </summary>
    public TermStatus GetStatus(DateTime now) =>
        IsCancelled ? TermStatus.Cancelled
        : (now >= TermStart && now <= TermEnd) ? TermStatus.Active
        : TermStatus.Inactive;

    /// <summary>Aktif mi (tarih + iptal değil). Faz öncüsü.</summary>
    public bool IsActive(DateTime now) => GetStatus(now) == TermStatus.Active;

    public bool IsRegistrationOpen(DateTime now) =>
        IsActive(now) &&
        (RegistrationOverride ?? (now >= RegistrationStart && now <= RegistrationEnd));

    public bool IsAddDropOpen(DateTime now) =>
        IsActive(now) &&
        (AddDropOverride ?? (now >= AddDropStart && now <= AddDropEnd));

    public bool IsWithdrawalOpen(DateTime now) =>
        IsActive(now) &&
        (WithdrawalOverride ?? (now >= TermStart && now <= WithdrawalDeadline));

    public bool IsGradeEntryOpen(DateTime now) =>
        IsActive(now) &&
        (GradeEntryOverride ?? (now >= GradeEntryStart && now <= GradeEntryEnd));
}
