using SIS.Domain.Enums;

namespace SIS.Application.Scheduling.Solver;

public sealed record SectionInput
{
    public required int Id { get; init; }
    public required int CourseId { get; init; }
    public required string CourseCode { get; init; }
    public required string CourseName { get; init; }
    public required int SectionNumber { get; init; }
    public required int Capacity { get; init; }
    public required int TheoryHours { get; init; }
    public required int LabHours { get; init; }
    public int? InstructorId { get; init; }
    public int? DepartmentId { get; init; }
    public required TeachingMethod TeachingMethod { get; init; }
    public int? RequiredTheoryClassroomType { get; init; }
    public int? RequiredLabClassroomType { get; init; }
    public bool IsPinned { get; init; }
    public bool IsScheduleLocked { get; init; }

    // NEW (Plan 2 Task 7 — for H5 CurriculumConflictEncoder):
    public int? ProgramId { get; init; }

    /// <summary>
    /// Plan 6 — H5 curriculum çakışma kontrolü için section'ın yıl bilgisi (0..3 = 1.-4. sınıf).
    /// SolverRequestBuilder runtime'da CurriculumCourses.SuggestedSemester'dan türetir:
    /// year = (SuggestedSemester - 1) / 2. null = curriculum mapping yok → H5 atlar.
    /// </summary>
    public int? Year { get; init; }

    /// <summary>
    /// Lab component için paralel grup sayısı. 1 (default) = tek oda yeterli.
    /// >1 = section kapasitesi tek lab odasına sığmıyor, paralel split gerekiyor.
    /// SolverRequestBuilder runtime'da hesaplar: ⌈capacity / max_lab_capacity⌉.
    /// </summary>
    public int LabGroupCount { get; init; } = 1;

    /// <summary>
    /// Plan 5 — Hocaya özel haftalık maksimum saat override.
    /// 0 (default) = SolverRequest.DefaultInstructorMaxWeeklyHours kullanılır.
    /// >0 = bu section'ın hocası için bu sayı geçerlidir (rank-based veya kişiye özel).
    /// SolverRequestBuilder runtime'da Instructor.MaxWeeklyHours'tan yükler.
    /// </summary>
    public int InstructorMaxWeeklyHours { get; init; }
}
