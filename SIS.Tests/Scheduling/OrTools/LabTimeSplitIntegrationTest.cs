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
/// Plan 7b Task 8 — End-to-end integration tests for multi-interval lab time-split.
/// Verifies LabHours > MaxLabBlockHours splits the lab into N intervals on different days,
/// and that LabHours ≤ MaxLabBlockHours preserves the existing single-interval behavior.
/// </summary>
public class LabTimeSplitIntegrationTest
{
    [Fact]
    public async Task SolveAsync_LabHours4_MaxBlock2_PlacesTwoBlocksOnDifferentDays()
    {
        var section = new SectionInput
        {
            Id = 1,
            CourseId = 100,
            CourseCode = "PHYS201",
            CourseName = "Physics Lab",
            SectionNumber = 1,
            Capacity = 24,
            TheoryHours = 0,
            LabHours = 4,
            TeachingMethod = TeachingMethod.FaceToFace
        };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "L1", Capacity = 30, Type = ClassroomType.Lab }
        };
        var slots = BuildFiveDayFourSlotGrid();

        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[] { section },
            Classrooms = classrooms,
            TimeSlots = slots,
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true, ["H4"] = true },
            MaxLabBlockHours = 2
        };

        var engine = BuildEngine(maxBlockHours: 2);
        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().BeOneOf(new[] { SolverStatus.Optimal, SolverStatus.Feasible });
        result.Assignments.Should().HaveCount(1);

        var assignedSlots = result.Assignments[0].Slots;
        assignedSlots.Should().HaveCount(4,
            because: "2 lab blocks × 2 saat = 4 slot");

        var distinctDays = assignedSlots
            .Select(s => slots.First(t => t.Id == s.TimeSlotId).DayOfWeek)
            .Distinct()
            .ToList();
        distinctDays.Should().HaveCount(2,
            because: "4h lab split into 2 different-day blocks");
    }

    [Fact]
    public async Task SolveAsync_LabHours5_MaxBlock2_PlacesThreeBlocks()
    {
        var section = new SectionInput
        {
            Id = 1,
            CourseId = 100,
            CourseCode = "CHEM301",
            CourseName = "ChemLab",
            SectionNumber = 1,
            Capacity = 20,
            TheoryHours = 0,
            LabHours = 5,
            TeachingMethod = TeachingMethod.FaceToFace
        };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "CL", Capacity = 30, Type = ClassroomType.Lab }
        };
        var slotsRef = BuildFiveDayFourSlotGrid();
        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[] { section },
            Classrooms = classrooms,
            TimeSlots = slotsRef,
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true, ["H4"] = true },
            MaxLabBlockHours = 2
        };

        var engine = BuildEngine(maxBlockHours: 2);
        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().BeOneOf(new[] { SolverStatus.Optimal, SolverStatus.Feasible });
        var assignedSlots = result.Assignments[0].Slots;
        assignedSlots.Should().HaveCount(5,
            because: "5h lab → blocks [2,2,1] → 5 slots total");

        var distinctDays = assignedSlots
            .Select(s => slotsRef.First(t => t.Id == s.TimeSlotId).DayOfWeek)
            .Distinct()
            .Count();
        distinctDays.Should().Be(3,
            because: "3 lab blocks → 3 different days");
    }

    [Fact]
    public async Task SolveAsync_LabHours2_NoSplit_SingleInterval()
    {
        // Backward-compat: LabHours ≤ MaxLabBlockHours → tek interval, mevcut Plan 4-7 davranışı.
        var section = new SectionInput
        {
            Id = 1,
            CourseId = 100,
            CourseCode = "X",
            CourseName = "X",
            SectionNumber = 1,
            Capacity = 20,
            TheoryHours = 0,
            LabHours = 2,
            TeachingMethod = TeachingMethod.FaceToFace
        };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "L", Capacity = 30, Type = ClassroomType.Lab }
        };
        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[] { section },
            Classrooms = classrooms,
            TimeSlots = BuildFiveDayFourSlotGrid(),
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true, ["H4"] = true },
            MaxLabBlockHours = 2
        };

        var engine = BuildEngine(maxBlockHours: 2);
        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().BeOneOf(new[] { SolverStatus.Optimal, SolverStatus.Feasible });
        result.Assignments[0].Slots.Should().HaveCount(2,
            because: "2h lab tek interval");
    }

    private static IReadOnlyList<TimeSlotInput> BuildFiveDayFourSlotGrid()
    {
        var slots = new List<TimeSlotInput>();
        for (int d = 1; d <= 5; d++)
        {
            for (int s = 0; s < 4; s++)
            {
                slots.Add(new TimeSlotInput
                {
                    Id = (d - 1) * 4 + s + 1,
                    DayOfWeek = (DayOfWeek)d,
                    SlotOrder = s + 1,
                    StartTime = new TimeOnly(9 + s, 0),
                    EndTime = new TimeOnly(9 + s, 50),
                    AllowedMode = TimeSlotAllowedMode.Both
                });
            }
        }
        return slots;
    }

    private static CpSatSolverEngine BuildEngine(int maxBlockHours)
    {
        var encoders = new IConstraintEncoder[]
        {
            new ClassroomConflictEncoder(),
            new ClassroomTypeEncoder()
        };
        var modelBuilder = new ScheduleModelBuilder(encoders);
        var mapper = new SolutionMapper();
        var unsatInterpreter = new UnsatCoreInterpreter();
        var preflightChecker = new PreflightFeasibilityChecker();

        var configMock = new Mock<ISystemConfigProvider>();
        // Single wildcard setup: branches on key, returns fallback for any other key.
        // Matches the pattern used in ClassroomTypeAndAmphiIntegrationTest.
        configMock.Setup(x => x.GetIntAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string k, int f, CancellationToken _) =>
                k == "Schedule.MaxLabBlockHours" ? maxBlockHours : f);
        configMock.Setup(x => x.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string k, bool f, CancellationToken _) => f);

        return new CpSatSolverEngine(modelBuilder, mapper, configMock.Object, unsatInterpreter, preflightChecker);
    }
}
