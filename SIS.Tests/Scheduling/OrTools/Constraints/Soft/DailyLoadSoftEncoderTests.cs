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

public class DailyLoadSoftEncoderTests
{
    [Fact]
    public void Encode_FlagDisabled_NoPenalty()
    {
        var (model, ctx) = BuildScenario(s2Enabled: false, hoursPerSection: 4, sectionsForInstructor: 2, allMonday: true);
        new DailyLoadSoftEncoder().Encode(ctx);
        ctx.Penalties.Should().BeEmpty();
    }

    [Fact]
    public void Encode_DailyHoursAtCap_NoPenalty()
    {
        // 2 sections × 3h = 6h, cap = 6 → no penalty
        var (model, ctx) = BuildScenario(s2Enabled: true, hoursPerSection: 3, sectionsForInstructor: 2, allMonday: true);
        new DailyLoadSoftEncoder().Encode(ctx);
        FinishModel(model, ctx);
        var solver = new CpSolver();
        solver.Solve(model);
        // 2 present × 10000 = 20000
        solver.ObjectiveValue.Should().Be(20000);
    }

    [Fact]
    public void Encode_DailyHoursOverCap_PenaltyAdded()
    {
        // Plan 13 V1 alignment (clock-hour): 50dk slot × 5 kredi = 250dk per section.
        // 2 sections aynı gün = 500dk daily. Cap 6 saat = 360dk. Excess = 140dk.
        // V1 paritesi: excessHours = floor(140/60) = 2. Penalty = 2 × 10 = 20.
        var (model, ctx) = BuildScenario(s2Enabled: true, hoursPerSection: 5, sectionsForInstructor: 2, allMonday: true);
        new DailyLoadSoftEncoder().Encode(ctx);
        FinishModel(model, ctx);
        var solver = new CpSolver();
        solver.Solve(model);
        solver.ObjectiveValue.Should().Be(20000 - 20);
    }

    [Fact]
    public void Encode_NoInstructor_Skipped()
    {
        var (model, ctx) = BuildScenario(s2Enabled: true, hoursPerSection: 8, sectionsForInstructor: 0, allMonday: true);
        new DailyLoadSoftEncoder().Encode(ctx);
        ctx.Penalties.Should().BeEmpty(
            because: "No instructor → encoder cannot aggregate; skip");
    }

    private static (CpModel, EncoderContext) BuildScenario(bool s2Enabled, int hoursPerSection, int sectionsForInstructor, bool allMonday)
    {
        // sectionsForInstructor sections share InstructorId=99; remaining (count-...) have null instructor.
        var model = new CpModel();
        var slots = Enumerable.Range(0, hoursPerSection * Math.Max(1, sectionsForInstructor))
            .Select(i => new TimeSlotInput
            {
                Id = i + 1,
                DayOfWeek = allMonday ? DayOfWeek.Monday : (DayOfWeek)((i / hoursPerSection) % 7),
                SlotOrder = (i % hoursPerSection) + 1,
                StartTime = new TimeOnly(8 + (i % hoursPerSection), 0),
                EndTime = new TimeOnly(8 + (i % hoursPerSection), 50),
                AllowedMode = TimeSlotAllowedMode.Both
            }).ToArray();
        if (slots.Length == 0)
            slots = new[] { new TimeSlotInput { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50), AllowedMode = TimeSlotAllowedMode.Both } };

        var sections = new List<SectionInput>();
        for (int i = 0; i < Math.Max(sectionsForInstructor, 1); i++)
        {
            sections.Add(new SectionInput
            {
                Id = i + 1, CourseId = i + 1, CourseCode = $"C{i + 1}", CourseName = $"C{i + 1}",
                SectionNumber = 1, Capacity = 30,
                TheoryHours = hoursPerSection, LabHours = 0,
                InstructorId = sectionsForInstructor > 0 ? 99 : null,
                TeachingMethod = TeachingMethod.FaceToFace
            });
        }
        var room = new ClassroomInput { Id = 1, Name = "R", Capacity = 50, Type = ClassroomType.Lecture };
        var request = new SolverRequest
        {
            TermId = 1, FacultyId = 1,
            Sections = sections, Classrooms = new[] { room }, TimeSlots = slots,
            ConstraintFlags = new Dictionary<string, bool>(),
            SoftThresholds = new SoftThresholds { S2MaxDailyHours = 6, S2Weight = 10 }
        };

        var variables = new DecisionVariables(model, totalSlots: slots.Length);
        foreach (var s in sections)
        {
            variables.CreateIntervalsForSection(s, slots);
            variables.CreateRoomVariable(s.Id, ComponentType.Lecture, request.Classrooms);
            model.Add(((BoolVar)variables.GetSectionPresent(s.Id)) == 1);
        }
        // Force each section to start at hoursPerSection-step indices on Monday only — to ensure same-day overlap.
        for (int i = 0; i < sectionsForInstructor; i++)
        {
            var lec = variables.GetLectureInterval(sections[i].Id)!;
            // Section i starts at index 0 if i==0, else index hoursPerSection (still Monday in allMonday=true case)
            model.Add(lec.StartExpr() == 0);  // overlap allowed by H1 only if no overlap encoder; we don't run H1 here.
        }

        // Plan 10 Phase 2 — S2 ConstraintDefinition kaldırıldı; DailyLoadSoftEncoder artık
        // H-DL (Code) + Category="Soft" (ConstraintCategories) ile yönetiliyor.
        var requestWithCat = request with
        {
            ConstraintFlags = new Dictionary<string, bool> { ["H-DL"] = s2Enabled },
            ConstraintCategories = new Dictionary<string, string> { ["H-DL"] = "Soft" },
        };
        return (model, new EncoderContext
        {
            Model = model, Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = requestWithCat,
            ConstraintFlags = requestWithCat.ConstraintFlags,
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
