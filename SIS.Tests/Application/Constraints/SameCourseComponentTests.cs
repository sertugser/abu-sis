using FluentAssertions;

namespace SIS.Tests.Application.Constraints;

public class SameCourseComponentTests : SchedulingTestBase
{
    [Fact]
    public async Task ValidateExistingAsync_SameCourseSectionsInSameSlot_ReturnsSameCourseOverlapConflict()
    {
        var instructor1 = AddInstructor(1);
        var instructor2 = AddInstructor(2);
        var classroom1 = AddClassroom(1, "D101", 40);
        var classroom2 = AddClassroom(2, "D102", 40);
        var course = AddCourse(1, "CENG301", theoryHours: 2);
        AddSection(1, course.Id, instructor1.Id, classroom1.Id, 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));
        AddSection(2, course.Id, instructor2.Id, classroom2.Id, 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict =>
            conflict.ConflictType == SameCourseComponentConflict
            || conflict.ConflictType == SameCourseSectionConflict);
    }

    [Fact]
    public async Task ValidateExistingAsync_SameCourseSectionsInDifferentSlots_DoesNotReturnSameCourseOverlapConflict()
    {
        var instructor1 = AddInstructor(1);
        var instructor2 = AddInstructor(2);
        var classroom1 = AddClassroom(1, "D101", 40);
        var classroom2 = AddClassroom(2, "D102", 40);
        var course = AddCourse(1, "CENG302", theoryHours: 2);
        AddSection(1, course.Id, instructor1.Id, classroom1.Id, 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));
        AddSection(2, course.Id, instructor2.Id, classroom2.Id, 30,
            (DayOfWeek.Tuesday, 9, 0, 11, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().NotContain(conflict =>
            conflict.ConflictType == SameCourseComponentConflict
            || conflict.ConflictType == SameCourseSectionConflict);
    }

    [Fact]
    public async Task ValidateAsync_SameCourseCandidateInSameSlot_ReturnsSameCourseOverlapConflict()
    {
        var instructor1 = AddInstructor(1);
        var instructor2 = AddInstructor(2);
        var classroom1 = AddClassroom(1, "D101", 40);
        var classroom2 = AddClassroom(2, "D102", 40);
        var course = AddCourse(1, "CENG303", theoryHours: 2);
        AddSection(1, course.Id, instructor1.Id, classroom1.Id, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0),
            (DayOfWeek.Monday, 10, 0, 11, 0));
        AddSection(2, course.Id, null, null, 30);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 2,
            InstructorId = instructor2.Id,
            ClassroomId = classroom2.Id,
            ScheduleSlots = Slots(
                (DayOfWeek.Monday, 9, 0, 10, 0),
                (DayOfWeek.Monday, 10, 0, 11, 0))
        });

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict =>
            conflict.ConflictType == SameCourseComponentConflict
            || conflict.ConflictType == SameCourseSectionConflict);
    }
}
