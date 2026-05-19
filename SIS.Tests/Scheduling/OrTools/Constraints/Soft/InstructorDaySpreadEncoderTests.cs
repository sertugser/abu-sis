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

public class InstructorDaySpreadEncoderTests
{
    [Fact]
    public void Encode_FlagDisabled_NoPenalty()
    {
        var (model, ctx) = BuildScenario(
            s8Enabled: false,
            sectionHours: new[] { 2, 2 },
            forcedStartSlots: new[] { 0, 4 });
        new InstructorDaySpreadEncoder().Encode(ctx);
        ctx.Penalties.Should().BeEmpty();
    }

    [Fact]
    public void Encode_OptimalSpread_NoPenalty()
    {
        // 1 section × 3h, instructor 99, cap=6 → minPossibleDays=ceil(3/6)=1.
        // Force section on Monday (slot 0). usedDays=1 → no penalty.
        var (model, ctx) = BuildScenario(
            s8Enabled: true,
            sectionHours: new[] { 3 },
            forcedStartSlots: new[] { 0 });
        new InstructorDaySpreadEncoder().Encode(ctx);
        FinishModel(model, ctx);
        var solver = new CpSolver();
        var status = solver.Solve(model);
        status.Should().BeOneOf(CpSolverStatus.Optimal, CpSolverStatus.Feasible);
        // 1 present × 10000 - 0 penalty = 10000
        solver.ObjectiveValue.Should().Be(10000);
    }

    [Fact]
    public void Encode_ExcessSpread_PenaltyAdded()
    {
        // 2 sections × 2h = 4h total, instructor 99, cap=6 → minPossibleDays=ceil(4/6)=1.
        // Force section 0 → Monday (slot 0); section 1 → Tuesday (slot 4 in our 4-slot/day grid).
        // usedDays=2 → excess=1 → penalty = 1 × 4 = 4. Objective = 20000 - 4 = 19996.
        var (model, ctx) = BuildScenario(
            s8Enabled: true,
            sectionHours: new[] { 2, 2 },
            forcedStartSlots: new[] { 0, 4 });
        new InstructorDaySpreadEncoder().Encode(ctx);
        FinishModel(model, ctx);
        var solver = new CpSolver();
        var status = solver.Solve(model);
        status.Should().BeOneOf(CpSolverStatus.Optimal, CpSolverStatus.Feasible);
        solver.ObjectiveValue.Should().Be(20000 - 4);
    }

    /// <summary>
    /// Build a multi-day grid (Mon + Tue, 4 slots/day = 8 slots total). Each section
    /// shares InstructorId=99. forcedStartSlots[i] forces section i's lecture interval
    /// to begin at the given slot index.
    /// </summary>
    private static (CpModel, EncoderContext) BuildScenario(bool s8Enabled, int[] sectionHours, int[] forcedStartSlots)
    {
        const int slotsPerDay = 4;
        var slots = new List<TimeSlotInput>();
        for (int day = 0; day < 2; day++)
        {
            for (int order = 0; order < slotsPerDay; order++)
            {
                slots.Add(new TimeSlotInput
                {
                    Id = day * slotsPerDay + order + 1,
                    DayOfWeek = day == 0 ? DayOfWeek.Monday : DayOfWeek.Tuesday,
                    SlotOrder = order + 1,
                    StartTime = new TimeOnly(8 + order, 0),
                    EndTime = new TimeOnly(8 + order, 50),
                    AllowedMode = TimeSlotAllowedMode.Both
                });
            }
        }

        var sections = new List<SectionInput>();
        for (int i = 0; i < sectionHours.Length; i++)
        {
            sections.Add(new SectionInput
            {
                Id = i + 1,
                CourseId = i + 1,
                CourseCode = $"C{i + 1}",
                CourseName = $"C{i + 1}",
                SectionNumber = 1,
                Capacity = 30,
                TheoryHours = sectionHours[i],
                LabHours = 0,
                InstructorId = 99,
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
            SoftThresholds = new SoftThresholds { S2MaxDailyHours = 6, S8Weight = 4 }
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
            ConstraintFlags = new Dictionary<string, bool> { ["S8"] = s8Enabled },
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
