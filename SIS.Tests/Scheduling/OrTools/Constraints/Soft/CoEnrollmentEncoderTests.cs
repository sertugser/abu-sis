using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;
using Xunit;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Soft;

public class CoEnrollmentEncoderTests
{
    [Fact]
    public void Encode_FlagDisabled_NoPenalty()
    {
        var pairs = new[] { new CoEnrollmentPairInput(1, 2, 30) };
        var (model, ctx) = BuildScenario(s4Enabled: false, pairs: pairs,
            forcedStartA: 0, forcedStartB: 0);
        new CoEnrollmentEncoder().Encode(ctx);
        ctx.Penalties.Should().BeEmpty();
    }

    [Fact]
    public void Encode_NoPairs_NoPenalty()
    {
        var (model, ctx) = BuildScenario(s4Enabled: true,
            pairs: Array.Empty<CoEnrollmentPairInput>(),
            forcedStartA: 0, forcedStartB: 0);
        new CoEnrollmentEncoder().Encode(ctx);
        ctx.Penalties.Should().BeEmpty();
    }

    [Fact]
    public void Encode_PairSectionsOverlap_PenaltyAdded()
    {
        // 2 sections (Course 1, Course 2), pair (1, 2, 30 students)
        // Both forced to same Monday slot 0 → overlap → penalty = max(1, 30 × 1 / 2) = 15
        // Objective = 2 × 10000 - 15 = 19985
        var pairs = new[] { new CoEnrollmentPairInput(1, 2, 30) };
        var (model, ctx) = BuildScenario(s4Enabled: true, pairs: pairs,
            forcedStartA: 0, forcedStartB: 0);
        new CoEnrollmentEncoder().Encode(ctx);
        FinishModel(model, ctx);
        var solver = new CpSolver();
        solver.Solve(model);
        solver.ObjectiveValue.Should().Be(19985);
    }

    [Fact]
    public void Encode_PairSectionsNonOverlapping_NoPenaltyInObjective()
    {
        // 2 sections forced to non-overlapping slots (A at 0, B at 3 in 4-slot Monday grid).
        // Reified overlap literal must resolve to 0 → no penalty in objective.
        // Defensive coverage of the NOT side of the bidirectional overlap reification.
        var pairs = new[] { new CoEnrollmentPairInput(1, 2, 30) };
        var (model, ctx) = BuildScenario(s4Enabled: true, pairs: pairs,
            forcedStartA: 0, forcedStartB: 3);
        new CoEnrollmentEncoder().Encode(ctx);
        FinishModel(model, ctx);
        var solver = new CpSolver();
        solver.Solve(model);
        solver.ObjectiveValue.Should().Be(20000,
            because: "no overlap → reified literal = 0 → penalty term contributes 0 to objective");
    }

    private static (CpModel, EncoderContext) BuildScenario(
        bool s4Enabled,
        IReadOnlyList<CoEnrollmentPairInput> pairs,
        int forcedStartA,
        int forcedStartB)
    {
        var model = new CpModel();
        // 4-slot Monday grid
        var slots = new[]
        {
            new TimeSlotInput { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1, StartTime = new TimeOnly(9, 0),  EndTime = new TimeOnly(9, 50),  AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 50), AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 3, DayOfWeek = DayOfWeek.Monday, SlotOrder = 3, StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(11, 50), AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 4, DayOfWeek = DayOfWeek.Monday, SlotOrder = 4, StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(13, 50), AllowedMode = TimeSlotAllowedMode.Both },
        };

        var sectionA = new SectionInput
        {
            Id = 1, CourseId = 1, CourseCode = "C1", CourseName = "C1",
            SectionNumber = 1, Capacity = 30, TheoryHours = 1, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace
        };
        var sectionB = new SectionInput
        {
            Id = 2, CourseId = 2, CourseCode = "C2", CourseName = "C2",
            SectionNumber = 1, Capacity = 30, TheoryHours = 1, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace
        };
        var room = new ClassroomInput { Id = 1, Name = "R", Capacity = 50, Type = ClassroomType.Lecture };

        var request = new SolverRequest
        {
            TermId = 1, FacultyId = 1,
            Sections = new[] { sectionA, sectionB },
            Classrooms = new[] { room },
            TimeSlots = slots,
            ConstraintFlags = new Dictionary<string, bool>(),
            SoftThresholds = new SoftThresholds
            {
                S4Weight = 1,
                S4MinCoPairThreshold = 0
            },
            CoEnrollmentPairs = pairs
        };

        var variables = new DecisionVariables(model, totalSlots: slots.Length);
        foreach (var s in new[] { sectionA, sectionB })
        {
            variables.CreateIntervalsForSection(s, slots);
            variables.CreateRoomVariable(s.Id, ComponentType.Lecture, request.Classrooms);
            model.Add(((BoolVar)variables.GetSectionPresent(s.Id)) == 1);
        }

        // Force lecture starts (no AddNoOverlap → free to overlap when the test wants it).
        var lecA = variables.GetLectureInterval(sectionA.Id)!;
        var lecB = variables.GetLectureInterval(sectionB.Id)!;
        model.Add(lecA.StartExpr() == forcedStartA);
        model.Add(lecB.StartExpr() == forcedStartB);

        var ctx = new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = request,
            ConstraintFlags = new Dictionary<string, bool> { ["S4"] = s4Enabled },
            Penalties = new List<LinearExpr>()
        };
        return (model, ctx);
    }

    private static void FinishModel(CpModel model, EncoderContext ctx)
    {
        var presence = ctx.Variables.AllSectionPresent.Values.OfType<BoolVar>().ToList();
        LinearExpr obj = LinearExpr.Sum(presence) * 10000;
        if (ctx.Penalties.Count > 0) obj -= LinearExpr.Sum(ctx.Penalties);
        model.Maximize(obj);
    }
}
