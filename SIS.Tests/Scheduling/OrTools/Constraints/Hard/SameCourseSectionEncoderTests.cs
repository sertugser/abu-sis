using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class SameCourseSectionEncoderTests
{
    private static SectionInput Section(int id, int courseId, int theory, int lab, int sectionNumber = 1) => new()
    {
        Id = id,
        CourseId = courseId,
        CourseCode = $"C{courseId}",
        CourseName = "X",
        SectionNumber = sectionNumber,
        Capacity = 40,
        TheoryHours = theory,
        LabHours = lab,
        TeachingMethod = TeachingMethod.FaceToFace
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
    public void Code_IsH8_AndIsHard()
    {
        var encoder = new SameCourseSectionEncoder();

        encoder.Code.Should().Be("H8");
        encoder.IsHard.Should().BeTrue();
    }

    [Fact]
    public void Encode_WhenDisabled_DoesNotAddAnyAssumption()
    {
        var encoder = new SameCourseSectionEncoder();
        var ctx = BuildContext(
            new[]
            {
                Section(1, courseId: 100, theory: 3, lab: 0, sectionNumber: 1),
                Section(2, courseId: 100, theory: 3, lab: 0, sectionNumber: 2)
            },
            flags: new Dictionary<string, bool> { ["H8"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_TwoSectionsSameCourse_RegistersOnePairAssumption()
    {
        var encoder = new SameCourseSectionEncoder();
        var ctx = BuildContext(
            new[]
            {
                Section(1, courseId: 100, theory: 3, lab: 0, sectionNumber: 1),
                Section(2, courseId: 100, theory: 3, lab: 0, sectionNumber: 2)
            },
            flags: new Dictionary<string, bool> { ["H8"] = true });

        encoder.Encode(ctx);

        // 1 group of 2 lecture sections → C(2,2) = 1 pair assumption
        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_TwoSectionsDifferentCourses_NoAssumption()
    {
        var encoder = new SameCourseSectionEncoder();
        var ctx = BuildContext(
            new[]
            {
                Section(1, courseId: 100, theory: 3, lab: 0, sectionNumber: 1),
                Section(2, courseId: 200, theory: 3, lab: 0, sectionNumber: 1)
            },
            flags: new Dictionary<string, bool> { ["H8"] = true });

        encoder.Encode(ctx);

        // Different courses → no within-course grouping
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_FourSectionsSameCourse_Run303Scenario_RegistersSixPairAssumptions()
    {
        // Run 303 reproduction: 4 CENG101 sections, lecture-only setup
        var encoder = new SameCourseSectionEncoder();
        var ctx = BuildContext(
            new[]
            {
                Section(1, courseId: 100, theory: 3, lab: 0, sectionNumber: 1),
                Section(2, courseId: 100, theory: 3, lab: 0, sectionNumber: 2),
                Section(3, courseId: 100, theory: 3, lab: 0, sectionNumber: 3),
                Section(4, courseId: 100, theory: 3, lab: 0, sectionNumber: 4)
            },
            flags: new Dictionary<string, bool> { ["H8"] = true });

        encoder.Encode(ctx);

        // C(4,2) = 6 pairs → 6 assumptions for fine-grained UNSAT core
        ctx.Assumptions.Count.Should().Be(6);
    }

    [Fact]
    public void Encode_MixedComponents_RegistersPairsForLectureAndLab()
    {
        // 3 sections same course, all mixed (theory + lab)
        // Lecture group: C(3,2) = 3 pairs
        // Lab group: C(3,2) = 3 pairs
        // Total: 6 assumptions
        var encoder = new SameCourseSectionEncoder();
        var ctx = BuildContext(
            new[]
            {
                Section(1, courseId: 100, theory: 3, lab: 2, sectionNumber: 1),
                Section(2, courseId: 100, theory: 3, lab: 2, sectionNumber: 2),
                Section(3, courseId: 100, theory: 3, lab: 2, sectionNumber: 3)
            },
            flags: new Dictionary<string, bool> { ["H8"] = true });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(6);
    }

    [Fact]
    public void Encode_SingleSectionPerCourse_NoAssumption()
    {
        var encoder = new SameCourseSectionEncoder();
        var ctx = BuildContext(
            new[]
            {
                Section(1, courseId: 100, theory: 3, lab: 0, sectionNumber: 1),
                Section(2, courseId: 200, theory: 3, lab: 0, sectionNumber: 1),
                Section(3, courseId: 300, theory: 3, lab: 0, sectionNumber: 1)
            },
            flags: new Dictionary<string, bool> { ["H8"] = true });

        encoder.Encode(ctx);

        // Each course has only 1 section → no within-course pair
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_MixedCourseGroups_HandlesEachIndependently()
    {
        // Course 100: 3 sections (3 lecture pairs)
        // Course 200: 2 sections (1 lecture pair)
        // Course 300: 1 section (no pair)
        // Total: 4 assumptions
        var encoder = new SameCourseSectionEncoder();
        var ctx = BuildContext(
            new[]
            {
                Section(1, courseId: 100, theory: 3, lab: 0, sectionNumber: 1),
                Section(2, courseId: 100, theory: 3, lab: 0, sectionNumber: 2),
                Section(3, courseId: 100, theory: 3, lab: 0, sectionNumber: 3),
                Section(4, courseId: 200, theory: 3, lab: 0, sectionNumber: 1),
                Section(5, courseId: 200, theory: 3, lab: 0, sectionNumber: 2),
                Section(6, courseId: 300, theory: 3, lab: 0, sectionNumber: 1)
            },
            flags: new Dictionary<string, bool> { ["H8"] = true });

        encoder.Encode(ctx);

        // C(3,2) + C(2,2) + C(1,2)=0 = 3 + 1 + 0 = 4
        ctx.Assumptions.Count.Should().Be(4);
    }
}
