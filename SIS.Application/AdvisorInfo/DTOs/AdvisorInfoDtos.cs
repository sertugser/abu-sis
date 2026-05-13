namespace SIS.Application.AdvisorInfo.DTOs;

/// <summary>
/// Öğrencinin danışman bilgileri. <see cref="HasAdvisor"/> false ise diğer alanlar null.
/// </summary>
public sealed record AdvisorInfoDto(
    bool HasAdvisor,
    string? Title,
    string? FirstName,
    string? LastName,
    string? FullName,       // "{Title} {First} {Last}".Trim() — Title boşsa sadece isim
    string? FacultyName,
    string? DepartmentName,
    string? ProgramName,    // Öğrencinin programı (StudentProgram.Program.Name)
    string? Phone,          // ApplicationUser.PhoneNumber, null/empty olabilir
    string? Email);

/// <summary>
/// Danışmanın aktif dönem haftalık ders programı.
/// </summary>
public sealed record AdvisorScheduleDto(
    string? TermLabel,      // AcademicTerm.Name (örn "2025-2026 Güz"); aktif dönem yoksa null
    IReadOnlyList<AdvisorScheduleItemDto> Items);

/// <summary>
/// Tek bir SectionSchedule satırı (bir derste birden fazla blok olabilir).
/// </summary>
public sealed record AdvisorScheduleItemDto(
    int SectionId,
    string CourseCode,
    string CourseName,
    int SectionNumber,
    int DayOfWeek,          // 1=Pazartesi .. 7=Pazar (System.DayOfWeek casting: Mon=1..Sun=0 → +7 if 0)
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? ClassroomCode,  // null mümkün (online section)
    string BlockType);      // "Theory" | "Lab" — ScheduleBlockType enum ToString
