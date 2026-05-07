using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class FacultyClassroomEncoderTests
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
    public void Code_IsH10_AndIsHard()
    {
        var encoder = new FacultyClassroomEncoder();

        encoder.Code.Should().Be("H10");
        encoder.IsHard.Should().BeTrue();
    }

    [Fact]
    public void Encode_WhenDisabled_DoesNotAddAnyAssumption()
    {
        var encoder = new FacultyClassroomEncoder();
        var ctx = BuildContext(
            new[] { Section(1) },
            flags: new Dictionary<string, bool> { ["H10"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_OneSection_RegistersOneAssumption()
    {
        var encoder = new FacultyClassroomEncoder();
        var ctx = BuildContext(
            new[] { Section(1) },
            flags: new Dictionary<string, bool> { ["H10"] = true });

        encoder.Encode(ctx);

        // 1 section → 1 diagnostic assumption (per-section marker)
        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_ThreeSections_RegistersThreeAssumptions()
    {
        var encoder = new FacultyClassroomEncoder();
        var ctx = BuildContext(
            new[] { Section(1), Section(2), Section(3) },
            flags: new Dictionary<string, bool> { ["H10"] = true });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(3);
    }

    [Fact]
    public void Encode_NoSections_NoAssumption()
    {
        var encoder = new FacultyClassroomEncoder();
        var ctx = BuildContext(
            Array.Empty<SectionInput>(),
            flags: new Dictionary<string, bool> { ["H10"] = true });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }
}
