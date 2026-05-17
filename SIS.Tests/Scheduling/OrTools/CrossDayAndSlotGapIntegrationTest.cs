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

public class CrossDayAndSlotGapIntegrationTest
{
    [Fact]
    public async Task SolveAsync_CrossDayBoundary_SchedulesOnlyValidStarts()
    {
        // 3-slot grid: Mon-09:00, Mon-10:00, Tue-09:00. Theory=2 → only slot 0 valid.
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "X", CourseName = "X",
            SectionNumber = 1, Capacity = 30,
            TheoryHours = 2, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace
        };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "C", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var slots = new[]
        {
            new TimeSlotInput { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2,
                StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 3, DayOfWeek = DayOfWeek.Tuesday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both }
        };
        var request = new SolverRequest
        {
            TermId = 1, FacultyId = 1,
            Sections = new[] { section }, Classrooms = classrooms, TimeSlots = slots,
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true }
        };

        var engine = BuildEngine();
        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().BeOneOf(new[] { SolverStatus.Optimal, SolverStatus.Feasible });
        result.Assignments.Should().HaveCount(1);
        var assigned = result.Assignments[0].Slots;
        assigned.Should().HaveCount(2,
            because: "2-saatlik theory 2 slot kapsar");
        // Both assigned slots must be on Monday (slot 0 + slot 1).
        var assignedSlotIds = assigned.Select(s => s.TimeSlotId).OrderBy(x => x).ToList();
        assignedSlotIds.Should().BeEquivalentTo(new[] { 1, 2 },
            because: "only Mon-09:00 + Mon-10:00 is the valid 2-slot block; Mon→Tue cross-day rejected");
    }

    [Fact]
    public async Task SolveAsync_LunchGapBlocksInvalidBlock_PreflightInfeasible()
    {
        // 3-slot grid: Mon-09:00, Mon-10:00, Mon-12:30 (gap 100dk between slot 1 and 2).
        // Theory=3 → no valid block → preflight infeasible.
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "X", CourseName = "X",
            SectionNumber = 1, Capacity = 30,
            TheoryHours = 3, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace
        };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "C", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var slots = new[]
        {
            new TimeSlotInput { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2,
                StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 3, DayOfWeek = DayOfWeek.Monday, SlotOrder = 3,
                StartTime = new TimeOnly(12, 30), EndTime = new TimeOnly(13, 20),
                AllowedMode = TimeSlotAllowedMode.Both }
        };
        var request = new SolverRequest
        {
            TermId = 1, FacultyId = 1,
            Sections = new[] { section }, Classrooms = classrooms, TimeSlots = slots,
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true }
        };

        var engine = BuildEngine();
        var result = await engine.SolveAsync(request, CancellationToken.None);

        // Section drops at preflight (all sections infeasible → engine returns early).
        result.Status.Should().Be(SolverStatus.Infeasible,
            because: "all sections fail preflight — engine takes the all-infeasible path, not solver Optimal/Feasible");
        result.Assignments.Should().BeEmpty(
            because: "3-saatlik theory için Mon grid'inde valid block yok (lunch 100dk gap)");
        result.Failures.Should().ContainSingle(
            because: "exactly one section was rejected; future preflight extensions should not silently break this assertion")
            .Which.Reason.Should().Contain("3 saatlik",
                because: "preflight rejection reason mentions duration");
    }

    private static CpSatSolverEngine BuildEngine()
    {
        var encoders = new IConstraintEncoder[]
        {
            new ClassroomConflictEncoder()
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
