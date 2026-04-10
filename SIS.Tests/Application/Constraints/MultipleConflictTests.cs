using FluentAssertions;

namespace SIS.Tests.Application.Constraints;

public class MultipleConflictTests : SchedulingTestBase
{
    [Fact]
    public async Task ValidateExistingAsync_SameSlotSmallClassroomSameInstructor_ReturnsClassroomInstructorAndCapacityConflicts()
    {
        var instructor = AddInstructor(1, maxWeeklyHours: 20);
        var classroom = AddClassroom(1, "D101", 20);
        var course1 = AddCourse(1, "CENG901", theoryHours: 2);
        var course2 = AddCourse(2, "CENG902", theoryHours: 2);
        AddSection(1, course1.Id, instructor.Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));
        AddSection(2, course2.Id, instructor.Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == ClassroomConflict);
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == InstructorConflict);
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == CapacityConflict);
    }
}
