namespace SIS.Domain.Entities;

using SIS.Domain.Enums;

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

    /// <summary>
    /// Müfredatın yaşam döngüsü durumu.
    /// Draft: Düzenlenebilir. Published: Kilitli (ders eklenip çıkarılamaz, öğrencilere bağlı).
    /// Archived: Geçmiş, yeni bağlanamaz.
    /// </summary>
    public CurriculumStatus Status { get; set; } = CurriculumStatus.Draft;

    /// <summary>Yayın tarihi (Draft → Published geçişinde doldurulur).</summary>
    public DateTime? PublishedAt { get; set; }

    // ── Dönem AKTS tavanı override'ları (müfredat-özel kuralları) ──
    // null = SystemConfig global varsayılanını kullan.
    // Proliz "Müfredat Sınıf Dönem Tanımları"nın muadili — her müfredat farklı kural tanımlayabilir.
    // Örn: Tıp Fakültesi 45 AKTS'ye izin verebilirken Güzel Sanatlar 36 AKTS ile sınırlayabilir.

    /// <summary>CGPA &lt; 2.00 için dönem AKTS tavanı (null = global).</summary>
    public int? EctsLimitBelow200 { get; set; }

    /// <summary>CGPA 2.00–2.49 için dönem AKTS tavanı (null = global).</summary>
    public int? EctsLimit200To249 { get; set; }

    /// <summary>CGPA ≥ 2.50 için dönem AKTS tavanı (null = global).</summary>
    public int? EctsLimitAbove250 { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ICollection<CurriculumCourse> CurriculumCourses { get; set; } = new List<CurriculumCourse>();
    public ICollection<CurriculumElectivePool> ElectivePools { get; set; } = new List<CurriculumElectivePool>();
}
