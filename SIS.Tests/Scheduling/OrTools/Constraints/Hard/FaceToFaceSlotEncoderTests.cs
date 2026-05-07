using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class FaceToFaceSlotEncoderTests
{
    private static SectionInput Section(int id, TeachingMethod method, int theory = 3, int lab = 0) => new()
    {
        Id = id,
        CourseId = 100,
        CourseCode = "X",
        CourseName = "X",
        SectionNumber = 1,
        Capacity = 40,
        TheoryHours = theory,
        LabHours = lab,
        TeachingMethod = method
    };

    private static IReadOnlyList<TimeSlotInput> SlotsWithOnlineOnly()
    {
        return new List<TimeSlotInput>
        {
            new() { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both },
            new() { Id = 2, DayOfWeek = DayOfWeek.Saturday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.OnlineOnly },  // weekend = online only
            new() { Id = 3, DayOfWeek = DayOfWeek.Monday, SlotOrder = 2,
                StartTime = new TimeOnly(20, 10), EndTime = new TimeOnly(21, 0),
                AllowedMode = TimeSlotAllowedMode.OnlineOnly }  // evening = online only
        };
    }

    private static IReadOnlyList<TimeSlotInput> SlotsAllBoth()
    {
        return new List<TimeSlotInput>
        {
            new() { Id = 1, DayOfWeek = DayOfWeek.Monday, SlotOrder = 1,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50),
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
    public void Code_IsH17_AndIsHard()
    {
        var encoder = new FaceToFaceSlotEncoder();

        encoder.Code.Should().Be("H17");
        encoder.IsHard.Should().BeTrue();
    }

    [Fact]
    public void Encode_WhenDisabled_DoesNotAddAnyAssumption()
    {
        var encoder = new FaceToFaceSlotEncoder();
        var ctx = BuildContext(
            new[] { Section(1, TeachingMethod.FaceToFace) },
            SlotsWithOnlineOnly(),
            flags: new Dictionary<string, bool> { ["H17"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_NoOnlineOnlySlots_NoAssumption()
    {
        var encoder = new FaceToFaceSlotEncoder();
        var ctx = BuildContext(
            new[] { Section(1, TeachingMethod.FaceToFace) },
            SlotsAllBoth(),
            flags: new Dictionary<string, bool> { ["H17"] = true });

        encoder.Encode(ctx);

        // No OnlineOnly slots in grid → nothing to forbid
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_FaceToFaceSection_WithOnlineOnlySlots_RegistersAssumptions()
    {
        var encoder = new FaceToFaceSlotEncoder();
        var ctx = BuildContext(
            new[] { Section(1, TeachingMethod.FaceToFace) },
            SlotsWithOnlineOnly(),  // 2 OnlineOnly slots (indices 1 and 2)
            flags: new Dictionary<string, bool> { ["H17"] = true });

        encoder.Encode(ctx);

        // 1 F2F section × 2 forbidden slots = 2 assumptions for lecture component
        ctx.Assumptions.Count.Should().Be(2);
    }

    [Fact]
    public void Encode_OnlineSection_NotConstrained()
    {
        var encoder = new FaceToFaceSlotEncoder();
        var ctx = BuildContext(
            new[] { Section(1, TeachingMethod.Online) },
            SlotsWithOnlineOnly(),
            flags: new Dictionary<string, bool> { ["H17"] = true });

        encoder.Encode(ctx);

        // Online section CAN use OnlineOnly slots → no constraint
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_HybridSection_NotConstrained()
    {
        var encoder = new FaceToFaceSlotEncoder();
        var ctx = BuildContext(
            new[] { Section(1, TeachingMethod.Hybrid) },
            SlotsWithOnlineOnly(),
            flags: new Dictionary<string, bool> { ["H17"] = true });

        encoder.Encode(ctx);

        // Hybrid section: H17 only constrains pure F2F. (Hybrid policy may differ in v1; v2 takes literal interpretation.)
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_F2FSection_WithLab_ConstrainsBothComponents()
    {
        var encoder = new FaceToFaceSlotEncoder();
        var ctx = BuildContext(
            new[] { Section(1, TeachingMethod.FaceToFace, theory: 3, lab: 2) },
            SlotsWithOnlineOnly(),  // 2 OnlineOnly slots
            flags: new Dictionary<string, bool> { ["H17"] = true });

        encoder.Encode(ctx);

        // Lecture × 2 forbidden + Lab × 2 forbidden = 4 assumptions
        ctx.Assumptions.Count.Should().Be(4);
    }

    [Fact]
    public void Encode_MultipleSectionsMixedMethods_OnlyF2FConstrained()
    {
        var encoder = new FaceToFaceSlotEncoder();
        var ctx = BuildContext(
            new[]
            {
                Section(1, TeachingMethod.FaceToFace),  // 2 assumptions
                Section(2, TeachingMethod.Online),       // 0
                Section(3, TeachingMethod.FaceToFace),  // 2 assumptions
                Section(4, TeachingMethod.Hybrid)        // 0
            },
            SlotsWithOnlineOnly(),
            flags: new Dictionary<string, bool> { ["H17"] = true });

        encoder.Encode(ctx);

        // 2 F2F sections × 2 OnlineOnly slots = 4 assumptions
        ctx.Assumptions.Count.Should().Be(4);
    }
}
