using FluentAssertions;
using Moq;
using SIS.Application.Common.Services;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Mapping;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools;

/// <summary>
/// Plan 9 Phase 5 Task 16 — Combined synthetic integration test.
/// Plan 9'da yapılan fix'lerin end-to-end engine ile birlikte çalıştığını gösterir.
///
/// Senaryo: 6 section, 2 day × 5 slot grid (lunch slot dahil), 2 classroom (lecture + lab):
/// <list type="bullet">
///   <item>S1 FaceToFace + lecture-only (regular).</item>
///   <item>S2 Online + lecture-only — Tuesday slot 0 FaceToFaceOnly → H17 ban (Plan 9 Task 12 simetri).</item>
///   <item>S3 FaceToFace + multi-block lab (LabHours=4 + MaxLabBlockHours=2) →
///         Plan 9 Issue 3 (H13 plural) + Issue 4a (S1 multi-block) + Issue 4b (S3 lab iter).</item>
///   <item>S4 FaceToFace + lab-only (LabHours=2) → Plan 9 Issue 4b S3 lab parity.</item>
///   <item>S5+S6 same CourseId 105 (lecture-only + lab-only) → Plan 9 Task 11 H6 cross-section.</item>
/// </list>
/// Engine all 6 section'ı yerleştirmeli; objective ≤ presence reward (6 × 10000 = 60000).
/// </summary>
public class Plan9CombinedIntegrationTest
{
    [Fact]
    public async Task SolveAsync_AllPlan9FixesActive_ProducesFeasibleSchedule()
    {
        var sections = new List<SectionInput>
        {
            BuildLecture(id: 1, courseId: 100, code: "BM 101", instructorId: 1),
            BuildOnline(id: 2, courseId: 102, code: "ONL 102", instructorId: 2),
            BuildMultiBlockLab(id: 3, courseId: 103, code: "BM 103", instructorId: 3),
            BuildLabOnly(id: 4, courseId: 104, code: "BM 104", instructorId: 4),
            BuildLecture(id: 5, courseId: 105, code: "BM 105", instructorId: 5),
            BuildLabOnly(id: 6, courseId: 105, code: "BM 105", instructorId: 5, sectionNumber: 2),
        };
        var slots = BuildTwoDayGridWithF2FOnlyAndLunch();
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "D1", Capacity = 50, Type = ClassroomType.Lecture },
            new ClassroomInput { Id = 2, Name = "L1", Capacity = 40, Type = ClassroomType.Lab },
        };

        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = sections,
            Classrooms = classrooms,
            TimeSlots = slots,
            MaxLabBlockHours = 2,
            ConstraintFlags = new Dictionary<string, bool>
            {
                ["H1"] = true, ["H6"] = true, ["H13"] = true, ["H17"] = true,
                ["S1"] = true, ["S3"] = true, ["S5"] = true,
            },
            SoftThresholds = new SoftThresholds(),
        };

        var engine = BuildEngine();
        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().BeOneOf(new[] { SolverStatus.Optimal, SolverStatus.Feasible });
        result.Assignments.Should().HaveCount(6,
            because: "all 6 sections placed (no production blocker scenarios in this synthetic case)");

        // Verify Plan 9 Task 12: Online section S2 must NOT land on Tuesday slot 0 (F2FOnly).
        var s2Assignment = result.Assignments.Single(a => a.SectionId == 2);
        var tuesdayF2FOnlyId = slots[5].Id; // index 5 = Tuesday slot 0 (F2FOnly)
        s2Assignment.Slots.Should().NotContain(s => s.TimeSlotId == tuesdayF2FOnlyId,
            because: "Plan 9 Task 12 — Online section FaceToFaceOnly slot'a yerleşemez");

        // Verify Plan 9 Task 11: same CourseId S5 (lecture) and S6 (lab) must NOT overlap.
        var s5 = result.Assignments.Single(a => a.SectionId == 5);
        var s6 = result.Assignments.Single(a => a.SectionId == 6);
        var s5SlotIds = s5.Slots.Select(s => s.TimeSlotId).ToHashSet();
        var s6SlotIds = s6.Slots.Select(s => s.TimeSlotId).ToHashSet();
        s5SlotIds.Overlaps(s6SlotIds).Should().BeFalse(
            because: "Plan 9 Task 11 — same CourseId cross-section component overlap yasak");

        // Objective sanity: presence reward dominant, soft penalties bounded.
        result.Diagnostics.ObjectiveValue.Should().BeLessThanOrEqualTo(60000,
            because: "objective bounded by presence reward (6 × 10000)");
    }

    private static SectionInput BuildLecture(int id, int courseId, string code, int instructorId, int sectionNumber = 1) =>
        new()
        {
            Id = id,
            CourseId = courseId,
            CourseCode = code,
            CourseName = code,
            SectionNumber = sectionNumber,
            Capacity = 30,
            TheoryHours = 2,
            LabHours = 0,
            InstructorId = instructorId,
            TeachingMethod = TeachingMethod.FaceToFace,
        };

    private static SectionInput BuildOnline(int id, int courseId, string code, int instructorId) =>
        new()
        {
            Id = id,
            CourseId = courseId,
            CourseCode = code,
            CourseName = code,
            SectionNumber = 1,
            Capacity = 30,
            TheoryHours = 2,
            LabHours = 0,
            InstructorId = instructorId,
            TeachingMethod = TeachingMethod.Online,
        };

    private static SectionInput BuildMultiBlockLab(int id, int courseId, string code, int instructorId) =>
        new()
        {
            Id = id,
            CourseId = courseId,
            CourseCode = code,
            CourseName = code,
            SectionNumber = 1,
            Capacity = 30,
            TheoryHours = 0,
            LabHours = 4,           // > MaxLabBlockHours=2 → 2 blocks
            LabGroupCount = 1,
            InstructorId = instructorId,
            TeachingMethod = TeachingMethod.FaceToFace,
        };

    private static SectionInput BuildLabOnly(int id, int courseId, string code, int instructorId, int sectionNumber = 1) =>
        new()
        {
            Id = id,
            CourseId = courseId,
            CourseCode = code,
            CourseName = code,
            SectionNumber = sectionNumber,
            Capacity = 30,
            TheoryHours = 0,
            LabHours = 2,
            LabGroupCount = 1,
            InstructorId = instructorId,
            TeachingMethod = TeachingMethod.FaceToFace,
        };

    /// <summary>
    /// 2 day × 5 slot grid (10 slots). Monday all Both. Tuesday slot 0 (index 5) FaceToFaceOnly
    /// (Online section'lar için Plan 9 Task 12 ban tetiklenir). Slot 2 (11:30) lunch overlap.
    /// </summary>
    private static IReadOnlyList<TimeSlotInput> BuildTwoDayGridWithF2FOnlyAndLunch()
    {
        var times = new (int hour, int minute)[]
        {
            (9, 0),    // 09:00-09:50
            (10, 0),   // 10:00-10:50
            (11, 30),  // 11:30-12:20 ← lunch overlap
            (13, 0),   // 13:00-13:50
            (14, 0),   // 14:00-14:50
        };

        var slots = new List<TimeSlotInput>();
        for (int d = 1; d <= 2; d++)
        {
            for (int s = 0; s < times.Length; s++)
            {
                var (h, m) = times[s];
                var allowedMode = (d == 2 && s == 0)
                    ? TimeSlotAllowedMode.FaceToFaceOnly  // Tuesday slot 0 → F2F only
                    : TimeSlotAllowedMode.Both;
                slots.Add(new TimeSlotInput
                {
                    Id = (d - 1) * times.Length + s + 1,
                    DayOfWeek = (DayOfWeek)d,
                    SlotOrder = s + 1,
                    StartTime = new TimeOnly(h, m),
                    EndTime = new TimeOnly(h, m).AddMinutes(50),
                    AllowedMode = allowedMode,
                });
            }
        }
        return slots;
    }

    private static CpSatSolverEngine BuildEngine()
    {
        var encoders = new IConstraintEncoder[]
        {
            new ClassroomConflictEncoder(),                  // H1
            new TheoryLabComponentEncoder(),                 // H6 (cross-section Plan 9 Task 11)
            new LunchBreakEncoder(new TimeOnly(11, 40), new TimeOnly(12, 20)), // H13 (multi-block lab Plan 9 Task 4)
            new FaceToFaceSlotEncoder(),                     // H17 (Online + F2FOnly Plan 9 Task 12)
            new OccupancyEncoder(),                          // S1 (multi-block presence Plan 9 Task 5)
            new SlotCongestionEncoder(),                     // S3 (lab interval iter Plan 9 Task 6)
            new LunchBreakSoftEncoder(),                     // S5
        };
        var modelBuilder = new ScheduleModelBuilder(encoders);
        var mapper = new SolutionMapper();
        var unsatInterpreter = new UnsatCoreInterpreter();
        var preflightChecker = new PreflightFeasibilityChecker();

        var configMock = new Mock<ISystemConfigProvider>();
        configMock.Setup(x => x.GetIntAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, int f, CancellationToken _) => f);
        configMock.Setup(x => x.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, bool f, CancellationToken _) => f);

        return new CpSatSolverEngine(modelBuilder, mapper, configMock.Object, unsatInterpreter, preflightChecker);
    }
}
