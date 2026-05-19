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
/// Plan 9 Task 11 — H6 cross-section component overlap parity with v1
/// <c>CourseComponentEvaluator</c>.
///
/// v1 davranış: Aynı CourseId'nin farklı section'larında (örn. split lecture-only +
/// lab-only) theory ve lab bileşenleri çakışamaz. Öğrenci pratiğinde aynı dersin
/// hem lecture hem lab section'ına kayıt yapar; ikisi aynı saatte düşerse öğrenci
/// iki yerde olamaz.
///
/// Pre-fix: H6 sadece same-section theory+lab çakışmasını engelliyordu; aynı CourseId
/// cross-section çakışması serbest kalıyordu.
/// </summary>
public class CrossSectionComponentOverlapTests
{
    [Fact]
    public void SameCourse_LectureOnlyAndLabOnly_SingleSlot_ProducesUnsat()
    {
        // Section A: lecture-only (TheoryHours=1, LabHours=0)
        // Section B: lab-only (TheoryHours=0, LabHours=1)
        // Same CourseId 100. 1-slot grid → her ikisi de slot 0'a düşmek zorunda
        // → cross-section H6 ihlali → UNSAT.
        var sectionA = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "BM 101", CourseName = "Algoritma",
            SectionNumber = 1, Capacity = 30,
            TheoryHours = 1, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace,
        };
        var sectionB = new SectionInput
        {
            Id = 2, CourseId = 100, CourseCode = "BM 101", CourseName = "Algoritma",
            SectionNumber = 2, Capacity = 30,
            TheoryHours = 0, LabHours = 1, LabGroupCount = 1,
            TeachingMethod = TeachingMethod.FaceToFace,
        };
        var lectureRoom = new ClassroomInput { Id = 1, Name = "D1", Capacity = 50, Type = ClassroomType.Lecture };
        var labRoom = new ClassroomInput { Id = 2, Name = "L1", Capacity = 30, Type = ClassroomType.Lab };
        var slots = new[]
        {
            new TimeSlotInput
            {
                Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both,
            },
        };

        var model = new CpModel();
        var vars = new DecisionVariables(model, totalSlots: slots.Length);
        vars.CreateIntervalsForSection(sectionA, slots);
        vars.CreateRoomVariable(sectionA.Id, ComponentType.Lecture, new[] { lectureRoom });
        vars.CreateIntervalsForSection(sectionB, slots);
        vars.CreateRoomVariable(sectionB.Id, ComponentType.Lab, new[] { labRoom }, groupCount: 1);

        // Force presence so the lab/lecture intervals are real
        model.Add(((BoolVar)vars.GetSectionPresent(sectionA.Id)) == 1);
        model.Add(((BoolVar)vars.GetSectionPresent(sectionB.Id)) == 1);

        var ctx = new EncoderContext
        {
            Model = model,
            Variables = vars,
            Assumptions = new AssumptionRegistry(model),
            Request = new SolverRequest
            {
                TermId = 1, FacultyId = 1,
                Sections = new List<SectionInput> { sectionA, sectionB },
                Classrooms = new List<ClassroomInput> { lectureRoom, labRoom },
                TimeSlots = slots,
            },
            ConstraintFlags = new Dictionary<string, bool> { ["H6"] = true },
            Penalties = new List<LinearExpr>(),
        };

        new TheoryLabComponentEncoder().Encode(ctx);

        var status = new CpSolver().Solve(model);
        status.Should().Be(CpSolverStatus.Infeasible,
            because: "post-fix cross-section H6 must forbid same-CourseId theoryA × labB " +
                     "overlap (v1 CourseComponentEvaluator parity). Pre-fix this scenario was " +
                     "Feasible — only same-section theory+lab was checked.");
    }

    [Fact]
    public void DifferentCourses_LectureAndLab_SingleSlot_StillFeasible()
    {
        // Section A: CourseId=100, lecture-only
        // Section B: CourseId=200, lab-only (DIFFERENT course)
        // Cross-section H6 only fires for same CourseId → both sections may share slot 0.
        var sectionA = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "BM 101", CourseName = "Algoritma",
            SectionNumber = 1, Capacity = 30,
            TheoryHours = 1, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace,
        };
        var sectionB = new SectionInput
        {
            Id = 2, CourseId = 200, CourseCode = "BM 102", CourseName = "Veri Yapilari",
            SectionNumber = 1, Capacity = 30,
            TheoryHours = 0, LabHours = 1, LabGroupCount = 1,
            TeachingMethod = TeachingMethod.FaceToFace,
        };
        var lectureRoom = new ClassroomInput { Id = 1, Name = "D1", Capacity = 50, Type = ClassroomType.Lecture };
        var labRoom = new ClassroomInput { Id = 2, Name = "L1", Capacity = 30, Type = ClassroomType.Lab };
        var slots = new[]
        {
            new TimeSlotInput
            {
                Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both,
            },
        };

        var model = new CpModel();
        var vars = new DecisionVariables(model, totalSlots: slots.Length);
        vars.CreateIntervalsForSection(sectionA, slots);
        vars.CreateRoomVariable(sectionA.Id, ComponentType.Lecture, new[] { lectureRoom });
        vars.CreateIntervalsForSection(sectionB, slots);
        vars.CreateRoomVariable(sectionB.Id, ComponentType.Lab, new[] { labRoom }, groupCount: 1);

        model.Add(((BoolVar)vars.GetSectionPresent(sectionA.Id)) == 1);
        model.Add(((BoolVar)vars.GetSectionPresent(sectionB.Id)) == 1);

        var ctx = new EncoderContext
        {
            Model = model,
            Variables = vars,
            Assumptions = new AssumptionRegistry(model),
            Request = new SolverRequest
            {
                TermId = 1, FacultyId = 1,
                Sections = new List<SectionInput> { sectionA, sectionB },
                Classrooms = new List<ClassroomInput> { lectureRoom, labRoom },
                TimeSlots = slots,
            },
            ConstraintFlags = new Dictionary<string, bool> { ["H6"] = true },
            Penalties = new List<LinearExpr>(),
        };

        new TheoryLabComponentEncoder().Encode(ctx);

        var status = new CpSolver().Solve(model);
        status.Should().NotBe(CpSolverStatus.Infeasible,
            because: "different CourseId → cross-section H6 must NOT fire");
    }
}
