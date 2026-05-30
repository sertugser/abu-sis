namespace SIS.Application.CourseRegistration.DTOs;

public class CourseHistoryTermDto
{
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Semester { get; set; } = string.Empty;
    public List<CourseHistoryItemDto> Courses { get; set; } = new();
}

public class CourseHistoryItemDto
{
    public int RegistrationId { get; set; }
    public int CourseSectionId { get; set; }
    public int CourseId { get; set; }
    public int SectionNumber { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Ects { get; set; }

    // Phase 1 — referans tasarım için eklenen alanlar
    /// <summary>Ulusal/yerel kredi. CreditSnapshot varsa o, yoksa Course.NationalCredit.</summary>
    public int NationalCredit { get; set; }
    public int TheoryHours { get; set; }
    public int LabHours { get; set; }
    public int? ApplicationHours { get; set; }

    /// <summary>Dersin müfredattaki sınıfı (1-4). Öğrencinin aktif curriculum'unda hesaplanır. Bulunmazsa null.</summary>
    public int? ClassYear { get; set; }

    /// <summary>"Zorunlu" / "Seçmeli" / "Alttan FF" / "Devamlı Alttan NA" gibi. Server hesaplar.
    /// PDF "Alış Şekli" sütununda detaylı gösterilir.</summary>
    public string AcquisitionType { get; set; } = string.Empty;

    /// <summary>Dersin müfredatta zorunlu olup olmadığı. UI "Z/S" sütunu için (Z=Zorunlu, S=Seçmeli).
    /// Müfredat dışı dersler için false (seçmeli kabul edilir).</summary>
    public bool IsCompulsory { get; set; }

    /// <summary>Dersi sahiplenen birim adı (Course.Department.Name) — "Ortak Dersler" gibi.</summary>
    public string? OwnerDepartmentName { get; set; }

    /// <summary>Dersin verilme dili (Türkçe/İngilizce/…).</summary>
    public string LanguageDisplay { get; set; } = string.Empty;

    public string InstructorName { get; set; } = string.Empty;
    public string? Grade { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsRepeated { get; set; }
}

/// <summary>
/// PDF "Alınan Dersler" raporu için DTO. Öğrenci bilgi bloğu + dönem listesi + alt toplam.
/// </summary>
public class CourseHistoryReportDto
{
    // Öğrenci bilgi bloğu (PDF üstü)
    public string StudentNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int? CurrentClassYear { get; set; }
    public string? FacultyName { get; set; }
    public string? ProgramName { get; set; }
    public string? AdvisorName { get; set; }
    public string? ActiveTermName { get; set; }

    public List<CourseHistoryTermDto> Terms { get; set; } = new();

    // Alt toplam (referans PDF: "Toplam Ders Sayısı: 7  Ders Saati: 20  Kredi: 18  AKTS: 30")
    public int TotalCourses { get; set; }
    public int TotalTheoryHours { get; set; }
    public int TotalCredit { get; set; }
    public int TotalEcts { get; set; }
}
