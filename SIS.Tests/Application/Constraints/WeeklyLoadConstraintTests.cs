using FluentAssertions;

namespace SIS.Tests.Application.Constraints;

public class WeeklyLoadConstraintTests : SchedulingTestBase
{
    [Fact]
    public async Task ValidateExistingAsync_InstructorAtWeeklyLimit_DoesNotReturnWeeklyLoadConflict()
    {
        var instructor = AddInstructor(1, maxWeeklyHours: 4);
        var course1 = AddCourse(1, "CENG501", theoryHours: 2);
        var course2 = AddCourse(2, "CENG502", theoryHours: 2);
        AddSection(1, course1.Id, instructor.Id, AddClassroom(1, "D101", 40).Id, 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));
        AddSection(2, course2.Id, instructor.Id, AddClassroom(2, "D102", 40).Id, 30,
            (DayOfWeek.Tuesday, 9, 0, 11, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == WeeklyLoadConflict);
    }

    [Fact]
    public async Task ValidateExistingAsync_InstructorOneHourOverWeeklyLimit_ReturnsWeeklyLoadConflict()
    {
        var instructor = AddInstructor(1, maxWeeklyHours: 3);
        var course1 = AddCourse(1, "CENG503", theoryHours: 2);
        var course2 = AddCourse(2, "CENG504", theoryHours: 2);
        AddSection(1, course1.Id, instructor.Id, AddClassroom(1, "D101", 40).Id, 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));
        AddSection(2, course2.Id, instructor.Id, AddClassroom(2, "D102", 40).Id, 30,
            (DayOfWeek.Tuesday, 9, 0, 11, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == WeeklyLoadConflict);
    }

    [Fact]
    public async Task ValidateAsync_CandidateKeepsInstructorAtWeeklyLimit_DoesNotReturnWeeklyLoadConflict()
    {
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 9, 0, 10, 0, 1);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 10, 0, 11, 0, 2);

        var instructor = AddInstructor(1, maxWeeklyHours: 4);
        var existingCourse = AddCourse(1, "CENG505", theoryHours: 2);
        var candidateCourse = AddCourse(2, "CENG506", theoryHours: 2);
        AddSection(1, existingCourse.Id, instructor.Id, AddClassroom(1, "D101", 40).Id, 30,
            (DayOfWeek.Tuesday, 9, 0, 10, 0),
            (DayOfWeek.Tuesday, 10, 0, 11, 0));
        AddSection(2, candidateCourse.Id, null, null, 30);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 2,
            InstructorId = instructor.Id,
            ClassroomId = AddClassroom(2, "D102", 40).Id,
            ScheduleSlots = Slots(
                (DayOfWeek.Monday, 9, 0, 10, 0),
                (DayOfWeek.Monday, 10, 0, 11, 0))
        });

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == WeeklyLoadConflict);
    }

    [Fact]
    public async Task ValidateAsync_CandidatePushesInstructorOneHourOverWeeklyLimit_ReturnsWeeklyLoadConflict()
    {
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 9, 0, 10, 0, 1);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 10, 0, 11, 0, 2);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 11, 0, 12, 0, 3);

        var instructor = AddInstructor(1, maxWeeklyHours: 4);
        var existingCourse = AddCourse(1, "CENG507", theoryHours: 2);
        var candidateCourse = AddCourse(2, "CENG508", theoryHours: 3);
        AddSection(1, existingCourse.Id, instructor.Id, AddClassroom(1, "D101", 40).Id, 30,
            (DayOfWeek.Tuesday, 9, 0, 10, 0),
            (DayOfWeek.Tuesday, 10, 0, 11, 0));
        AddSection(2, candidateCourse.Id, null, null, 30);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 2,
            InstructorId = instructor.Id,
            ClassroomId = AddClassroom(2, "D102", 40).Id,
            ScheduleSlots = Slots(
                (DayOfWeek.Monday, 9, 0, 10, 0),
                (DayOfWeek.Monday, 10, 0, 11, 0),
                (DayOfWeek.Monday, 11, 0, 12, 0))
        });

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == WeeklyLoadConflict);
    }
}
