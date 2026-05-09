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

public class ClassroomTypeAndAmphiIntegrationTest
{
    [Fact]
    public async Task SolveAsync_LargeCapacity_AssignsToAmphitheater()
    {
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "CENG101", CourseName = "Algo",
            SectionNumber = 1, Capacity = 120,
            TheoryHours = 2, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace
        };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "L1", Capacity = 200, Type = ClassroomType.Lecture },
            new ClassroomInput { Id = 2, Name = "A1", Capacity = 200, Type = ClassroomType.Amphitheater }
        };
        var slots = BuildMonOnly4Slots();
        var request = new SolverRequest
        {
            TermId = 1, FacultyId = 1,
            Sections = new[] { section }, Classrooms = classrooms, TimeSlots = slots,
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true, ["H4"] = true },
            AmphiThreshold = 100
        };

        var engine = BuildEngine(amphiThreshold: 100);
        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().BeOneOf(new[] { SolverStatus.Optimal, SolverStatus.Feasible });
        result.Assignments.Should().HaveCount(1);
        var classroomIdsUsed = result.Assignments[0].Slots.Select(s => s.ClassroomId).Distinct();
        classroomIdsUsed.Should().BeEquivalentTo(new[] { 2 },
            because: "Capacity 120 >= AmphiThreshold 100 -> only Amphitheater (id=2)");
    }

    [Fact]
    public async Task SolveAsync_RequiredTheoryClassroomTypeComputer_AssignsToComputerRoom()
    {
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "CENG310", CourseName = "Programming",
            SectionNumber = 1, Capacity = 30,
            TheoryHours = 2, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace,
            RequiredTheoryClassroomType = (int)ClassroomType.Computer
        };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "L1", Capacity = 50, Type = ClassroomType.Lecture },
            new ClassroomInput { Id = 2, Name = "PC", Capacity = 40, Type = ClassroomType.Computer }
        };
        var request = new SolverRequest
        {
            TermId = 1, FacultyId = 1,
            Sections = new[] { section }, Classrooms = classrooms,
            TimeSlots = BuildMonOnly4Slots(),
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true, ["H4"] = true },
            AmphiThreshold = 100
        };

        var engine = BuildEngine(amphiThreshold: 100);
        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().BeOneOf(new[] { SolverStatus.Optimal, SolverStatus.Feasible });
        result.Assignments[0].Slots.Select(s => s.ClassroomId).Distinct()
            .Should().BeEquivalentTo(new[] { 2 },
                because: "RequiredTheoryClassroomType=Computer -> only PC room (id=2)");
    }

    [Fact]
    public async Task SolveAsync_SmallCapacityNoRequirement_AnyLectureRoomAccepted()
    {
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "CENG101", CourseName = "Algo",
            SectionNumber = 1, Capacity = 30,
            TheoryHours = 2, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace
        };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "L1", Capacity = 50, Type = ClassroomType.Lecture },
            new ClassroomInput { Id = 2, Name = "A1", Capacity = 200, Type = ClassroomType.Amphitheater }
        };
        var request = new SolverRequest
        {
            TermId = 1, FacultyId = 1,
            Sections = new[] { section }, Classrooms = classrooms,
            TimeSlots = BuildMonOnly4Slots(),
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true, ["H4"] = true },
            AmphiThreshold = 100
        };

        var engine = BuildEngine(amphiThreshold: 100);
        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().BeOneOf(new[] { SolverStatus.Optimal, SolverStatus.Feasible });
        result.Assignments.Should().HaveCount(1,
            because: "no theory type constraint + capacity below threshold -> any lecture-friendly room OK");
    }

    private static IReadOnlyList<TimeSlotInput> BuildMonOnly4Slots() => new[]
    {
        new TimeSlotInput { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
            StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
            AllowedMode = TimeSlotAllowedMode.Both },
        new TimeSlotInput { Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2,
            StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 50),
            AllowedMode = TimeSlotAllowedMode.Both },
        new TimeSlotInput { Id = 3, DayOfWeek = DayOfWeek.Monday, SlotOrder = 3,
            StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(11, 50),
            AllowedMode = TimeSlotAllowedMode.Both },
        new TimeSlotInput { Id = 4, DayOfWeek = DayOfWeek.Monday, SlotOrder = 4,
            StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(12, 50),
            AllowedMode = TimeSlotAllowedMode.Both }
    };

    private static CpSatSolverEngine BuildEngine(int amphiThreshold)
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
        // Single wildcard setup serves both AmphiThreshold and any other GetIntAsync caller.
        // (Moq last-registered-wins would shadow a separate specific-key setup anyway.)
        configMock.Setup(x => x.GetIntAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string k, int f, CancellationToken _) =>
                k == "Classroom.AmphiThreshold" ? amphiThreshold : f);
        // Plan 9 Issue 1 — opt in to AmphiThreshold rule so these tests preserve original intent.
        // Plan 9 Issue 6 — opt in to ClassroomTypeRequirement rule (RequiredTheoryClassroomType test).
        configMock.Setup(x => x.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string k, bool f, CancellationToken _) => k switch
            {
                "Schedule.AmphiThresholdEnabled" => true,
                "Schedule.ClassroomTypeRequirementEnabled" => true,
                _ => f
            });

        return new CpSatSolverEngine(modelBuilder, mapper, configMock.Object, unsatInterpreter, preflightChecker);
    }
}
