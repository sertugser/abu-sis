namespace SIS.Domain.Entities;

public class StudentGrade
{
    public int Id { get; set; }

    public int CourseRegistrationId { get; set; }
    public CourseRegistration CourseRegistration { get; set; } = null!;

    public int GradeComponentId { get; set; }
    public GradeComponent GradeComponent { get; set; } = null!;

    public decimal? Score { get; set; }   // 0-100 arası, null = henüz girilmedi
}
