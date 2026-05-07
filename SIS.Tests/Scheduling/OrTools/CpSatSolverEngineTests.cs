using FluentAssertions;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints;
using SIS.Infrastructure.Services.Scheduling.OrTools.Constraints.Hard;
using SIS.Infrastructure.Services.Scheduling.OrTools.Mapping;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools;

public class CpSatSolverEngineTests
{
    private static SectionInput Section(int id, int courseId, int theory, int lab) => new()
    {
        Id = id,
        CourseId = courseId,
        CourseCode = "X",
        CourseName = "X",
        SectionNumber = id,
        Capacity = 40,
        TheoryHours = theory,
        LabHours = lab,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    private static CpSatSolverEngine BuildEngine()
    {
        var encoders = new IConstraintEncoder[]
        {
            new ClassroomConflictEncoder()
        };
        var modelBuilder = new ScheduleModelBuilder(encoders);
        var mapper = new SolutionMapper();
        return new CpSatSolverEngine(modelBuilder, mapper);
    }

    [Fact]
    public async Task SolveAsync_TwoNonOverlappingSections_ReturnsOptimalOrFeasible()
    {
        var engine = BuildEngine();
        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[] { Section(1, 100, 3, 0), Section(2, 200, 3, 0) },
            Classrooms = new List<ClassroomInput>
            {
                new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
            },
            TimeSlots = new List<TimeSlotInput>(),
            ConstraintFlags = new Dictionary<string, bool> { ["H1"] = true }
        };

        var result = await engine.SolveAsync(request, CancellationToken.None);

        result.Status.Should().BeOneOf(SolverStatus.Optimal, SolverStatus.Feasible);
        result.Diagnostics.EngineVersion.Should().Be("v2-ortools");
    }

    [Fact]
    public async Task SolveAsync_PreCanceledToken_ThrowsOperationCanceledException()
    {
        var engine = BuildEngine();
        using var cts = new CancellationTokenSource();
        cts.Cancel();   // cancel BEFORE SolveAsync is called
        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new[] { Section(1, 100, 3, 0) },
            Classrooms = new List<ClassroomInput>
            {
                new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
            },
            TimeSlots = new List<TimeSlotInput>()
        };

        var act = async () => await engine.SolveAsync(request, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
