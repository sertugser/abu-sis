namespace SIS.Application.OpenedDepartmentCourses.DTOs;

/// <summary>
/// Aktif dönemde öğrencinin bölümüne ait açılmış bir CourseSection satırı.
/// Sınıf bilgisi öğrencinin aktif curriculum'undaki CurriculumCourse.SuggestedSemester'dan
/// hesaplanır; ders müfredatta yoksa null döner ("—" gösterilir).
/// </summary>
public sealed record OpenedDepartmentCourseDto(
    int SectionId,
    int SectionNumber,        // Şb
    string CourseCode,        // Ders Kodu
    string CourseName,        // Ders Adı
    int? ClassYear,           // Sınıf (1-4), müfredat dışıysa null
    int ApplicationHours,     // U (Uygulama)
    int LabHours,             // L
    int TheoryHours,          // T
    int NationalCredit,       // KRD
    int Ects);                // AKTS

public sealed record OpenedDepartmentCoursesDto(
    string? TermLabel,                                       // Aktif dönem; yoksa null
    string? DepartmentName,                                  // Bölüm adı (öğrencinin programı)
    IReadOnlyList<OpenedDepartmentCourseDto> Items);
