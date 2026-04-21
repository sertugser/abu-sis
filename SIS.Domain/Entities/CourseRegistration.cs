using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class CourseRegistration
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int CourseSectionId { get; set; }
    public CourseSection CourseSection { get; set; } = null!;
    public int? RegistrationPlanId { get; set; }
    public RegistrationPlan? RegistrationPlan { get; set; }
    // Term is derived via CourseSection.TermId - no direct FK needed
    public RegistrationStatus Status { get; set; }
    public GradeCode? Grade { get; set; }
    public bool IsRepeated { get; set; }                       // FR-56C: grade improvement repeat
    public string? AdvisorNote { get; set; }                   // Danışman red/onay notu
    public bool MandatoryAttendance { get; set; }              // FR-106
    public int CourseWithdrawalCount { get; set; }             // how many times this specific course was withdrawn

    /// <summary>
    /// Kayıt anında donan AKTS değeri. Havuz kuralı veya müfredat atamasından gelir.
    /// GPA/transkript/dönem AKTS sınırı hepsi bu değerden okur — dersin katalog AKTS'si sonradan değişse
    /// bile geçmiş kayıtlar ve transkript etkilenmez (grandfathering).
    /// Null ise dersin katalog AKTS'si kullanılır (eski kayıtlar için geriye uyumlu).
    /// </summary>
    public int? EctsSnapshot { get; set; }

    /// <summary>
    /// Bu kayıt hangi CurriculumElectivePool'a karşılık geliyor (null = zorunlu veya tekil seçmeli).
    /// Havuz tamamlama kontrolü ve mezuniyet denetimi için kullanılır.
    /// </summary>
    public int? AppliedElectivePoolId { get; set; }
    public CurriculumElectivePool? AppliedElectivePool { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
