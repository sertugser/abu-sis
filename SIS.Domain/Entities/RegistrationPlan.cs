using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

/// <summary>
/// Öğrencinin bir dönemdeki kayıt planı. Tüm CourseRegistration'lar bu plana bağlıdır.
/// Danışman planın tamamını onaylar veya reddeder.
/// </summary>
public class RegistrationPlan
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;
    public RegistrationPlanStatus Status { get; set; } = RegistrationPlanStatus.Draft;
    public DateTime? SubmittedAt { get; set; }
    public string? AdvisorNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<CourseRegistration> Registrations { get; set; } = new List<CourseRegistration>();
}
