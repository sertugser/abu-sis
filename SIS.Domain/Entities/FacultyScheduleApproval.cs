namespace SIS.Domain.Entities;

/// <summary>
/// Dekanın fakülte ders programını onayladığını kaydeder.
/// Scheduling → Published geçişinde bu onay zorunludur.
/// </summary>
public class FacultyScheduleApproval
{
    public int Id { get; set; }
    public int FacultyId { get; set; }
    public Faculty Faculty { get; set; } = null!;
    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;
    public int ApprovedByUserId { get; set; }
    public DateTime ApprovedAt { get; set; }
    public string? Note { get; set; }
}
