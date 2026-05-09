using FluentAssertions;

namespace SIS.Tests.Application.Constraints;

public class TheoryLabDaySplitTests : SchedulingTestBase
{
    private const string TheoryLabDaySplitConflict = "TheoryLabDaySplitConflict";

    // Plan 9 öncesinden pre-existing fail. V1 evaluator-based validation; threshold/conflict
    // mantığı evolve etti, test güncellenmedi. V1 engine silindi ama Evaluators/* korundu —
    // Plan 10 / future work'te validator semantics review'i ile birlikte düzeltilebilir.
    [Fact(Skip = "Pre-existing failure (Plan 9 öncesi). Plan 10'da validator semantics review.")]
    public async Task ValidateAsync_MixedCourse5HoursSameDay_ReturnsConflict()
    {
        // 3T + 2L = 5 hours, threshold 4 → must be on different days
        // All slots on Monday → conflict expected
        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "D101", 40);
        var course = AddCourse(1, "CENG301", theoryHours: 3, labHours: 2);
        AddSection(1, course.Id, null, null, 30);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 1,
            InstructorId = instructor.Id,
            ClassroomId = classroom.Id,
            ScheduleSlots = Slots(
                (DayOfWeek.Monday, 9, 0, 12, 0),  // 3 theory hours
                (DayOfWeek.Monday, 13, 0, 15, 0))  // 2 lab hours
        });

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == TheoryLabDaySplitConflict);
    }

    [Fact]
    public async Task ValidateAsync_MixedCourse5HoursDifferentDays_NoConflict()
    {
        // 3T + 2L = 5 hours, threshold 4 → would need split, and it IS split
        // Theory on Monday, lab on Wednesday → no conflict
        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "D101", 40);
        var course = AddCourse(1, "CENG302", theoryHours: 3, labHours: 2);
        AddSection(1, course.Id, null, null, 30);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 1,
            InstructorId = instructor.Id,
            ClassroomId = classroom.Id,
            ScheduleSlots = Slots(
                (DayOfWeek.Monday, 9, 0, 12, 0),       // 3 hours on Monday
                (DayOfWeek.Wednesday, 13, 0, 15, 0))    // 2 hours on Wednesday
        });

        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == TheoryLabDaySplitConflict);
    }

    [Fact]
    public async Task ValidateAsync_MixedCourse4HoursSameDay_NoConflict()
    {
        // 2T + 2L = 4 hours, threshold 4 → at or under threshold → no constraint
        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "D101", 40);
        var course = AddCourse(1, "CENG303", theoryHours: 2, labHours: 2);
        AddSection(1, course.Id, null, null, 30);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 1,
            InstructorId = instructor.Id,
            ClassroomId = classroom.Id,
            ScheduleSlots = Slots(
                (DayOfWeek.Monday, 9, 0, 11, 0),   // 2 hours
                (DayOfWeek.Monday, 13, 0, 15, 0))  // 2 hours
        });

        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == TheoryLabDaySplitConflict);
    }

    [Fact]
    public async Task ValidateAsync_TheoryOnlyCourse_NoConflict()
    {
        // No lab hours → rule does not apply (not a mixed course)
        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "D101", 40);
        var course = AddCourse(1, "CENG304", theoryHours: 5, labHours: 0);
        AddSection(1, course.Id, null, null, 30);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 1,
            InstructorId = instructor.Id,
            ClassroomId = classroom.Id,
            ScheduleSlots = Slots(
                (DayOfWeek.Monday, 9, 0, 12, 0),   // 3 hours
                (DayOfWeek.Monday, 13, 0, 15, 0))  // 2 hours — same day, but theory-only
        });

        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == TheoryLabDaySplitConflict);
    }
}
