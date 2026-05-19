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
/// Plan 8 Task 15 — DIST SameDay (hard + soft tier).
/// Hard mode: dayA == dayB enforced via model.Add.
/// Soft mode: reified diffLit = (dayA != dayB), penalty = diffLit × softWeight.
/// </summary>
public class DistributionConstraintEncoderSameDayTests
{
    [Fact]
    public void Encode_SameDay_HardMode_ForcesSameDay()
    {
        // Hard mode: Unavailable. Force section A start to slot 0 (Mon),
        // and force section B start to either Mon-slot-0 or Tue-slot-0.
        // Encoder must add dayA == dayB → forces B to Mon-slot-0.
        var (model, ctx, sections) = BuildScenario(
            prefLevel: PreferenceLevel.Unavailable,
            forceADay: DayOfWeek.Monday,
            forceBDay: null /* free, but must end up Mon */);

        new DistributionConstraintEncoder().Encode(ctx);
        FinishModel(model, ctx);

        var solver = new CpSolver();
        var status = solver.Solve(model);

        status.Should().BeOneOf(CpSolverStatus.Optimal, CpSolverStatus.Feasible);

        // Both sections must be on Monday (only slot 0 of Monday is in domain).
        var dayA = ctx.Variables.AllComponentDayVars[(sections[0].Id, ComponentType.Lecture)];
        var dayB = ctx.Variables.AllComponentDayVars[(sections[1].Id, ComponentType.Lecture)];
        solver.Value(dayA).Should().Be(solver.Value(dayB),
            because: "hard SameDay forces both lectures onto same day");
        solver.Value(dayA).Should().Be((long)DayOfWeek.Monday);
    }

    [Fact]
    public void Encode_SameDay_SoftDiscouraged_DifferentDayPenalty()
    {
        // Soft mode: Discouraged. Force section A to Mon, section B to Tue → different day.
        // Penalty fires: 1 × DistDiscouragedWeight (50).
        // Objective = 2 sections × 10000 - 50 = 19950.
        var (model, ctx, _) = BuildScenario(
            prefLevel: PreferenceLevel.Discouraged,
            forceADay: DayOfWeek.Monday,
            forceBDay: DayOfWeek.Tuesday);

        new DistributionConstraintEncoder().Encode(ctx);
        FinishModel(model, ctx);

        var solver = new CpSolver();
        var status = solver.Solve(model);

        status.Should().BeOneOf(CpSolverStatus.Optimal, CpSolverStatus.Feasible);
        solver.ObjectiveValue.Should().Be(20000 - 50);
    }

    [Fact]
    public void Encode_SameDay_SoftStronglyDiscouraged_HigherPenalty()
    {
        // Soft mode: StronglyDiscouraged. Same different-day setup.
        // Penalty: 1 × DistStronglyDiscouragedWeight (200).
        // Objective = 20000 - 200 = 19800.
        var (model, ctx, _) = BuildScenario(
            prefLevel: PreferenceLevel.StronglyDiscouraged,
            forceADay: DayOfWeek.Monday,
            forceBDay: DayOfWeek.Tuesday);

        new DistributionConstraintEncoder().Encode(ctx);
        FinishModel(model, ctx);

        var solver = new CpSolver();
        var status = solver.Solve(model);

        status.Should().BeOneOf(CpSolverStatus.Optimal, CpSolverStatus.Feasible);
        solver.ObjectiveValue.Should().Be(20000 - 200);
    }

    /// <summary>
    /// 2 sections (CourseId 1, 2), each TheoryHours=1, 2-day grid (Mon + Tue, 2 slots each).
    /// Force lecture start to slot 0 of forced day. 1 dist constraint over both courses.
    /// Force presence == 1.
    /// </summary>
    private static (CpModel model, EncoderContext ctx, SectionInput[] sections) BuildScenario(
        PreferenceLevel prefLevel,
        DayOfWeek? forceADay,
        DayOfWeek? forceBDay)
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

        // 2-day × 2-slot grid: indices 0,1 = Mon; 2,3 = Tue.
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
                Id = 3, DayOfWeek = DayOfWeek.Tuesday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both,
            },
            new TimeSlotInput
            {
                Id = 4, DayOfWeek = DayOfWeek.Tuesday, SlotOrder = 2,
                StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 50),
                AllowedMode = TimeSlotAllowedMode.Both,
            },
        };

        var classrooms = new[]
        {
            new ClassroomInput { Id = 9, Name = "R", Capacity = 50, Type = ClassroomType.Lecture },
        };

        var dist = new DistributionConstraintInput(
            Id: 42,
            DistributionType: DistributionType.SameDay,
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

        // Pin lectures to specific days: slot 0 (Mon) or slot 2 (Tue).
        // Lecture domain validated against slots → only [0, 1, 2, 3] valid for 1h block.
        // Mon-slot-0 → start index 0 (DayOfWeek=Monday=1).
        // Tue-slot-0 → start index 2 (DayOfWeek=Tuesday=2).
        if (forceADay.HasValue)
        {
            var startA = variables.GetLectureInterval(sections[0].Id)!.StartExpr();
            var idxA = forceADay.Value == DayOfWeek.Monday ? 0 : 2;
            model.Add(startA == idxA);
        }
        if (forceBDay.HasValue)
        {
            var startB = variables.GetLectureInterval(sections[1].Id)!.StartExpr();
            var idxB = forceBDay.Value == DayOfWeek.Monday ? 0 : 2;
            model.Add(startB == idxB);
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
