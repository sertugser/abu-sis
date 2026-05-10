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

public class WeekdayBalanceEncoderTests
{
    [Fact]
    public void Encode_FlagDisabled_NoPenalty()
    {
        // 4 sections forced on Monday (slot 0). With S12 disabled, no penalty must be added.
        var (model, ctx) = BuildScenario(
            s12Enabled: false,
            forcedStartSlots: new[] { 0, 0, 0, 0 });
        new WeekdayBalanceEncoder().Encode(ctx);
        ctx.Penalties.Should().BeEmpty();
    }

    [Fact]
    public void Encode_BalancedDistribution_NoPenalty()
    {
        // 7 sections, one per weekday Mon..Sun (one slot per day grid). max=1, min=1, diff=0.
        // Objective = 7 × 10000 - 0 = 70000.
        var (model, ctx) = BuildScenario(
            s12Enabled: true,
            forcedStartSlots: new[] { 0, 1, 2, 3, 4, 5, 6 });
        new WeekdayBalanceEncoder().Encode(ctx);
        FinishModel(model, ctx);
        var solver = new CpSolver();
        var status = solver.Solve(model);
        status.Should().BeOneOf(CpSolverStatus.Optimal, CpSolverStatus.Feasible);
        solver.ObjectiveValue.Should().Be(70000);
    }

    [Fact]
    public void Encode_ImbalancedDistribution_PenaltyAdded()
    {
        // 4 sections all forced on Monday (slot 0). max=4 (Mon), min=0 (other 6 days).
        // diff=4, penalty=4 × S12Weight(2)=8. Objective = 4 × 10000 - 8 = 39992.
        var (model, ctx) = BuildScenario(
            s12Enabled: true,
            forcedStartSlots: new[] { 0, 0, 0, 0 });
        new WeekdayBalanceEncoder().Encode(ctx);
        FinishModel(model, ctx);
        var solver = new CpSolver();
        var status = solver.Solve(model);
        status.Should().BeOneOf(CpSolverStatus.Optimal, CpSolverStatus.Feasible);
        solver.ObjectiveValue.Should().Be(40000 - 8);
    }

    /// <summary>
    /// Build a 7-day grid (Mon-Sun, 1 slot/day = 7 slots total). forcedStartSlots[i]
    /// forces section i's lecture interval to begin at the given slot index (0=Mon, 6=Sun).
    /// </summary>
    private static (CpModel, EncoderContext) BuildScenario(bool s12Enabled, int[] forcedStartSlots)
    {
        var weekdays = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        };
        var slots = new List<TimeSlotInput>();
        for (int day = 0; day < 7; day++)
        {
            slots.Add(new TimeSlotInput
            {
                Id = day + 1,
                DayOfWeek = weekdays[day],
                SlotOrder = 1,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(8, 50),
                AllowedMode = TimeSlotAllowedMode.Both
            });
        }

        var sections = new List<SectionInput>();
        for (int i = 0; i < forcedStartSlots.Length; i++)
        {
            sections.Add(new SectionInput
            {
                Id = i + 1,
                CourseId = i + 1,
                CourseCode = $"C{i + 1}",
                CourseName = $"C{i + 1}",
                SectionNumber = 1,
                Capacity = 30,
                TheoryHours = 1,
                LabHours = 0,
                InstructorId = null,
                TeachingMethod = TeachingMethod.FaceToFace
            });
        }

        var room = new ClassroomInput { Id = 1, Name = "R", Capacity = 50, Type = ClassroomType.Lecture };
        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = sections,
            Classrooms = new[] { room },
            TimeSlots = slots,
            ConstraintFlags = new Dictionary<string, bool>(),
            SoftThresholds = new SoftThresholds { S12Weight = 2 }
        };

        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: slots.Count);
        foreach (var s in sections)
        {
            variables.CreateIntervalsForSection(s, slots);
            variables.CreateRoomVariable(s.Id, ComponentType.Lecture, request.Classrooms);
            model.Add(((BoolVar)variables.GetSectionPresent(s.Id)) == 1);
        }
        for (int i = 0; i < sections.Count; i++)
        {
            var lec = variables.GetLectureInterval(sections[i].Id)!;
            model.Add(lec.StartExpr() == forcedStartSlots[i]);
        }

        return (model, new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = request,
            ConstraintFlags = new Dictionary<string, bool> { ["S12"] = s12Enabled },
            Penalties = new List<LinearExpr>()
        });
    }

    private static void FinishModel(CpModel model, EncoderContext ctx)
    {
        var presence = ctx.Variables.AllSectionPresent.Values.OfType<BoolVar>().ToList();
        LinearExpr obj = LinearExpr.Sum(presence) * 10000;
        if (ctx.Penalties.Count > 0) obj -= LinearExpr.Sum(ctx.Penalties);
        model.Maximize(obj);
    }
}
