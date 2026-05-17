using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Soft;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Soft;

/// <summary>
/// Plan 8 Task 4 — S1 OccupancyEncoder real impl. Penalty = deviation × S1Weight per
/// (section, classroom) presence literal. v1 OccupancyEvaluator parity (integer math).
/// </summary>
public class OccupancyEncoderTests
{
    /// <summary>
    /// Builds a minimal model with 1 section + 1 classroom and 1 (section, classroom)
    /// presence literal. Test bodies attach the final objective expression AFTER the
    /// encoder runs, so penalties added by the encoder are captured.
    /// </summary>
    private static (CpModel Model, EncoderContext Ctx, BoolVar Presence) BuildContext(
        bool s1Enabled, int sectionCapacity, int classroomCapacity)
    {
        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: 50);

        var section = new SectionInput
        {
            Id = 1,
            CourseId = 100,
            CourseCode = "CS101",
            CourseName = "Intro",
            SectionNumber = 1,
            Capacity = sectionCapacity,
            TheoryHours = 3,
            LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace,
        };

        var classroom = new ClassroomInput
        {
            Id = 10,
            Name = "A-101",
            Capacity = classroomCapacity,
            Type = ClassroomType.Lecture,
        };

        // Register section + lecture room IntVar (single classroom domain).
        variables.CreateIntervalsForSection(section);
        variables.CreateRoomVariable(section.Id, ComponentType.Lecture, new[] { classroom });

        // Create the (section, classroom, Lecture) presence literal — encoder iterates
        // AllPresenceLiterals which is populated by CreateOptionalRoomInterval.
        variables.CreateOptionalRoomInterval(
            section.Id, classroom.Id, ComponentType.Lecture, duration: section.TheoryHours);

        // Force presence to TRUE so penalty term is non-zero (otherwise solver could pick
        // presence=0 to avoid penalty).
        var presence = (BoolVar)variables.AllPresenceLiterals[
            (section.Id, classroom.Id, ComponentType.Lecture)];
        model.Add(presence == 1);

        var ctx = new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = new SolverRequest
            {
                TermId = 1,
                FacultyId = 1,
                Sections = new List<SectionInput> { section },
                Classrooms = new List<ClassroomInput> { classroom },
                TimeSlots = new List<TimeSlotInput>(),
            },
            ConstraintFlags = new Dictionary<string, bool> { ["S1"] = s1Enabled },
            Penalties = new List<LinearExpr>(),
        };

        return (model, ctx, presence);
    }

    [Fact]
    public void Encode_FlagDisabled_NoPenalty()
    {
        var (_, ctx, _) = BuildContext(s1Enabled: false, sectionCapacity: 30, classroomCapacity: 100);

        new OccupancyEncoder().Encode(ctx);

        ctx.Penalties.Should().BeEmpty();
    }

    [Fact]
    public void Encode_RateBelowMin_PenaltyAdded()
    {
        // 30 / 100 = 30% < 60% min → deviation = 30, penalty = 30 × 1 = 30, objective = 9970.
        var (model, ctx, presence) = BuildContext(s1Enabled: true, sectionCapacity: 30, classroomCapacity: 100);

        new OccupancyEncoder().Encode(ctx);
        model.Maximize(LinearExpr.Sum(new[] { presence }) * 10000 - LinearExpr.Sum(ctx.Penalties));

        var solver = new CpSolver();
        var status = solver.Solve(model);

        status.Should().Be(CpSolverStatus.Optimal);
        ctx.Penalties.Should().HaveCount(1);
        solver.ObjectiveValue.Should().Be(9970);
    }

    [Fact]
    public void Encode_RateInTargetRange_NoPenalty()
    {
        // 50 / 70 = 71% ∈ [60, 85] → deviation = 0, no penalty, objective = 10000.
        var (model, ctx, presence) = BuildContext(s1Enabled: true, sectionCapacity: 50, classroomCapacity: 70);

        new OccupancyEncoder().Encode(ctx);
        model.Maximize(LinearExpr.Sum(new[] { presence }) * 10000 - LinearExpr.Sum(ctx.Penalties));

        var solver = new CpSolver();
        var status = solver.Solve(model);

        status.Should().Be(CpSolverStatus.Optimal);
        ctx.Penalties.Should().BeEmpty();
        solver.ObjectiveValue.Should().Be(10000);
    }

    [Fact]
    public void Encode_RateAboveMax_PenaltyAdded()
    {
        // 90 / 100 = 90% > 85% max → deviation = 5, penalty = 5 × 1 = 5, objective = 9995.
        var (model, ctx, presence) = BuildContext(s1Enabled: true, sectionCapacity: 90, classroomCapacity: 100);

        new OccupancyEncoder().Encode(ctx);
        model.Maximize(LinearExpr.Sum(new[] { presence }) * 10000 - LinearExpr.Sum(ctx.Penalties));

        var solver = new CpSolver();
        var status = solver.Solve(model);

        status.Should().Be(CpSolverStatus.Optimal);
        ctx.Penalties.Should().HaveCount(1);
        solver.ObjectiveValue.Should().Be(9995);
    }
}
