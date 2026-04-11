namespace SIS.Domain.Entities;

using SIS.Domain.Enums;

public class Course
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? EnglishName { get; set; }        // İngilizce ders adı
    public string? ExamType { get; set; }           // Sınav Değer Tipi (default: "Otomatik Hesapla ve Harflendir")
    public int? DepartmentId { get; set; }        // owning department (nullable: some courses are cross-departmental)
    public Department? Department { get; set; }
    public int Ects { get; set; }
    public int TheoryHours { get; set; }
    public int LabHours { get; set; }
    public bool IsNonGpa { get; set; }                      // true = S/U graded
    public bool IsElective { get; set; }
    public ElectiveSubtype? ElectiveSubtype { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public CourseLanguage Language { get; set; } = CourseLanguage.Turkish;
    public TeachingMethod TeachingMethod { get; set; } = TeachingMethod.FaceToFace;
    public CourseStatus Status { get; set; } = CourseStatus.Active;
    public string? Description { get; set; }
    public CourseLevel Level { get; set; } = CourseLevel.Undergraduate;
    public int? NationalCredit { get; set; }
    public ICollection<CoursePrerequisite> Prerequisites { get; set; } = new List<CoursePrerequisite>();
    public ICollection<CourseEquivalency> Equivalencies { get; set; } = new List<CourseEquivalency>();
}
