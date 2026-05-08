using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class TheoryLabDaySplitEncoderTests
{
    private static SectionInput Section(int id, int theory, int lab) => new()
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

    private static IReadOnlyList<TimeSlotInput> StandardWeek()
    {
        var slots = new List<TimeSlotInput>();
        var id = 1;
        // 5 days × 10 slots/day = 50 slots
        for (var day = DayOfWeek.Monday; day <= DayOfWeek.Friday; day++)
        {
            for (var s = 0; s < 10; s++)
            {
                slots.Add(new TimeSlotInput
                {
                    Id = id++,
                    DayOfWeek = day,
                    SlotOrder = s + 1,
                    StartTime = new TimeOnly(9, 0).AddMinutes(s * 50),
                    EndTime = new TimeOnly(9, 50).AddMinutes(s * 50),
                    AllowedMode = TimeSlotAllowedMode.Both
                });
            }
        }
        return slots;
    }

    private static EncoderContext BuildContext(
        IReadOnlyList<SectionInput> sections,
        IReadOnlyDictionary<string, bool>? flags = null)
    {
        var timeSlots = StandardWeek();
        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: timeSlots.Count);
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
    public void Code_IsH16_AndIsHard()
    {
        var encoder = new TheoryLabDaySplitEncoder(splitThreshold: 4);

        encoder.Code.Should().Be("H16");
        encoder.IsHard.Should().BeTrue();
    }

    [Fact]
    public void Code_IsH16_NotH12()
    {
        var encoder = new TheoryLabDaySplitEncoder();
        encoder.Code.Should().Be("H16");
    }

    [Fact]
    public void Encode_WhenDisabled_DoesNotAddAnyAssumption()
    {
        var encoder = new TheoryLabDaySplitEncoder(splitThreshold: 4);
        var ctx = BuildContext(
            new[] { Section(1, theory: 3, lab: 2) },
            flags: new Dictionary<string, bool> { ["H16"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_SectionAtOrBelowThreshold_NoAssumption()
    {
        var encoder = new TheoryLabDaySplitEncoder(splitThreshold: 4);
        var ctx = BuildContext(
            new[] { Section(1, theory: 2, lab: 2) },  // total 4 = threshold (NOT greater)
            flags: new Dictionary<string, bool> { ["H16"] = true });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_SectionAboveThreshold_RegistersAssumption()
    {
        var encoder = new TheoryLabDaySplitEncoder(splitThreshold: 4);
        var ctx = BuildContext(
            new[] { Section(1, theory: 3, lab: 2) },  // total 5 > 4
            flags: new Dictionary<string, bool> { ["H16"] = true });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_LectureOnlySection_NoAssumption()
    {
        var encoder = new TheoryLabDaySplitEncoder(splitThreshold: 4);
        var ctx = BuildContext(
            new[] { Section(1, theory: 5, lab: 0) },
            flags: new Dictionary<string, bool> { ["H16"] = true });

        encoder.Encode(ctx);

        // No lab → no day-split needed
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_LabOnlySection_NoAssumption()
    {
        var encoder = new TheoryLabDaySplitEncoder(splitThreshold: 4);
        var ctx = BuildContext(
            new[] { Section(1, theory: 0, lab: 5) },
            flags: new Dictionary<string, bool> { ["H16"] = true });

        encoder.Encode(ctx);

        // No theory → no day-split needed
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_MultipleSections_OnlyAboveThresholdRegister()
    {
        var encoder = new TheoryLabDaySplitEncoder(splitThreshold: 4);
        var ctx = BuildContext(
            new[]
            {
                Section(1, theory: 3, lab: 2),  // 5 > 4 → 1 assumption
                Section(2, theory: 2, lab: 2),  // 4, not > 4 → 0
                Section(3, theory: 4, lab: 2),  // 6 > 4 → 1 assumption
                Section(4, theory: 1, lab: 1),  // 2 < 4 → 0
                Section(5, theory: 5, lab: 0)   // no lab → 0
            },
            flags: new Dictionary<string, bool> { ["H16"] = true });

        encoder.Encode(ctx);

        // Only sections 1 and 3 get assumptions
        ctx.Assumptions.Count.Should().Be(2);
    }

    [Fact]
    public void Constructor_NegativeOrZeroThreshold_ThrowsArgumentOutOfRange()
    {
        var act = () => new TheoryLabDaySplitEncoder(splitThreshold: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
