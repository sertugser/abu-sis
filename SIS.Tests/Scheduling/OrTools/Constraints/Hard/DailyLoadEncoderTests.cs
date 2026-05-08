using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class DailyLoadEncoderTests
{
    [Fact]
    public void Encode_InstructorWithSections_RegistersAssumptionPerDayPerInstructor()
    {
        // 1 instructor, 2 sections (theory only) — encoder registers (instructor, day) assumptions
        // for each distinct workday in the slot grid (Mon-Fri = enum values 1..5).
        var sections = new[]
        {
            Section(1, instructorId: 5, theory: 3, lab: 0),
            Section(2, instructorId: 5, theory: 4, lab: 0)
        };
        var encoder = new DailyLoadEncoder(maxDailyHours: 9);
        var ctx = BuildContext(sections);

        encoder.Encode(ctx);

        // 1 instructor × 5 weekdays = 5 assumptions
        ctx.Assumptions.Count.Should().Be(5);
    }

    [Fact]
    public void Encode_NoInstructor_NoConstraint()
    {
        var sections = new[] { Section(1, instructorId: null, theory: 5, lab: 0) };
        var encoder = new DailyLoadEncoder(maxDailyHours: 9);
        var ctx = BuildContext(sections);

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_FlagDisabled_NoOp()
    {
        var sections = new[] { Section(1, instructorId: 5, theory: 5, lab: 0) };
        var encoder = new DailyLoadEncoder(maxDailyHours: 9);
        var ctx = BuildContext(sections,
            flags: new Dictionary<string, bool> { ["H-DL"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_TwoInstructorsEachWithSections_RegistersPerInstructorPerDay()
    {
        var sections = new[]
        {
            Section(1, instructorId: 5, theory: 3, lab: 0),
            Section(2, instructorId: 6, theory: 3, lab: 0)
        };
        var encoder = new DailyLoadEncoder(maxDailyHours: 9);
        var ctx = BuildContext(sections);

        encoder.Encode(ctx);

        // 2 instructors × 5 days = 10 assumptions
        ctx.Assumptions.Count.Should().Be(10);
    }

    [Fact]
    public void Constructor_ZeroOrNegativeMax_Throws()
    {
        Action zero = () => new DailyLoadEncoder(maxDailyHours: 0);
        Action negative = () => new DailyLoadEncoder(maxDailyHours: -1);

        zero.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
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
        IReadOnlyDictionary<string, bool>? flags = null)
    {
        flags ??= new Dictionary<string, bool> { ["H-DL"] = true };

        var classrooms = new[]
        {
            new ClassroomInput { Id = 9, Name = "C", Capacity = 50, Type = ClassroomType.Lecture }
        };
        // 5 days × 4 slots = 20 slots, days 1..5 (Mon-Fri)
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
