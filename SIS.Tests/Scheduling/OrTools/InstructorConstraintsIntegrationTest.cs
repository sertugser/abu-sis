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
/// Plan 5 Task 9 — Instructor constraints end-to-end test.
/// 2 instructors × 2 sections (3h theory each) → all of H1+H2+H-WL+H-DL+H-DO must hold:
///   - H2: per-instructor TimeSlotIds unique (no double-booking)
///   - H-WL: ≤ 30 weekly hours per instructor
///   - H-DL: ≤ 9 hours per day per instructor (implicit — 3h sections)
///   - H-DO: ≤ 4 distinct workdays per instructor (5-workday grid)
///   - H1: classroom no-conflict
/// </summary>
public class InstructorConstraintsIntegrationTest
{
    [Fact]
    public async Task SolveAsync_TwoInstructorsFourSections_EnforcesAllInstructorConstraints()
    {
        // Arrange
        // 2 instructors × 2 sections each = 4 sections × 3h theory = 6h per instructor.
        // Both well within 30h DefaultInstructorMaxWeeklyHours (H-WL).
        // 5-workday × 4 slots/day = 20 slots → H-DO requires ≤ 4 distinct days per instructor.
        // 2 classrooms → H1 ensures no double-booking.
        // H2 ensures same-instructor sections at distinct times.
        var sections = new[]
        {
            BuildSection(1, instructorId: 5, theory: 3, lab: 0),
            BuildSection(2, instructorId: 5, theory: 3, lab: 0),
            BuildSection(3, instructorId: 6, theory: 3, lab: 0),
            BuildSection(4, instructorId: 6, theory: 3, lab: 0)
        };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "C1", Capacity = 50, Type = ClassroomType.Lecture },
            new ClassroomInput { Id = 2, Name = "C2", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var slots = BuildSlots(20);

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
                ["H2"] = true,
                ["H-WL"] = true,
                ["H-DL"] = true,
                ["H-DO"] = true
            },
            DefaultInstructorMaxWeeklyHours = 30
        };

        var engine = BuildEngine();

        // Act
        var result = await engine.SolveAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().BeOneOf(
            new[] { SolverStatus.Optimal, SolverStatus.Feasible },
            because: "all 4 sections should fit within H1+H2+H-WL+H-DL+H-DO constraints");
        result.Assignments.Should().HaveCount(4, because: "all 4 sections should be scheduled");

        // Per-instructor verification
        foreach (var instructorId in new[] { 5, 6 })
        {
            var instructorSectionIds = sections
                .Where(s => s.InstructorId == instructorId)
                .Select(s => s.Id)
                .ToHashSet();
            var instructorAssignments = result.Assignments
                .Where(a => instructorSectionIds.Contains(a.SectionId))
                .ToList();
            instructorAssignments.Should().HaveCount(2,
                because: $"instructor {instructorId} has 2 sections, both scheduled");

            // H2: per-instructor TimeSlotId uniqueness (no double-booking).
            var allSlotIds = instructorAssignments
                .SelectMany(a => a.Slots.Select(s => s.TimeSlotId))
                .ToList();
            allSlotIds.Should().OnlyHaveUniqueItems(
                because: $"H2: instructor {instructorId} cannot teach 2 sections at same time");

            // H-WL: total slot count = total weekly hours; ≤ 30.
            var totalHours = instructorAssignments.Sum(a => a.Slots.Count);
            totalHours.Should().BeLessThanOrEqualTo(30, because: "H-WL: weekly load cap");
            totalHours.Should().Be(6, because: "2 sections × 3h theory = 6 total slots per instructor");

            // H-DO: at most 4 distinct workdays used (5 workdays - 1).
            var daysUsed = instructorAssignments
                .SelectMany(a => a.Slots.Select(s => slots.First(ts => ts.Id == s.TimeSlotId).DayOfWeek))
                .Distinct()
                .Count();
            daysUsed.Should().BeLessThanOrEqualTo(4,
                because: "H-DO: at least 1 day off in 5-workday grid");
        }
    }

    // ─────────────── Helpers ───────────────

    private static SectionInput BuildSection(int id, int instructorId, int theory, int lab) => new()
    {
        Id = id,
        CourseId = 100 + id,
        CourseCode = $"X{id}",
        CourseName = $"X{id}",
        SectionNumber = 1,
        Capacity = 30,
        TheoryHours = theory,
        LabHours = lab,
        InstructorId = instructorId,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    private static IReadOnlyList<TimeSlotInput> BuildSlots(int count)
    {
        var slots = new List<TimeSlotInput>(count);
        int id = 1;
        // Mon-Fri × 4 slots/day = 20
        for (var day = DayOfWeek.Monday; day <= DayOfWeek.Friday && id <= count; day++)
        {
            for (int s = 0; s < 4 && id <= count; s++)
            {
                var startHour = 9 + s;
                slots.Add(new TimeSlotInput
                {
                    Id = id++,
                    DayOfWeek = day,
                    SlotOrder = s + 1,
                    StartTime = new TimeOnly(startHour, 0),
                    EndTime = new TimeOnly(startHour, 50),
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
            new InstructorConflictEncoder(),
            new WeeklyLoadEncoder(),
            new DailyLoadEncoder(maxDailyHours: 9),
            new NoDayOffEncoder()
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
