namespace SIS.Application.CourseRegistration.DTOs;

/// <summary>Öğrenci/danışman ders-kayıt ekranının tek veri kaynağı: müfredat-ağacı.
/// Şubeler GÖMÜLMEZ — "+"a basınca SectionPicker ucundan lazy çekilir.</summary>
public sealed record StudentRegistrationCatalogDto(
    RegistrationStudentHeaderDto Student,
    RegistrationTermInfoDto Term,
    IReadOnlyList<CatalogClassYearDto> ClassYears,
    IReadOnlyList<CatalogCourseRowDto> RetakeCourses,
    IReadOnlyList<CatalogPoolSlotDto> ElectivePoolSlots,
    IReadOnlyList<SelectedCourseDto> SelectedCourses);

public sealed record RegistrationStudentHeaderDto(
    int Id, string StudentNumber, string FullName, string ProgramName, string CurriculumName,
    int ClassYear, int CompletedSemesters, decimal Cgpa, int EctsLimit,
    string FinancialStatus,
    string StudyDurationStatus);

public sealed record RegistrationTermInfoDto(
    int Id, string Name, int Year, string TermType,
    bool IsRegistrationOpen, bool IsAddDropOpen, bool HasAcademicCalendar,
    string? RegistrationMessage);

public sealed record CatalogClassYearDto(
    int ClassYear,
    IReadOnlyList<CatalogSemesterDto> Semesters);

public sealed record CatalogSemesterDto(
    string Parity,
    int SemesterNumber,
    IReadOnlyList<CatalogCourseRowDto> Courses);

public sealed record CatalogCourseRowDto(
    int CourseId, string Code, string Name,
    bool IsCompulsory, int NationalCredit, int Ects,
    int SuggestedSemester, int ClassYear,
    string TakeStatus,
    bool PrerequisitesMet,
    string? PreviousAttemptInfo,
    string? ActiveRegistrationStatus,
    bool HasOpenSections,
    IReadOnlyList<ApplicablePoolSlotRefDto> ApplicablePoolSlots);

public sealed record ApplicablePoolSlotRefDto(int PoolId, string SlotName, string GroupCode, string GroupName, int? EctsPerCourse);

public sealed record CatalogPoolSlotDto(
    int PoolId, string GroupCode, string GroupName, string SlotName,
    int SuggestedSemesterStart, int SuggestedSemesterEnd,
    int RequiredCourseCount, int CompletedCourseCount,
    int RequiredEcts, int EarnedEcts, int? EctsPerCourse, bool IsFulfilled);

public sealed record SelectedCourseDto(
    int RegistrationId, int CourseSectionId, int CourseId,
    string CourseCode, string CourseName,
    bool IsCompulsory, int TheoryHours, int LabHours, int Ects, int SuggestedSemester,
    string TakeType, string Status,
    string? OpeningReasonName, string? ElectiveGroupCode);
