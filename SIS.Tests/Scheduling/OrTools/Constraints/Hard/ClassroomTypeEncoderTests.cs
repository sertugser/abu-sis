using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Constraints.Hard;

public class ClassroomTypeEncoderTests
{
    private static SectionInput Section(int id, int theory, int lab) => new()
    {
        Id = id,
        CourseId = 100,
        CourseCode = "X",
        CourseName = "X",
        SectionNumber = 1,
        Capacity = 40,
        TheoryHours = theory,
        LabHours = lab,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    private static EncoderContext BuildContextWithRooms(
        IReadOnlyList<SectionInput> sections,
        IReadOnlyList<ClassroomInput> classrooms,
        IReadOnlyDictionary<string, bool>? flags = null)
    {
        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: 50);
        foreach (var s in sections)
        {
            variables.CreateIntervalsForSection(s);
            if (s.TheoryHours > 0)
                variables.CreateRoomVariable(s.Id, ComponentType.Lecture, classrooms);
            if (s.LabHours > 0)
                variables.CreateRoomVariable(s.Id, ComponentType.Lab, classrooms, groupCount: s.LabGroupCount);
        }

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
                Classrooms = classrooms,
                TimeSlots = new List<TimeSlotInput>()
            },
            ConstraintFlags = flags ?? new Dictionary<string, bool>(),
            Penalties = new List<LinearExpr>()
        };
    }

    [Fact]
    public void Code_IsH4_AndIsHard()
    {
        var encoder = new ClassroomTypeEncoder();

        encoder.Code.Should().Be("H4");
        encoder.IsHard.Should().BeTrue();
    }

    [Fact]
    public void Encode_WhenDisabled_DoesNotAddAnyAssumption()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "Lecture", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 0, lab: 2) },  // lab-only section
            classrooms,
            flags: new Dictionary<string, bool> { ["H4"] = false });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_LabOnlySectionInLectureRoom_RegistersAssumption()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 0, lab: 2) },
            classrooms,
            flags: new Dictionary<string, bool> { ["H4"] = true });

        encoder.Encode(ctx);

        // Lab section forbidden from Lecture room → 1 assumption
        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_LabOnlySectionInLabRoom_NoAssumption()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 10, Name = "L139", Capacity = 30, Type = ClassroomType.Lab }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 0, lab: 2) },
            classrooms,
            flags: new Dictionary<string, bool> { ["H4"] = true });

        encoder.Encode(ctx);

        // Lab section in Lab room → OK, no assumption
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_LabOnlySectionInComputerRoom_NoAssumption()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 11, Name = "PC Lab", Capacity = 30, Type = ClassroomType.Computer }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 0, lab: 2) },
            classrooms,
            flags: new Dictionary<string, bool> { ["H4"] = true });

        encoder.Encode(ctx);

        // Computer room qualifies → OK
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_MixedSection_LabComponentInLectureRoom_RegistersAssumption()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 3, lab: 2) },  // mixed
            classrooms,
            flags: new Dictionary<string, bool> { ["H4"] = true });

        encoder.Encode(ctx);

        // Lab component is constrained; lecture component is not. 1 assumption (lab→lecture room forbidden).
        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_LabSectionMultiGroup_NonLabClassroom_RegistersAssumptionForAllGroups()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "L1", Capacity = 40, Type = ClassroomType.Lab },
            new() { Id = 10, Name = "L2", Capacity = 40, Type = ClassroomType.Lab },
            new() { Id = 11, Name = "Lecture A", Capacity = 100, Type = ClassroomType.Lecture }
        };
        var section = new SectionInput
        {
            Id = 1,
            CourseId = 100,
            CourseCode = "X",
            CourseName = "X",
            SectionNumber = 1,
            Capacity = 80,
            TheoryHours = 0,
            LabHours = 2,
            TeachingMethod = TeachingMethod.FaceToFace,
            LabGroupCount = 2
        };
        var ctx = BuildContextWithRooms(
            new[] { section },
            classrooms,
            flags: new Dictionary<string, bool> { ["H4"] = true });

        encoder.Encode(ctx);

        // 1 non-lab classroom (Lecture) × 1 literal = 1 assumption.
        // The single literal gates BOTH labRooms IntVars (consistent with H3 pattern).
        ctx.Assumptions.Count.Should().Be(1);
    }

    [Fact]
    public void Encode_LectureOnlySection_NoAssumptionRegardlessOfRoomType()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture },
            new() { Id = 10, Name = "L139", Capacity = 30, Type = ClassroomType.Lab },
            new() { Id = 11, Name = "PC Lab", Capacity = 30, Type = ClassroomType.Computer }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 3, lab: 0) },  // lecture-only
            classrooms,
            flags: new Dictionary<string, bool> { ["H4"] = true });

        encoder.Encode(ctx);

        // Lecture-only sections have no type restriction → 0 assumptions
        ctx.Assumptions.Count.Should().Be(0);
    }

    [Fact]
    public void Encode_TheoryRequiresAmphitheater_ForbidsNonAmphitheaterRooms()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 1, Name = "L1", Capacity = 80, Type = ClassroomType.Lecture },
            new() { Id = 2, Name = "A1", Capacity = 200, Type = ClassroomType.Amphitheater }
        };
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "X", CourseName = "X",
            SectionNumber = 1, Capacity = 50,
            TheoryHours = 2, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace,
            RequiredTheoryClassroomType = (int)ClassroomType.Amphitheater
        };
        var ctx = BuildContextWithRooms(new[] { section }, classrooms,
            flags: new Dictionary<string, bool> { ["H4"] = true });

        encoder.Encode(ctx);
        ctx.Assumptions.RegisterAllAsAssumptions();

        // Solver: theory room must equal Amphitheater (id=2).
        ctx.Variables.GetSectionPresent(1).Let(p => ctx.Model.Add((BoolVar)p == 1));
        var solver = new CpSolver();
        var status = solver.Solve(ctx.Model);
        status.Should().BeOneOf(new[] { CpSolverStatus.Optimal, CpSolverStatus.Feasible });
        solver.Value(ctx.Variables.GetLectureRoom(1)).Should().Be(2,
            because: "RequiredTheoryClassroomType=Amphitheater forces room id=2");
    }

    [Fact]
    public void Encode_LabRequiresPhysicsLab_ForbidsRegularLab()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 1, Name = "L1", Capacity = 30, Type = ClassroomType.Lab },
            new() { Id = 2, Name = "PL", Capacity = 30, Type = ClassroomType.PhysicsLab }
        };
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "X", CourseName = "X",
            SectionNumber = 1, Capacity = 24,
            TheoryHours = 0, LabHours = 2,
            TeachingMethod = TeachingMethod.FaceToFace,
            RequiredLabClassroomType = (int)ClassroomType.PhysicsLab
        };
        var ctx = BuildContextWithRooms(new[] { section }, classrooms,
            flags: new Dictionary<string, bool> { ["H4"] = true });

        encoder.Encode(ctx);
        // Force all assumption literals to true so OnlyEnforceIf constraints are active.
        foreach (var lit in ctx.Assumptions.AllLiterals)
            ctx.Model.Add((BoolVar)lit == 1);

        ctx.Variables.GetSectionPresent(1).Let(p => ctx.Model.Add((BoolVar)p == 1));
        var solver = new CpSolver();
        var status = solver.Solve(ctx.Model);
        status.Should().BeOneOf(new[] { CpSolverStatus.Optimal, CpSolverStatus.Feasible });
        solver.Value(ctx.Variables.GetLabRoom(1)).Should().Be(2,
            because: "RequiredLabClassroomType=PhysicsLab forces lab id=2");
    }

    [Fact]
    public void Encode_AmphiThreshold_LargeCapacity_ForbidsNonAmphitheater()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 1, Name = "L1", Capacity = 200, Type = ClassroomType.Lecture },
            new() { Id = 2, Name = "A1", Capacity = 200, Type = ClassroomType.Amphitheater }
        };
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "X", CourseName = "X",
            SectionNumber = 1, Capacity = 150,
            TheoryHours = 2, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace
        };
        var ctx = BuildContextWithRoomsAndAmphiThreshold(new[] { section }, classrooms,
            amphiThreshold: 100,
            flags: new Dictionary<string, bool> { ["H4"] = true });

        encoder.Encode(ctx);
        // Force all assumption literals to true so OnlyEnforceIf constraints are active.
        foreach (var lit in ctx.Assumptions.AllLiterals)
            ctx.Model.Add((BoolVar)lit == 1);

        ctx.Variables.GetSectionPresent(1).Let(p => ctx.Model.Add((BoolVar)p == 1));
        var solver = new CpSolver();
        var status = solver.Solve(ctx.Model);
        status.Should().BeOneOf(new[] { CpSolverStatus.Optimal, CpSolverStatus.Feasible });
        solver.Value(ctx.Variables.GetLectureRoom(1)).Should().Be(2,
            because: "Capacity 150 >= AmphiThreshold 100 -> Amphitheater required");
    }

    [Fact]
    public void Encode_AmphiThreshold_SmallCapacity_NoConstraint()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 1, Name = "L1", Capacity = 50, Type = ClassroomType.Lecture },
            new() { Id = 2, Name = "A1", Capacity = 200, Type = ClassroomType.Amphitheater }
        };
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "X", CourseName = "X",
            SectionNumber = 1, Capacity = 30,
            TheoryHours = 2, LabHours = 0,
            TeachingMethod = TeachingMethod.FaceToFace
        };
        var ctx = BuildContextWithRoomsAndAmphiThreshold(new[] { section }, classrooms,
            amphiThreshold: 100,
            flags: new Dictionary<string, bool> { ["H4"] = true });

        encoder.Encode(ctx);

        // No assumption added for AmphiThreshold (capacity 30 < 100).
        ctx.Assumptions.Count.Should().Be(0,
            because: "Capacity 30 < threshold 100 -> no AmphiThreshold-driven literal");
    }

    [Fact]
    public void Encode_TheoryNoRequirementAndCapacityBelowThreshold_AddsNoLiterals()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 1, Name = "L1", Capacity = 50, Type = ClassroomType.Lecture },
            new() { Id = 2, Name = "S1", Capacity = 40, Type = ClassroomType.Seminar }
        };
        var ctx = BuildContextWithRooms(
            new[] { Section(1, theory: 2, lab: 0) },
            classrooms,
            flags: new Dictionary<string, bool> { ["H4"] = true });

        encoder.Encode(ctx);

        ctx.Assumptions.Count.Should().Be(0,
            because: "No type requirement + capacity below threshold -> no theory constraint");
    }

    [Fact]
    public void Encode_LabRequiresChemistryLab_ButOnlyRegularLabAvailable_RegistersAssumptions()
    {
        var encoder = new ClassroomTypeEncoder();
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 1, Name = "L1", Capacity = 30, Type = ClassroomType.Lab }
        };
        var section = new SectionInput
        {
            Id = 1, CourseId = 100, CourseCode = "X", CourseName = "X",
            SectionNumber = 1, Capacity = 24,
            TheoryHours = 0, LabHours = 2,
            TeachingMethod = TeachingMethod.FaceToFace,
            RequiredLabClassroomType = (int)ClassroomType.ChemistryLab
        };
        var ctx = BuildContextWithRooms(new[] { section }, classrooms,
            flags: new Dictionary<string, bool> { ["H4"] = true });

        encoder.Encode(ctx);

        // Only Lab classroom present; RequiredLab=ChemistryLab → forbidden literal added.
        ctx.Assumptions.Count.Should().BeGreaterThan(0,
            because: "Required ChemistryLab type unavailable; encoder still emits literals (UNSAT downstream)");
    }

    private static EncoderContext BuildContextWithRoomsAndAmphiThreshold(
        IReadOnlyList<SectionInput> sections,
        IReadOnlyList<ClassroomInput> classrooms,
        int amphiThreshold,
        IReadOnlyDictionary<string, bool>? flags = null)
    {
        var model = new CpModel();
        var variables = new DecisionVariables(model, totalSlots: 50);
        foreach (var s in sections)
        {
            variables.CreateIntervalsForSection(s);
            if (s.TheoryHours > 0)
                variables.CreateRoomVariable(s.Id, ComponentType.Lecture, classrooms);
            if (s.LabHours > 0)
                variables.CreateRoomVariable(s.Id, ComponentType.Lab, classrooms, groupCount: s.LabGroupCount);
        }

        return new EncoderContext
        {
            Model = model,
            Variables = variables,
            Assumptions = new AssumptionRegistry(model),
            Request = new SolverRequest
            {
                TermId = 1, FacultyId = 1,
                Sections = sections, Classrooms = classrooms,
                TimeSlots = new List<TimeSlotInput>()
            },
            ConstraintFlags = flags ?? new Dictionary<string, bool>(),
            Penalties = new List<LinearExpr>(),
            AmphiThreshold = amphiThreshold
        };
    }
}

internal static class ClassroomTypeEncoderTestsExtensions
{
    // Tiny extension to thread a literal-as-IntVar cast inline in tests.
    public static void Let<T>(this T self, Action<T> action) => action(self);
}
