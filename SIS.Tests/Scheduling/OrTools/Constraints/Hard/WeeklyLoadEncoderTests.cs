using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class WeeklyLoadEncoderTests
{
    [Fact]
    public void Encode_InstructorOverloaded_RegistersConstraint()
    {
        // Instructor 5: 3 sections × 10 hours each = 30 total
        // maxWeekly override = 25 → constraint registered
        var sections = new[]
        {
            Section(1, instructorId: 5, theory: 5, lab: 5, maxWeekly: 25),
            Section(2, instructorId: 5, theory: 5, lab: 5, maxWeekly: 25),
            Section(3, instructorId: 5, theory: 5, lab: 5, maxWeekly: 25)
        };
        var encoder = new WeeklyLoadEncoder();
        var ctx = BuildContext(sections, defaultMaxWeekly: 30);

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_InstructorMaxHoursZero_FallsBackToDefault()
    {
        var sections = new[]
        {
            Section(1, instructorId: 5, theory: 5, lab: 5, maxWeekly: 0),
            Section(2, instructorId: 5, theory: 5, lab: 5, maxWeekly: 0),
            Section(3, instructorId: 5, theory: 5, lab: 5, maxWeekly: 0)
        };
        var encoder = new WeeklyLoadEncoder();
        var ctx = BuildContext(sections, defaultMaxWeekly: 20);

        encoder.Encode(ctx);

        // Default 20 → constraint registered (3 sections × 10h = 30 > 20)
        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_DefaultZeroAndOverrideZero_ConstraintDisabled()
    {
        var sections = new[]
        {
            Section(1, instructorId: 5, theory: 5, lab: 5, maxWeekly: 0)
        };
        var encoder = new WeeklyLoadEncoder();
        var ctx = BuildContext(sections, defaultMaxWeekly: 0);

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_NoInstructor_Skipped()
    {
        var sections = new[]
        {
            Section(1, instructorId: null, theory: 10, lab: 10, maxWeekly: 0)
        };
        var encoder = new WeeklyLoadEncoder();
        var ctx = BuildContext(sections, defaultMaxWeekly: 5);

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_FlagDisabled_NoOp()
    {
        var sections = new[]
        {
            Section(1, instructorId: 5, theory: 5, lab: 5, maxWeekly: 1)
        };
        var encoder = new WeeklyLoadEncoder();
        var ctx = BuildContext(sections, defaultMaxWeekly: 30,
            flags: new Dictionary<string, bool> { ["H-WL"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_SectionZeroHours_SkipsSection()
    {
        // Section with no theory and no lab hours contributes nothing to the sum.
        // terms list ends empty → constraint not registered for this instructor.
        var sections = new[]
        {
            Section(1, instructorId: 5, theory: 0, lab: 0, maxWeekly: 25)
        };
        var encoder = new WeeklyLoadEncoder();
        var ctx = BuildContext(sections, defaultMaxWeekly: 30);

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    private static SectionInput Section(int id, int? instructorId, int theory, int lab, int maxWeekly) => new()
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
        InstructorMaxWeeklyHours = maxWeekly,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    private static EncoderContext BuildContext(
        IReadOnlyList<SectionInput> sections,
        int defaultMaxWeekly,
        IReadOnlyDictionary<string, bool>? flags = null)
    {
        flags ??= new Dictionary<string, bool> { ["H-WL"] = true };

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
            ConstraintFlags = flags,
            DefaultInstructorMaxWeeklyHours = defaultMaxWeekly
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
