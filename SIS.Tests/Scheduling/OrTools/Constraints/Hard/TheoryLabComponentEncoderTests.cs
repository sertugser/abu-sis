using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class TheoryLabComponentEncoderTests
{
    private static SectionInput Section(int id, int theory, int lab) => new()
    {
        Id = id,
        CourseId = 100 + id,
        CourseCode = $"X{id}",
        CourseName = "X",
        SectionNumber = 1,
        Capacity = 40,
        TheoryHours = theory,
        LabHours = lab,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    private static EncoderContext BuildContext(IReadOnlyList<SectionInput> sections, IReadOnlyDictionary<string, bool>? flags = null)
    {
        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: 50);
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
                TimeSlots = new List<TimeSlotInput>()
            },
            ConstraintFlags = flags ?? new Dictionary<string, bool>(),
            Penalties = new List<LinearExpr>()
        };
    }

    [Fact]
    public void Code_IsH6_AndIsHard()
    {
        var encoder = new TheoryLabComponentEncoder();

        encoder.Code.Should().Be("H6");
        encoder.IsHard.Should().BeTrue();
    }

    [Fact]
    public void Encode_WhenDisabled_DoesNotAddAnyAssumption()
    {
        var encoder = new TheoryLabComponentEncoder();
        var ctx = BuildContext(
            new[] { Section(1, theory: 3, lab: 2) },
            flags: new Dictionary<string, bool> { ["H6"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_MixedSection_RegistersOneAssumption()
    {
        var encoder = new TheoryLabComponentEncoder();
        var ctx = BuildContext(
            new[] { Section(1, theory: 3, lab: 2) },
            flags: new Dictionary<string, bool> { ["H6"] = true });

        encoder.Encode(ctx);

        // 1 mixed section → 1 per-section assumption
        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_LectureOnlySection_NoAssumption()
    {
        var encoder = new TheoryLabComponentEncoder();
        var ctx = BuildContext(
            new[] { Section(1, theory: 3, lab: 0) },
            flags: new Dictionary<string, bool> { ["H6"] = true });

        encoder.Encode(ctx);

        // No lab → no theory-lab overlap to constrain
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_LabOnlySection_NoAssumption()
    {
        var encoder = new TheoryLabComponentEncoder();
        var ctx = BuildContext(
            new[] { Section(1, theory: 0, lab: 2) },
            flags: new Dictionary<string, bool> { ["H6"] = true });

        encoder.Encode(ctx);

        // No theory → no theory-lab overlap to constrain
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_ThreeMixedSections_RegistersThreeAssumptions()
    {
        var encoder = new TheoryLabComponentEncoder();
        var ctx = BuildContext(
            new[]
            {
                Section(1, theory: 3, lab: 2),
                Section(2, theory: 2, lab: 2),
                Section(3, theory: 4, lab: 1)
            },
            flags: new Dictionary<string, bool> { ["H6"] = true });

        encoder.Encode(ctx);

        // 3 mixed sections → 3 per-section assumptions
        ctx.Assumptions.Count.Should().Be(3);
    }

    [Fact]
    public void Encode_MixedAndNonMixedSections_OnlyMixedRegisterAssumption()
    {
        var encoder = new TheoryLabComponentEncoder();
        var ctx = BuildContext(
            new[]
            {
                Section(1, theory: 3, lab: 2),  // mixed → 1 assumption
                Section(2, theory: 3, lab: 0),  // lecture-only → skipped
                Section(3, theory: 0, lab: 2),  // lab-only → skipped
                Section(4, theory: 4, lab: 2)   // mixed → 1 assumption
            },
            flags: new Dictionary<string, bool> { ["H6"] = true });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(2);
    }
}
