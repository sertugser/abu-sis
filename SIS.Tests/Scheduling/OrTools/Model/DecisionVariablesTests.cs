using FluentAssertions;
using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;

namespace SIS.Tests.Scheduling.OrTools.Model;

public class DecisionVariablesTests
{
    private static SectionInput Section(int id, int theory, int lab) => new()
    {
        Id = id,
        CourseId = 100,
        CourseCode = "CENG101",
        CourseName = "Algorithms",
        SectionNumber = 1,
        Capacity = 40,
        TheoryHours = theory,
        LabHours = lab,
        TeachingMethod = TeachingMethod.FaceToFace
    };

    [Fact]
    public void CreateIntervalForSection_LectureOnly_ReturnsLectureIntervalAndNoLab()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);

        registry.CreateIntervalsForSection(Section(1, theory: 3, lab: 0));

        registry.GetLectureInterval(1).Should().NotBeNull();
        registry.GetLabInterval(1).Should().BeNull();
    }

    [Fact]
    public void CreateIntervalForSection_LectureAndLab_ReturnsBothIntervals()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);

        registry.CreateIntervalsForSection(Section(2, theory: 3, lab: 2));

        registry.GetLectureInterval(2).Should().NotBeNull();
        registry.GetLabInterval(2).Should().NotBeNull();
    }

    [Fact]
    public void CreateRoomVariableForSection_RegistersIntVar()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(1, theory: 3, lab: 0));

        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture },
            new() { Id = 10, Name = "L139", Capacity = 40, Type = ClassroomType.Lab }
        };

        registry.CreateRoomVariable(sectionId: 1, ComponentType.Lecture, classrooms);

        registry.GetLectureRoom(1).Should().NotBeNull();
    }

    [Fact]
    public void GetLectureInterval_UnregisteredSection_ThrowsKeyNotFoundException()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);

        var act = () => registry.GetLectureInterval(999);

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void GetLectureInterval_LabOnlySection_ReturnsNull()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(5, theory: 0, lab: 2));

        registry.GetLectureInterval(5).Should().BeNull();
    }

    [Fact]
    public void GetLabInterval_LectureOnlySection_ReturnsNull()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(6, theory: 3, lab: 0));

        registry.GetLabInterval(6).Should().BeNull();
    }

    [Fact]
    public void GetLabInterval_UnregisteredSection_ThrowsKeyNotFoundException()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);

        var act = () => registry.GetLabInterval(999);

        act.Should().Throw<KeyNotFoundException>();
    }
}
