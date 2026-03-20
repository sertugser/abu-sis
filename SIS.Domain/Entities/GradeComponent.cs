namespace SIS.Domain.Entities;

public class GradeComponent
{
    public int Id { get; set; }

    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public int AcademicTermId { get; set; }
    public AcademicTerm AcademicTerm { get; set; } = null!;

    public string Name { get; set; } = string.Empty;   // "Vize", "Final", "Ödev 1", "Lab"
    public decimal Weight { get; set; }                 // 30.00 = %30
    public int Order { get; set; }                      // Görüntüleme sırası

    public ICollection<StudentGrade> StudentGrades { get; set; } = new List<StudentGrade>();
}
