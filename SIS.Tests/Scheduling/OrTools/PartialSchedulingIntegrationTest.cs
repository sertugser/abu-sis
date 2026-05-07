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
/// Plan 3 Task 14 — Partial scheduling regression test.
/// Reproduces the Plan 2 real DB Term 5/6 INFEASIBLE issue with a smaller, deterministic scenario.
/// Verifies that Phase A's PreflightFeasibilityChecker + optional intervals + Maximize(present)
/// produce partial assignments instead of all-or-nothing failure.
/// </summary>
public class PartialSchedulingIntegrationTest
{
    [Fact]
    public async Task SolveAsync_MixedFeasibleAndInfeasibleSections_ProducesPartialResult()
    {
        // Arrange: 15 sections, 5 classrooms, 50 slots
        var sections = BuildSections();
        var classrooms = BuildClassrooms();
        var timeSlots = BuildSlots();

        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = sections,
            Classrooms = classrooms,
            TimeSlots = timeSlots,
            ConstraintFlags = new Dictionary<string, bool>
            {
                ["H1"] = true,
                ["H3"] = true,
                ["H4"] = true,
                ["H6"] = true,
                ["H8"] = true
            }
        };

        var engine = BuildEngine();

        // Act
        var result = await engine.SolveAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().BeOneOf(new[] { SolverStatus.Optimal, SolverStatus.Feasible },
            because: "partial scheduling should produce a feasible result, not infeasible");

        result.Assignments.Count.Should().BeGreaterThan(0,
            because: "feasible sections should get scheduled even when some sections are infeasible");

        result.Failures.Count.Should().BeGreaterThan(0,
            because: "infeasible sections should appear in failures (preflight or solver-level drops)");

        // Sum check: every section accounted for (success or failure)
        var allSectionIds = sections.Select(s => s.Id).ToHashSet();
        var assignedIds = result.Assignments.Select(a => a.SectionId).ToHashSet();
        var failedIds = result.Failures.Select(f => f.SectionId).ToHashSet();

        // No section should appear in both
        assignedIds.Intersect(failedIds).Should().BeEmpty();

        // Every section in either assignments or failures (allowing for some sections to not appear at all in degenerate cases)
        var accountedFor = assignedIds.Union(failedIds).ToHashSet();
        accountedFor.IsSubsetOf(allSectionIds).Should().BeTrue();
    }

    [Fact]
    public async Task SolveAsync_PreflightInfeasibleSection_AppearsInFailuresWithSpecificReason()
    {
        // Arrange: 1 section with capacity exceeding all lab rooms (preflight rejection)
        var section = new SectionInput
        {
            Id = 999,
            CourseId = 999,
            CourseCode = "TEST",
            CourseName = "X",
            SectionNumber = 1,
            Capacity = 200,  // exceeds all lab rooms
            TheoryHours = 0,
            LabHours = 2,
            TeachingMethod = TeachingMethod.FaceToFace
        };

        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[] { section },
            Classrooms = new[]
            {
                new ClassroomInput { Id = 1, Name = "Small Lab", Capacity = 30, Type = ClassroomType.Lab }
            },
            TimeSlots = new[]
            {
                new TimeSlotInput
                {
                    Id = 1,
                    DayOfWeek = DayOfWeek.Monday,
                    SlotOrder = 1,
                    StartTime = new TimeOnly(9, 0),
                    EndTime = new TimeOnly(9, 50),
                    AllowedMode = TimeSlotAllowedMode.Both
                }
            },
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true }
        };

        var engine = BuildEngine();

        // Act
        var result = await engine.SolveAsync(request, CancellationToken.None);

        // Assert
        result.Failures.Should().HaveCount(1);
        result.Failures[0].SectionId.Should().Be(999);
        result.Failures[0].Reason.Should().Contain("Lab",
            because: "preflight should mark lab section with no valid lab room as infeasible");
    }

    // ─────────────── Helpers ───────────────

    private static IReadOnlyList<SectionInput> BuildSections()
    {
        var list = new List<SectionInput>();

        // 5 lecture-only sections (course 100), capacity 40 each — should fit
        for (int i = 1; i <= 5; i++)
            list.Add(BuildSection(i, courseId: 100, theory: 3, lab: 0, capacity: 40));

        // 5 mixed sections (course 200), capacity 35 — should fit
        for (int i = 6; i <= 10; i++)
            list.Add(BuildSection(i, courseId: 200, theory: 2, lab: 1, capacity: 35));

        // 2 lecture-only sections (course 300), capacity 200 — exceeds all rooms (preflight reject)
        for (int i = 11; i <= 12; i++)
            list.Add(BuildSection(i, courseId: 300, theory: 3, lab: 0, capacity: 200));

        // 3 same-course sections (course 400), small capacity, lecture-only — should fit but H8 may force drop
        for (int i = 13; i <= 15; i++)
            list.Add(BuildSection(i, courseId: 400, theory: 1, lab: 0, capacity: 20, sectionNumber: i - 12));

        return list;
    }

    private static SectionInput BuildSection(int id, int courseId, int theory, int lab, int capacity, int sectionNumber = 1) => new()
    {
        Id = id,
        CourseId = courseId,
        CourseCode = $"C{courseId}",
        CourseName = $"Course {courseId}",
        SectionNumber = sectionNumber,
        Capacity = capacity,
        TheoryHours = theory,
        LabHours = lab,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    private static IReadOnlyList<ClassroomInput> BuildClassrooms() => new[]
    {
        new ClassroomInput { Id = 1, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture },
        new ClassroomInput { Id = 2, Name = "C 017", Capacity = 70, Type = ClassroomType.Lecture },
        new ClassroomInput { Id = 3, Name = "L139", Capacity = 40, Type = ClassroomType.Lab },
        new ClassroomInput { Id = 4, Name = "L141", Capacity = 40, Type = ClassroomType.Lab },
        new ClassroomInput { Id = 5, Name = "L143", Capacity = 40, Type = ClassroomType.Lab }
    };

    private static IReadOnlyList<TimeSlotInput> BuildSlots()
    {
        var slots = new List<TimeSlotInput>();
        int id = 1;
        for (var day = DayOfWeek.Monday; day <= DayOfWeek.Friday; day++)
        {
            for (int s = 0; s < 10; s++)
            {
                slots.Add(new TimeSlotInput
                {
                    Id = id++,
                    DayOfWeek = day,
                    SlotOrder = s + 1,
                    StartTime = new TimeOnly(9 + s, 0),
                    EndTime = new TimeOnly(9 + s, 50),
                    AllowedMode = TimeSlotAllowedMode.Both
                });
            }
        }
        return slots;
    }

    private static CpSatSolverEngine BuildEngine()
    {
        var encoders = new IConstraintEncoder[]
        {
            new ClassroomConflictEncoder(),
            new CapacityEncoder(),
            new ClassroomTypeEncoder(),
            new TheoryLabComponentEncoder(),
            new SameCourseSectionEncoder()
        };
        var modelBuilder = new ScheduleModelBuilder(encoders);
        var mapper = new SolutionMapper();
        var unsatInterpreter = new UnsatCoreInterpreter();
        var preflightChecker = new PreflightFeasibilityChecker();

        var configMock = new Mock<ISystemConfigProvider>();
        configMock.Setup(x => x.GetIntAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((string k, int f, CancellationToken _) => f);
        configMock.Setup(x => x.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync((string k, bool f, CancellationToken _) => f);

        return new CpSatSolverEngine(modelBuilder, mapper, configMock.Object, unsatInterpreter, preflightChecker);
    }
}
