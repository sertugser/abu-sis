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
/// Plan 8 Task 21 — Combined integration test exercising multiple Plan 8 soft encoders
/// together (S2 DailyLoadSoft + S5 LunchBreakSoft + S8 InstructorDaySpread)
/// alongside Plan 7b hard encoders. Senaryo: 4 section, 2 hoca (X=1 + Y=2, herbirine 2 section),
/// no co-enrollment, no DIST, no AdjacentYear; 2-day grid (Mon+Tue) lunch slot dahil.
/// Solver tüm 4 section'ı yerleştirmeli ve ObjectiveValue presence reward
/// (4 × 10000 = 40000)'dan görece düşük penalty ile sapmalı.
/// </summary>
public class Plan8CombinedIntegrationTest
{
    [Fact]
    public async Task SolveAsync_AllSoftActive_ProducesLowPenaltySchedule()
    {
        // 4 sections: instructor X (id=1) → 2 sections, instructor Y (id=2) → 2 sections.
        // All TheoryHours=2, no co-enrollment, no DIST, no AdjacentYear pairs.
        var sections = new[]
        {
            BuildSection(id: 1, instructorId: 1, courseId: 100, code: "C100"),
            BuildSection(id: 2, instructorId: 1, courseId: 200, code: "C200"),
            BuildSection(id: 3, instructorId: 2, courseId: 300, code: "C300"),
            BuildSection(id: 4, instructorId: 2, courseId: 400, code: "C400"),
        };

        // 2-day grid (Mon + Tue). 5 slots/day around lunch:
        // 09:00, 10:00, 11:30 (lunch overlap with 11:40-12:20 default), 13:00, 14:00.
        // Solver should avoid the lunch slot when possible (S5 penalty).
        var slots = BuildTwoDayGrid();

        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "R1", Capacity = 50, Type = ClassroomType.Lecture },
            new ClassroomInput { Id = 2, Name = "R2", Capacity = 50, Type = ClassroomType.Lecture },
        };

        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = sections,
            Classrooms = classrooms,
            TimeSlots = slots,
            ConstraintFlags = new Dictionary<string, bool>
            {
                ["H1"] = true,
                ["H-DL"] = true,  // Plan 10 Phase 2: S2 → H-DL Soft
                ["H13"] = true,   // Plan 10: S5 → H13 Soft
                ["S8"] = true,
            },
            ConstraintCategories = new Dictionary<string, string>
            {
                ["H-DL"] = "Soft",  // S2 davranışı
                ["H13"] = "Soft",   // S5 davranışı
            },
            SoftThresholds = new SoftThresholds(),  // defaults
        };

        var engine = BuildEngine();
        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().BeOneOf(new[] { SolverStatus.Optimal, SolverStatus.Feasible });
        result.Assignments.Should().HaveCount(4, because: "all 4 sections placed");

        // Objective = (presenceCount × 10000) - softPenaltySum.
        // 4 sections present → max = 40000. Soft penalty must stay manageable
        // (well under 100 with this small scenario; expect close to 40000).
        result.Diagnostics.ObjectiveValue.Should().BeLessThanOrEqualTo(40000,
            because: "objective bounded by presence reward (4 × 10000)");
        result.Diagnostics.ObjectiveValue.Should().BeGreaterThan(40000 - 100,
            because: "with multiple soft encoders active and a non-conflicting 2-day grid, "
                  + "penalty must stay relatively low (objective dominant)");
    }

    private static SectionInput BuildSection(int id, int instructorId, int courseId, string code) =>
        new SectionInput
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
            TeachingMethod = TeachingMethod.FaceToFace,
        };

    private static IReadOnlyList<TimeSlotInput> BuildTwoDayGrid()
    {
        // Monday (DayOfWeek=1) + Tuesday (DayOfWeek=2), 5 slots each.
        // Slot 3 (11:30-12:20) overlaps default lunch window 11:40-12:20 → S5 penalty.
        // Solver should prefer non-lunch slots.
        var times = new (int hour, int minute)[]
        {
            (9, 0),    // 09:00-09:50
            (10, 0),   // 10:00-10:50
            (11, 30),  // 11:30-12:20  ← lunch overlap
            (13, 0),   // 13:00-13:50
            (14, 0),   // 14:00-14:50
        };

        var slots = new List<TimeSlotInput>();
        for (int d = 1; d <= 2; d++)
        {
            for (int s = 0; s < times.Length; s++)
            {
                var (h, m) = times[s];
                slots.Add(new TimeSlotInput
                {
                    Id = (d - 1) * times.Length + s + 1,
                    DayOfWeek = (DayOfWeek)d,
                    SlotOrder = s + 1,
                    StartTime = new TimeOnly(h, m),
                    EndTime = new TimeOnly(h, m).AddMinutes(50),
                    AllowedMode = TimeSlotAllowedMode.Both,
                });
            }
        }
        return slots;
    }

    private static CpSatSolverEngine BuildEngine()
    {
        // H1 (ClassroomConflict) for room non-overlap.
        // Plan 8 soft encoders: S1 (Occupancy), S3 (SlotCongestion), S5 (LunchBreakSoft),
        // S2 (DailyLoadSoft), S8 (InstructorDaySpread).
        // S1 + S3 registered for completeness; activated only if flag set (here: not set → no-op).
        var encoders = new IConstraintEncoder[]
        {
            new ClassroomConflictEncoder(),
            new OccupancyEncoder(),
            new SlotCongestionEncoder(),
            new LunchBreakSoftEncoder(),
            new DailyLoadSoftEncoder(),
            new InstructorDaySpreadEncoder(),
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
