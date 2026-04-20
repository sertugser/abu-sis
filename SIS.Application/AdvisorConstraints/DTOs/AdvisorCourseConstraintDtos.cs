using SIS.Domain.Enums;

namespace SIS.Application.AdvisorConstraints.DTOs;

public class AdvisorCourseConstraintDto
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public int AdvisorId { get; set; }
    public AdvisorConstraintType ConstraintType { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UpsertAdvisorCourseConstraintRequest
{
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public int? TermId { get; set; }
    public AdvisorConstraintType ConstraintType { get; set; }
    public string? Note { get; set; }
}

public class UpdateAdvisorCourseConstraintRequest
{
    public AdvisorConstraintType ConstraintType { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
}

public class EffectiveAdvisorConstraintDto
{
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public int TermId { get; set; }
    public AdvisorConstraintType ConstraintType { get; set; }
    public string? Note { get; set; }
}
