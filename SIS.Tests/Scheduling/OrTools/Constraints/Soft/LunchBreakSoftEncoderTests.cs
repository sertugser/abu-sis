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

public class LunchBreakSoftEncoderTests
{
    [Fact]
    public void Encode_FlagDisabled_NoPenalty()
    {
        var (model, ctx) = BuildScenario(s5Enabled: false, sectionStartsLunch: true);
        new LunchBreakSoftEncoder().Encode(ctx);
        ctx.Penalties.Should().BeEmpty();
    }

    [Fact]
    public void Encode_SectionInLunchSlot_PenaltyAdded()
    {
        // Section forced to start at slot 12:00-12:50, lunch 11:40-12:20 → overlap → penalty 8
        var (model, ctx) = BuildScenario(s5Enabled: true, sectionStartsLunch: true);
        new LunchBreakSoftEncoder().Encode(ctx);
        FinishModel(model, ctx);
        var solver = new CpSolver();
        solver.Solve(model);
        // 1 present × 10000 - 8 = 9992
        solver.ObjectiveValue.Should().Be(9992);
    }

    [Fact]
    public void Encode_SectionAvoidsLunch_NoPenalty()
    {
        var (model, ctx) = BuildScenario(s5Enabled: true, sectionStartsLunch: false);
        new LunchBreakSoftEncoder().Encode(ctx);
        FinishModel(model, ctx);
        var solver = new CpSolver();
        solver.Solve(model);
        solver.ObjectiveValue.Should().Be(10000);
    }

    private static (CpModel, EncoderContext) BuildScenario(bool s5Enabled, bool sectionStartsLunch)
    {
        // Plan 10 — S5 ConstraintDefinition kaldırıldı; LunchBreakSoftEncoder artık
        // H13 (Code) + Category="Soft" (ConstraintCategories) ile yönetiliyor.
        var model = new CpModel();
        var slots = new[]
        {
            new TimeSlotInput { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50), AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(12, 50), AllowedMode = TimeSlotAllowedMode.Both },
        };
        var section = new SectionInput
        {
            Id = 1, CourseId = 1, CourseCode = "X", CourseName = "X",
            SectionNumber = 1, Capacity = 30, TheoryHours = 1, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace
        };
        var room = new ClassroomInput { Id = 1, Name = "R", Capacity = 50, Type = ClassroomType.Lecture };
        var request = new SolverRequest
        {
            TermId = 1, FacultyId = 1,
            Sections = new[] { section }, Classrooms = new[] { room }, TimeSlots = slots,
            ConstraintFlags = new Dictionary<string, bool> { ["H13"] = s5Enabled },
            ConstraintCategories = new Dictionary<string, string> { ["H13"] = "Soft" },
            SoftThresholds = new SoftThresholds
            {
                S5LunchBreakStart = new TimeOnly(11, 40),
                S5LunchBreakEnd = new TimeOnly(12, 20),
                S5Weight = 8
            }
        };
        var variables = new DecisionVariables(model, totalSlots: slots.Length);
        variables.CreateIntervalsForSection(section, slots);
        variables.CreateRoomVariable(1, ComponentType.Lecture, request.Classrooms);
        var lecture = variables.GetLectureInterval(1)!;
        // Force start
        var forcedStart = sectionStartsLunch ? 1 : 0;
        model.Add(lecture.StartExpr() == forcedStart);
        model.Add(((BoolVar)variables.GetSectionPresent(1)) == 1);

        var ctx = new EncoderContext
        {
            Model = model, Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = request,
            ConstraintFlags = request.ConstraintFlags,
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
