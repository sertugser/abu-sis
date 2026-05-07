using FluentAssertions;
using Moq;
using SIS.Application.Common.Services;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Mapping;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools;

/// <summary>
/// Plan 3 Task 12 — Soft constraints integration test.
/// Builds CpSatSolverEngine with all 7 soft encoders + a minimal hard encoder set
/// and asserts the engine still produces a feasible result. Soft encoders are diagnostic
/// stubs in Plan 3, so their presence must not break feasibility.
/// </summary>
public class SoftConstraintsIntegrationTest
{
    [Fact]
    public async Task SolveAsync_AllSoftEncodersRegistered_ProducesValidResult()
    {
        // Arrange: trivial feasible request — single section, single classroom, single slot.
        var sections = new[]
        {
            new SectionInput
            {
                Id = 1, CourseId = 100, CourseCode = "X", CourseName = "X",
                SectionNumber = 1, Capacity = 40, TheoryHours = 1, LabHours = 0,
                TeachingMethod = TeachingMethod.FaceToFace
            }
        };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var timeSlots = new[]
        {
            new TimeSlotInput
            {
                Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both
            }
        };
        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = sections,
            Classrooms = classrooms,
            TimeSlots = timeSlots,
            ConstraintFlags = new Dictionary<string, bool>
            {
                ["H1"] = true, ["H8"] = true,
                ["S1"] = true, ["S3"] = true, ["S4"] = true,
                ["S5_S7"] = true, ["S9"] = true, ["S12"] = true, ["S20"] = true
            }
        };

        var encoders = new IConstraintEncoder[]
        {
            new ClassroomConflictEncoder(),  // H1
            new SameCourseSectionEncoder(),  // H8
            new OccupancyEncoder(),
            new SlotCongestionEncoder(),
            new CoEnrollmentEncoder(),
            new StudentGapEncoder(),
            new EarlyLateEncoder(),
            new WeekdayBalanceEncoder(),
            new AdjacentYearEncoder()
        };

        var configMock = new Mock<ISystemConfigProvider>();
        configMock
            .Setup(x => x.GetIntAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string k, int f, CancellationToken _) => f);
        configMock
            .Setup(x => x.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string k, bool f, CancellationToken _) => f);

        var modelBuilder = new ScheduleModelBuilder(encoders);
        var mapper = new SolutionMapper();
        var unsatInterpreter = new UnsatCoreInterpreter();
        var preflightChecker = new PreflightFeasibilityChecker();
        var engine = new CpSatSolverEngine(modelBuilder, mapper, configMock.Object, unsatInterpreter, preflightChecker);

        // Act
        var result = await engine.SolveAsync(request, CancellationToken.None);

        // Assert: all soft encoders coexist with hard encoders without breaking the model
        result.Status.Should().BeOneOf(
            new[] { SolverStatus.Optimal, SolverStatus.Feasible },
            because: "soft encoders are diagnostic stubs and should not affect feasibility");
        result.Assignments.Should().HaveCount(1, because: "single feasible section should be scheduled");
        result.Failures.Should().BeEmpty();
    }
}
