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

    [Fact]
    public void CreateOptionalRoomInterval_RegistersIntervalAndPresenceLiteral()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        var section = Section(1, theory: 3, lab: 0);
        registry.CreateIntervalsForSection(section);
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
        };
        registry.CreateRoomVariable(1, ComponentType.Lecture, classrooms);

        var optInterval = registry.CreateOptionalRoomInterval(1, 9, ComponentType.Lecture, duration: 3);

        optInterval.Should().NotBeNull();
        registry.AllOptionalIntervals.Should().ContainKey((1, 9, ComponentType.Lecture));
        registry.AllPresenceLiterals.Should().ContainKey((1, 9, ComponentType.Lecture));
    }

    [Fact]
    public void CreateOptionalRoomInterval_CalledTwice_ReturnsSameInterval()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        var section = Section(1, theory: 3, lab: 0);
        registry.CreateIntervalsForSection(section);
        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "C 005", Capacity = 50, Type = ClassroomType.Lecture }
        };
        registry.CreateRoomVariable(1, ComponentType.Lecture, classrooms);

        var first = registry.CreateOptionalRoomInterval(1, 9, ComponentType.Lecture, duration: 3);
        var second = registry.CreateOptionalRoomInterval(1, 9, ComponentType.Lecture, duration: 3);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void CreateIntervalsForSection_RegistersPresenceLiteral()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(1, theory: 3, lab: 0));

        var present = registry.GetSectionPresent(1);

        present.Should().NotBeNull();
    }

    [Fact]
    public void GetSectionPresent_UnregisteredSection_ThrowsKeyNotFoundException()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);

        Action act = () => registry.GetSectionPresent(999);

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void AllSectionPresent_AfterRegistering_ReturnsAllSections()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(1, theory: 3, lab: 0));
        registry.CreateIntervalsForSection(Section(2, theory: 0, lab: 2));
        registry.CreateIntervalsForSection(Section(3, theory: 3, lab: 2));

        registry.AllSectionPresent.Should().HaveCount(3);
        registry.AllSectionPresent.Should().ContainKeys(1, 2, 3);
    }

    // Plan 4 Task 2 — Parallel lab support: N IntVar lab rooms per section
    [Fact]
    public void CreateRoomVariable_LabGroupCount2_RegistersTwoRoomVars()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(1, theory: 0, lab: 2));

        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "L1", Capacity = 40, Type = ClassroomType.Lab },
            new() { Id = 10, Name = "L2", Capacity = 40, Type = ClassroomType.Lab }
        };

        registry.CreateRoomVariable(1, ComponentType.Lab, classrooms, groupCount: 2);

        var labRooms = registry.GetLabRooms(1);
        labRooms.Should().HaveCount(2);
        // AllLabRooms accessor surfaces the same list
        registry.AllLabRooms.Should().ContainKey(1);
        registry.AllLabRooms[1].Should().BeSameAs(labRooms);
        // Distinct IntVar instances per group (AddAllDifferent operates on these)
        labRooms[0].Should().NotBeSameAs(labRooms[1]);
    }

    [Fact]
    public void GetLabRoom_BackwardCompat_ReturnsFirstGroupRoom()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(1, theory: 0, lab: 2));

        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "L", Capacity = 40, Type = ClassroomType.Lab }
        };

        registry.CreateRoomVariable(1, ComponentType.Lab, classrooms, groupCount: 1);

        // Shim must return same instance as index 0 of the multi-room list
        registry.GetLabRoom(1).Should().BeSameAs(registry.GetLabRooms(1)[0]);
    }

    [Fact]
    public void CreateRoomVariable_GroupCountZero_ThrowsArgumentOutOfRange()
    {
        var model = new CpModel();
        var registry = new DecisionVariables(model, totalSlots: 50);
        registry.CreateIntervalsForSection(Section(1, theory: 0, lab: 2));

        var classrooms = new List<ClassroomInput>
        {
            new() { Id = 9, Name = "L", Capacity = 40, Type = ClassroomType.Lab }
        };

        Action act = () => registry.CreateRoomVariable(1, ComponentType.Lab, classrooms, groupCount: 0);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .Where(ex => ex.ParamName == "groupCount");
    }
}
