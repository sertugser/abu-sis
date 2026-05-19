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
/// Plan 9 Issue 1 — AmphiThresholdEnabled flag covers the Term 5 UNSAT regression.
/// When the flag is false (production default until amphi inventory exists), AmphiThreshold
/// rule is suppressed; when true, capacity ≥ threshold sections must land in Amphitheater rooms.
/// Tests use the real <see cref="DecisionVariables"/> API (CreateIntervalsForSection +
/// CreateRoomVariable) — the plan's pseudocode references older method names.
/// </summary>
public class AmphiThresholdToggleTests
{
    [Fact]
    public void Disabled_LargeSection_NoAmphi_Feasible()
    {
        // Arrange: Capacity 150, threshold 100, ONLY Lecture room (no Amphitheater).
        // With flag DISABLED, encoder must NOT enforce amphi — solver feasible.
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "BM 101", CourseName = "Algoritma",
            SectionNumber = 1, Capacity = 150,
            TheoryHours = 3, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace
        };
        var classroom = new ClassroomInput
        {
            Id = 10, Name = "D101", Capacity = 200, Type = ClassroomType.Lecture
        };

        var ctx = BuildCtx(
            section, classroom,
            amphiThreshold: 100,
            amphiThresholdEnabled: false);

        // Act
        new ClassroomTypeEncoder().Encode(ctx);
        ctx.Assumptions.RegisterAllAsAssumptions();

        // Force section presence so the model is non-trivial.
        ctx.Variables.GetSectionPresent(1).Let(p => ctx.Model.Add((BoolVar)p == 1));

        var solver = new CpSolver();
        var status = solver.Solve(ctx.Model);

        // Assert: feasible — no amphi requirement when flag is off.
        status.Should().BeOneOf(new[] { CpSolverStatus.Optimal, CpSolverStatus.Feasible });
        ctx.Assumptions.Count.Should().Be(0,
            because: "AmphiThresholdEnabled=false suppresses the rule entirely; no literals registered.");
    }

    [Fact]
    public void Enabled_LargeSection_NoAmphi_Infeasible()
    {
        // Arrange: same data as above, but flag ENABLED → encoder must enforce amphi.
        // No Amphitheater available → solver Infeasible.
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "BM 101", CourseName = "Algoritma",
            SectionNumber = 1, Capacity = 150,
            TheoryHours = 3, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace
        };
        var classroom = new ClassroomInput
        {
            Id = 10, Name = "D101", Capacity = 200, Type = ClassroomType.Lecture
        };

        var ctx = BuildCtx(
            section, classroom,
            amphiThreshold: 100,
            amphiThresholdEnabled: true);

        // Act
        new ClassroomTypeEncoder().Encode(ctx);
        // Force assumption literals true so OnlyEnforceIf constraints actually fire.
        foreach (var lit in ctx.Assumptions.AllLiterals)
            ctx.Model.Add((BoolVar)lit == 1);

        // Force section presence so dropping the section is not a feasible escape.
        ctx.Variables.GetSectionPresent(1).Let(p => ctx.Model.Add((BoolVar)p == 1));

        var solver = new CpSolver();
        var status = solver.Solve(ctx.Model);

        // Assert: infeasible — amphi required, none available.
        status.Should().Be(CpSolverStatus.Infeasible);
    }

    private static EncoderContext BuildCtx(
        SectionInput section,
        ClassroomInput classroom,
        int amphiThreshold,
        bool amphiThresholdEnabled)
    {
        var classrooms = new[] { classroom };
        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: 50);
        variables.CreateIntervalsForSection(section);
        if (section.TheoryHours > 0)
            variables.CreateRoomVariable(section.Id, ComponentType.Lecture, classrooms);
        if (section.LabHours > 0)
            variables.CreateRoomVariable(section.Id, ComponentType.Lab, classrooms,
                groupCount: section.LabGroupCount);

        return new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = new SolverRequest
            {
                TermId = 1, FacultyId = 1,
                Sections = new[] { section },
                Classrooms = classrooms,
                TimeSlots = new List<TimeSlotInput>(),
                AmphiThreshold = amphiThreshold,
                AmphiThresholdEnabled = amphiThresholdEnabled
            },
            ConstraintFlags = new Dictionary<string, bool> { ["H4"] = true },
            Penalties = new List<LinearExpr>(),
            AmphiThreshold = amphiThreshold,
            AmphiThresholdEnabled = amphiThresholdEnabled
        };
    }
}

// Note: Let<T> extension provided by ClassroomTypeEncoderTestsExtensions in the same namespace.
