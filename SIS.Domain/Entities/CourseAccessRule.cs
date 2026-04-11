namespace SIS.Domain.Entities;

public class CourseAccessRule
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public int? AllowedFacultyId { get; set; }
    public Faculty? AllowedFaculty { get; set; }
    public int? AllowedDepartmentId { get; set; }
    public Department? AllowedDepartment { get; set; }
    public int? AllowedProgramId { get; set; }
    public AcademicProgram? AllowedProgram { get; set; }
    public int? MinClassYear { get; set; }
    public int? MaxClassYear { get; set; }
    public decimal? MinCgpa { get; set; }
    public bool RequiresAdvisorApproval { get; set; }
    public bool IsOpenToOwnDepartment { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
