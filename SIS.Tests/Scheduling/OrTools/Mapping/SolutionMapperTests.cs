using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Mapping;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Mapping;

public class SolutionMapperTests
{
    [Fact]
    public void MapInfeasible_WithEmptyUnsatCore_ReturnsInfeasibleStatusAndAllSectionsAsFailures()
    {
        var mapper = new SolutionMapper();
        var request = BuildSimpleRequest();

        var result = mapper.MapInfeasible(
            unsatCore: new List<UnsatCoreEntry>(),
            request,
            elapsedSeconds: 1.5);

        result.Status.Should().Be(SolverStatus.Infeasible);
        result.Failures.Should().HaveCount(1);
        result.Failures[0].SectionId.Should().Be(1);
        result.Diagnostics.EngineVersion.Should().Be("v2-ortools");
        result.Diagnostics.ElapsedSeconds.Should().Be(1.5);
    }

    [Fact]
    public void MapTimeout_AllSectionsListedAsFailures()
    {
        var mapper = new SolutionMapper();
        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[]
            {
                BuildSection(1, theory: 3, lab: 0),
                BuildSection(2, theory: 3, lab: 2),
                BuildSection(3, theory: 0, lab: 2)
            },
            Classrooms = new List<ClassroomInput>(),
            TimeSlots = BuildOneSlot()
        };

        // Build a trivial CpModel to satisfy CpSolver creation requirement (we don't actually solve)
        var model = new CpModel();
        var dummyVar = model.NewBoolVar("dummy");
        model.AddAssumptions(new ILiteral[] { dummyVar });
        var solver = new CpSolver();
        // Don't solve; just call MapTimeout — it shouldn't depend on actual solver state for failures

        var result = mapper.MapTimeout(solver, BuildEmptyBuildResult(model), request, elapsedSeconds: 30.5);

        result.Status.Should().Be(SolverStatus.Timeout);
        result.Failures.Should().HaveCount(3);
        result.Failures.Should().AllSatisfy(f => f.Reason.Should().Contain("timeout"));
        result.Assignments.Should().BeEmpty();
        result.Diagnostics.EngineVersion.Should().Be("v2-ortools");
        result.Diagnostics.ElapsedSeconds.Should().Be(30.5);
    }

    [Fact]
    public async Task MapSuccess_OneLectureOnlySection_BuildsAssignmentWithLectureSlots()
    {
        // Build a minimal feasible CP-SAT model (1 section, 1 classroom, multiple slots)
        var mapper = new SolutionMapper();
        var section = BuildSection(1, theory: 2, lab: 0);
        var classroom = new ClassroomInput { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture };
        var timeSlots = BuildSimpleSlots(8);
        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[] { section },
            Classrooms = new List<ClassroomInput> { classroom },
            TimeSlots = timeSlots,
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true }
        };

        var encoders = new IConstraintEncoder[] { new ClassroomConflictEncoder() };
        var modelBuilder = new ScheduleModelBuilder(encoders);
        var build = modelBuilder.Build(request);

        var solver = new CpSolver();
        var status = solver.Solve(build.Model);
        status.Should().BeOneOf(CpSolverStatus.Optimal, CpSolverStatus.Feasible);

        var result = mapper.MapSuccess(solver, build, request, elapsedSeconds: 0.5, status);

        result.Status.Should().BeOneOf(SolverStatus.Optimal, SolverStatus.Feasible);
        result.Assignments.Should().HaveCount(1);
        result.Assignments[0].SectionId.Should().Be(1);
        result.Assignments[0].Slots.Should().HaveCount(2);  // 2 theory hours = 2 slots
        result.Assignments[0].Slots.Should().AllSatisfy(s =>
        {
            s.ClassroomId.Should().Be(9);
            s.ComponentType.Should().Be(ComponentType.Lecture);
        });
        result.Diagnostics.EngineVersion.Should().Be("v2-ortools");
        result.Diagnostics.ElapsedSeconds.Should().Be(0.5);

        await Task.CompletedTask;
    }

    [Fact]
    public void MapSuccess_OptimalStatus_ReturnsOptimalSolverStatus()
    {
        var mapper = new SolutionMapper();
        var request = BuildSimpleSolvableRequest();
        var (solver, build) = SolveRequest(request);

        var result = mapper.MapSuccess(solver, build, request, elapsedSeconds: 0.1, CpSolverStatus.Optimal);

        result.Status.Should().Be(SolverStatus.Optimal);
    }

    [Fact]
    public void MapSuccess_FeasibleStatus_ReturnsFeasibleSolverStatus()
    {
        var mapper = new SolutionMapper();
        var request = BuildSimpleSolvableRequest();
        var (solver, build) = SolveRequest(request);

        var result = mapper.MapSuccess(solver, build, request, elapsedSeconds: 0.1, CpSolverStatus.Feasible);

        result.Status.Should().Be(SolverStatus.Feasible);
    }

    // ─────────────── Helpers ───────────────

    private static SolverRequest BuildSimpleRequest() => new()
    {
        TermId = 1,
        FacultyId = 1,
        Sections = new[] { BuildSection(1, 3, 0) },
        Classrooms = new List<ClassroomInput>(),
        TimeSlots = BuildOneSlot()
    };

    private static SolverRequest BuildSimpleSolvableRequest() => new()
    {
        TermId = 1,
        FacultyId = 1,
        Sections = new[] { BuildSection(1, 2, 0) },
        Classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
        },
        TimeSlots = BuildSimpleSlots(8),
        ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true }
    };

    private static (CpSolver solver, ScheduleModelBuildResult build) SolveRequest(SolverRequest request)
    {
        var encoders = new IConstraintEncoder[] { new ClassroomConflictEncoder() };
        var modelBuilder = new ScheduleModelBuilder(encoders);
        var build = modelBuilder.Build(request);
        var solver = new CpSolver();
        solver.Solve(build.Model);
        return (solver, build);
    }

    private static SectionInput BuildSection(int id, int theory, int lab) => new()
    {
        Id = id,
        CourseId = 100,
        CourseCode = "X",
        CourseName = "X",
        SectionNumber = id,
        Capacity = 40,
        TheoryHours = theory,
        LabHours = lab,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    private static IReadOnlyList<TimeSlotInput> BuildOneSlot() => new List<TimeSlotInput>
    {
        new()
        {
            Id = 1,
            DayOfWeek = DayOfWeek.Monday,
            SlotOrder = 1,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 50),
            AllowedMode = TimeSlotAllowedMode.Both
        }
    };

    private static IReadOnlyList<TimeSlotInput> BuildSimpleSlots(int count)
    {
        var slots = new List<TimeSlotInput>();
        for (int i = 0; i < count; i++)
        {
            slots.Add(new TimeSlotInput
            {
                Id = i + 1,
                DayOfWeek = DayOfWeek.Monday,
                SlotOrder = i + 1,
                StartTime = new TimeOnly(9, 0).AddMinutes(i * 50),
                EndTime = new TimeOnly(9, 50).AddMinutes(i * 50),
                AllowedMode = TimeSlotAllowedMode.Both
            });
        }
        return slots;
    }

    private static ScheduleModelBuildResult BuildEmptyBuildResult(CpModel model)
    {
        var registry = new AssumptionRegistry(model);
        var variables = new DecisionVariables(model, totalSlots: 1);
        return new ScheduleModelBuildResult(model, variables, registry);
    }
}
