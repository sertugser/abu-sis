namespace SIS.Application.OpenedDepartmentCourses.DTOs;

/// <summary>
/// Aktif dönemde öğrencinin müfredatına ait açılmış bir CourseSection satırı.
/// Sınıf bilgisi öncelikli olarak CurriculumCourse.SuggestedSemester'dan;
/// 0 ise ders kodu ilk rakamından (CourseCodeHelper) çıkarılır.
/// </summary>
public sealed record OpenedDepartmentCourseDto(
    int SectionId,
    int SectionNumber,        // Şb
    string CourseCode,        // Ders Kodu
    string CourseName,        // Ders Adı
    int ClassYear,            // Sınıf (1-4) — null değil, fallback ile her zaman dolu
    int ApplicationHours,     // U (Uygulama)
    int LabHours,             // L
    int TheoryHours,          // T
    bool IsCompulsory,        // Z (Zorunlu/Seçmeli — true=Z, false=S)
    int NationalCredit,       // KRD
    int Ects);                // AKTS

/// <summary>
/// Boş sonuç dönerken kök nedeni ayrıştırır — UI dal-bazlı mesaj göstermeli.
/// </summary>
public enum OpenedDepartmentCoursesEmptyReason
{
    None = 0,                 // Items dolu
    StudentNotFound = 1,      // userId ile öğrenci kaydı yok
    NoMainProgram = 2,        // Aktif Main programı yok
    NoActiveTerm = 3,         // Sistemde aktif (IsActivated) dönem yok
    Empty = 4                 // Hepsi var ama 0 section
}

public sealed record OpenedDepartmentCoursesDto(
    string? TermLabel,                                       // Aktif dönem; yoksa null
    string? DepartmentName,                                  // Bölüm adı (öğrencinin programı)
    OpenedDepartmentCoursesEmptyReason EmptyReason,          // Boş ise kök neden
    IReadOnlyList<OpenedDepartmentCourseDto> Items);
