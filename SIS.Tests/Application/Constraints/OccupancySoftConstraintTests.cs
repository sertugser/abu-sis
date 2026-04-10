using FluentAssertions;

namespace SIS.Tests.Application.Constraints;

public class OccupancySoftConstraintTests : SchedulingTestBase
{
    [Fact]
    public async Task ValidateExistingAsync_LowOccupancy_AddsS1Note()
    {
        SetSystemConfig("SoftConstraint.S1.Enabled", "true");

        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "D101", 100);
        var course = AddCourse(1, "CENG801", theoryHours: 2);
        AddSection(1, course.Id, instructor.Id, classroom.Id, 20,
            (DayOfWeek.Monday, 9, 0, 11, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Notes.Should().Contain(note => note.Contains("S1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateExistingAsync_IdealOccupancy_DoesNotAddS1Note()
    {
        SetSystemConfig("SoftConstraint.S1.Enabled", "true");

        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "D101", 100);
        var course = AddCourse(1, "CENG802", theoryHours: 2);
        AddSection(1, course.Id, instructor.Id, classroom.Id, 70,
            (DayOfWeek.Monday, 9, 0, 11, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Notes.Should().NotContain(note => note.Contains("S1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_LowOccupancyCandidate_AddsS1Note()
    {
        SetSystemConfig("SoftConstraint.S1.Enabled", "true");

        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "D101", 100);
        var course = AddCourse(1, "CENG803", theoryHours: 2);
        AddSection(1, course.Id, null, null, 20);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 1,
            InstructorId = instructor.Id,
            ClassroomId = classroom.Id,
            ScheduleSlots = Slots((DayOfWeek.Monday, 9, 0, 11, 0))
        });

        result.IsValid.Should().BeTrue();
        result.Notes.Should().Contain(note => note.Contains("S1", StringComparison.Ordinal));
    }
}
