namespace SIS.Domain.Entities;

/// <summary>
/// Bir dersin hangi bölüm/fakülte öğrencilerine açık olduğunu belirler.
/// Varsayılan: sadece dersin kendi bölümü. DepartmentManager başka bölüm/fakültelere de açabilir.
/// </summary>
public class CourseAccessPolicy
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;
    public int? FacultyId { get; set; }
    public Faculty? Faculty { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
}
