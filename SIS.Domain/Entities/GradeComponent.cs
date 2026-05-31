using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class GradeComponent
{
    public int Id { get; set; }

    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public int AcademicTermId { get; set; }
    public AcademicTerm AcademicTerm { get; set; } = null!;

    public string Name { get; set; } = string.Empty;   // "Vize", "Final", "Ödev 1", "Lab"

    /// <summary>
    /// Legacy free-text label (e.g. "Midterm", "Final", "Lab"). Kept for display/export purposes only.
    /// Do NOT use for grade enforcement logic — use <see cref="Type"/> instead.
    /// </summary>
    public string ExamType { get; set; } = "Other";    // Midterm, Final, Makeup, Assignment, Project, Lab, Quiz, Other

    /// <summary>
    /// Authoritative classification used by GradeController.Finalize for weight-bounds and
    /// final-floor enforcement. Defaults to <see cref="GradeComponentType.Other"/> (the
    /// post-migration default). When all components remain Other the weight-bounds check is
    /// skipped to preserve legacy behavior for unclassified courses.
    /// </summary>
    public GradeComponentType Type { get; set; } = GradeComponentType.Other;

    /// <summary>Kısa ad (Proliz "Sınav Kısa Adı"). Opsiyonel, default boş.</summary>
    public string ShortName { get; set; } = string.Empty;

    public decimal Weight { get; set; }                // 30.00 = %30
    public int Order { get; set; }                     // Görüntüleme sırası

    public bool IsRequired { get; set; } = true;        // Bu sınav kalemi zorunlu mu?
    public decimal? MinimumScore { get; set; }          // Örn: Finalden en az 50 alma şartı
    public bool RequiresAttendanceEligibility { get; set; } = false; // Devamsızlıktan kalmamış olma şartı
    public int? PrerequisiteGradeComponentId { get; set; } // Örn: Final için vizeye girmiş olma şartı

    public GradeComponent? PrerequisiteGradeComponent { get; set; }

    /// <summary>
    /// Hoca bu komponentin notlarını öğrenciye "İlan Et" dediği an. UTC.
    /// null = ilan edilmedi; öğrenci notunu/sınıf istatistiklerini göremez.
    /// Görünürlük kapısı: <c>IWebModuleControlService</c> (term-seviyesi admin kapısı) AND <see cref="PublishedAt"/> != null
    /// — ikisi de gerekli (tek-kaynak, ikibaşlılık yok).
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    public ICollection<GradeComponent> DependentComponents { get; set; } = new List<GradeComponent>();
    public ICollection<StudentGrade> StudentGrades { get; set; } = new List<StudentGrade>();
}