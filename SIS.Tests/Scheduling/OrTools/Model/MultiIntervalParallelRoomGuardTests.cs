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
using Xunit;

namespace SIS.Tests.Scheduling.OrTools.Model;

/// <summary>
/// Plan 9 Issue 2 — Multi-interval × parallel-rooms graceful skip.
///
/// Plan 7b Task 7/8 introduced two NotSupportedException guards in DecisionVariables /
/// ClassroomConflictEncoder for the combination
///   LabHours &gt; MaxLabBlockHours (multi-interval split) × LabGroupCount &gt; 1 (parallel rooms).
/// Real DB Term 6 contained at least one such section (BM 102) which made the entire solve
/// crash with HTTP 500. Plan 9 Issue 2 replaces those throws with a graceful skip:
/// the section ends up in <see cref="SolverResult.Failures"/> with a mitigation message,
/// and the rest of the schedule still solves.
/// </summary>
public class MultiIntervalParallelRoomGuardTests
{
    [Fact]
    public async Task MultiBlock_ParallelRoom_Section_AppearsInFailures_NotException()
    {
        // Arrange — a single section that triggers the multi-interval × parallel-rooms combo:
        //   LabHours=4, MaxLabBlockHours=2 → 2-block split.
        //   Capacity=60, lab capacity=30 → LabGroupCount=2 (parallel rooms).
        var section = new SectionInput
        {
            Id = 1889,
            CourseId = 102,
            CourseCode = "BM 102",
            CourseName = "Programlama",
            SectionNumber = 1,
            Capacity = 60,
            TheoryHours = 0,
            LabHours = 4,
            LabGroupCount = 2,
            TeachingMethod = TeachingMethod.FaceToFace
        };
        var rooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "L1", Capacity = 30, Type = ClassroomType.Lab },
            new ClassroomInput { Id = 2, Name = "L2", Capacity = 30, Type = ClassroomType.Lab }
        };
        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[] { section },
            Classrooms = rooms,
            TimeSlots = BuildFiveDayFourSlotGrid(),
            MaxLabBlockHours = 2,
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true, ["H4"] = true }
        };

        var engine = BuildEngine(maxBlockHours: 2);

        // Act
        var act = async () => await engine.SolveAsync(request, CancellationToken.None);

        // Assert — no exception, instead a Failure entry with the mitigation hint.
        var result = await act.Should().NotThrowAsync();
        result.Subject.Failures.Should().Contain(f => f.SectionId == 1889);
        var failure = result.Subject.Failures.First(f => f.SectionId == 1889);
        failure.Reason.Should().Contain("multi-interval lab");
    }

    private static IReadOnlyList<TimeSlotInput> BuildFiveDayFourSlotGrid()
    {
        var slots = new List<TimeSlotInput>();
        for (int d = 1; d <= 5; d++)
            for (int s = 0; s < 4; s++)
                slots.Add(new TimeSlotInput
                {
                    Id = (d - 1) * 4 + s + 1,
                    DayOfWeek = (DayOfWeek)d,
                    SlotOrder = s + 1,
                    StartTime = new TimeOnly(9 + s, 0),
                    EndTime = new TimeOnly(9 + s, 50),
                    AllowedMode = TimeSlotAllowedMode.Both
                });
        return slots;
    }

    private static CpSatSolverEngine BuildEngine(int maxBlockHours)
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
            .ReturnsAsync((string k, int f, CancellationToken _) => k switch
            {
                "Schedule.MaxLabBlockHours" => maxBlockHours,
                _ => f
            });
        configMock.Setup(x => x.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, bool f, CancellationToken _) => f);

        return new CpSatSolverEngine(modelBuilder, mapper, configMock.Object, unsatInterpreter, preflightChecker);
    }
}
