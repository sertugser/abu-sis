using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;
using Xunit;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// Plan 8 Task 19 — DIST Precedence (hard + soft tier).
/// Precedence: section A starts before section B → <c>startA &lt; startB</c>.
/// Hard: <c>model.Add(startA &lt; startB)</c>. Soft: reified
/// <c>violLit ⇔ (startA &gt;= startB)</c>; penalty = <c>violLit × softWeight</c>.
/// </summary>
public class DistributionConstraintEncoderPrecedenceTests
{
    [Fact]
    public void Encode_Precedence_HardMode_ForcesABeforeB()
    {
        // Hard mode: Unavailable. Force section B start = slot 2.
        // Encoder forces startA < startB → A must be slot 0 or 1.
        var (model, ctx, sections) = BuildScenario(
            prefLevel: PreferenceLevel.Unavailable,
            forceAStart: null,
            forceBStart: 2);

        new DistributionConstraintEncoder().Encode(ctx);
        FinishModel(model, ctx);

        var solver = new CpSolver();
        var status = solver.Solve(model);

        status.Should().BeOneOf(CpSolverStatus.Optimal, CpSolverStatus.Feasible);

        var startA = solver.Value(ctx.Variables.GetLectureInterval(sections[0].Id)!.StartExpr());
        var startB = solver.Value(ctx.Variables.GetLectureInterval(sections[1].Id)!.StartExpr());
        startA.Should().BeLessThan(startB,
            because: "hard Precedence forces section A to start strictly before section B");
        startA.Should().BeOneOf(0L, 1L);
        startB.Should().Be(2L);
    }

    [Fact]
    public void Encode_Precedence_SoftDiscouraged_BAfterAPenalty()
    {
        // Soft mode: Discouraged. Force A start 2, B start 0 → violation (A NOT before B).
        // Penalty: violLit == 1 → 1 × DistDiscouragedWeight (50).
        // Objective = 2 × 10000 - 50 = 19950.
        var (model, ctx, _) = BuildScenario(
            prefLevel: PreferenceLevel.Discouraged,
            forceAStart: 2,
            forceBStart: 0);

        new DistributionConstraintEncoder().Encode(ctx);
        FinishModel(model, ctx);

        var solver = new CpSolver();
        var status = solver.Solve(model);

        status.Should().BeOneOf(CpSolverStatus.Optimal, CpSolverStatus.Feasible);
        solver.ObjectiveValue.Should().Be(20000 - 50);
    }

    [Fact]
    public void Encode_Precedence_SoftStronglyDiscouraged_HigherPenalty()
    {
        // Soft mode: StronglyDiscouraged. Same violation setup (A=2, B=0).
        // Penalty: 1 × DistStronglyDiscouragedWeight (200).
        // Objective = 20000 - 200 = 19800.
        var (model, ctx, _) = BuildScenario(
            prefLevel: PreferenceLevel.StronglyDiscouraged,
            forceAStart: 2,
            forceBStart: 0);

        new DistributionConstraintEncoder().Encode(ctx);
        FinishModel(model, ctx);

        var solver = new CpSolver();
        var status = solver.Solve(model);

        status.Should().BeOneOf(CpSolverStatus.Optimal, CpSolverStatus.Feasible);
        solver.ObjectiveValue.Should().Be(20000 - 200);
    }

    /// <summary>
    /// 2 sections (CourseId 1, 2), TheoryHours=1 each. 1 classroom. 4-slot Monday grid.
    /// Force section presence == 1. Optionally pin lecture start indexes.
    /// </summary>
    private static (CpModel model, EncoderContext ctx, SectionInput[] sections) BuildScenario(
        PreferenceLevel prefLevel,
        int? forceAStart,
        int? forceBStart)
    {
        var sections = new[]
        {
            new SectionInput
            {
                Id = 1,
                CourseId = 1,
                CourseCode = "C1",
                CourseName = "C1",
                SectionNumber = 1,
                Capacity = 30,
                TheoryHours = 1,
                LabHours = 0,
                TeachingMethod = TeachingMethod.FaceToFace,
            },
            new SectionInput
            {
                Id = 2,
                CourseId = 2,
                CourseCode = "C2",
                CourseName = "C2",
                SectionNumber = 1,
                Capacity = 30,
                TheoryHours = 1,
                LabHours = 0,
                TeachingMethod = TeachingMethod.FaceToFace,
            },
        };

        // 4-slot Monday grid (start indexes 0..3 valid for 1h theory block).
        var slots = new[]
        {
            new TimeSlotInput
            {
                Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both,
            },
            new TimeSlotInput
            {
                Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2,
                StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 50),
                AllowedMode = TimeSlotAllowedMode.Both,
            },
            new TimeSlotInput
            {
                Id = 3, DayOfWeek = DayOfWeek.Monday, SlotOrder = 3,
                StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(11, 50),
                AllowedMode = TimeSlotAllowedMode.Both,
            },
            new TimeSlotInput
            {
                Id = 4, DayOfWeek = DayOfWeek.Monday, SlotOrder = 4,
                StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(12, 50),
                AllowedMode = TimeSlotAllowedMode.Both,
            },
        };

        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "R1", Capacity = 50, Type = ClassroomType.Lecture },
        };

        var dist = new DistributionConstraintInput(
            Id: 42,
            DistributionType: DistributionType.Precedence,
            PreferenceLevel: prefLevel,
            Courses: new[]
            {
                new DistributionCourseInput(1, null, 0),
                new DistributionCourseInput(2, null, 1),
            });

        var flags = new Dictionary<string, bool> { ["DIST"] = true };

        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: slots.Length);

        foreach (var s in sections)
        {
            variables.CreateIntervalsForSection(s, slots);
            variables.CreateRoomVariable(s.Id, ComponentType.Lecture, classrooms);
            // Force section presence == 1 so penalty terms cannot be optimised away.
            model.Add(((BoolVar)variables.GetSectionPresent(s.Id)) == 1);
        }

        if (forceAStart.HasValue)
        {
            var startA = variables.GetLectureInterval(sections[0].Id)!.StartExpr();
            model.Add(startA == forceAStart.Value);
        }
        if (forceBStart.HasValue)
        {
            var startB = variables.GetLectureInterval(sections[1].Id)!.StartExpr();
            model.Add(startB == forceBStart.Value);
        }

        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = sections,
            Classrooms = classrooms,
            TimeSlots = slots,
            ConstraintFlags = flags,
            DistributionConstraints = new[] { dist },
            SoftThresholds = new SoftThresholds
            {
                DistDiscouragedWeight = 50,
                DistStronglyDiscouragedWeight = 200,
            },
        };

        var ctx = new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = request,
            ConstraintFlags = flags,
            Penalties = new List<LinearExpr>(),
        };

        return (model, ctx, sections);
    }

    /// <summary>
    /// Standard objective: maximise (sum(presence) × 10000) - sum(penalties).
    /// 2 sections × 10000 = 20000 baseline.
    /// </summary>
    private static void FinishModel(CpModel model, EncoderContext ctx)
    {
        var presence = ctx.Variables.AllSectionPresent.Values.OfType<BoolVar>().ToList();
        LinearExpr obj = LinearExpr.Sum(presence) * 10000;
        if (ctx.Penalties.Count > 0) obj -= LinearExpr.Sum(ctx.Penalties);
        model.Maximize(obj);
    }
}
