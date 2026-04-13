using FluentAssertions;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Scheduling;

namespace SIS.Tests.Application.Scheduling;

public class MutableScheduleStateTests
{
    private const int TermId = 1;

    private static SectionSnapshot CreateSnapshot(
        int sectionId = 1,
        int courseId = 100,
        int? instructorId = 10,
        int theoryHours = 3,
        int labHours = 0)
    {
        return new SectionSnapshot
        {
            SectionId = sectionId,
            CourseId = courseId,
            CourseCode = $"CS{courseId}",
            CourseName = $"Course {courseId}",
            TermId = TermId,
            TheoryHours = theoryHours,
            LabHours = labHours,
            Capacity = 40,
            EnrolledCount = 30,
            InstructorId = instructorId,
            InstructorName = "Test Instructor",
            InstructorMaxWeeklyHours = 20,
            DepartmentId = 1,
            ClassroomType = ClassroomType.Lecture
        };
    }

    private static List<BlockAssignment> CreateTheoryBlocks(int classroomId = 1)
    {
        return new List<BlockAssignment>
        {
            new(BlockType.Theory, classroomId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0)),
            new(BlockType.Theory, classroomId, DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(11, 0)),
            new(BlockType.Theory, classroomId, DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(10, 0)),
        };
    }

    [Fact]
    public void Assign_adds_section_to_placements_and_indexes()
    {
        // Arrange
        var state = new MutableScheduleState(TermId);
        var snapshot = CreateSnapshot(sectionId: 1, instructorId: 10);
        var blocks = CreateTheoryBlocks(classroomId: 5);

        // Act
        state.Assign(1, snapshot, blocks);

        // Assert — placements
        state.IsAssigned(1).Should().BeTrue();
        state.AllPlacements.Should().ContainKey(1);
        state.GetPlacement(1)!.Blocks.Should().HaveCount(3);

        // Assert — classroom occupancy
        var crMonday = state.GetClassroomOccupancy(5, DayOfWeek.Monday);
        crMonday.Should().HaveCount(2);
        crMonday.Should().AllSatisfy(s => s.SectionId.Should().Be(1));

        var crWednesday = state.GetClassroomOccupancy(5, DayOfWeek.Wednesday);
        crWednesday.Should().HaveCount(1);

        // Assert — instructor occupancy
        var instrMonday = state.GetInstructorOccupancy(10, DayOfWeek.Monday);
        instrMonday.Should().HaveCount(2);

        var instrWednesday = state.GetInstructorOccupancy(10, DayOfWeek.Wednesday);
        instrWednesday.Should().HaveCount(1);

        // Assert — day section counts (section touches Monday and Wednesday)
        state.GetDaySectionCount(DayOfWeek.Monday).Should().Be(1);
        state.GetDaySectionCount(DayOfWeek.Wednesday).Should().Be(1);
        state.GetDaySectionCount(DayOfWeek.Friday).Should().Be(0);
    }

    [Fact]
    public void Unassign_removes_section_and_cleans_indexes()
    {
        // Arrange
        var state = new MutableScheduleState(TermId);
        var snapshot = CreateSnapshot(sectionId: 2, instructorId: 20);
        var blocks = CreateTheoryBlocks(classroomId: 3);
        state.Assign(2, snapshot, blocks);

        // Act
        state.Unassign(2);

        // Assert
        state.IsAssigned(2).Should().BeFalse();
        state.GetPlacement(2).Should().BeNull();
        state.AllPlacements.Should().BeEmpty();

        state.GetClassroomOccupancy(3, DayOfWeek.Monday).Should().BeEmpty();
        state.GetClassroomOccupancy(3, DayOfWeek.Wednesday).Should().BeEmpty();

        state.GetInstructorOccupancy(20, DayOfWeek.Monday).Should().BeEmpty();
        state.GetInstructorOccupancy(20, DayOfWeek.Wednesday).Should().BeEmpty();

        state.GetDaySectionCount(DayOfWeek.Monday).Should().Be(0);
        state.GetDaySectionCount(DayOfWeek.Wednesday).Should().Be(0);
    }

    [Fact]
    public void Clone_creates_independent_copy()
    {
        // Arrange
        var state = new MutableScheduleState(TermId);
        var snapshot = CreateSnapshot(sectionId: 3, instructorId: 30);
        var blocks = CreateTheoryBlocks(classroomId: 7);
        state.Assign(3, snapshot, blocks);

        // Act
        var clone = state.Clone();
        state.Unassign(3);

        // Assert — original is empty
        state.IsAssigned(3).Should().BeFalse();
        state.AllPlacements.Should().BeEmpty();

        // Assert — clone still has the section
        clone.IsAssigned(3).Should().BeTrue();
        clone.GetPlacement(3).Should().NotBeNull();
        clone.GetPlacement(3)!.Blocks.Should().HaveCount(3);
        clone.GetClassroomOccupancy(7, DayOfWeek.Monday).Should().HaveCount(2);
        clone.GetInstructorOccupancy(30, DayOfWeek.Monday).Should().HaveCount(2);
        clone.GetDaySectionCount(DayOfWeek.Monday).Should().Be(1);
    }

    [Fact]
    public void ToSectionSnapshot_returns_snapshot_with_time_blocks()
    {
        // Arrange — section with theory (Monday) + lab (Wednesday)
        var state = new MutableScheduleState(TermId);
        var snapshot = CreateSnapshot(sectionId: 4, courseId: 200, instructorId: 40, theoryHours: 2, labHours: 2);
        var blocks = new List<BlockAssignment>
        {
            new(BlockType.Theory, 10, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0)),
            new(BlockType.Lab, 20, DayOfWeek.Wednesday, new TimeOnly(13, 0), new TimeOnly(15, 0)),
        };
        state.Assign(4, snapshot, blocks);

        // Act
        var result = state.ToSectionSnapshot(4);

        // Assert
        result.SectionId.Should().Be(4);
        result.CourseId.Should().Be(200);
        result.ClassroomId.Should().Be(10); // first block's classroom

        result.TimeBlocks.Should().HaveCount(2);
        result.TimeBlocks[0].Day.Should().Be(DayOfWeek.Monday);
        result.TimeBlocks[0].StartTime.Should().Be(new TimeOnly(9, 0));
        result.TimeBlocks[0].EndTime.Should().Be(new TimeOnly(11, 0));
        result.TimeBlocks[0].ClassroomId.Should().Be(10);

        result.TimeBlocks[1].Day.Should().Be(DayOfWeek.Wednesday);
        result.TimeBlocks[1].StartTime.Should().Be(new TimeOnly(13, 0));
        result.TimeBlocks[1].EndTime.Should().Be(new TimeOnly(15, 0));
        result.TimeBlocks[1].ClassroomId.Should().Be(20);

        // 2 hours theory + 2 hours lab = 4
        result.AssignedTimeSlotCount.Should().Be(4);

        // Original snapshot fields preserved
        result.TheoryHours.Should().Be(2);
        result.LabHours.Should().Be(2);
        result.InstructorId.Should().Be(40);
    }

    [Fact]
    public void GetAssignedNonPinnedSectionIds_excludes_pinned()
    {
        // Arrange
        var state = new MutableScheduleState(TermId);

        var snap1 = CreateSnapshot(sectionId: 10);
        var snap2 = CreateSnapshot(sectionId: 20);
        var snap3 = CreateSnapshot(sectionId: 30);

        var blocks1 = new List<BlockAssignment>
        {
            new(BlockType.Theory, 1, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0))
        };
        var blocks2 = new List<BlockAssignment>
        {
            new(BlockType.Theory, 1, DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(10, 0))
        };
        var blocks3 = new List<BlockAssignment>
        {
            new(BlockType.Theory, 1, DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(10, 0))
        };

        state.Assign(10, snap1, blocks1, isPinned: true);
        state.Assign(20, snap2, blocks2, isPinned: false);
        state.Assign(30, snap3, blocks3, isPinned: false);

        // Act
        var nonPinned = state.GetAssignedNonPinnedSectionIds();

        // Assert
        nonPinned.Should().HaveCount(2);
        nonPinned.Should().Contain(20);
        nonPinned.Should().Contain(30);
        nonPinned.Should().NotContain(10);

        state.IsPinned(10).Should().BeTrue();
        state.IsPinned(20).Should().BeFalse();
    }
}
