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
/// Plan 10 Task 1 — multi-interval lab × parallel-rooms encoder support.
///
/// Pre-fix (Plan 9 Issue 2): ClassroomConflictEncoder MarkUnsupported + skip when
/// labIntervals.Count > 1 AND labRooms.Count > 1. Production'da CENG 102, BM 102,
/// BM 332 gibi 5 section bu kombinasyon nedeniyle yerleşemiyordu (Term 6 %94.4).
///
/// Post-fix: guard kaldırılıyor; mevcut helper'lar (CreateOptionalRoomIntervalForLabBlock +
/// CreateRoomVariable Lab branch'inin AddAllDifferent'ı) zaten composable — encoder
/// per (block, group, classroom) optional interval üretiyor + AddNoOverlap her classroom için.
/// </summary>
public class MultiBlockParallelRoomsTests
{
    /// <summary>5-day × 5-slot grid. Lab block start'ları her gün için {0,1,2} (back-to-back).</summary>
    private static IReadOnlyList<TimeSlotInput> BuildWeekGrid()
    {
        var slots = new List<TimeSlotInput>();
        var days = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday
        };
        var times = new (TimeOnly start, TimeOnly end)[]
        {
            (new TimeOnly(9, 0),  new TimeOnly(9, 50)),
            (new TimeOnly(9, 50), new TimeOnly(10, 40)),
            (new TimeOnly(10, 40), new TimeOnly(11, 30)),
            (new TimeOnly(13, 0), new TimeOnly(13, 50)),
            (new TimeOnly(13, 50), new TimeOnly(14, 40)),
        };
        int id = 1;
        foreach (var day in days)
        {
            for (int s = 0; s < times.Length; s++)
            {
                slots.Add(new TimeSlotInput
                {
                    Id = id++,
                    DayOfWeek = day,
                    SlotOrder = s + 1,
                    StartTime = times[s].start,
                    EndTime = times[s].end,
                    AllowedMode = TimeSlotAllowedMode.Both,
                });
            }
        }
        return slots;
    }

    [Fact]
    public void Encode_LabHours4_TwoBlocks_TwoGroups_ProducesFeasibleSolution()
    {
        // CENG 102 senaryosu: LabHours=4, MaxLabBlockHours=2 → 2 block, LabGroupCount=2.
        // 2 lab classroom (1 per group). Solver 2 block × 2 group = 4 placement yapmalı.
        var slots = BuildWeekGrid();
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "CENG 102", CourseName = "Programming",
            SectionNumber = 1, Capacity = 30,
            TheoryHours = 0, LabHours = 4, LabGroupCount = 2,
            TeachingMethod = TeachingMethod.FaceToFace,
        };
        var lab1 = new ClassroomInput { Id = 10, Name = "L1", Capacity = 30, Type = ClassroomType.Lab };
        var lab2 = new ClassroomInput { Id = 11, Name = "L2", Capacity = 30, Type = ClassroomType.Lab };

        var model = new CpModel();
        var vars = new DecisionVariables(model, totalSlots: slots.Count);
        vars.CreateIntervalsForSection(section, slots, createLabInterval: false);
        vars.CreateLabIntervalsForSection(section, slots, maxLabBlockHours: 2);
        vars.CreateRoomVariable(section.Id, ComponentType.Lab, new[] { lab1, lab2 }, groupCount: 2);

        // Force presence so the lab interval starts must take real values.
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
                Classrooms = new List<ClassroomInput> { lab1, lab2 },
                TimeSlots = slots,
                MaxLabBlockHours = 2,
            },
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true },
            Penalties = new List<LinearExpr>(),
        };

        new ClassroomConflictEncoder().Encode(ctx);

        // Pre-fix: section MarkUnsupported listesinde olur.
        vars.GetUnsupportedSections().Should().BeEmpty(
            because: "Plan 10 Task 1 — multi-interval × parallel-rooms artık supported");

        ctx.Assumptions.RegisterAllAsAssumptions();

        var solver = new CpSolver();
        var status = solver.Solve(model);
        status.Should().BeOneOf(new[] { CpSolverStatus.Feasible, CpSolverStatus.Optimal },
            because: "encoder 2 blok × 2 grup × 2 oda kombinasyonunu çözebilmeli");

        // Verify: each (block, group) lands in a real slot
        var labIntervals = vars.GetLabIntervals(section.Id);
        labIntervals.Should().HaveCount(2, because: "LabHours=4 / MaxLabBlockHours=2 → 2 blocks");

        // Each block start ∈ valid range
        foreach (var iv in labIntervals)
        {
            var startVal = solver.Value(iv.StartExpr());
            startVal.Should().BeGreaterThanOrEqualTo(0);
            startVal.Should().BeLessThan(slots.Count);
        }

        // Each group lands in a different classroom (AddAllDifferent on labRooms)
        var labRooms = vars.GetLabRooms(section.Id);
        var room0 = solver.Value(labRooms[0]);
        var room1 = solver.Value(labRooms[1]);
        room0.Should().NotBe(room1, because: "paralel grup farklı odalarda (CreateRoomVariable AddAllDifferent)");
    }

    [Fact]
    public void Encode_LabHours6_ThreeBlocks_TwoGroups_ProducesFeasibleSolution()
    {
        // BM 332-1 senaryosu: LabHours=6, MaxLabBlockHours=2 → 3 block, LabGroupCount=2.
        var slots = BuildWeekGrid();
        var section = new SectionInput
        {
            Id = 1, CourseId = 332, CourseCode = "BM 332", CourseName = "Microprocessor",
            SectionNumber = 1, Capacity = 30,
            TheoryHours = 0, LabHours = 6, LabGroupCount = 2,
            TeachingMethod = TeachingMethod.FaceToFace,
        };
        var lab1 = new ClassroomInput { Id = 10, Name = "L1", Capacity = 30, Type = ClassroomType.Lab };
        var lab2 = new ClassroomInput { Id = 11, Name = "L2", Capacity = 30, Type = ClassroomType.Lab };

        var model = new CpModel();
        var vars = new DecisionVariables(model, totalSlots: slots.Count);
        vars.CreateIntervalsForSection(section, slots, createLabInterval: false);
        vars.CreateLabIntervalsForSection(section, slots, maxLabBlockHours: 2);
        vars.CreateRoomVariable(section.Id, ComponentType.Lab, new[] { lab1, lab2 }, groupCount: 2);
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
                Classrooms = new List<ClassroomInput> { lab1, lab2 },
                TimeSlots = slots,
                MaxLabBlockHours = 2,
            },
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true },
            Penalties = new List<LinearExpr>(),
        };

        new ClassroomConflictEncoder().Encode(ctx);

        vars.GetUnsupportedSections().Should().BeEmpty();
        ctx.Assumptions.RegisterAllAsAssumptions();

        new CpSolver().Solve(model).Should().BeOneOf(
            new[] { CpSolverStatus.Feasible, CpSolverStatus.Optimal },
            because: "3 blok × 2 grup kombinasyonu da çalışmalı (BM 332-1 senaryosu)");

        vars.GetLabIntervals(section.Id).Should().HaveCount(3,
            because: "LabHours=6 / MaxLabBlockHours=2 → 3 blocks");
    }

    [Fact]
    public void Encode_TwoGroups_OneClassroom_StillUnsatViaAllDifferent()
    {
        // Negative: 2 grup ama 1 lab odası → AddAllDifferent ihlali → UNSAT.
        // Bu Plan 10'la ilgili değil (Plan 4'ten beri); ama smoke check.
        var slots = BuildWeekGrid();
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "CENG 102", CourseName = "Programming",
            SectionNumber = 1, Capacity = 30,
            TheoryHours = 0, LabHours = 4, LabGroupCount = 2,
            TeachingMethod = TeachingMethod.FaceToFace,
        };
        var lab1 = new ClassroomInput { Id = 10, Name = "L1", Capacity = 30, Type = ClassroomType.Lab };

        var model = new CpModel();
        var vars = new DecisionVariables(model, totalSlots: slots.Count);
        vars.CreateIntervalsForSection(section, slots, createLabInterval: false);
        vars.CreateLabIntervalsForSection(section, slots, maxLabBlockHours: 2);
        vars.CreateRoomVariable(section.Id, ComponentType.Lab, new[] { lab1 }, groupCount: 2);
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
                Classrooms = new List<ClassroomInput> { lab1 },
                TimeSlots = slots,
                MaxLabBlockHours = 2,
            },
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true },
            Penalties = new List<LinearExpr>(),
        };

        new ClassroomConflictEncoder().Encode(ctx);
        ctx.Assumptions.RegisterAllAsAssumptions();

        new CpSolver().Solve(model).Should().Be(CpSolverStatus.Infeasible,
            because: "2 grup, 1 oda → AddAllDifferent unsatisfiable");
    }
}
