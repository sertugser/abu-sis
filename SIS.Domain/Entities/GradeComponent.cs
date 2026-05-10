namespace SIS.Domain.Entities;

public class GradeComponent
{
    public int Id { get; set; }

    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public int AcademicTermId { get; set; }
    public AcademicTerm AcademicTerm { get; set; } = null!;

    public string Name { get; set; } = string.Empty;   // "Vize", "Final", "Ödev 1", "Lab"
    public string ExamType { get; set; } = "Other";    // Midterm, Final, Makeup, Assignment, Project, Lab, Quiz, Other

    public decimal Weight { get; set; }                // 30.00 = %30
    public int Order { get; set; }                     // Görüntüleme sırası

    public bool IsRequired { get; set; } = true;        // Bu sınav kalemi zorunlu mu?
    public decimal? MinimumScore { get; set; }          // Örn: Finalden en az 50 alma şartı
    public bool RequiresAttendanceEligibility { get; set; } = false; // Devamsızlıktan kalmamış olma şartı
    public int? PrerequisiteGradeComponentId { get; set; } // Örn: Final için vizeye girmiş olma şartı

    public GradeComponent? PrerequisiteGradeComponent { get; set; }

    public ICollection<GradeComponent> DependentComponents { get; set; } = new List<GradeComponent>();
    public ICollection<StudentGrade> StudentGrades { get; set; } = new List<StudentGrade>();
}