using FluentAssertions;

namespace SIS.Tests.Application.Constraints;

public class InstructorConflictEdgeCaseTests : SchedulingTestBase
{
    [Fact]
    public async Task ValidateExistingAsync_SameInstructorPartialOverlap_ReturnsInstructorConflict()
    {
        var instructor = AddInstructor(1);
        var course1 = AddCourse(1, "CENG701", theoryHours: 1);
        var course2 = AddCourse(2, "CENG702", theoryHours: 1);
        AddSection(1, course1.Id, instructor.Id, AddClassroom(1, "D101", 40).Id, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0));
        AddSection(2, course2.Id, instructor.Id, AddClassroom(2, "D102", 40).Id, 30,
            (DayOfWeek.Monday, 9, 30, 10, 30));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == InstructorConflict);
    }

    [Fact]
    public async Task ValidateExistingAsync_SameInstructorAdjacentSlots_DoesNotReturnInstructorConflict()
    {
        var instructor = AddInstructor(1);
        var course1 = AddCourse(1, "CENG703", theoryHours: 1);
        var course2 = AddCourse(2, "CENG704", theoryHours: 1);
        AddSection(1, course1.Id, instructor.Id, AddClassroom(1, "D101", 40).Id, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0));
        AddSection(2, course2.Id, instructor.Id, AddClassroom(2, "D102", 40).Id, 30,
            (DayOfWeek.Monday, 10, 0, 11, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == InstructorConflict);
    }

    [Fact]
    public async Task ValidateExistingAsync_SameInstructorDifferentTerm_DoesNotReturnInstructorConflict()
    {
        var instructor = AddInstructor(1);
        var activeCourse = AddCourse(1, "CENG705", theoryHours: 1);
        var previousCourse = AddCourse(2, "CENG706", theoryHours: 1);
        AddSection(1, activeCourse.Id, instructor.Id, AddClassroom(1, "D101", 40).Id, ActiveTermId, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0));
        AddSection(2, previousCourse.Id, instructor.Id, AddClassroom(2, "D102", 40).Id, PreviousTermId, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == InstructorConflict);
    }

    [Fact]
    public async Task ValidateAsync_CandidateSameInstructorPartialOverlap_ReturnsInstructorConflict()
    {
        var instructor = AddInstructor(1);
        var existingCourse = AddCourse(1, "CENG707", theoryHours: 1);
        var candidateCourse = AddCourse(2, "CENG708", theoryHours: 1);
        AddSection(1, existingCourse.Id, instructor.Id, AddClassroom(1, "D101", 40).Id, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0));
        AddSection(2, candidateCourse.Id, null, null, 30);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 2,
            InstructorId = instructor.Id,
            ClassroomId = AddClassroom(2, "D102", 40).Id,
            ScheduleSlots = Slots((DayOfWeek.Monday, 9, 30, 10, 30))
        });

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == InstructorConflict);
    }
}
