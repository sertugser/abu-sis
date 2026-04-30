namespace SIS.Domain.Entities;

/// <summary>
/// Lookup entity for "Açma Nedeni" (Opening Reason) of a curriculum course.
/// DB-driven (admin-manageable in future). Behavior flags allow Phase 4 services
/// to read special handling rules without migration (e.g. "Hobi" excluded from GPA,
/// "Dönemsel (Teorik)" bypasses schedule conflict checks).
/// </summary>
public class CurriculumCourseOpeningReason
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // Behavior flags (Phase 4 services will read these; Phase 1 leaves them at defaults)
    public bool AllowsAdvisorGrading { get; set; } = false;
    public bool BypassScheduleConflict { get; set; } = false;
    public bool BypassMandatoryAttendance { get; set; } = false;
    public bool IsExcludedFromGpa { get; set; } = false;
    public bool IsCreditFree { get; set; } = false;
    public bool RequiresApproval { get; set; } = false;
    public bool AllowsCustomGradingScale { get; set; } = false;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
