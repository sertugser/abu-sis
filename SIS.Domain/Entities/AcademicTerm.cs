using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class AcademicTerm
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;
    public int Year { get; set; }
    public SemesterType Semester { get; set; }
    public TermType TermType { get; set; } = TermType.Regular;

    public DateTime RegistrationStart { get; set; }
    public DateTime RegistrationEnd { get; set; }
    public DateTime AddDropStart { get; set; }
    public DateTime AddDropEnd { get; set; }
    public DateTime WithdrawalDeadline { get; set; }
    public DateTime TermStart { get; set; }
    public DateTime TermEnd { get; set; }

    // YENİ: Not girişi tarihleri
    public DateTime GradeEntryStart { get; set; }
    public DateTime GradeEntryEnd { get; set; }

    // YENİ: Admin override — null = takvim konuşur
    public bool? RegistrationOverride { get; set; }
    public bool? AddDropOverride { get; set; }
    public bool? WithdrawalOverride { get; set; }
    public bool? GradeEntryOverride { get; set; }

    public TermStatus Status { get; set; } = TermStatus.Draft;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<TermCalendarEvent> CalendarEvents { get; set; } = new List<TermCalendarEvent>();

    // Computed: Ready state'i = aktif dönem
    public bool IsActive => Status == TermStatus.Ready;

    public bool IsRegistrationOpen(DateTime now) =>
        Status == TermStatus.Ready &&
        (RegistrationOverride ?? (now >= RegistrationStart && now <= RegistrationEnd));

    public bool IsAddDropOpen(DateTime now) =>
        Status == TermStatus.Ready &&
        (AddDropOverride ?? (now >= AddDropStart && now <= AddDropEnd));

    public bool IsWithdrawalOpen(DateTime now) =>
        Status == TermStatus.Ready &&
        (WithdrawalOverride ?? (now >= TermStart && now <= WithdrawalDeadline));

    public bool IsGradeEntryOpen(DateTime now) =>
        Status == TermStatus.Ready &&
        (GradeEntryOverride ?? (now >= GradeEntryStart && now <= GradeEntryEnd));
}
