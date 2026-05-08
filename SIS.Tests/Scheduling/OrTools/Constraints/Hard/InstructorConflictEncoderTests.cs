using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class InstructorConflictEncoderTests
{
    [Fact]
    public void Encode_SameInstructorTwoSections_AddsNoOverlapAndAssumption()
    {
        var encoder = new InstructorConflictEncoder();
        var sections = new[]
        {
            Section(1, instructorId: 5, theory: 2, lab: 0),
            Section(2, instructorId: 5, theory: 2, lab: 0)
        };
        var ctx = BuildContextWithInstructors(sections);

        encoder.Encode(ctx);

        // 1 instructor with >=2 sections -> 1 NoOverlap + 1 assumption
        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_DifferentInstructors_NoConstraintAdded()
    {
        var encoder = new InstructorConflictEncoder();
        var sections = new[]
        {
            Section(1, instructorId: 5, theory: 2, lab: 0),
            Section(2, instructorId: 6, theory: 2, lab: 0)
        };
        var ctx = BuildContextWithInstructors(sections);

        encoder.Encode(ctx);

        // 2 instructors but each has only 1 section - no internal conflict possible
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_SectionWithoutInstructor_Skipped()
    {
        var encoder = new InstructorConflictEncoder();
        var sections = new[]
        {
            Section(1, instructorId: null, theory: 2, lab: 0),
            Section(2, instructorId: null, theory: 2, lab: 0)
        };
        var ctx = BuildContextWithInstructors(sections);

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_FlagDisabled_NoOp()
    {
        var encoder = new InstructorConflictEncoder();
        var sections = new[]
        {
            Section(1, instructorId: 5, theory: 2, lab: 0),
            Section(2, instructorId: 5, theory: 2, lab: 0)
        };
        var flags = new Dictionary<string, bool> { ["H2"] = false };
        var ctx = BuildContextWithInstructors(sections, flags);

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    private static SectionInput Section(int id, int? instructorId, int theory, int lab) => new()
    {
        Id = id,
        CourseId = 100 + id,
        CourseCode = $"X{id}",
        CourseName = $"X{id}",
        SectionNumber = 1,
        Capacity = 30,
        TheoryHours = theory,
        LabHours = lab,
        InstructorId = instructorId,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    private static EncoderContext BuildContextWithInstructors(
        IReadOnlyList<SectionInput> sections,
        IReadOnlyDictionary<string, bool>? flags = null)
    {
        flags ??= new Dictionary<string, bool> { ["H2"] = true };

        var classrooms = new[]
        {
            new ClassroomInput { Id = 9, Name = "C", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var slots = Enumerable.Range(0, 20).Select(i => new TimeSlotInput
        {
            Id = i + 1,
            DayOfWeek = (DayOfWeek)((i / 4) + 1),
            SlotOrder = (i % 4) + 1,
            StartTime = new TimeOnly(9 + (i % 4), 0),
            EndTime = new TimeOnly(9 + (i % 4), 50),
            AllowedMode = TimeSlotAllowedMode.Both
        }).ToList();

        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = sections,
            Classrooms = classrooms,
            TimeSlots = slots,
            ConstraintFlags = flags
        };

        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: slots.Count);
        var assumptions = new AssumptionRegistry(model);

        foreach (var section in sections)
        {
            variables.CreateIntervalsForSection(section);
            if (section.TheoryHours > 0)
                variables.CreateRoomVariable(section.Id, ComponentType.Lecture, classrooms);
            if (section.LabHours > 0)
                variables.CreateRoomVariable(section.Id, ComponentType.Lab, classrooms,
                    groupCount: section.LabGroupCount);
        }

        return new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = assumptions,
            Request = request,
            ConstraintFlags = flags,
            Penalties = new List<LinearExpr>()
        };
    }
}
