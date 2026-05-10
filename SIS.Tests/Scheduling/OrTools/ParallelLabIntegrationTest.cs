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
/// Plan 4 Task 8 — Parallel lab integration test.
/// 80-capacity section + 2x40-cap lab rooms (LabGroupCount=2) → v2 must produce parallel lab split:
/// lecture (3 slots × 1 room = 3 entries) + lab (2 slots × 2 parallel rooms = 4 entries).
/// </summary>
public class ParallelLabIntegrationTest
{
    [Fact]
    public async Task SolveAsync_80CapSection_TwoLab40Rooms_ProducesParallelSplit()
    {
        // Arrange
        var section = new SectionInput
        {
            Id = 1,
            CourseId = 100,
            CourseCode = "BM101",
            CourseName = "Programming",
            SectionNumber = 1,
            Capacity = 80,
            TheoryHours = 3,
            LabHours = 2,
            TeachingMethod = TeachingMethod.FaceToFace,
            LabGroupCount = 2
        };

        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "AMFI", Capacity = 110, Type = ClassroomType.Amphitheater },
            new ClassroomInput { Id = 2, Name = "L1", Capacity = 40, Type = ClassroomType.Lab },
            new ClassroomInput { Id = 3, Name = "L2", Capacity = 40, Type = ClassroomType.Lab }
        };

        var slots = BuildSlots(20);

        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[] { section },
            Classrooms = classrooms,
            TimeSlots = slots,
            ConstraintFlags = new Dictionary<string, bool>
            {
                ["H1"] = true,
                ["H3"] = true,
                ["H4"] = true,
                ["H6"] = true
            }
        };

        var engine = BuildEngine();

        // Act
        var result = await engine.SolveAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().BeOneOf(
            new[] { SolverStatus.Optimal, SolverStatus.Feasible },
            because: "80-cap section with 2x40 lab rooms must be feasible via parallel split");

        result.Assignments.Should().HaveCount(1);

        var assignment = result.Assignments[0];
        var lectureSlots = assignment.Slots.Where(s => s.ComponentType == ComponentType.Lecture).ToList();
        var labSlots = assignment.Slots.Where(s => s.ComponentType == ComponentType.Lab).ToList();

        // Lecture: 3 slots × 1 room = 3 entries
        lectureSlots.Should().HaveCount(3, because: "TheoryHours=3 with single lecture room");

        // Lab: 2 slots × 2 parallel rooms = 4 entries
        labSlots.Should().HaveCount(4, because: "LabHours=2 × LabGroupCount=2 parallel rooms");

        // Both lab rooms (2 and 3) used
        var labClassroomIds = labSlots.Select(s => s.ClassroomId).Distinct().ToList();
        labClassroomIds.Should().HaveCount(2, because: "two distinct lab rooms must be assigned");
        labClassroomIds.Should().Contain(new[] { 2, 3 });

        // Same time slots for both rooms (parallel — both groups run simultaneously)
        var slotsForRoom2 = labSlots
            .Where(s => s.ClassroomId == 2)
            .Select(s => s.TimeSlotId)
            .OrderBy(x => x)
            .ToList();
        var slotsForRoom3 = labSlots
            .Where(s => s.ClassroomId == 3)
            .Select(s => s.TimeSlotId)
            .OrderBy(x => x)
            .ToList();
        slotsForRoom2.Should().BeEquivalentTo(slotsForRoom3,
            because: "parallel lab groups must run on identical time slots");
    }

    // ─────────────── Helpers ───────────────

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
            new CapacityEncoder(),
            new ClassroomTypeEncoder(),
            new TheoryLabComponentEncoder()
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
