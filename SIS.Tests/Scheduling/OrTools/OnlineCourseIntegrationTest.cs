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

public class OnlineCourseIntegrationTest
{
    [Fact]
    public async Task SolveAsync_OnlineSection_AssignmentHasZeroClassroomId()
    {
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "ONLINE101", CourseName = "Distance Learning",
            SectionNumber = 1, Capacity = 50,
            TheoryHours = 2, LabHours = 0,
            TeachingMethod = TeachingMethod.Online
        };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "L1", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var request = new SolverRequest
        {
            TermId = 1, FacultyId = 1,
            Sections = new[] { section }, Classrooms = classrooms,
            TimeSlots = BuildSlots(),
            ConstraintFlags = new Dictionary<string, bool>
                { ["H1"] = true, ["H4"] = true, ["H17"] = true }
        };

        var engine = BuildEngine();
        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().BeOneOf(new[] { SolverStatus.Optimal, SolverStatus.Feasible });
        result.Assignments.Should().HaveCount(1);
        result.Assignments[0].Slots.Should().HaveCount(2,
            because: "TheoryHours=2 → 2 slot");
        result.Assignments[0].Slots.Should().AllSatisfy(s => s.ClassroomId.Should().Be(0,
            because: "Online section room IntVar oluşmadı → mapping ClassroomId=0 sentinel"));
    }

    [Fact]
    public async Task SolveAsync_OnlineSectionAndFaceToFaceMixed_BothScheduled()
    {
        // Online and FaceToFace sections may share slots: H1 (classroom conflict) is keyed on the
        // room IntVar, and Online sections allocate no room IntVar — so the two sections
        // never compete for a resource and the solver may place them on the same slot range.
        // If a future encoder (cohort/instructor conflict) is added to BuildEngine() this
        // assumption needs revisiting.
        var sections = new[]
        {
            new SectionInput
            {
                Id = 1, CourseId = 100, CourseCode = "ONLINE101", CourseName = "Online",
                SectionNumber = 1, Capacity = 50,
                TheoryHours = 2, LabHours = 0,
                TeachingMethod = TeachingMethod.Online
            },
            new SectionInput
            {
                Id = 2, CourseId = 200, CourseCode = "F2F101", CourseName = "InPerson",
                SectionNumber = 1, Capacity = 30,
                TheoryHours = 2, LabHours = 0,
                TeachingMethod = TeachingMethod.FaceToFace
            }
        };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "L1", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var request = new SolverRequest
        {
            TermId = 1, FacultyId = 1,
            Sections = sections, Classrooms = classrooms,
            TimeSlots = BuildSlots(),
            ConstraintFlags = new Dictionary<string, bool>
                { ["H1"] = true, ["H4"] = true, ["H17"] = true }
        };

        var engine = BuildEngine();
        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().BeOneOf(new[] { SolverStatus.Optimal, SolverStatus.Feasible });
        result.Assignments.Should().HaveCount(2);

        var online = result.Assignments.Single(a => a.SectionId == 1);
        online.Slots.Should().AllSatisfy(s => s.ClassroomId.Should().Be(0));

        var f2f = result.Assignments.Single(a => a.SectionId == 2);
        f2f.Slots.Should().AllSatisfy(s => s.ClassroomId.Should().Be(1));
    }

    [Fact]
    public async Task SolveAsync_OnlineSectionWithoutClassrooms_StillScheduled()
    {
        // Online section schedules even when the faculty has no physical classroom assignment.
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "ONLINE", CourseName = "X",
            SectionNumber = 1, Capacity = 50,
            TheoryHours = 2, LabHours = 0,
            TeachingMethod = TeachingMethod.Online
        };
        var request = new SolverRequest
        {
            TermId = 1, FacultyId = 1,
            Sections = new[] { section },
            Classrooms = Array.Empty<ClassroomInput>(),
            TimeSlots = BuildSlots(),
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true }
        };

        var engine = BuildEngine();
        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().BeOneOf(new[] { SolverStatus.Optimal, SolverStatus.Feasible });
        result.Assignments.Should().HaveCount(1,
            because: "Online section schedules without consuming the dummy classroom (no room IntVar)");
    }

    private static IReadOnlyList<TimeSlotInput> BuildSlots() => new[]
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

    private static CpSatSolverEngine BuildEngine()
    {
        var encoders = new IConstraintEncoder[]
        {
            new ClassroomConflictEncoder(),
            new ClassroomTypeEncoder(),
            new FaceToFaceSlotEncoder()
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
