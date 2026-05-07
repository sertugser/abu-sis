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
    private static SectionInput Section(int id, int? instructorId = null) => new()
    {
        Id = id,
        CourseId = 100,
        CourseCode = "X",
        CourseName = "X",
        SectionNumber = 1,
        Capacity = 40,
        TheoryHours = 3,
        LabHours = 0,
        TeachingMethod = TeachingMethod.FaceToFace,
        InstructorId = instructorId
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
    public void Code_IsH16_AndIsHard()
    {
        var encoder = new MaxConsecutiveHoursEncoder(maxHoursPerDay: 4);

        encoder.Code.Should().Be("H16");
        encoder.IsHard.Should().BeTrue();
    }

    [Fact]
    public void Encode_WhenDisabled_DoesNotAddAnyAssumption()
    {
        var encoder = new MaxConsecutiveHoursEncoder(maxHoursPerDay: 4);
        var ctx = BuildContext(
            new[] { Section(1, instructorId: 100) },
            flags: new Dictionary<string, bool> { ["H16"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_NoSectionsWithInstructor_NoAssumption()
    {
        var encoder = new MaxConsecutiveHoursEncoder(maxHoursPerDay: 4);
        var ctx = BuildContext(
            new[] { Section(1, instructorId: null) },  // no instructor assigned
            flags: new Dictionary<string, bool> { ["H16"] = true });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_OneInstructorMultipleSections_RegistersOneAssumption()
    {
        var encoder = new MaxConsecutiveHoursEncoder(maxHoursPerDay: 4);
        var ctx = BuildContext(
            new[]
            {
                Section(1, instructorId: 100),
                Section(2, instructorId: 100),
                Section(3, instructorId: 100)
            },
            flags: new Dictionary<string, bool> { ["H16"] = true });

        encoder.Encode(ctx);

        // 1 instructor → 1 per-instructor diagnostic assumption
        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_ThreeInstructors_RegistersThreeAssumptions()
    {
        var encoder = new MaxConsecutiveHoursEncoder(maxHoursPerDay: 4);
        var ctx = BuildContext(
            new[]
            {
                Section(1, instructorId: 100),
                Section(2, instructorId: 200),
                Section(3, instructorId: 300)
            },
            flags: new Dictionary<string, bool> { ["H16"] = true });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(3);
    }

    [Fact]
    public void Encode_MixOfInstructorAndNoInstructor_OnlyInstructorsCount()
    {
        var encoder = new MaxConsecutiveHoursEncoder(maxHoursPerDay: 4);
        var ctx = BuildContext(
            new[]
            {
                Section(1, instructorId: 100),
                Section(2, instructorId: null),
                Section(3, instructorId: 100),
                Section(4, instructorId: 200)
            },
            flags: new Dictionary<string, bool> { ["H16"] = true });

        encoder.Encode(ctx);

        // 2 distinct instructors (100, 200) → 2 assumptions; section 2 (no instructor) skipped
        ctx.Assumptions.Count.Should().Be(2);
    }

    [Fact]
    public void Constructor_NegativeOrZeroMaxHours_ThrowsArgumentOutOfRange()
    {
        var act = () => new MaxConsecutiveHoursEncoder(maxHoursPerDay: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
