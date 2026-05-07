using FluentAssertions;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools;

namespace SIS.Tests.Scheduling.OrTools;

public class PreflightFeasibilityCheckerTests
{
    private static SectionInput Section(int id, int theory, int lab, int capacity = 40) => new()
    {
        Id = id,
        CourseId = 100,
        CourseCode = "X",
        CourseName = "X",
        SectionNumber = 1,
        Capacity = capacity,
        TheoryHours = theory,
        LabHours = lab,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    [Fact]
    public void Check_LabSectionWithNoLabRooms_MarkedInfeasible()
    {
        var checker = new PreflightFeasibilityChecker();
        var sections = new[] { Section(1, theory: 0, lab: 2, capacity: 40) };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "Lecture only", Capacity = 50, Type = ClassroomType.Lecture }
        };

        var result = checker.Check(sections, classrooms);

        result.FeasibleSections.Should().BeEmpty();
        result.InfeasibleSections.Should().HaveCount(1);
        result.InfeasibleSections[0].Section.Id.Should().Be(1);
        result.InfeasibleSections[0].Reason.Should().Contain("Lab");
    }

    [Fact]
    public void Check_LectureSectionWithCapacityExceedingAllRooms_MarkedInfeasible()
    {
        var checker = new PreflightFeasibilityChecker();
        var sections = new[] { Section(1, theory: 3, lab: 0, capacity: 200) };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "Small", Capacity = 50, Type = ClassroomType.Lecture }
        };

        var result = checker.Check(sections, classrooms);

        result.FeasibleSections.Should().BeEmpty();
        result.InfeasibleSections.Should().HaveCount(1);
        result.InfeasibleSections[0].Reason.Should().Contain("kapasit");
    }

    [Fact]
    public void Check_ValidSection_MarkedFeasible()
    {
        var checker = new PreflightFeasibilityChecker();
        var sections = new[] { Section(1, theory: 3, lab: 2, capacity: 40) };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "Lab", Capacity = 50, Type = ClassroomType.Lab },
            new ClassroomInput { Id = 2, Name = "Lecture", Capacity = 50, Type = ClassroomType.Lecture }
        };

        var result = checker.Check(sections, classrooms);

        result.FeasibleSections.Should().HaveCount(1);
        result.InfeasibleSections.Should().BeEmpty();
    }

    [Fact]
    public void Check_ComputerLabAcceptedForLabSection()
    {
        var checker = new PreflightFeasibilityChecker();
        var sections = new[] { Section(1, theory: 0, lab: 2, capacity: 40) };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "PC Lab", Capacity = 50, Type = ClassroomType.Computer }
        };

        var result = checker.Check(sections, classrooms);

        // Computer type counts as a valid lab classroom
        result.FeasibleSections.Should().HaveCount(1);
        result.InfeasibleSections.Should().BeEmpty();
    }

    [Fact]
    public void Check_MixedSectionLabRoomTooSmall_MarkedInfeasible()
    {
        // Section needs both theory + lab. Lab room exists but capacity insufficient.
        var checker = new PreflightFeasibilityChecker();
        var sections = new[] { Section(1, theory: 3, lab: 2, capacity: 40) };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "Lecture", Capacity = 60, Type = ClassroomType.Lecture },
            new ClassroomInput { Id = 2, Name = "Tiny Lab", Capacity = 20, Type = ClassroomType.Lab }
        };

        var result = checker.Check(sections, classrooms);

        result.InfeasibleSections.Should().HaveCount(1);
        result.InfeasibleSections[0].Reason.Should().Contain("Lab");
    }

    [Fact]
    public void Check_MultipleSectionsMixed_PartialFeasibility()
    {
        var checker = new PreflightFeasibilityChecker();
        var sections = new[]
        {
            Section(1, theory: 3, lab: 2, capacity: 40),  // feasible
            Section(2, theory: 3, lab: 0, capacity: 200),  // capacity too large
            Section(3, theory: 0, lab: 2, capacity: 40)    // feasible
        };
        var classrooms = new[]
        {
            new ClassroomInput { Id = 1, Name = "Lab", Capacity = 50, Type = ClassroomType.Lab },
            new ClassroomInput { Id = 2, Name = "Lecture", Capacity = 70, Type = ClassroomType.Lecture }
        };

        var result = checker.Check(sections, classrooms);

        result.FeasibleSections.Should().HaveCount(2);
        result.FeasibleSections.Select(s => s.Id).Should().BeEquivalentTo(new[] { 1, 3 });
        result.InfeasibleSections.Should().HaveCount(1);
        result.InfeasibleSections[0].Section.Id.Should().Be(2);
    }
}
