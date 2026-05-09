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
/// Plan 8 Task 20 — Soft penalty integration test (end-to-end).
/// Verifies CP-SAT objective steers solver toward mid-day slots when S9 EarlyLate
/// is active. Senaryo: 1 section, 5 Pazartesi slotu (07:30, 09:00, 11:00, 13:00, 18:30).
/// 07:30 ≤ MorningStart=8:00 ve 18:30 ≥ EveningStart=17:00 → her biri 6 puan penalty.
/// 09:00, 11:00, 13:00 mid-day (penalty = 0) → solver bunlardan birini seçmeli.
/// </summary>
public class SoftPenaltyIntegrationTest
{
    [Fact]
    public async Task SolveAsync_S9Active_PicksMidDayOverEarlyOrLate()
    {
        var slots = new[]
        {
            Slot(1, 7, 30),
            Slot(2, 9, 0),
            Slot(3, 11, 0),
            Slot(4, 13, 0),
            Slot(5, 18, 30),
        };
        var section = new SectionInput
        {
            Id = 1,
            CourseId = 1,
            CourseCode = "X",
            CourseName = "X",
            SectionNumber = 1,
            Capacity = 30,
            TheoryHours = 1,
            LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace
        };
        var room = new ClassroomInput { Id = 1, Name = "R", Capacity = 50, Type = ClassroomType.Lecture };
        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[] { section },
            Classrooms = new[] { room },
            TimeSlots = slots,
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true, ["S9"] = true },
            SoftThresholds = new SoftThresholds()  // defaults: morning 8:00, evening 17:00, weight 6
        };

        var engine = BuildEngine();
        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().BeOneOf(new[] { SolverStatus.Optimal, SolverStatus.Feasible });
        result.Assignments.Should().HaveCount(1);

        // AssignedSlot has TimeSlotId only (no StartTime). Lookup via TimeSlots list.
        var assignedSlotId = result.Assignments.Single().Slots.Single().TimeSlotId;
        var pickedSlot = slots.First(s => s.Id == assignedSlotId);
        var midDayCandidates = new[] { new TimeOnly(9, 0), new TimeOnly(11, 0), new TimeOnly(13, 0) };
        pickedSlot.StartTime.Should().BeOneOf(midDayCandidates,
            because: "S9 favors mid-day (zero penalty) over 7:30 (early, 6) or 18:30 (late, 6)");
    }

    private static TimeSlotInput Slot(int id, int hour, int minute) =>
        new TimeSlotInput
        {
            Id = id,
            DayOfWeek = DayOfWeek.Monday,
            SlotOrder = id,
            StartTime = new TimeOnly(hour, minute),
            EndTime = new TimeOnly(hour, minute).AddMinutes(50),
            AllowedMode = TimeSlotAllowedMode.Both
        };

    private static CpSatSolverEngine BuildEngine()
    {
        // H1 (ClassroomConflict) for room non-overlap; S9 (EarlyLate) for soft penalty.
        var encoders = new IConstraintEncoder[]
        {
            new ClassroomConflictEncoder(),
            new EarlyLateEncoder()
        };
        var modelBuilder = new ScheduleModelBuilder(encoders);
        var mapper = new SolutionMapper();
        var unsatInterpreter = new UnsatCoreInterpreter();
        var preflightChecker = new PreflightFeasibilityChecker();

        var configMock = new Mock<ISystemConfigProvider>();
        configMock.Setup(x => x.GetIntAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, int f, CancellationToken _) => f);
        configMock.Setup(x => x.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, bool f, CancellationToken _) => f);

        return new CpSatSolverEngine(modelBuilder, mapper, configMock.Object, unsatInterpreter, preflightChecker);
    }
}
