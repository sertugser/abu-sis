using FluentAssertions;
using Moq;
using SIS.Application.Common.Services;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Mapping;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools;

/// <summary>
/// Plan 6 Task 7 — H5 curriculum + cohort mode end-to-end integration test.
/// 2 sections (different courses) sharing (deptId=1, year=1) cohort, 2 classrooms × 8 timeslots.
/// Curriculum mode: (DeptId, Year) group AddNoOverlap forces separation.
/// Cohort mode: explicit pair (1, 2) forces separation.
/// </summary>
public class CurriculumCohortIntegrationTest
{
    [Fact]
    public async Task SolveAsync_CurriculumMode_TwoSectionsSameCohort_ScheduledAtDifferentSlots()
    {
        // Arrange
        var sections = new[]
        {
            Section(1, courseId: 100, deptId: 1, year: 1, theory: 2),
            Section(2, courseId: 200, deptId: 1, year: 1, theory: 2)
        };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "C1", Capacity = 50, Type = ClassroomType.Lecture },
            new ClassroomInput { Id = 2, Name = "C2", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var slots = BuildSlots(8); // 2 days × 4 slots

        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = sections,
            Classrooms = classrooms,
            TimeSlots = slots,
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true, ["H5"] = true },
            CurriculumConflictMode = "curriculum"
        };

        var engine = BuildEngine();

        // Act
        var result = await engine.SolveAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().BeOneOf(
            new[] { SolverStatus.Optimal, SolverStatus.Feasible });
        result.Assignments.Should().HaveCount(2);

        // Verify no shared time slot between the two sections (curriculum mode forces separation).
        var slots1 = result.Assignments[0].Slots.Select(s => s.TimeSlotId).ToHashSet();
        var slots2 = result.Assignments[1].Slots.Select(s => s.TimeSlotId).ToHashSet();
        slots1.Intersect(slots2).Should().BeEmpty(
            because: "H5 curriculum mode forbids same-cohort time overlap");
    }

    [Fact]
    public async Task SolveAsync_CohortMode_OnePairProvided_ScheduledAtDifferentSlots()
    {
        // Arrange
        var sections = new[]
        {
            Section(1, courseId: 100, deptId: 1, year: 1, theory: 2),
            Section(2, courseId: 200, deptId: 1, year: 1, theory: 2)
        };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "C1", Capacity = 50, Type = ClassroomType.Lecture },
            new ClassroomInput { Id = 2, Name = "C2", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var slots = BuildSlots(8);

        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = sections,
            Classrooms = classrooms,
            TimeSlots = slots,
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true, ["H5"] = true },
            CurriculumConflictMode = "cohort",
            CohortConflictPairs = new (int, int)[] { (1, 2) }
        };

        var engine = BuildEngine();

        // Act
        var result = await engine.SolveAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().BeOneOf(
            new[] { SolverStatus.Optimal, SolverStatus.Feasible });
        result.Assignments.Should().HaveCount(2);

        var slots1 = result.Assignments[0].Slots.Select(s => s.TimeSlotId).ToHashSet();
        var slots2 = result.Assignments[1].Slots.Select(s => s.TimeSlotId).ToHashSet();
        slots1.Intersect(slots2).Should().BeEmpty(
            because: "cohort pair forbids time overlap");
    }

    // ─────────────── Helpers ───────────────

    private static SectionInput Section(int id, int courseId, int? deptId, int? year, int theory) => new()
    {
        Id = id,
        CourseId = courseId,
        CourseCode = $"X{id}",
        CourseName = $"X{id}",
        SectionNumber = 1,
        Capacity = 30,
        TheoryHours = theory,
        LabHours = 0,
        DepartmentId = deptId,
        Year = year,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    private static IReadOnlyList<TimeSlotInput> BuildSlots(int count)
    {
        return Enumerable.Range(0, count).Select(i => new TimeSlotInput
        {
            Id = i + 1,
            DayOfWeek = (DayOfWeek)((i / 4) + 1),
            SlotOrder = (i % 4) + 1,
            StartTime = new TimeOnly(9 + (i % 4), 0),
            EndTime = new TimeOnly(9 + (i % 4), 50),
            AllowedMode = TimeSlotAllowedMode.Both
        }).ToList();
    }

    private static CpSatSolverEngine BuildEngine()
    {
        var encoders = new IConstraintEncoder[]
        {
            new ClassroomConflictEncoder(),
            new CurriculumConflictEncoder()
        };
        var modelBuilder = new ScheduleModelBuilder(encoders);
        var mapper = new SolutionMapper();
        var unsatInterpreter = new UnsatCoreInterpreter();
        var preflightChecker = new PreflightFeasibilityChecker();

        var configMock = new Mock<ISystemConfigProvider>();
        configMock.Setup(x => x.GetIntAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string k, int f, CancellationToken _) => f);
        configMock.Setup(x => x.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string k, bool f, CancellationToken _) => f);

        return new CpSatSolverEngine(modelBuilder, mapper, configMock.Object, unsatInterpreter, preflightChecker);
    }
}
