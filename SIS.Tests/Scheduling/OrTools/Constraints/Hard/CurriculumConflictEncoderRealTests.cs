using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class CurriculumConflictEncoderRealTests
{
    [Fact]
    public void Encode_CurriculumMode_TwoSectionsSameDeptYear_AddsNoOverlap()
    {
        var sections = new[]
        {
            Section(1, courseId: 100, deptId: 1, year: 1, theory: 2, lab: 0),
            Section(2, courseId: 200, deptId: 1, year: 1, theory: 2, lab: 0)
        };
        var encoder = new CurriculumConflictEncoder();
        var ctx = BuildContext(sections, mode: "curriculum");

        encoder.Encode(ctx);

        // 1 group with 2 different-course sections → 1 NoOverlap, 1 assumption
        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_CurriculumMode_DifferentDepts_NoConstraint()
    {
        var sections = new[]
        {
            Section(1, courseId: 100, deptId: 1, year: 1, theory: 2, lab: 0),
            Section(2, courseId: 200, deptId: 2, year: 1, theory: 2, lab: 0)
        };
        var encoder = new CurriculumConflictEncoder();
        var ctx = BuildContext(sections, mode: "curriculum");

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_CurriculumMode_SameCourse_Skipped()
    {
        // Same course — H8 (SameCourseSection) handles, H5 should not duplicate.
        var sections = new[]
        {
            Section(1, courseId: 100, deptId: 1, year: 1, theory: 2, lab: 0),
            Section(2, courseId: 100, deptId: 1, year: 1, theory: 2, lab: 0)
        };
        var encoder = new CurriculumConflictEncoder();
        var ctx = BuildContext(sections, mode: "curriculum");

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0,
            because: "H5 only constrains different-course sections; H8 handles same-course");
    }

    [Fact]
    public void Encode_CurriculumMode_NullYear_Skipped()
    {
        var sections = new[]
        {
            Section(1, courseId: 100, deptId: 1, year: null, theory: 2, lab: 0),
            Section(2, courseId: 200, deptId: 1, year: 1, theory: 2, lab: 0)
        };
        var encoder = new CurriculumConflictEncoder();
        var ctx = BuildContext(sections, mode: "curriculum");

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0,
            because: "section without Year is excluded; only section 2 remains in group → no pair");
    }

    [Fact]
    public void Encode_FlagDisabled_NoOp()
    {
        var sections = new[]
        {
            Section(1, courseId: 100, deptId: 1, year: 1, theory: 2, lab: 0),
            Section(2, courseId: 200, deptId: 1, year: 1, theory: 2, lab: 0)
        };
        var encoder = new CurriculumConflictEncoder();
        var ctx = BuildContext(sections, mode: "curriculum",
            flags: new Dictionary<string, bool> { ["H5"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    private static SectionInput Section(int id, int courseId, int? deptId, int? year, int theory, int lab) => new()
    {
        Id = id,
        CourseId = courseId,
        CourseCode = $"X{id}",
        CourseName = $"X{id}",
        SectionNumber = 1,
        Capacity = 30,
        TheoryHours = theory,
        LabHours = lab,
        DepartmentId = deptId,
        Year = year,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    private static EncoderContext BuildContext(
        IReadOnlyList<SectionInput> sections,
        string mode,
        IReadOnlyDictionary<string, bool>? flags = null)
    {
        flags ??= new Dictionary<string, bool> { ["H5"] = true };

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
            CurriculumConflictMode = mode
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
