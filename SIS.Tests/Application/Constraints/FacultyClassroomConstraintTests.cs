using FluentAssertions;

namespace SIS.Tests.Application.Constraints;

public class FacultyClassroomConstraintTests : SchedulingTestBase
{
    [Fact]
    public async Task ValidateExistingAsync_UnassignedClassroomWhenTermAssignmentsExist_ReturnsFacultyClassroomConflict()
    {
        var instructor = AddInstructor(1);
        var assignedClassroom = AddClassroom(1, "ENG-101", 40);
        var unassignedClassroom = AddClassroom(2, "GEN-101", 40);
        AddClassroomAssignment(1, assignedClassroom.Id, EngineeringFacultyId);
        var course = AddCourse(1, "CENG201", theoryHours: 2);
        AddSection(1, course.Id, instructor.Id, unassignedClassroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == FacultyClassroomConflict);
    }

    [Fact]
    public async Task ValidateExistingAsync_ClassroomAssignedToCourseFaculty_DoesNotReturnFacultyClassroomConflict()
    {
        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "ENG-101", 40);
        AddClassroomAssignment(1, classroom.Id, EngineeringFacultyId);
        var course = AddCourse(1, "CENG202", theoryHours: 2);
        AddSection(1, course.Id, instructor.Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == FacultyClassroomConflict);
    }

    [Fact]
    public async Task ValidateExistingAsync_NoTermAssignments_AllowsFallbackToAnyClassroom()
    {
        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "FREE-101", 40);
        var course = AddCourse(1, "CENG203", theoryHours: 2);
        AddSection(1, course.Id, instructor.Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == FacultyClassroomConflict);
    }

    [Fact]
    public async Task ValidateAsync_UnassignedCandidateClassroomWhenTermAssignmentsExist_ReturnsFacultyClassroomConflict()
    {
        var instructor = AddInstructor(1);
        var assignedClassroom = AddClassroom(1, "ENG-101", 40);
        var unassignedClassroom = AddClassroom(2, "GEN-101", 40);
        AddClassroomAssignment(1, assignedClassroom.Id, EngineeringFacultyId);
        var course = AddCourse(1, "CENG204", theoryHours: 2);
        AddSection(1, course.Id, null, null, 30);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 1,
            InstructorId = instructor.Id,
            ClassroomId = unassignedClassroom.Id,
            ScheduleSlots = Slots((DayOfWeek.Monday, 9, 0, 11, 0))
        });

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == FacultyClassroomConflict);
    }

    [Fact]
    public async Task ValidateAsync_AssignedCandidateClassroom_DoesNotReturnFacultyClassroomConflict()
    {
        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "ENG-101", 40);
        AddClassroomAssignment(1, classroom.Id, EngineeringFacultyId);
        var course = AddCourse(1, "CENG205", theoryHours: 2);
        AddSection(1, course.Id, null, null, 30);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 1,
            InstructorId = instructor.Id,
            ClassroomId = classroom.Id,
            ScheduleSlots = Slots((DayOfWeek.Monday, 9, 0, 11, 0))
        });

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == FacultyClassroomConflict);
    }
}
