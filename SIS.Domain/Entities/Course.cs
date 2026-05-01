namespace SIS.Domain.Entities;

using SIS.Domain.Enums;

public class Course
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? EnglishName { get; set; }        // İngilizce ders adı
    public string? ExamType { get; set; }           // Sınav Değer Tipi (default: "Otomatik Hesapla ve Harflendir")
    // Madde 10: Sahiplik zorunluluğu uygulama katmanında yönetilir.
    // Yeni dersler için DepartmentId doldurma zorunlu (AdminCourseCatalogService).
    // Ortak dersler (MATH/FIZ/AIIT/TRD/YDL) "Temel Bilimler Koordinatörlüğü" gibi
    // Department.IsServiceUnit=true olan birimlere atanır. Mevcut null'lar seed migration'da çevrilir.
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int Ects { get; set; }
    public int TheoryHours { get; set; }
    public int LabHours { get; set; }
    public int? ApplicationHours { get; set; }              // Uygulama Saati (Phase 2 — distinct from LabHours)
    public string? AlternativeName { get; set; }            // Diğer Adı (Phase 2)
    public bool ShowInInfoPackage { get; set; } = true;     // Bilgi Paketinde Gösterme (Phase 2)
    public CourseOpenType? OpenType { get; set; }           // Açma Tipi (Phase 2)
    public ClassroomType? TheoryClassroomType { get; set; }
    public ClassroomType? LabClassroomType { get; set; }
    public bool IsNonGpa { get; set; }                      // true = S/U graded
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public CourseLanguage Language { get; set; } = CourseLanguage.Turkish;
    public TeachingMethod TeachingMethod { get; set; } = TeachingMethod.FaceToFace;
    public CourseStatus Status { get; set; } = CourseStatus.Active;
    public string? Description { get; set; }
    public CourseLevel Level { get; set; } = CourseLevel.Undergraduate;
    public int? NationalCredit { get; set; }

    // Proliz "Diğer Ön Koşul Tanımları" — kayıt anında uygulanan kural alanları.
    // Hepsi opt-in: null/false ise ilgili kontrol atlanır.
    /// <summary>Bu dersi alabilmek için tamamlanmış olması gereken minimum kredi miktarı.</summary>
    public int? MinCreditsRequired { get; set; }
    /// <summary>Bu dersi alabilmek için tamamlanmış olması gereken minimum AKTS miktarı.</summary>
    public int? MinEctsRequired { get; set; }
    /// <summary>true → öğrencinin alt sınıflardan başarısız (FF/NA/U) dersi olmamalı.</summary>
    public bool RequiresNoFailedLowerCourses { get; set; }
    /// <summary>true → bu ders sadece tek başına alınabilir, başka derslerle aynı dönemde kayıtlanamaz.</summary>
    public bool CanBeTakenStandalone { get; set; }

    public ICollection<CoursePrerequisite> Prerequisites { get; set; } = new List<CoursePrerequisite>();
    public ICollection<CourseEquivalency> Equivalencies { get; set; } = new List<CourseEquivalency>();
}
