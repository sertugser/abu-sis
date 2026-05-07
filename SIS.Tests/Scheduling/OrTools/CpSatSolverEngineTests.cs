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

public class CpSatSolverEngineTests
{
    private static SectionInput Section(int id, int courseId, int theory, int lab) => new()
    {
        Id = id,
        CourseId = courseId,
        CourseCode = "X",
        CourseName = "X",
        SectionNumber = id,
        Capacity = 40,
        TheoryHours = theory,
        LabHours = lab,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    private static (CpSatSolverEngine engine, Mock<ISystemConfigProvider> configMock) BuildEngineWithConfig(
        int timeLimitSeconds = 30,
        int numWorkers = 8,
        int randomSeed = 42,
        bool logToConsole = false)
    {
        var configMock = new Mock<ISystemConfigProvider>();
        configMock.Setup(x => x.GetIntAsync("Schedule.OrTools.TimeLimitSeconds", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(timeLimitSeconds);
        configMock.Setup(x => x.GetIntAsync("Schedule.OrTools.NumWorkers", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(numWorkers);
        configMock.Setup(x => x.GetIntAsync("Schedule.OrTools.RandomSeed", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(randomSeed);
        configMock.Setup(x => x.GetBoolAsync("Schedule.OrTools.LogToConsole", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(logToConsole);

        var encoders = new IConstraintEncoder[] { new ClassroomConflictEncoder() };
        var modelBuilder = new ScheduleModelBuilder(encoders);
        var mapper = new SolutionMapper();
        var unsatInterpreter = new UnsatCoreInterpreter();
        var engine = new CpSatSolverEngine(modelBuilder, mapper, configMock.Object, unsatInterpreter);
        return (engine, configMock);
    }

    // Backward-compat for existing tests that don't care about config:
    private static CpSatSolverEngine BuildEngine() => BuildEngineWithConfig().engine;

    [Fact]
    public async Task SolveAsync_TwoNonOverlappingSections_ReturnsOptimalOrFeasible()
    {
        var engine = BuildEngine();
        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[] { Section(1, 100, 3, 0), Section(2, 200, 3, 0) },
            Classrooms = new List<ClassroomInput>
            {
                new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
            },
            TimeSlots = Enumerable.Range(1, 8)
                .Select(i => new TimeSlotInput
                {
                    Id = i,
                    DayOfWeek = DayOfWeek.Monday,
                    SlotOrder = i,
                    StartTime = new TimeOnly(8 + i, 0),
                    EndTime = new TimeOnly(8 + i, 50),
                    AllowedMode = TimeSlotAllowedMode.Both
                })
                .ToList(),
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true }
        };

        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().BeOneOf(SolverStatus.Optimal, SolverStatus.Feasible);
        result.Diagnostics.EngineVersion.Should().Be("v2-ortools");
    }

    [Fact]
    public async Task SolveAsync_PreCanceledToken_ThrowsOperationCanceledException()
    {
        var engine = BuildEngine();
        using var cts = new CancellationTokenSource();
        cts.Cancel();   // cancel BEFORE SolveAsync is called
        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[] { Section(1, 100, 3, 0) },
            Classrooms = new List<ClassroomInput>
            {
                new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
            },
            TimeSlots = new List<TimeSlotInput>
            {
                new() { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                    StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                    AllowedMode = TimeSlotAllowedMode.Both }
            }
        };

        var act = async () => await engine.SolveAsync(request, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SolveAsync_ReadsSolverParametersFromConfigProvider()
    {
        var (engine, configMock) = BuildEngineWithConfig(
            timeLimitSeconds: 60,
            numWorkers: 4,
            randomSeed: 123);

        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[] { Section(1, 100, 3, 0) },
            Classrooms = new List<ClassroomInput>
            {
                new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
            },
            TimeSlots = new List<TimeSlotInput>
            {
                new() { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                    StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                    AllowedMode = TimeSlotAllowedMode.Both }
            },
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true }
        };

        await engine.SolveAsync(request, CancellationToken.None);

        configMock.Verify(x => x.GetIntAsync("Schedule.OrTools.TimeLimitSeconds", It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        configMock.Verify(x => x.GetIntAsync("Schedule.OrTools.NumWorkers", It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        configMock.Verify(x => x.GetIntAsync("Schedule.OrTools.RandomSeed", It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        configMock.Verify(x => x.GetBoolAsync("Schedule.OrTools.LogToConsole", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
