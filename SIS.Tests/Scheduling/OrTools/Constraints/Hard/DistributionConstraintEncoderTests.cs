using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

/// <summary>
/// Plan 8 Task 14 — DIST infrastructure refactor (dispatch shell).
/// Scaffolding sanity tests: encoder must run cleanly when the constraint list is empty
/// and skip Preferred/StronglyPreferred tiers (Plan 9 polish slot, currently no-op).
/// Per-type encoder bodies are filled in Tasks 15-19; this file only proves the
/// dispatch shell + PreferenceLevel switch.
/// </summary>
public class DistributionConstraintEncoderTests
{
    [Fact]
    public void Encode_EmptyDistConstraints_NoCrash()
    {
        var encoder = new DistributionConstraintEncoder();
        var ctx = BuildScenario(Array.Empty<DistributionConstraintInput>());

        var act = () => encoder.Encode(ctx);

        act.Should().NotThrow();
        ctx.Penalties.Should().BeEmpty();
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_PreferredLevel_Skipped()
    {
        // PreferenceLevel.Preferred → switch returns -1 → continue. No assumption / no penalty.
        // (Plan 9 reserves Preferred/StronglyPreferred for a positive-bonus tier.)
        var dist = new DistributionConstraintInput(
            Id: 1,
            DistributionType: DistributionType.SameDay,
            PreferenceLevel: PreferenceLevel.Preferred,
            Courses: new[]
            {
                new DistributionCourseInput(100, null, 0),
                new DistributionCourseInput(200, null, 1),
            });

        var encoder = new DistributionConstraintEncoder();
        var ctx = BuildScenario(new[] { dist });

        encoder.Encode(ctx);

        ctx.Penalties.Should().BeEmpty();
        ctx.Assumptions.Count.Should().Be(0);
    }

    /// <summary>
    /// Minimal scenario: 2 sections (CourseId 100 / 200), 1 classroom, 1 slot.
    /// Presence literals forced to 1 so penalty terms cannot be optimised away.
    /// </summary>
    private static EncoderContext BuildScenario(
        IReadOnlyList<DistributionConstraintInput> distConstraints,
        IReadOnlyDictionary<string, bool>? flags = null)
    {
        flags ??= new Dictionary<string, bool> { ["DIST"] = true };

        var sections = new[]
        {
            new SectionInput
            {
                Id = 1,
                CourseId = 100,
                CourseCode = "CS101",
                CourseName = "A",
                SectionNumber = 1,
                Capacity = 30,
                TheoryHours = 1,
                LabHours = 0,
                TeachingMethod = TeachingMethod.FaceToFace,
            },
            new SectionInput
            {
                Id = 2,
                CourseId = 200,
                CourseCode = "CS102",
                CourseName = "B",
                SectionNumber = 1,
                Capacity = 30,
                TheoryHours = 1,
                LabHours = 0,
                TeachingMethod = TeachingMethod.FaceToFace,
            },
        };

        var classrooms = new[]
        {
            new ClassroomInput { Id = 9, Name = "C", Capacity = 50, Type = ClassroomType.Lecture },
        };

        var slots = new[]
        {
            new TimeSlotInput
            {
                Id = 1,
                DayOfWeek = DayOfWeek.Monday,
                SlotOrder = 1,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(9, 50),
                AllowedMode = TimeSlotAllowedMode.Both,
            },
        };

        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: slots.Length);

        foreach (var s in sections)
        {
            variables.CreateIntervalsForSection(s);
            variables.CreateRoomVariable(s.Id, ComponentType.Lecture, classrooms);
            variables.CreateOptionalRoomInterval(
                s.Id, classrooms[0].Id, ComponentType.Lecture, duration: s.TheoryHours);
            var presence = (BoolVar)variables.AllPresenceLiterals[
                (s.Id, classrooms[0].Id, ComponentType.Lecture)];
            model.Add(presence == 1);
        }

        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = sections,
            Classrooms = classrooms,
            TimeSlots = slots,
            ConstraintFlags = flags,
            DistributionConstraints = distConstraints,
        };

        return new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = request,
            ConstraintFlags = flags,
            Penalties = new List<LinearExpr>(),
        };
    }
}
