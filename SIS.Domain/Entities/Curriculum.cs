namespace SIS.Domain.Entities;

public class Curriculum
{
    public int Id { get; set; }
    public int ProgramId { get; set; }
    public AcademicProgram Program { get; set; } = null!;
    public string Version { get; set; } = string.Empty;    // e.g., "2022"
    public string? Name { get; set; }              // "Bilgisayar Müh. 2024 Müfredatı"
    public string? ShortName { get; set; }          // Kısa ad
    public string? EnglishName { get; set; }        // Uluslararası kullanım
    public DateTime? StartDate { get; set; }        // Müfredat geçerlilik başlangıcı
    public DateTime? EndDate { get; set; }          // Müfredat geçerlilik bitişi
    public string? DecisionDate { get; set; }       // Senato karar tarihi
    public string? DecisionNo { get; set; }         // Senato karar numarası
    public string? DecisionNote { get; set; }       // Karar açıklaması
    public string? GradingSystem { get; set; }      // Ders Geçme Sistemi (default: "Ders Geçme")
    public int? InternshipDuration { get; set; }    // Staj süresi (gün)
    public string? Description { get; set; }        // Genel açıklama
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ICollection<CurriculumCourse> CurriculumCourses { get; set; } = new List<CurriculumCourse>();
    public ICollection<CurriculumElectivePool> ElectivePools { get; set; } = new List<CurriculumElectivePool>();
}
