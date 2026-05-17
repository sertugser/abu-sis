using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class LunchBreakEncoderTests
{
    private static SectionInput Section(int id, int theory = 3, int lab = 0) => new()
    {
        Id = id,
        CourseId = 100,
        CourseCode = "X",
        CourseName = "X",
        SectionNumber = 1,
        Capacity = 40,
        TheoryHours = theory,
        LabHours = lab,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    /// <summary>Builds a TimeSlot list with one slot crossing the lunch window for testing.</summary>
    private static IReadOnlyList<TimeSlotInput> BuildSlotsWithLunch()
    {
        return new List<TimeSlotInput>
        {
            new() { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2,
                StartTime = new TimeOnly(11, 40), EndTime = new TimeOnly(12, 20),
                AllowedMode = TimeSlotAllowedMode.Both },  // ← lunch window slot
            new() { Id = 3, DayOfWeek = DayOfWeek.Monday, SlotOrder = 3,
                StartTime = new TimeOnly(13, 15), EndTime = new TimeOnly(14, 5),
                AllowedMode = TimeSlotAllowedMode.Both }
        };
    }

    /// <summary>TimeSlots with NO lunch overlap.</summary>
    private static IReadOnlyList<TimeSlotInput> BuildSlotsNoLunch()
    {
        return new List<TimeSlotInput>
        {
            new() { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 2, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2,
                StartTime = new TimeOnly(13, 15), EndTime = new TimeOnly(14, 5),
                AllowedMode = TimeSlotAllowedMode.Both }
        };
    }

    private static EncoderContext BuildContext(
        IReadOnlyList<SectionInput> sections,
        IReadOnlyList<TimeSlotInput> timeSlots,
        IReadOnlyDictionary<string, bool>? flags = null)
    {
        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: Math.Max(timeSlots.Count, 1));
        foreach (var s in sections)
            variables.CreateIntervalsForSection(s);

        return new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = new SolverRequest
            {
                TermId = 1,
                FacultyId = 1,
                Sections = sections,
                Classrooms = new List<ClassroomInput>(),
                TimeSlots = timeSlots
            },
            ConstraintFlags = flags ?? new Dictionary<string, bool>(),
            Penalties = new List<LinearExpr>()
        };
    }

    [Fact]
    public void Code_IsH13_AndIsHard()
    {
        var encoder = new LunchBreakEncoder(
            lunchStart: new TimeOnly(11, 40), lunchEnd: new TimeOnly(12, 20));

        encoder.Code.Should().Be("H13");
        encoder.IsHard.Should().BeTrue();
    }

    [Fact]
    public void Encode_WhenDisabled_DoesNotAddAnyAssumption()
    {
        var encoder = new LunchBreakEncoder(
            lunchStart: new TimeOnly(11, 40), lunchEnd: new TimeOnly(12, 20));
        var ctx = BuildContext(
            new[] { Section(1) },
            BuildSlotsWithLunch(),
            flags: new Dictionary<string, bool> { ["H13"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_NoLunchSlots_NoAssumption()
    {
        var encoder = new LunchBreakEncoder(
            lunchStart: new TimeOnly(11, 40), lunchEnd: new TimeOnly(12, 20));
        var ctx = BuildContext(
            new[] { Section(1) },
            BuildSlotsNoLunch(),
            flags: new Dictionary<string, bool> { ["H13"] = true });

        encoder.Encode(ctx);

        // No slot starts in lunch window → nothing to forbid
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_OneLunchSlot_RegistersAssumption()
    {
        var encoder = new LunchBreakEncoder(
            lunchStart: new TimeOnly(11, 40), lunchEnd: new TimeOnly(12, 20));
        var ctx = BuildContext(
            new[] { Section(1) },
            BuildSlotsWithLunch(),
            flags: new Dictionary<string, bool> { ["H13"] = true });

        encoder.Encode(ctx);

        // Slot 2 (11:40 start) is in lunch window; section 1 has theory → 1 forbid + 1 assumption
        ctx.Assumptions.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Encode_AdminMovesLunchTo1145_NewWindowApplies()
    {
        // Admin changes lunch to 11:45-12:25 — slot at 11:40 is now BEFORE lunch (not in window)
        var encoder = new LunchBreakEncoder(
            lunchStart: new TimeOnly(11, 45), lunchEnd: new TimeOnly(12, 25));
        var ctx = BuildContext(
            new[] { Section(1) },
            BuildSlotsWithLunch(),  // slot 2 is at 11:40
            flags: new Dictionary<string, bool> { ["H13"] = true });

        encoder.Encode(ctx);

        // Slot at 11:40 starts BEFORE 11:45 → not in window → no forbid
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_LunchEndBeforeStart_ThrowsArgumentException()
    {
        var act = () => new LunchBreakEncoder(
            lunchStart: new TimeOnly(13, 0), lunchEnd: new TimeOnly(11, 0));
        act.Should().Throw<ArgumentException>();
    }
}
