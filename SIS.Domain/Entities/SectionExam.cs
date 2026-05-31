namespace SIS.Domain.Entities;

/// <summary>
/// Bir GradeComponent (sınav kalemi: Vize/Final) için belirli bir şubenin
/// sınav OTURUMU — ne zaman/nerede yapılacağı. Etki oranı/ad burada DEĞİL,
/// ortak GradeComponent'te tutulur (ikibaşlılık yok). Şube bazlıdır.
/// </summary>
public class SectionExam
{
    public int Id { get; set; }

    public int CourseSectionId { get; set; }
    public CourseSection CourseSection { get; set; } = null!;

    public int GradeComponentId { get; set; }
    public GradeComponent GradeComponent { get; set; } = null!;

    /// <summary>Sınav tarihi + saati. UTC.</summary>
    public DateTime ExamDate { get; set; }

    /// <summary>Sınav süresi (dakika). Opsiyonel; çakışma penceresi için kullanılır.</summary>
    public int? DurationMinutes { get; set; }

    /// <summary>"Sınav tarihi öğrencilere ilan edilsin" işaretlendi mi.</summary>
    public bool IsAnnouncedToStudents { get; set; }

    /// <summary>İlan zamanı (UTC). null = henüz ilan edilmedi.</summary>
    public DateTime? AnnouncedAt { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<SectionExamClassroom> Classrooms { get; set; } = new List<SectionExamClassroom>();
}
