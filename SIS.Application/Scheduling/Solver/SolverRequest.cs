using System.Collections.Generic;

namespace SIS.Application.Scheduling.Solver;

public sealed record SolverRequest
{
    public required int TermId { get; init; }
    public required int FacultyId { get; init; }
    public int? DepartmentId { get; init; }
    public int? Seed { get; init; }
    public IReadOnlyList<SectionInput> Sections { get; init; } = [];
    public IReadOnlyList<ClassroomInput> Classrooms { get; init; } = [];
    public IReadOnlyList<TimeSlotInput> TimeSlots { get; init; } = [];
    public IReadOnlyDictionary<string, bool> ConstraintFlags { get; init; } =
        new Dictionary<string, bool>();
    public IReadOnlyList<CourseRelationship> Corequisites { get; init; } = [];

    /// <summary>
    /// Plan 5 — SystemConfig.Schedule.DefaultInstructorMaxWeeklyHours'tan okunur (default 30).
    /// SectionInput.InstructorMaxWeeklyHours == 0 olan section'lar için fallback.
    /// </summary>
    public int DefaultInstructorMaxWeeklyHours { get; init; } = 30;

    /// <summary>
    /// Plan 6 — H5 müfredat çakışma kontrolü modu.
    /// "curriculum" (default): (DepartmentId, Year) groups → AddNoOverlap.
    /// "cohort": CohortConflictPairs üzerinden pair-level no-overlap.
    /// "enrollment": Plan 6.5 — enrollment matrix mode.
    /// SchedulingAlgorithmConfigs.Schedule.CurriculumConflictMode'tan okunur.
    /// </summary>
    public string CurriculumConflictMode { get; init; } = "curriculum";

    /// <summary>
    /// Plan 6 — cohort modunda H5 encoder'ın kullanacağı section pair'leri.
    /// SolverRequestBuilder cohort mode aktifken v1 CohortBuilder portu ile hesaplar.
    /// curriculum mode'da boş kalır.
    /// </summary>
    public IReadOnlyList<(int SectionA, int SectionB)> CohortConflictPairs { get; init; } = [];

    /// <summary>
    /// Plan 6 Task 10 — DistributionConstraints (SameDay/DifferentDay/SameRoom/BackToBack/NotBackToBack/Precedence).
    /// SolverRequestBuilder DistributionConstraints + DistributionConstraintCourses tablolarından yükler.
    /// Encoder Plan 6.5'a ertelendi (real impl); şu an diagnostic-only stub.
    /// </summary>
    public IReadOnlyList<DistributionConstraintInput> DistributionConstraints { get; init; } = [];
}
