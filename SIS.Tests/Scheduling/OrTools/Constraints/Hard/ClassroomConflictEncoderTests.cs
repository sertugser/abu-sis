using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class ClassroomConflictEncoderTests
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

    private static EncoderContext BuildContext(
        IReadOnlyList<SectionInput> sections,
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
                TimeSlots = new List<TimeSlotInput>()
            },
            ConstraintFlags = flags ?? new Dictionary<string, bool>(),
            Penalties = new List<LinearExpr>()
        };
    }

    [Fact]
    public void Code_IsH1_AndIsHard()
    {
        var encoder = new ClassroomConflictEncoder();

        encoder.Code.Should().Be("H1");
        encoder.IsHard.Should().BeTrue();
    }

    [Fact]
    public void Encode_WhenDisabled_DoesNotAddAnyAssumption()
    {
        var encoder = new ClassroomConflictEncoder();
        var ctx = BuildContext(
            new[] { Section(1, theory: 3, lab: 0), Section(2, theory: 3, lab: 0) },
            flags: new Dictionary<string, bool> { ["H1"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_WhenEnabled_AddsAssumptionsForLecturePairs()
    {
        var encoder = new ClassroomConflictEncoder();
        var ctx = BuildContext(
            new[] { Section(1, theory: 3, lab: 0), Section(2, theory: 3, lab: 0) },
            flags: new Dictionary<string, bool> { ["H1"] = true });

        encoder.Encode(ctx);

        // Two sections with lecture → 1 pair → at least 1 assumption literal
        ctx.Assumptions.Count.Should().BeGreaterThan(0);
    }
}
