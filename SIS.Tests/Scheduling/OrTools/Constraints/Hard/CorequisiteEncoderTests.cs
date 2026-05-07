using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class CorequisiteEncoderTests
{
    private static SectionInput Section(int id, int courseId = 100) => new()
    {
        Id = id,
        CourseId = courseId,
        CourseCode = $"C{courseId}",
        CourseName = "X",
        SectionNumber = 1,
        Capacity = 40,
        TheoryHours = 3,
        LabHours = 0,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    private static EncoderContext BuildContext(
        IReadOnlyList<SectionInput> sections,
        IReadOnlyList<CourseRelationship> corequisites,
        IReadOnlyDictionary<string, bool>? flags = null)
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
                TimeSlots = new List<TimeSlotInput>(),
                Corequisites = corequisites
            },
            ConstraintFlags = flags ?? new Dictionary<string, bool>(),
            Penalties = new List<LinearExpr>()
        };
    }

    [Fact]
    public void Code_IsH11_AndIsHard()
    {
        var encoder = new CorequisiteEncoder();

        encoder.Code.Should().Be("H11");
        encoder.IsHard.Should().BeTrue();
    }

    [Fact]
    public void Encode_WhenDisabled_DoesNotAddAnyAssumption()
    {
        var encoder = new CorequisiteEncoder();
        var ctx = BuildContext(
            new[] { Section(1) },
            new[] { new CourseRelationship(100, 200) },
            flags: new Dictionary<string, bool> { ["H11"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_NoCorequisites_NoAssumption()
    {
        var encoder = new CorequisiteEncoder();
        var ctx = BuildContext(
            new[] { Section(1) },
            corequisites: Array.Empty<CourseRelationship>(),
            flags: new Dictionary<string, bool> { ["H11"] = true });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_OneCorequisite_RegistersOneAssumption()
    {
        var encoder = new CorequisiteEncoder();
        var ctx = BuildContext(
            new[] { Section(1) },
            new[] { new CourseRelationship(100, 200) },
            flags: new Dictionary<string, bool> { ["H11"] = true });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_ThreeCorequisites_RegistersThreeAssumptions()
    {
        var encoder = new CorequisiteEncoder();
        var ctx = BuildContext(
            new[] { Section(1) },
            new[]
            {
                new CourseRelationship(100, 200),
                new CourseRelationship(100, 300),
                new CourseRelationship(400, 500)
            },
            flags: new Dictionary<string, bool> { ["H11"] = true });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(3);
    }
}
