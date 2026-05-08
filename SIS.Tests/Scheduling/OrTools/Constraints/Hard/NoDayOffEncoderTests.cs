using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class NoDayOffEncoderTests
{
    [Fact]
    public void Encode_InstructorWithSections_RegistersOneAssumption()
    {
        // 5 workdays in slot grid (Mon-Fri = enum 1..5) → > 4 → constraint fires.
        // 1 instructor, 2 sections → 1 assumption.
        var sections = new[]
        {
            Section(1, instructorId: 5, theory: 2, lab: 0),
            Section(2, instructorId: 5, theory: 2, lab: 0)
        };
        var encoder = new NoDayOffEncoder();
        var ctx = BuildContext(sections);

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_NoInstructor_Skipped()
    {
        var sections = new[] { Section(1, instructorId: null, theory: 5, lab: 0) };
        var encoder = new NoDayOffEncoder();
        var ctx = BuildContext(sections);

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_FlagDisabled_NoOp()
    {
        var sections = new[] { Section(1, instructorId: 5, theory: 5, lab: 0) };
        var encoder = new NoDayOffEncoder();
        var ctx = BuildContext(sections,
            flags: new Dictionary<string, bool> { ["H-DO"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_FewerThanFiveWorkdays_TriviallyHolds()
    {
        // 3 workdays → workdays.Count <= 4 → encoder skips (constraint trivially satisfied).
        var sections = new[] { Section(1, instructorId: 5, theory: 2, lab: 0) };
        var encoder = new NoDayOffEncoder();
        var ctx = BuildContext(sections, workdayCount: 3);

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_TwoInstructorsEachWithSections_RegistersOnePerInstructor()
    {
        var sections = new[]
        {
            Section(1, instructorId: 5, theory: 2, lab: 0),
            Section(2, instructorId: 6, theory: 2, lab: 0)
        };
        var encoder = new NoDayOffEncoder();
        var ctx = BuildContext(sections);

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(2);
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

    private static EncoderContext BuildContext(
        IReadOnlyList<SectionInput> sections,
        IReadOnlyDictionary<string, bool>? flags = null,
        int workdayCount = 5)
    {
        flags ??= new Dictionary<string, bool> { ["H-DO"] = true };

        var classrooms = new[]
        {
            new ClassroomInput { Id = 9, Name = "C", Capacity = 50, Type = ClassroomType.Lecture }
        };
        // workdayCount × 4 slots per day. Days are 1..workdayCount (Mon-Fri max).
        var slotCount = workdayCount * 4;
        var slots = Enumerable.Range(0, slotCount).Select(i => new TimeSlotInput
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
