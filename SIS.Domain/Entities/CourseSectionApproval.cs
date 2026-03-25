using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class CourseSectionApproval
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;

    /// <summary>Sistemin önerdiği şube sayısı (forecast'ten gelen)</summary>
    public int RecommendedSectionCount { get; set; }

    /// <summary>Bölüm başkanının onayladığı şube sayısı (null = henüz karar verilmedi)</summary>
    public int? ApprovedSectionCount { get; set; }

    /// <summary>Tahmini toplam talep</summary>
    public int PredictedDemand { get; set; }

    public SectionApprovalStatus Status { get; set; } = SectionApprovalStatus.Pending;

    /// <summary>Bölüm başkanının notu (açıklama, gerekçe vb.)</summary>
    public string? Note { get; set; }

    public string? DecidedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
}
