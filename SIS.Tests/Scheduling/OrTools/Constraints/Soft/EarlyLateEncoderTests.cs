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

public class EarlyLateEncoderTests
{
    [Theory]
    [InlineData(0, 6)]   // 7:30-8:30 ≤ MorningStart=8:00 → penalty 6
    [InlineData(1, 0)]   // 10:00 → no penalty
    [InlineData(2, 6)]   // 18:00 ≥ EveningStart=17:00 → penalty 6
    public void Encode_StartSlotPattern_PenaltyMatches(int forcedSlotIdx, long expectedPenalty)
    {
        var slots = new[]
        {
            new TimeSlotInput { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1, StartTime = new TimeOnly(7, 30), EndTime = new TimeOnly(8, 20), AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 50), AllowedMode = TimeSlotAllowedMode.Both },
            new TimeSlotInput { Id = 3, DayOfWeek = DayOfWeek.Monday, SlotOrder = 3, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(18, 50), AllowedMode = TimeSlotAllowedMode.Both },
        };
        var (model, ctx) = BuildScenario(slots, forcedSlotIdx);
        new EarlyLateEncoder().Encode(ctx);
        FinishModel(model, ctx);
        var solver = new CpSolver();
        solver.Solve(model);
        solver.ObjectiveValue.Should().Be(10000 - expectedPenalty);
    }

    private static (CpModel, EncoderContext) BuildScenario(TimeSlotInput[] slots, int forcedStart)
    {
        var model = new CpModel();
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
            ConstraintFlags = new Dictionary<string, bool>(),
            SoftThresholds = new SoftThresholds
            {
                S9MorningStart = new TimeOnly(8, 0),
                S9EveningStart = new TimeOnly(17, 0),
                S9Weight = 6
            }
        };
        var variables = new DecisionVariables(model, totalSlots: slots.Length);
        variables.CreateIntervalsForSection(section, slots);
        variables.CreateRoomVariable(1, ComponentType.Lecture, request.Classrooms);
        var lecture = variables.GetLectureInterval(1)!;
        model.Add(lecture.StartExpr() == forcedStart);
        model.Add(((BoolVar)variables.GetSectionPresent(1)) == 1);

        return (model, new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = request,
            ConstraintFlags = new Dictionary<string, bool> { ["S9"] = true },
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
