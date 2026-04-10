using FluentAssertions;

namespace SIS.Tests.Application.Constraints;

public class ClassroomConflictEdgeCaseTests : SchedulingTestBase
{
    [Fact]
    public async Task ValidateExistingAsync_SameClassroomPartialOverlap_ReturnsClassroomConflict()
    {
        var classroom = AddClassroom(1, "D101", 40);
        var course1 = AddCourse(1, "CENG601", theoryHours: 1);
        var course2 = AddCourse(2, "CENG602", theoryHours: 1);
        AddSection(1, course1.Id, AddInstructor(1).Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0));
        AddSection(2, course2.Id, AddInstructor(2).Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 30, 10, 30));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == ClassroomConflict);
    }

    [Fact]
    public async Task ValidateExistingAsync_SameClassroomAdjacentSlots_DoesNotReturnClassroomConflict()
    {
        var classroom = AddClassroom(1, "D101", 40);
        var course1 = AddCourse(1, "CENG603", theoryHours: 1);
        var course2 = AddCourse(2, "CENG604", theoryHours: 1);
        AddSection(1, course1.Id, AddInstructor(1).Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0));
        AddSection(2, course2.Id, AddInstructor(2).Id, classroom.Id, 30,
            (DayOfWeek.Monday, 10, 0, 11, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == ClassroomConflict);
    }

    [Fact]
    public async Task ValidateExistingAsync_SameClassroomDifferentDay_DoesNotReturnClassroomConflict()
    {
        var classroom = AddClassroom(1, "D101", 40);
        var course1 = AddCourse(1, "CENG605", theoryHours: 1);
        var course2 = AddCourse(2, "CENG606", theoryHours: 1);
        AddSection(1, course1.Id, AddInstructor(1).Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0));
        AddSection(2, course2.Id, AddInstructor(2).Id, classroom.Id, 30,
            (DayOfWeek.Tuesday, 9, 0, 10, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == ClassroomConflict);
    }

    [Fact]
    public async Task ValidateAsync_CandidateSameClassroomPartialOverlap_ReturnsClassroomConflict()
    {
        var classroom = AddClassroom(1, "D101", 40);
        var existingCourse = AddCourse(1, "CENG607", theoryHours: 1);
        var candidateCourse = AddCourse(2, "CENG608", theoryHours: 1);
        AddSection(1, existingCourse.Id, AddInstructor(1).Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0));
        AddSection(2, candidateCourse.Id, null, null, 30);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 2,
            InstructorId = AddInstructor(2).Id,
            ClassroomId = classroom.Id,
            ScheduleSlots = Slots((DayOfWeek.Monday, 9, 30, 10, 30))
        });

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == ClassroomConflict);
    }
}
