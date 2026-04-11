using FluentAssertions;

namespace SIS.Tests.Application.Constraints;

public class CoEnrollmentConstraintTests : SchedulingTestBase
{
    [Fact]
    public async Task ValidateExistingAsync_FrequentCoEnrollmentPairInSameSlot_AddsS4Note()
    {
        SetSystemConfig("SoftConstraint.S4.Enabled", "true");

        var course1 = AddCourse(1, "CENG821", theoryHours: 2);
        var course2 = AddCourse(2, "MATH821", theoryHours: 2, departmentId: MathematicsDepartmentId);
        AddCoEnrollmentHistory(course1.Id, course2.Id, studentCount: 2);
        AddSection(1, course1.Id, AddInstructor(1).Id, AddClassroom(1, "D101", 40).Id, 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));
        AddSection(2, course2.Id, AddInstructor(2, departmentId: MathematicsDepartmentId).Id, AddClassroom(2, "D102", 40).Id, 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Notes.Should().Contain(note => note.Contains("S4", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateExistingAsync_FrequentCoEnrollmentPairInDifferentSlots_DoesNotAddS4Note()
    {
        SetSystemConfig("SoftConstraint.S4.Enabled", "true");

        var course1 = AddCourse(1, "CENG822", theoryHours: 2);
        var course2 = AddCourse(2, "MATH822", theoryHours: 2, departmentId: MathematicsDepartmentId);
        AddCoEnrollmentHistory(course1.Id, course2.Id, studentCount: 2);
        AddSection(1, course1.Id, AddInstructor(1).Id, AddClassroom(1, "D101", 40).Id, 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));
        AddSection(2, course2.Id, AddInstructor(2, departmentId: MathematicsDepartmentId).Id, AddClassroom(2, "D102", 40).Id, 30,
            (DayOfWeek.Tuesday, 9, 0, 11, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Notes.Should().NotContain(note => note.Contains("S4", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_FrequentCoEnrollmentCandidateInSameSlot_AddsS4Note()
    {
        SetSystemConfig("SoftConstraint.S4.Enabled", "true");

        var course1 = AddCourse(1, "CENG823", theoryHours: 2);
        var course2 = AddCourse(2, "MATH823", theoryHours: 2, departmentId: MathematicsDepartmentId);
        AddCoEnrollmentHistory(course1.Id, course2.Id, studentCount: 2);
        AddSection(1, course1.Id, AddInstructor(1).Id, AddClassroom(1, "D101", 40).Id, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0),
            (DayOfWeek.Monday, 10, 0, 11, 0));
        AddSection(2, course2.Id, null, null, 30);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 2,
            InstructorId = AddInstructor(2, departmentId: MathematicsDepartmentId).Id,
            ClassroomId = AddClassroom(2, "D102", 40).Id,
            ScheduleSlots = Slots(
                (DayOfWeek.Monday, 9, 0, 10, 0),
                (DayOfWeek.Monday, 10, 0, 11, 0))
        });

        result.IsValid.Should().BeTrue();
        result.Notes.Should().Contain(note => note.Contains("S4", StringComparison.Ordinal));
    }
}
