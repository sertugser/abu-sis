using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class CurriculumConflictEncoderTests
{
    private static SectionInput Section(int id, int theory, int lab, int? programId = null, int? year = null) => new()
    {
        Id = id,
        CourseId = 100 + id,
        CourseCode = $"X{id}",
        CourseName = "X",
        SectionNumber = 1,
        Capacity = 40,
        TheoryHours = theory,
        LabHours = lab,
        TeachingMethod = TeachingMethod.FaceToFace,
        ProgramId = programId,
        Year = year
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
    public void Code_IsH5_AndIsHard()
    {
        var encoder = new CurriculumConflictEncoder();

        encoder.Code.Should().Be("H5");
        encoder.IsHard.Should().BeTrue();
    }

    [Fact]
    public void Encode_WhenDisabled_DoesNotAddAnyAssumption()
    {
        var encoder = new CurriculumConflictEncoder();
        var ctx = BuildContext(
            new[]
            {
                Section(1, theory: 3, lab: 0, programId: 10, year: 1),
                Section(2, theory: 3, lab: 0, programId: 10, year: 1)
            },
            flags: new Dictionary<string, bool> { ["H5"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_TwoSectionsSameProgramYear_RegistersGroupAssumption()
    {
        var encoder = new CurriculumConflictEncoder();
        var ctx = BuildContext(
            new[]
            {
                Section(1, theory: 3, lab: 0, programId: 10, year: 1),
                Section(2, theory: 3, lab: 0, programId: 10, year: 1)
            },
            flags: new Dictionary<string, bool> { ["H5"] = true });

        encoder.Encode(ctx);

        // 1 group, 2 sections → 1 group-level assumption
        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_TwoSectionsDifferentPrograms_NoAssumption()
    {
        var encoder = new CurriculumConflictEncoder();
        var ctx = BuildContext(
            new[]
            {
                Section(1, theory: 3, lab: 0, programId: 10, year: 1),
                Section(2, theory: 3, lab: 0, programId: 20, year: 1)
            },
            flags: new Dictionary<string, bool> { ["H5"] = true });

        encoder.Encode(ctx);

        // Different program groups → no overlap constraint between them
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_TwoSectionsDifferentYears_NoAssumption()
    {
        var encoder = new CurriculumConflictEncoder();
        var ctx = BuildContext(
            new[]
            {
                Section(1, theory: 3, lab: 0, programId: 10, year: 1),
                Section(2, theory: 3, lab: 0, programId: 10, year: 2)
            },
            flags: new Dictionary<string, bool> { ["H5"] = true });

        encoder.Encode(ctx);

        // Same program, different years → independent groups
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_SectionsWithoutProgramOrYear_Skipped()
    {
        var encoder = new CurriculumConflictEncoder();
        var ctx = BuildContext(
            new[]
            {
                Section(1, theory: 3, lab: 0, programId: null, year: 1),
                Section(2, theory: 3, lab: 0, programId: 10, year: null),
                Section(3, theory: 3, lab: 0, programId: null, year: null)
            },
            flags: new Dictionary<string, bool> { ["H5"] = true });

        encoder.Encode(ctx);

        // Sections lacking program OR year are skipped — no group with ≥2 valid sections
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_ThreeSectionsTwoGroups_RegistersOnePerGroupWithTwoOrMore()
    {
        var encoder = new CurriculumConflictEncoder();
        var ctx = BuildContext(
            new[]
            {
                Section(1, theory: 3, lab: 0, programId: 10, year: 1),
                Section(2, theory: 3, lab: 0, programId: 10, year: 1),
                Section(3, theory: 3, lab: 0, programId: 20, year: 1)  // alone in (20,1)
            },
            flags: new Dictionary<string, bool> { ["H5"] = true });

        encoder.Encode(ctx);

        // (10,1) has 2 sections → 1 assumption; (20,1) has 1 → no assumption
        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_LectureOnlyConstraint_LabSectionsNotIncludedInGrouping()
    {
        var encoder = new CurriculumConflictEncoder();
        var ctx = BuildContext(
            new[]
            {
                Section(1, theory: 0, lab: 2, programId: 10, year: 1),  // lab-only
                Section(2, theory: 0, lab: 2, programId: 10, year: 1)   // lab-only
            },
            flags: new Dictionary<string, bool> { ["H5"] = true });

        encoder.Encode(ctx);

        // Both lab-only → no lecture intervals → no AddNoOverlap → no assumption
        ctx.Assumptions.Count.Should().Be(0);
    }
}
