using FluentAssertions;

namespace SIS.Tests.Application.Constraints;

public class TheoryLabDaySplitTests : SchedulingTestBase
{
    private const string TheoryLabDaySplitConflict = "TheoryLabDaySplitConflict";

    // Plan 10 Task 9 — H16 evaluator semantics 2026-05'te değişti:
    // ESKİ: total > threshold (4 saat) ise theory + lab AYRI günlerde zorunlu.
    // YENİ: theory + lab AYNI gün olabilir ama lab teoriden SONRA olmalı
    //       (lab.StartTime >= theory.EndTime). v1 CandidateGenerator.BuildMultiLabCombinations
    //       ile uyumlu. Bu test "5 saat aynı gün + sıralı" senaryosunun NO conflict olduğunu
    //       doğrular (önceki test eski threshold semantic'ini test ediyordu — Plan 10'da güncellendi).
    [Fact]
    public async Task ValidateAsync_MixedCourse5HoursSameDay_LabAfterTheory_NoConflict()
    {
        // 3T + 2L = 5 hours, theory 9-12 + lab 13-15 (lab AFTER theory) → no conflict.
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
                (DayOfWeek.Monday, 9, 0, 12, 0),   // 3 theory hours
                (DayOfWeek.Monday, 13, 0, 15, 0))  // 2 lab hours (after theory)
        });

        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == TheoryLabDaySplitConflict,
            because: "Yeni kural: lab teoriden SONRA aynı gün → conflict yok.");
    }

    [Fact]
    public async Task ValidateAsync_MixedCourse_LabBeforeTheory_SameDay_ReturnsConflict()
    {
        // Yeni kural ihlali: lab theory'den ÖNCE aynı günde → conflict.
        // Theory 13-16 (Monday), Lab 9-11 (Monday) → lab theory'den önce → H16 ihlal.
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
                (DayOfWeek.Monday, 9, 0, 11, 0),    // 2 lab hours (BEFORE theory) — ihlal
                (DayOfWeek.Monday, 13, 0, 16, 0))   // 3 theory hours
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
