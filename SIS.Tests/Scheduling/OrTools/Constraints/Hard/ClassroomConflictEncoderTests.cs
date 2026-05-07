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
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 3, lab: 0), Section(2, theory: 3, lab: 0) },
            classrooms,
            flags: new Dictionary<string, bool> { ["H1"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_TwoSectionsSameClassroomCandidate_RegistersClassroomAssumption()
    {
        var encoder = new ClassroomConflictEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 3, lab: 0), Section(2, theory: 3, lab: 0) },
            classrooms,
            flags: new Dictionary<string, bool> { ["H1"] = true });

        encoder.Encode(ctx);

        // 1 classroom with 2 sections → 1 classroom-level assumption registered
        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_TwoSectionsTwoClassrooms_RegistersOneAssumptionPerClassroom()
    {
        var encoder = new ClassroomConflictEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture },
            new() { Id = 10, Name = "C 006", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 3, lab: 0), Section(2, theory: 3, lab: 0) },
            classrooms,
            flags: new Dictionary<string, bool> { ["H1"] = true });

        encoder.Encode(ctx);

        // 2 classrooms, each candidates for both sections → 2 classroom-level assumptions
        ctx.Assumptions.Count.Should().Be(2);
    }

    [Fact]
    public void Encode_SingleSection_AddsNoAssumption()
    {
        // No conflict possible with just 1 section
        var encoder = new ClassroomConflictEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 3, lab: 0) },
            classrooms,
            flags: new Dictionary<string, bool> { ["H1"] = true });

        encoder.Encode(ctx);

        // 1 classroom × 1 section interval = only 1 interval per classroom; AddNoOverlap not added (Count < 2)
        ctx.Assumptions.Count.Should().Be(0);
    }
}
