using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class ClassroomTypeEncoderTests
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
    public void Code_IsH4_AndIsHard()
    {
        var encoder = new ClassroomTypeEncoder();

        encoder.Code.Should().Be("H4");
        encoder.IsHard.Should().BeTrue();
    }

    [Fact]
    public void Encode_WhenDisabled_DoesNotAddAnyAssumption()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "Lecture", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 0, lab: 2) },  // lab-only section
            classrooms,
            flags: new Dictionary<string, bool> { ["H4"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_LabOnlySectionInLectureRoom_RegistersAssumption()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 0, lab: 2) },
            classrooms,
            flags: new Dictionary<string, bool> { ["H4"] = true });

        encoder.Encode(ctx);

        // Lab section forbidden from Lecture room → 1 assumption
        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_LabOnlySectionInLabRoom_NoAssumption()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 10, Name = "L139", Capacity = 30, Type = ClassroomType.Lab }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 0, lab: 2) },
            classrooms,
            flags: new Dictionary<string, bool> { ["H4"] = true });

        encoder.Encode(ctx);

        // Lab section in Lab room → OK, no assumption
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_LabOnlySectionInComputerRoom_NoAssumption()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 11, Name = "PC Lab", Capacity = 30, Type = ClassroomType.Computer }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 0, lab: 2) },
            classrooms,
            flags: new Dictionary<string, bool> { ["H4"] = true });

        encoder.Encode(ctx);

        // Computer room qualifies → OK
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_MixedSection_LabComponentInLectureRoom_RegistersAssumption()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 3, lab: 2) },  // mixed
            classrooms,
            flags: new Dictionary<string, bool> { ["H4"] = true });

        encoder.Encode(ctx);

        // Lab component is constrained; lecture component is not. 1 assumption (lab→lecture room forbidden).
        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_LectureOnlySection_NoAssumptionRegardlessOfRoomType()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture },
            new() { Id = 10, Name = "L139", Capacity = 30, Type = ClassroomType.Lab },
            new() { Id = 11, Name = "PC Lab", Capacity = 30, Type = ClassroomType.Computer }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 3, lab: 0) },  // lecture-only
            classrooms,
            flags: new Dictionary<string, bool> { ["H4"] = true });

        encoder.Encode(ctx);

        // Lecture-only sections have no type restriction → 0 assumptions
        ctx.Assumptions.Count.Should().Be(0);
    }
}
