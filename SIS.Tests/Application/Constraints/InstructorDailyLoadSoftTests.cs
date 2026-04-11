using FluentAssertions;

namespace SIS.Tests.Application.Constraints;

public class InstructorDailyLoadSoftTests : SchedulingTestBase
{
    [Fact]
    public async Task ValidateExistingAsync_DailyLoadExceedsConfiguredSoftMax_AddsS2Note()
    {
        SetSystemConfig("SoftConstraint.S2.Enabled", "true");

        var instructor = AddInstructor(1, maxWeeklyHours: 20);
        var course1 = AddCourse(1, "CENG811", theoryHours: 3);
        var course2 = AddCourse(2, "CENG812", theoryHours: 4);
        AddSection(1, course1.Id, instructor.Id, AddClassroom(1, "D101", 40).Id, 30,
            (DayOfWeek.Monday, 9, 0, 12, 0));
        AddSection(2, course2.Id, instructor.Id, AddClassroom(2, "D102", 40).Id, 30,
            (DayOfWeek.Monday, 13, 0, 17, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Notes.Should().Contain(note => note.Contains("S2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateExistingAsync_DailyLoadAtConfiguredSoftMax_DoesNotAddS2Note()
    {
        SetSystemConfig("SoftConstraint.S2.Enabled", "true");

        var instructor = AddInstructor(1, maxWeeklyHours: 20);
        var course1 = AddCourse(1, "CENG813", theoryHours: 2);
        var course2 = AddCourse(2, "CENG814", theoryHours: 4);
        AddSection(1, course1.Id, instructor.Id, AddClassroom(1, "D101", 40).Id, 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));
        AddSection(2, course2.Id, instructor.Id, AddClassroom(2, "D102", 40).Id, 30,
            (DayOfWeek.Monday, 13, 0, 17, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Notes.Should().NotContain(note => note.Contains("S2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_CandidatePushesDailyLoadOverConfiguredSoftMax_AddsS2Note()
    {
        SetSystemConfig("SoftConstraint.S2.Enabled", "true");

        var instructor = AddInstructor(1, maxWeeklyHours: 20);
        var existingCourse = AddCourse(1, "CENG815", theoryHours: 3);
        var candidateCourse = AddCourse(2, "CENG816", theoryHours: 4);
        AddSection(1, existingCourse.Id, instructor.Id, AddClassroom(1, "D101", 40).Id, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0),
            (DayOfWeek.Monday, 10, 0, 11, 0),
            (DayOfWeek.Monday, 11, 0, 12, 0));
        AddSection(2, candidateCourse.Id, null, null, 30);

        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 13, 0, 14, 0, 4);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 14, 0, 15, 0, 5);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 15, 0, 16, 0, 6);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 16, 0, 17, 0, 7);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 2,
            InstructorId = instructor.Id,
            ClassroomId = AddClassroom(2, "D102", 40).Id,
            ScheduleSlots = Slots(
                (DayOfWeek.Monday, 13, 0, 14, 0),
                (DayOfWeek.Monday, 14, 0, 15, 0),
                (DayOfWeek.Monday, 15, 0, 16, 0),
                (DayOfWeek.Monday, 16, 0, 17, 0))
        });

        result.IsValid.Should().BeTrue();
        result.Notes.Should().Contain(note => note.Contains("S2", StringComparison.Ordinal));
    }
}
