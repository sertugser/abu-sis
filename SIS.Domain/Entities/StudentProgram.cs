using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class StudentProgram
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int ProgramId { get; set; }
    public AcademicProgram Program { get; set; } = null!;
    public int CurriculumId { get; set; }
    public Curriculum Curriculum { get; set; } = null!;
    public StudentProgramType Type { get; set; }
    public DateTime EnrollmentDate { get; set; }
    public bool IsActive { get; set; }
}
