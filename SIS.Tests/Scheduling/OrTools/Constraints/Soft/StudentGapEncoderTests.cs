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

/// <summary>
/// Plan 8 Task 12 — S7 StudentGapEncoder real-impl coverage.
/// Per-(DepartmentId, Year) pair: penalty if same-day AND effectiveGap ≥ cap.
/// Plan 13 V1 alignment (2026-05-15): penalty = (effectiveGap − cap + 1) × S7Weight
/// (multiplier'lı, V1 evaluator ile birebir).
/// </summary>
public class StudentGapEncoderTests
{
    [Fact]
    public void Encode_FlagDisabled_NoPenalty()
    {
        var (model, ctx) = BuildScenario(s7Enabled: false,
            sections: TwoSectionsSameGroup(),
            forcedStartA: 0, forcedStartB: 5);

        new StudentGapEncoder().Encode(ctx);

        ctx.Penalties.Should().BeEmpty();
    }

    [Fact]
    public void Encode_DifferentDeptYear_NoPenalty()
    {
        // Section A: Dept 1, Year 0; Section B: Dept 2, Year 1 → no pair to check.
        var sections = new[]
        {
            MakeSection(id: 1, deptId: 1, year: 0),
            MakeSection(id: 2, deptId: 2, year: 1)
        };
        var (model, ctx) = BuildScenario(s7Enabled: true, sections: sections,
            forcedStartA: 0, forcedStartB: 5);

        new StudentGapEncoder().Encode(ctx);

        ctx.Penalties.Should().BeEmpty(
            because: "different (Dept, Year) groups → no pairs in any single group");
    }

    [Fact]
    public void Encode_SameDayFarApart_PenaltyAdded()
    {
        // 2 sections same dept-year; both Monday; A at slot 0, B at slot 5.
        // effectiveGap = max(0, 5−1, 0−6) = 4. cap = 3 → triggered.
        // Plan 13 V1 alignment: multiplier = 4 − 3 + 1 = 2; penalty = 2 × 3 = 6.
        // Objective = 2 × 10000 − 6 = 19994.
        var (model, ctx) = BuildScenario(s7Enabled: true,
            sections: TwoSectionsSameGroup(),
            forcedStartA: 0, forcedStartB: 5);

        new StudentGapEncoder().Encode(ctx);
        FinishModel(model, ctx);

        var solver = new CpSolver();
        solver.Solve(model);
        solver.ObjectiveValue.Should().Be(19994);
    }

    [Fact]
    public void Encode_SameDayClose_NoPenalty()
    {
        // 2 sections same dept-year, same Monday, adjacent (A at 0, B at 1).
        // Gap = 1 - 1 = 0 < cap → no penalty. Objective = 20000.
        var (model, ctx) = BuildScenario(s7Enabled: true,
            sections: TwoSectionsSameGroup(),
            forcedStartA: 0, forcedStartB: 1);

        new StudentGapEncoder().Encode(ctx);
        FinishModel(model, ctx);

        var solver = new CpSolver();
        solver.Solve(model);
        solver.ObjectiveValue.Should().Be(20000);
    }

    private static SectionInput MakeSection(int id, int deptId, int year) => new()
    {
        Id = id,
        CourseId = id,
        CourseCode = $"C{id}",
        CourseName = $"C{id}",
        SectionNumber = 1,
        Capacity = 30,
        TheoryHours = 1,
        LabHours = 0,
        DepartmentId = deptId,
        Year = year,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    private static IReadOnlyList<SectionInput> TwoSectionsSameGroup() => new[]
    {
        MakeSection(id: 1, deptId: 1, year: 0),
        MakeSection(id: 2, deptId: 1, year: 0)
    };

    private static (CpModel, EncoderContext) BuildScenario(
        bool s7Enabled,
        IReadOnlyList<SectionInput> sections,
        int forcedStartA,
        int forcedStartB)
    {
        var model = new CpModel();
        // Multi-day grid: Monday slots 0-5, Tuesday slots 6-11 (12 total).
        var slots = new List<TimeSlotInput>();
        for (int i = 0; i < 6; i++)
        {
            slots.Add(new TimeSlotInput
            {
                Id = i + 1,
                DayOfWeek = DayOfWeek.Monday,
                SlotOrder = i + 1,
                StartTime = new TimeOnly(9 + i, 0),
                EndTime = new TimeOnly(9 + i, 50),
                AllowedMode = TimeSlotAllowedMode.Both
            });
        }
        for (int i = 0; i < 6; i++)
        {
            slots.Add(new TimeSlotInput
            {
                Id = i + 7,
                DayOfWeek = DayOfWeek.Tuesday,
                SlotOrder = i + 1,
                StartTime = new TimeOnly(9 + i, 0),
                EndTime = new TimeOnly(9 + i, 50),
                AllowedMode = TimeSlotAllowedMode.Both
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
            SoftThresholds = new SoftThresholds
            {
                S7MaxGapSlots = 3,
                S7Weight = 3
            }
        };

        var variables = new DecisionVariables(model, totalSlots: slots.Count);
        foreach (var s in sections)
        {
            variables.CreateIntervalsForSection(s, slots);
            variables.CreateRoomVariable(s.Id, ComponentType.Lecture, request.Classrooms);
            model.Add(((BoolVar)variables.GetSectionPresent(s.Id)) == 1);
        }

        // Force lecture starts (no AddNoOverlap; tests directly inspect penalty terms).
        if (sections.Count >= 1)
        {
            var lecA = variables.GetLectureInterval(sections[0].Id)!;
            model.Add(lecA.StartExpr() == forcedStartA);
        }
        if (sections.Count >= 2)
        {
            var lecB = variables.GetLectureInterval(sections[1].Id)!;
            model.Add(lecB.StartExpr() == forcedStartB);
        }

        var ctx = new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = request,
            ConstraintFlags = new Dictionary<string, bool> { ["S7"] = s7Enabled },
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
