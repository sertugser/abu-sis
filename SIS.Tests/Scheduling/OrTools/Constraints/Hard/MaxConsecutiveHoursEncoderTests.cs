using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class MaxConsecutiveHoursEncoderTests
{
    // ────────────── Code / type assertions (post-Task 8a) ──────────────

    [Fact]
    public void Code_IsH15_AndIsHard()
    {
        var encoder = new MaxConsecutiveHoursEncoder(maxHoursPerDay: 4);

        encoder.Code.Should().Be("H15");
        encoder.IsHard.Should().BeTrue();
    }

    [Fact]
    public void Code_IsH15_NotH16()
    {
        var encoder = new MaxConsecutiveHoursEncoder();
        encoder.Code.Should().Be("H15");
    }

    [Fact]
    public void Constructor_NegativeOrZeroMaxHours_ThrowsArgumentOutOfRange()
    {
        var act = () => new MaxConsecutiveHoursEncoder(maxHoursPerDay: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ────────────── Short-circuit / early-return ──────────────

    [Fact]
    public void Encode_WhenDisabled_DoesNotAddAnyAssumption()
    {
        var encoder = new MaxConsecutiveHoursEncoder(maxHoursPerDay: 4);
        var ctx = BuildContextWithFiveConsecutiveSlots(
            new[] { Section(1, instructorId: 100, theory: 3, lab: 0) },
            flags: new Dictionary<string, bool> { ["H15"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_NoSectionsWithInstructor_NoAssumption()
    {
        var encoder = new MaxConsecutiveHoursEncoder(maxHoursPerDay: 4);
        var ctx = BuildContextWithFiveConsecutiveSlots(
            new[] { Section(1, instructorId: null, theory: 3, lab: 0) });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_EmptyTimeSlots_NoAssumption()
    {
        // Real impl returns early when TimeSlots is empty (no consecutive blocks possible).
        var encoder = new MaxConsecutiveHoursEncoder(maxHoursPerDay: 4);
        var ctx = BuildContextWithEmptyTimeSlots(
            new[]
            {
                Section(1, instructorId: 100, theory: 3, lab: 0),
                Section(2, instructorId: 100, theory: 3, lab: 0)
            });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_BlockWithinThreshold_NoAssumption()
    {
        // Block of 4 slots with maxHours=4 → block.Count > maxHours is false → no window registered.
        var encoder = new MaxConsecutiveHoursEncoder(maxHoursPerDay: 4);
        var ctx = BuildContextWithFourConsecutiveSlots(
            new[] { Section(1, instructorId: 100, theory: 3, lab: 0) });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    // ────────────── Behavior tests (Plan 5 Task 8b — real impl) ──────────────

    [Fact]
    public void Encode_InstructorWith5ConsecutiveSlots_RegistersConstraintBlockingFiveInARow()
    {
        // 1 instructor, 1 section with 5 hours theory in single day's 5 ardışık slots.
        // maxHoursPerDay = 4 → infeasible if all 5 slots covered.
        var sections = new[]
        {
            Section(1, instructorId: 5, theory: 5, lab: 0)
        };
        var encoder = new MaxConsecutiveHoursEncoder(maxHoursPerDay: 4);
        var ctx = BuildContextWithFiveConsecutiveSlots(sections);

        encoder.Encode(ctx);

        // At least 1 sliding-window assumption registered (block-size 5, window 5 → 1 window).
        ctx.Assumptions.Count.Should().BeGreaterThanOrEqualTo(1);

        // Force assumptions ON and force section presence to 1 → solver must find INFEASIBLE
        // (5h theory in 5-slot block can't fit with maxHours=4).
        foreach (var lit in ctx.Assumptions.AllLiterals)
        {
            if (lit is BoolVar bv) ctx.Model.AddAssumption(bv);
        }
        if (ctx.Variables.GetSectionPresent(1) is BoolVar present)
            ctx.Model.Add(present == 1);

        var solver = new CpSolver();
        var status = solver.Solve(ctx.Model);
        status.Should().Be(CpSolverStatus.Infeasible);
    }

    [Fact]
    public void Encode_InstructorWith4ConsecutiveSlots_NoViolation()
    {
        // 1 section with 4 hours theory in 5-slot consecutive block — fits within maxHoursPerDay=4
        // (one of the 5 slots is left free in any (maxHours+1)=5-slot window).
        var sections = new[]
        {
            Section(1, instructorId: 5, theory: 4, lab: 0)
        };
        var encoder = new MaxConsecutiveHoursEncoder(maxHoursPerDay: 4);
        var ctx = BuildContextWithFiveConsecutiveSlots(sections);

        encoder.Encode(ctx);

        if (ctx.Variables.GetSectionPresent(1) is BoolVar present)
            ctx.Model.Add(present == 1);

        var solver = new CpSolver();
        var status = solver.Solve(ctx.Model);
        status.Should().BeOneOf(new[] { CpSolverStatus.Optimal, CpSolverStatus.Feasible });
    }

    [Fact]
    public void Encode_SixFiftyMinuteSlots_FeasibleWhenClockHoursWithinLimit()
    {
        // 6 ardışık 50dk slot = 300dk = TAM 5 saat. maxHoursPerDay=5 → limit 300dk.
        // Clock-saat semantiği: 300 > 300 DEĞİL → ihlal yok → 6-slot'luk section feasible.
        // (Eski SLOT-SAYISI mantığı: 6 slot > 5-pencere → infeasible derdi. Bu test clock-saat
        // ile slot-sayısının ayrıştığı durumu kanıtlar.)
        var sections = new[] { Section(1, instructorId: 5, theory: 6, lab: 0) };
        var encoder = new MaxConsecutiveHoursEncoder(maxHoursPerDay: 5);
        var ctx = BuildContextWithSixConsecutiveSlots(sections);

        encoder.Encode(ctx);
        foreach (var lit in ctx.Assumptions.AllLiterals)
            if (lit is BoolVar bv) ctx.Model.AddAssumption(bv);
        if (ctx.Variables.GetSectionPresent(1) is BoolVar present)
            ctx.Model.Add(present == 1);

        var solver = new CpSolver();
        var status = solver.Solve(ctx.Model);
        status.Should().BeOneOf(new[] { CpSolverStatus.Optimal, CpSolverStatus.Feasible });
    }

    [Fact]
    public void Encode_SevenFiftyMinuteSlots_InfeasibleWhenClockHoursExceedLimit()
    {
        // 7 ardışık 50dk slot = 350dk > 300dk (5 saat) → clock-saat ihlali → 7-slot section infeasible.
        var sections = new[] { Section(1, instructorId: 5, theory: 7, lab: 0) };
        var encoder = new MaxConsecutiveHoursEncoder(maxHoursPerDay: 5);
        var ctx = BuildContextWithSevenConsecutiveSlots(sections);

        encoder.Encode(ctx);
        foreach (var lit in ctx.Assumptions.AllLiterals)
            if (lit is BoolVar bv) ctx.Model.AddAssumption(bv);
        if (ctx.Variables.GetSectionPresent(1) is BoolVar present)
            ctx.Model.Add(present == 1);

        var solver = new CpSolver();
        var status = solver.Solve(ctx.Model);
        status.Should().Be(CpSolverStatus.Infeasible);
    }

    // ────────────── Helpers ──────────────

    private static SectionInput Section(int id, int? instructorId, int theory, int lab) => new()
    {
        Id = id,
        CourseId = 100 + id,
        CourseCode = $"X{id}",
        CourseName = $"X{id}",
        SectionNumber = 1,
        Capacity = 30,
        TheoryHours = theory,
        LabHours = lab,
        InstructorId = instructorId,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    /// <summary>
    /// 5 ardışık slot Mon 09:00-09:50, 09:55-10:45, 10:50-11:40, 11:45-12:35, 12:40-13:30.
    /// All gaps are 5min (≤ BreakDurationMinutes default), so all slots form a single consecutive block.
    /// </summary>
    private static EncoderContext BuildContextWithFiveConsecutiveSlots(
        IReadOnlyList<SectionInput> sections,
        IReadOnlyDictionary<string, bool>? flags = null)
    {
        var slots = new List<TimeSlotInput>
        {
            new() { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1, StartTime = new(9, 0),  EndTime = new(9, 50),  AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2, StartTime = new(9, 55), EndTime = new(10, 45), AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 3, DayOfWeek = DayOfWeek.Monday, SlotOrder = 3, StartTime = new(10, 50),EndTime = new(11, 40), AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 4, DayOfWeek = DayOfWeek.Monday, SlotOrder = 4, StartTime = new(11, 45),EndTime = new(12, 35), AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 5, DayOfWeek = DayOfWeek.Monday, SlotOrder = 5, StartTime = new(12, 40),EndTime = new(13, 30), AllowedMode = TimeSlotAllowedMode.Both }
        };
        return BuildContext(sections, slots, flags);
    }

    /// <summary>
    /// 4 ardışık slot. Block size = maxHoursPerDay (4) → no sliding window registered.
    /// </summary>
    private static EncoderContext BuildContextWithFourConsecutiveSlots(
        IReadOnlyList<SectionInput> sections,
        IReadOnlyDictionary<string, bool>? flags = null)
    {
        var slots = new List<TimeSlotInput>
        {
            new() { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1, StartTime = new(9, 0),  EndTime = new(9, 50),  AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2, StartTime = new(9, 55), EndTime = new(10, 45), AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 3, DayOfWeek = DayOfWeek.Monday, SlotOrder = 3, StartTime = new(10, 50),EndTime = new(11, 40), AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 4, DayOfWeek = DayOfWeek.Monday, SlotOrder = 4, StartTime = new(11, 45),EndTime = new(12, 35), AllowedMode = TimeSlotAllowedMode.Both }
        };
        return BuildContext(sections, slots, flags);
    }

    /// <summary>6 ardışık 50dk slot (5dk gap) — tek consecutive blok, toplam 300dk = 5 saat.</summary>
    private static EncoderContext BuildContextWithSixConsecutiveSlots(
        IReadOnlyList<SectionInput> sections,
        IReadOnlyDictionary<string, bool>? flags = null)
    {
        var slots = new List<TimeSlotInput>
        {
            new() { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1, StartTime = new(9, 0),  EndTime = new(9, 50),  AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2, StartTime = new(9, 55), EndTime = new(10, 45), AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 3, DayOfWeek = DayOfWeek.Monday, SlotOrder = 3, StartTime = new(10, 50),EndTime = new(11, 40), AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 4, DayOfWeek = DayOfWeek.Monday, SlotOrder = 4, StartTime = new(11, 45),EndTime = new(12, 35), AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 5, DayOfWeek = DayOfWeek.Monday, SlotOrder = 5, StartTime = new(12, 40),EndTime = new(13, 30), AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 6, DayOfWeek = DayOfWeek.Monday, SlotOrder = 6, StartTime = new(13, 35),EndTime = new(14, 25), AllowedMode = TimeSlotAllowedMode.Both }
        };
        return BuildContext(sections, slots, flags);
    }

    /// <summary>7 ardışık 50dk slot — toplam 350dk &gt; 5 saat.</summary>
    private static EncoderContext BuildContextWithSevenConsecutiveSlots(
        IReadOnlyList<SectionInput> sections,
        IReadOnlyDictionary<string, bool>? flags = null)
    {
        var slots = new List<TimeSlotInput>
        {
            new() { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1, StartTime = new(9, 0),  EndTime = new(9, 50),  AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2, StartTime = new(9, 55), EndTime = new(10, 45), AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 3, DayOfWeek = DayOfWeek.Monday, SlotOrder = 3, StartTime = new(10, 50),EndTime = new(11, 40), AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 4, DayOfWeek = DayOfWeek.Monday, SlotOrder = 4, StartTime = new(11, 45),EndTime = new(12, 35), AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 5, DayOfWeek = DayOfWeek.Monday, SlotOrder = 5, StartTime = new(12, 40),EndTime = new(13, 30), AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 6, DayOfWeek = DayOfWeek.Monday, SlotOrder = 6, StartTime = new(13, 35),EndTime = new(14, 25), AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 7, DayOfWeek = DayOfWeek.Monday, SlotOrder = 7, StartTime = new(14, 30),EndTime = new(15, 20), AllowedMode = TimeSlotAllowedMode.Both }
        };
        return BuildContext(sections, slots, flags);
    }

    /// <summary>
    /// Empty time-slot grid — verifies real impl's early-return path.
    /// Uses totalSlots=10 for DecisionVariables so CreateIntervalsForSection can succeed without slots.
    /// </summary>
    private static EncoderContext BuildContextWithEmptyTimeSlots(
        IReadOnlyList<SectionInput> sections,
        IReadOnlyDictionary<string, bool>? flags = null)
    {
        var classrooms = new[] { new ClassroomInput { Id = 9, Name = "C", Capacity = 50, Type = ClassroomType.Lecture } };
        var request = new SolverRequest
        {
            TermId = 1, FacultyId = 1, Sections = sections, Classrooms = classrooms,
            TimeSlots = new List<TimeSlotInput>(),
            ConstraintFlags = flags ?? new Dictionary<string, bool> { ["H15"] = true }
        };

        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: 10);
        var assumptions = new AssumptionRegistry(model);

        foreach (var section in sections)
        {
            variables.CreateIntervalsForSection(section);
            if (section.TheoryHours > 0)
                variables.CreateRoomVariable(section.Id, ComponentType.Lecture, classrooms);
            if (section.LabHours > 0)
                variables.CreateRoomVariable(section.Id, ComponentType.Lab, classrooms,
                    groupCount: section.LabGroupCount);
        }

        return new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = assumptions,
            Request = request,
            ConstraintFlags = request.ConstraintFlags,
            Penalties = new List<LinearExpr>()
        };
    }

    private static EncoderContext BuildContext(
        IReadOnlyList<SectionInput> sections,
        IReadOnlyList<TimeSlotInput> slots,
        IReadOnlyDictionary<string, bool>? flags)
    {
        var classrooms = new[] { new ClassroomInput { Id = 9, Name = "C", Capacity = 50, Type = ClassroomType.Lecture } };
        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = sections,
            Classrooms = classrooms,
            TimeSlots = slots,
            ConstraintFlags = flags ?? new Dictionary<string, bool> { ["H15"] = true }
        };

        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: slots.Count);
        var assumptions = new AssumptionRegistry(model);

        foreach (var section in sections)
        {
            variables.CreateIntervalsForSection(section);
            if (section.TheoryHours > 0)
                variables.CreateRoomVariable(section.Id, ComponentType.Lecture, classrooms);
            if (section.LabHours > 0)
                variables.CreateRoomVariable(section.Id, ComponentType.Lab, classrooms,
                    groupCount: section.LabGroupCount);
        }

        return new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = assumptions,
            Request = request,
            ConstraintFlags = request.ConstraintFlags,
            Penalties = new List<LinearExpr>()
        };
    }
}
