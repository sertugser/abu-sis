namespace SIS.Domain.Entities;

public class CurriculumCourse
{
    public int CurriculumId { get; set; }
    public Curriculum Curriculum { get; set; } = null!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public bool IsCompulsory { get; set; }
    public int SuggestedSemester { get; set; }             // which semester this course is typically taken
    public bool IncludeInGpa { get; set; } = true;          // Ortalamalara Katılım
    public bool ShowOnTranscript { get; set; } = true;       // Transkriptte Göster

    // Lookup FKs (replaced legacy string fields CourseCategory + OpeningReason)
    public int? CategoryId { get; set; }
    public CourseCategory? Category { get; set; }
    public int? OpeningReasonId { get; set; }
    public CurriculumCourseOpeningReason? OpeningReason { get; set; }

    public string? GroupCode { get; set; }                    // Grup Kodu (seçmeli grup)
    public int? ElectiveGroupId { get; set; }
    public ElectiveGroup? ElectiveGroup { get; set; }

    // ── Phase 2 fields ─────────────────────────────────────────────────
    // UI flags
    public bool IsMainGroupCourse { get; set; } = false;          // Bu bir ana grup dersi
    public bool IsFormationCourse { get; set; } = false;          // Formasyon Dersi
    public bool ExcludedFromCreditTotal { get; set; } = false;    // Krd Top. Dışı
    public bool ExcludedFromTranscriptTotal { get; set; } = false;// Transkript Krd/AKTS Toplama Dahil Değil
    public bool AutoAddOnNewRegistration { get; set; } = false;   // ÖSYM Yeni Kayıtta Otomatik Ekle
    public bool ShowAtTakenCurriculum { get; set; } = false;      // Alındığı Yerde Göster
    public bool ShowInstructorAtRegistration { get; set; } = true;// Ders Kayıtta Öğretim Elemanı Görünsün
    public bool HasCustomExamCurriculum { get; set; } = false;    // Derse Özel Sınav Müfredat Ata

    // Karar bilgileri (senato kararı vb.)
    public DateTime? DecisionDate { get; set; }
    public string? DecisionNo { get; set; }
    public string? DecisionNote { get; set; }

    // Müfredata özel açıklama (Course.Description'dan ayrı)
    public string? CurriculumDescription { get; set; }
}
