namespace SIS.Domain.Entities;

using SIS.Domain.Enums;

public class CourseSection
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;
    public int? InstructorId { get; set; }
    public Instructor? Instructor { get; set; }
    public int Capacity { get; set; }
    public int EnrolledCount { get; set; }
    public int? InternalQuota { get; set; }
    public int? ExternalQuota { get; set; }
    public int? ClassroomId { get; set; }
    public Classroom? Classroom { get; set; }
    public int SectionNumber { get; set; }

    /// <summary>
    /// Section lifecycle durumu. Draft (eksik) ↔ Active (tam atama) otomatik.
    /// Cancelled yalnızca admin manuel aksiyonu; runtime kontroller kilitlenir.
    /// </summary>
    public CourseSectionStatus Status { get; set; } = CourseSectionStatus.Draft;

    /// <summary>Geriye uyumluluk: mevcut kod `IsActive` okur, computed olarak Status'tan türetilir.</summary>
    public bool IsActive => Status == CourseSectionStatus.Active;

    public bool IsFinalized { get; set; }
    public bool IsScheduleLocked { get; set; } // true = otomatik scheduling değiştiremez
    public bool IsPinned { get; set; } // true = dışarıdan gelen hoca, saati sabit, optimizasyon değiştiremez

    /// <summary>
    /// Yayın sonrası geçici düzenleme bayrağı. Program onaylı (yayında) iken bu section'ı
    /// değiştirmek için "yeni kayda durdur": true iken yeni öğrenci plana ekleyemez (mevcut
    /// kayıtlılar korunur) ve yayın kilidi yalnızca bu section için gevşer → düzenlenebilir.
    /// Düzenleme bitince false'a çekilir + kayıtlı öğrencilere bildirim gider. Status'tan
    /// bağımsız ortogonal bir boyuttur (Active section da suspended olabilir).
    /// </summary>
    public bool IsEnrollmentSuspended { get; set; }

    /// <summary>
    /// Cross-list grup kimliği. Aynı gruptaki section'lar aynı hoca/sınıf/saat paylaşır,
    /// kapasite birleşik hesaplanır. Null = tek başına section.
    /// </summary>
    public int? CrossListGroupId { get; set; }

    public uint RowVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ICollection<CourseRegistration> Registrations { get; set; } = new List<CourseRegistration>();
    public ICollection<SectionSchedule> SectionSchedules { get; set; } = new List<SectionSchedule>();
}
