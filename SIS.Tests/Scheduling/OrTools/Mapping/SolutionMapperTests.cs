using FluentAssertions;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Mapping;

namespace SIS.Tests.Scheduling.OrTools.Mapping;

public class SolutionMapperTests
{
    [Fact]
    public void MapInfeasible_WithEmptyUnsatCore_ReturnsInfeasibleStatusAndAllSectionsAsFailures()
    {
        var mapper = new SolutionMapper();
        var request = new SolverRequest
        {
            TermId = 1,
            FacultyId = 1,
            Sections = new List<SectionInput>
            {
                new()
                {
                    Id = 1,
                    CourseId = 10,
                    CourseCode = "X",
                    CourseName = "X",
                    SectionNumber = 1,
                    Capacity = 40,
                    TheoryHours = 3,
                    LabHours = 0,
                    TeachingMethod = TeachingMethod.FaceToFace
                }
            },
            Classrooms = new List<ClassroomInput>(),
            TimeSlots = new List<TimeSlotInput>()
        };

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
}
