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
/// Plan 9 Issue 6 — ClassroomTypeRequirementEnabled flag covers the Term 5/6 UNSAT regression
/// caused by Course.RequiredTheoryClassroomType / Course.RequiredLabClassroomType being seeded
/// (e.g. by AutoScheduleTestData → Amphitheater) without a corresponding admin UI to manage them.
/// When the flag is false (production default until admin UI lands — Plan 10), both rules are
/// suppressed (Lab fallback Lab|Computer still applies). When true, Plan 7b behavior is restored.
/// Tests use the real <see cref="DecisionVariables"/> API (CreateIntervalsForSection +
/// CreateRoomVariable), mirroring <see cref="AmphiThresholdToggleTests"/>.
/// </summary>
public class ClassroomTypeRequirementToggleTests
{
    [Fact]
    public void Disabled_TheoryRequiresAmphi_NoMatchingRoom_Feasible()
    {
        // Arrange: section requires Amphitheater for theory, ONLY Lecture room available.
        // Flag DISABLED → encoder must NOT enforce the type rule → solver feasible.
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "BM 101", CourseName = "Algoritma",
            SectionNumber = 1, Capacity = 30,
            TheoryHours = 3, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace,
            RequiredTheoryClassroomType = (int)ClassroomType.Amphitheater
        };
        var classroom = new ClassroomInput
        {
            Id = 10, Name = "D101", Capacity = 50, Type = ClassroomType.Lecture
        };

        var ctx = BuildCtx(
            section, classroom,
            classroomTypeRequirementEnabled: false);

        // Act
        new ClassroomTypeEncoder().Encode(ctx);
        ctx.Assumptions.RegisterAllAsAssumptions();

        // Force section presence so the model is non-trivial.
        ctx.Variables.GetSectionPresent(1).Let(p => ctx.Model.Add((BoolVar)p == 1));

        var solver = new CpSolver();
        var status = solver.Solve(ctx.Model);

        // Assert: feasible — type rule disabled.
        status.Should().BeOneOf(new[] { CpSolverStatus.Optimal, CpSolverStatus.Feasible });
        ctx.Assumptions.Count.Should().Be(0,
            because: "ClassroomTypeRequirementEnabled=false suppresses the theory-type rule entirely; no literals registered.");
    }

    [Fact]
    public void Enabled_TheoryRequiresAmphi_NoMatchingRoom_Infeasible()
    {
        // Arrange: same data, but flag ENABLED → encoder enforces type rule → infeasible (no Amphitheater).
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "BM 101", CourseName = "Algoritma",
            SectionNumber = 1, Capacity = 30,
            TheoryHours = 3, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace,
            RequiredTheoryClassroomType = (int)ClassroomType.Amphitheater
        };
        var classroom = new ClassroomInput
        {
            Id = 10, Name = "D101", Capacity = 50, Type = ClassroomType.Lecture
        };

        var ctx = BuildCtx(
            section, classroom,
            classroomTypeRequirementEnabled: true);

        // Act
        new ClassroomTypeEncoder().Encode(ctx);
        // Force assumption literals true so OnlyEnforceIf constraints fire.
        foreach (var lit in ctx.Assumptions.AllLiterals)
            ctx.Model.Add((BoolVar)lit == 1);

        ctx.Variables.GetSectionPresent(1).Let(p => ctx.Model.Add((BoolVar)p == 1));

        var solver = new CpSolver();
        var status = solver.Solve(ctx.Model);

        // Assert: infeasible — Amphitheater required, none available.
        status.Should().Be(CpSolverStatus.Infeasible);
    }

    [Fact]
    public void Disabled_LabRequiresPhysicsLab_NoMatchingRoom_FallsBackToLabComputer()
    {
        // Arrange: section requires PhysicsLab, only a regular Lab room available.
        // Flag DISABLED → RequiredLabClassroomType ignored, default Lab|Computer fallback applies →
        // Lab room qualifies → solver feasible.
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "PHYS 201", CourseName = "Physics Lab",
            SectionNumber = 1, Capacity = 24,
            TheoryHours = 0, LabHours = 2,
            TeachingMethod = TeachingMethod.FaceToFace,
            RequiredLabClassroomType = (int)ClassroomType.PhysicsLab
        };
        var classroom = new ClassroomInput
        {
            Id = 20, Name = "L1", Capacity = 30, Type = ClassroomType.Lab
        };

        var ctx = BuildCtx(
            section, classroom,
            classroomTypeRequirementEnabled: false);

        // Act
        new ClassroomTypeEncoder().Encode(ctx);
        ctx.Assumptions.RegisterAllAsAssumptions();

        ctx.Variables.GetSectionPresent(1).Let(p => ctx.Model.Add((BoolVar)p == 1));

        var solver = new CpSolver();
        var status = solver.Solve(ctx.Model);

        // Assert: feasible — fallback accepts Lab classroom.
        status.Should().BeOneOf(new[] { CpSolverStatus.Optimal, CpSolverStatus.Feasible });
        ctx.Assumptions.Count.Should().Be(0,
            because: "ClassroomTypeRequirementEnabled=false ignores RequiredLabClassroomType; Lab|Computer fallback accepts the Lab room without literals.");
    }

    [Fact]
    public void Enabled_LabRequiresPhysicsLab_NoMatchingRoom_Infeasible()
    {
        // Arrange: same data, flag ENABLED → only PhysicsLab type qualifies → no PhysicsLab → infeasible.
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "PHYS 201", CourseName = "Physics Lab",
            SectionNumber = 1, Capacity = 24,
            TheoryHours = 0, LabHours = 2,
            TeachingMethod = TeachingMethod.FaceToFace,
            RequiredLabClassroomType = (int)ClassroomType.PhysicsLab
        };
        var classroom = new ClassroomInput
        {
            Id = 20, Name = "L1", Capacity = 30, Type = ClassroomType.Lab
        };

        var ctx = BuildCtx(
            section, classroom,
            classroomTypeRequirementEnabled: true);

        // Act
        new ClassroomTypeEncoder().Encode(ctx);
        // Force assumption literals true so OnlyEnforceIf constraints fire.
        foreach (var lit in ctx.Assumptions.AllLiterals)
            ctx.Model.Add((BoolVar)lit == 1);

        ctx.Variables.GetSectionPresent(1).Let(p => ctx.Model.Add((BoolVar)p == 1));

        var solver = new CpSolver();
        var status = solver.Solve(ctx.Model);

        // Assert: infeasible — PhysicsLab required, none available.
        status.Should().Be(CpSolverStatus.Infeasible);
    }

    private static EncoderContext BuildCtx(
        SectionInput section,
        ClassroomInput classroom,
        bool classroomTypeRequirementEnabled)
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
                ClassroomTypeRequirementEnabled = classroomTypeRequirementEnabled
            },
            ConstraintFlags = new Dictionary<string, bool> { ["H4"] = true },
            Penalties = new List<LinearExpr>(),
            ClassroomTypeRequirementEnabled = classroomTypeRequirementEnabled
        };
    }
}

// Note: Let<T> extension provided by ClassroomTypeEncoderTestsExtensions in the same namespace.
