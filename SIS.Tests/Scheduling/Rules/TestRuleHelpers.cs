using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.Rules;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Scheduling;
using SIS.Infrastructure.Services.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using SIS.Infrastructure.Services.Scheduling.Rules.Soft;
using SIS.Infrastructure.Services.Scheduling.Rules.Hybrid;

namespace SIS.Tests.Scheduling.Rules;

/// <summary>
/// Plan 11 — Rules test'leri için ortak builder helper'ları. 17 IRulePredicate'i set'leyen
/// gerçek RuleEngine instance'ı + RuntimeSnapshot/RuntimeCandidate kurulumu.
/// </summary>
internal static class TestRuleHelpers
{
    public static IRulePredicate[] AllPredicates() => new IRulePredicate[]
    {
        new H1_ClassroomConflictRule(),
        new H2_InstructorConflictRule(),
        new H3_CapacityRule(),
        new H4_ClassroomTypeRule(),
        new H5_CurriculumConflictRule(),
        new H6_TheoryLabComponentRule(),
        new H8_SameCourseSectionRule(),
        new H10_FacultyClassroomRule(),
        new H11_CorequisiteRule(),
        new H12_CreditHourRule(),
        new H13_LunchBreakRule(),
        new H15_MaxConsecutiveHoursRule(),
        new H16_TheoryLabDaySplitRule(),
        new H17_FaceToFaceSlotRule(),
        new HDL_DailyLoadRule(),
        new HDO_NoDayOffRule(),
        new HWL_WeeklyLoadRule(),
        // Plan 12 — soft + hybrid predicate'lar
        new S1_OccupancyRule(),
        new S2_DailyLoadSoftRule(),
        new S3_SlotCongestionRule(),
        new S4_CoEnrollmentRule(),
        new S5_LunchBreakSoftRule(),
        new S7_StudentGapRule(),
        new S8_InstructorDaySpreadRule(),
        new S12_WeekdayBalanceRule(),
        new S20_AdjacentYearConflictRule(),
        new DIST_DistributionRule(),
    };

    /// <summary>Boş in-memory DbContext üzerine ConstraintEvaluationService kurar (state null kullanmak için).</summary>
    public static RuleEngine BuildEngine(IEnumerable<IRulePredicate>? predicates = null)
    {
        return new RuleEngine(
            predicates ?? AllPredicates(),
            NullLogger<RuleEngine>.Instance);
    }

    public static SchedulingConstraintConfig DefaultConfig(Action<SchedulingConstraintConfig>? customize = null)
    {
        var cfg = new SchedulingConstraintConfig
        {
            LunchBreakIsHard = false,
            LunchBreakStart = new TimeOnly(11, 40),
            LunchBreakEnd = new TimeOnly(12, 20),
            MaxConsecutiveHours = 4,
            BreakDurationMinutes = 5,
            AmphiThreshold = 100,
            AmphiThresholdEnabled = false,
            TheoryLabSplitThreshold = 4,
            MaxDailyHours = 9,
            CurriculumConflictMode = "curriculum",
        };
        customize?.Invoke(cfg);
        return cfg;
    }

    public static RuntimeBlock Block(int day, int startHour, int durationHours,
        int? classroomId = 1, string classroomName = "C", ClassroomType type = ClassroomType.Lecture, int classroomCapacity = 30)
        => new(day, new TimeOnly(startHour, 0), new TimeOnly(startHour + durationHours, 0),
            classroomId, classroomName, type, classroomCapacity);

    public static RuntimeCandidate Candidate(
        int id, int courseId, int? instructorId = null,
        int capacity = 30, int theory = 3, int lab = 0,
        TeachingMethod method = TeachingMethod.FaceToFace,
        int instructorMaxWeeklyHours = 0,
        int? departmentId = null,
        IReadOnlyDictionary<int, int>? semestersByDept = null,
        IReadOnlyCollection<int>? accessibleDepts = null,
        params RuntimeBlock[] blocks)
        => new()
        {
            SectionId = id,
            SectionNumber = id,
            CourseId = courseId,
            CourseCode = $"C{courseId}",
            CourseName = $"Course {courseId}",
            DepartmentId = departmentId,
            InstructorId = instructorId,
            InstructorName = instructorId.HasValue ? $"Hoca {instructorId}" : string.Empty,
            InstructorMaxWeeklyHours = instructorMaxWeeklyHours,
            Capacity = capacity,
            TheoryHours = theory,
            LabHours = lab,
            TeachingMethod = method,
            SuggestedSemestersByDepartment = semestersByDept ?? new Dictionary<int, int>(),
            AccessibleDepartmentIds = accessibleDepts ?? Array.Empty<int>(),
            Blocks = blocks,
        };

    public static RuntimeSnapshot Snapshot(params RuntimeCandidate[] sections)
        => new() { TermId = 1, Sections = sections };
}
