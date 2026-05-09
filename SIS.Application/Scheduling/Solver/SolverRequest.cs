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

    /// <summary>
    /// Plan 7b — SystemConfig "Classroom.AmphiThreshold" (default 100). Capacity ≥ threshold
    /// olan section'lar Amphitheater dışındaki lecture room'a yerleşemez. CpSatSolverEngine yükler.
    /// </summary>
    public int AmphiThreshold { get; init; } = 100;

    /// <summary>
    /// Plan 9 Issue 1 — SystemConfig "Schedule.AmphiThresholdEnabled" (default false).
    /// Production'da DB'de 0 Amphitheater varken AmphiThreshold kuralı UNSAT üretiyordu (Term 5 blocker).
    /// Default false: Capacity ≥ AmphiThreshold kuralı kapalı; admin Amphitheater classroom ekledikten
    /// sonra true yaparak kuralı aktive eder. <see cref="SectionInput.RequiredTheoryClassroomType"/>
    /// (course-spesifik tip zorunluluğu) bu flag'tan bağımsız çalışır.
    /// </summary>
    public bool AmphiThresholdEnabled { get; init; } = false;

    /// <summary>
    /// Plan 9 Issue 6 — SystemConfig "Schedule.ClassroomTypeRequirementEnabled" (default false).
    /// <see cref="SectionInput.RequiredTheoryClassroomType"/> ve <see cref="SectionInput.RequiredLabClassroomType"/>
    /// kurallarını gate eder. Production'da bu alanlar (Course tarafında) seed edilmiş olabiliyor
    /// ama admin UI'sı yok (Plan 10 ekleyecek) — flag false iken kural devre dışı, AutoScheduleTestData
    /// gibi senaryolar UNSAT üretmiyor. True iken Plan 7b davranışı (Theory tip zorunluluğu + Lab override).
    /// </summary>
    public bool ClassroomTypeRequirementEnabled { get; init; } = false;

    /// <summary>
    /// Plan 7b — SystemConfig "Schedule.MaxLabBlockHours" (default 2). LabHours > value
    /// olan section'lar N intervala bölünür ve farklı günlere yerleştirilir (v1 paritesi).
    /// CpSatSolverEngine yükler.
    /// </summary>
    public int MaxLabBlockHours { get; init; } = 2;

    /// <summary>
    /// Plan 8 — Soft constraint eşik + weight'leri. SolverRequestBuilder SystemConfig'ten okur.
    /// Tüm 10 soft encoder bu record üzerinden parametre alır.
    /// </summary>
    public SoftThresholds SoftThresholds { get; init; } = new();

    /// <summary>
    /// Plan 8 — S4 CoEnrollment için pair listesi. Production'da 0 (CourseRegistrations boş);
    /// admin gerçek student verisi girince dolar. Encoder boş listede skip eder.
    /// </summary>
    public IReadOnlyList<CoEnrollmentPairInput> CoEnrollmentPairs { get; init; } = [];

    /// <summary>
    /// Plan 8 — S20 AdjacentYearConflict için cohort pair listesi. CurriculumConflictMode = "cohort"
    /// modunda CohortBuilder.AdjacentYearSoftPairs'ten doldurulur.
    /// </summary>
    public IReadOnlyList<(int SectionA, int SectionB)> AdjacentYearSoftPairs { get; init; } = [];
}
