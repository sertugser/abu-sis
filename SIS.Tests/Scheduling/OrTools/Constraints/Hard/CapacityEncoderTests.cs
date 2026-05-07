using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class CapacityEncoderTests
{
    private static SectionInput Section(int id, int theory, int lab, int capacity = 40) => new()
    {
        Id = id,
        CourseId = 100,
        CourseCode = "X",
        CourseName = "X",
        SectionNumber = 1,
        Capacity = capacity,
        TheoryHours = theory,
        LabHours = lab,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    private static EncoderContext BuildContextWithRooms(
        IReadOnlyList<SectionInput> sections,
        IReadOnlyList<ClassroomInput> classrooms,
        IReadOnlyDictionary<string, bool>? flags = null)
    {
        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: 50);
        foreach (var s in sections)
        {
            variables.CreateIntervalsForSection(s);
            if (s.TheoryHours > 0)
                variables.CreateRoomVariable(s.Id, ComponentType.Lecture, classrooms);
            if (s.LabHours > 0)
                variables.CreateRoomVariable(s.Id, ComponentType.Lab, classrooms);
        }

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
                Classrooms = classrooms,
                TimeSlots = new List<TimeSlotInput>()
            },
            ConstraintFlags = flags ?? new Dictionary<string, bool>(),
            Penalties = new List<LinearExpr>()
        };
    }

    [Fact]
    public void Code_IsH3_AndIsHard()
    {
        var encoder = new CapacityEncoder();

        encoder.Code.Should().Be("H3");
        encoder.IsHard.Should().BeTrue();
    }

    [Fact]
    public void Encode_WhenDisabled_DoesNotAddAnyAssumption()
    {
        var encoder = new CapacityEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "Small", Capacity = 30, Type = ClassroomType.Lecture }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 3, lab: 0, capacity: 40) },
            classrooms,
            flags: new Dictionary<string, bool> { ["H3"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_SectionFitsInClassroom_NoAssumption()
    {
        var encoder = new CapacityEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "Big", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 3, lab: 0, capacity: 40) },
            classrooms,
            flags: new Dictionary<string, bool> { ["H3"] = true });

        encoder.Encode(ctx);

        // 40 ≤ 50 → no constraint, no assumption
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_SectionExceedsClassroomCapacity_RegistersAssumption()
    {
        var encoder = new CapacityEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "Small", Capacity = 30, Type = ClassroomType.Lecture }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 3, lab: 0, capacity: 40) },
            classrooms,
            flags: new Dictionary<string, bool> { ["H3"] = true });

        encoder.Encode(ctx);

        // 40 > 30 → forbid section 1 in classroom 9 → 1 assumption
        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_LabSectionExceedsClassroomCapacity_RegistersAssumption()
    {
        var encoder = new CapacityEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 10, Name = "SmallLab", Capacity = 25, Type = ClassroomType.Lab }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 0, lab: 2, capacity: 40) },
            classrooms,
            flags: new Dictionary<string, bool> { ["H3"] = true });

        encoder.Encode(ctx);

        // Lab-only section 40 > 25 → forbid lab room → 1 assumption
        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_MixedSection_BothExceed_RegistersTwoAssumptions()
    {
        var encoder = new CapacityEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "Small", Capacity = 30, Type = ClassroomType.Lecture }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 3, lab: 2, capacity: 40) },
            classrooms,
            flags: new Dictionary<string, bool> { ["H3"] = true });

        encoder.Encode(ctx);

        // section has both lecture + lab; both exceed classroom capacity → 2 separate assumptions (one per component)
        ctx.Assumptions.Count.Should().Be(2);
    }
}
