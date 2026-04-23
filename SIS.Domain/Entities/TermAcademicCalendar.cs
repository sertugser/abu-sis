namespace SIS.Domain.Entities;

public class TermAcademicCalendar
{
    public int Id { get; set; }

    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;

    // 7 tarih — dönem fazlarının takvim çerçevesi
    public DateTime RegistrationStart { get; set; }
    public DateTime RegistrationEnd { get; set; }
    public DateTime AddDropStart { get; set; }
    public DateTime AddDropEnd { get; set; }
    public DateTime WithdrawalDeadline { get; set; }
    public DateTime GradeEntryStart { get; set; }
    public DateTime GradeEntryEnd { get; set; }

    // 4 override — null = takvim konuşur; true/false = admin override
    public bool? RegistrationOverride { get; set; }
    public bool? AddDropOverride { get; set; }
    public bool? WithdrawalOverride { get; set; }
    public bool? GradeEntryOverride { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
