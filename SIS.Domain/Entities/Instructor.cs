namespace SIS.Domain.Entities;

public class Instructor
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public int MaxWeeklyHours { get; set; }
    public string OfficeHours { get; set; } = string.Empty;
    public bool AdvisorOptOutRequested { get; set; }

    // Toplu danışman atama ayarları
    // null veya true: otomatik atamaya dahil
    // false: otomatik atamaya dahil değil
    public bool? IsAvailableForAdvisorAssignment { get; set; } = true;

    // null: sınırsız kapasite
    // 0: hiç yeni öğrenci atanmaz
    // pozitif değer: toplam danışmanlık kapasitesi
    public int? AdvisorAssignmentCapacity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<CourseSection> Sections { get; set; } = new List<CourseSection>();
    public ICollection<AcademicCvActivity> AcademicCvActivities { get; set; } = new List<AcademicCvActivity>();
}
