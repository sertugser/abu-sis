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
/// Plan 9 Task 12 — H17 simetri polish. Mevcut H17 sadece FaceToFace section'ları
/// OnlineOnly slot'tan engelliyor. Tersine: Online section FaceToFaceOnly slot'a
/// yerleşemez. v1 <c>FaceToFaceSlotRestrictionEvaluator</c> her iki yönü kapsıyor.
/// </summary>
public class FaceToFaceSlotEncoderOnlineTests
{
    [Fact]
    public void OnlineSection_AllFaceToFaceOnlySlots_ProducesUnsat()
    {
        // Online lecture-only section. Tüm slot'lar FaceToFaceOnly → Online yerleşemez → UNSAT.
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "ONL 101", CourseName = "OnlineDers",
            SectionNumber = 1, Capacity = 30,
            TheoryHours = 2, LabHours = 0,
            TeachingMethod = TeachingMethod.Online,
        };
        var slots = new[]
        {
            new TimeSlotInput
            {
                Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.FaceToFaceOnly,
            },
            new TimeSlotInput
            {
                Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2,
                StartTime = new TimeOnly(9, 50), EndTime = new TimeOnly(10, 40),
                AllowedMode = TimeSlotAllowedMode.FaceToFaceOnly,
            },
        };

        var model = new CpModel();
        var vars = new DecisionVariables(model, totalSlots: slots.Length);
        vars.CreateIntervalsForSection(section, slots);
        // Online → no room IntVar (Plan 7b)
        model.Add(((BoolVar)vars.GetSectionPresent(section.Id)) == 1);

        var ctx = new EncoderContext
        {
            Model = model,
            Variables = vars,
            Assumptions = new AssumptionRegistry(model),
            Request = new SolverRequest
            {
                TermId = 1, FacultyId = 1,
                Sections = new List<SectionInput> { section },
                Classrooms = new List<ClassroomInput>(),
                TimeSlots = slots,
            },
            ConstraintFlags = new Dictionary<string, bool> { ["H17"] = true },
            Penalties = new List<LinearExpr>(),
        };

        new FaceToFaceSlotEncoder().Encode(ctx);
        ctx.Assumptions.RegisterAllAsAssumptions();

        new CpSolver().Solve(model).Should().Be(CpSolverStatus.Infeasible,
            because: "Plan 9 Task 12 — Online section FaceToFaceOnly slot'a yerleşemez " +
                     "(simetri). Pre-fix encoder Online section'ı skip ediyordu → Feasible.");
    }

    [Fact]
    public void OnlineSection_OneFaceToFaceOnlyAndOneBothSlot_FindsBothSlot()
    {
        // 2 slot: slot 0 FaceToFaceOnly (Online ban), slot 1 Both (free).
        // Online section TheoryHours=1 → tek slot işgal eder. Encoder slot 0'ı banlar
        // → solver slot 1'i seçmek zorunda → Feasible.
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "ONL 101", CourseName = "OnlineDers",
            SectionNumber = 1, Capacity = 30,
            TheoryHours = 1, LabHours = 0,
            TeachingMethod = TeachingMethod.Online,
        };
        var slots = new[]
        {
            new TimeSlotInput
            {
                Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.FaceToFaceOnly,
            },
            new TimeSlotInput
            {
                Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2,
                StartTime = new TimeOnly(9, 50), EndTime = new TimeOnly(10, 40),
                AllowedMode = TimeSlotAllowedMode.Both,
            },
        };

        var model = new CpModel();
        var vars = new DecisionVariables(model, totalSlots: slots.Length);
        vars.CreateIntervalsForSection(section, slots);
        model.Add(((BoolVar)vars.GetSectionPresent(section.Id)) == 1);

        var ctx = new EncoderContext
        {
            Model = model,
            Variables = vars,
            Assumptions = new AssumptionRegistry(model),
            Request = new SolverRequest
            {
                TermId = 1, FacultyId = 1,
                Sections = new List<SectionInput> { section },
                Classrooms = new List<ClassroomInput>(),
                TimeSlots = slots,
            },
            ConstraintFlags = new Dictionary<string, bool> { ["H17"] = true },
            Penalties = new List<LinearExpr>(),
        };

        new FaceToFaceSlotEncoder().Encode(ctx);
        ctx.Assumptions.RegisterAllAsAssumptions();

        var solver = new CpSolver();
        var status = solver.Solve(model);
        status.Should().NotBe(CpSolverStatus.Infeasible);

        var iv = vars.GetLectureInterval(section.Id)!;
        solver.Value(iv.StartExpr()).Should().Be(1,
            because: "slot 0 FaceToFaceOnly → Online banned → solver must pick slot 1 (Both).");
    }
}
