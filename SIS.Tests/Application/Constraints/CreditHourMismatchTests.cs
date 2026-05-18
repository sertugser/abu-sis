using FluentAssertions;

namespace SIS.Tests.Application.Constraints;

public class CreditHourMismatchTests : SchedulingTestBase
{
    [Fact]
    public async Task ValidateExistingAsync_UnderAssignedSection_NoCreditHourMismatch()
    {
        // Kısmi atama desteği — under-assigned section artık Partial olarak izinli, hard violation
        // üretmez. Yalnız fazla atama (assignedHours > expected) hard fail kalır.
        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "D101", 40);
        var course = AddCourse(1, "CENG401", theoryHours: 3);
        AddSection(1, course.Id, instructor.Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == CreditHourMismatch);
    }

    [Fact]
    public async Task ValidateExistingAsync_OverAssignedSection_ReturnsCreditHourMismatch()
    {
        // Fazla atama hâlâ HARD fail.
        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "D101", 40);
        var course = AddCourse(1, "CENG410", theoryHours: 2);
        AddSection(1, course.Id, instructor.Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 12, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == CreditHourMismatch);
    }

    [Fact]
    public async Task ValidateExistingAsync_AssignedDurationMatchesRequiredHours_DoesNotReturnCreditHourMismatch()
    {
        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "D101", 40);
        var course = AddCourse(1, "CENG402", theoryHours: 3);
        AddSection(1, course.Id, instructor.Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 12, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == CreditHourMismatch);
    }

    [Fact]
    public async Task ValidateAsync_UnderAssignedCandidate_NoCreditHourMismatch()
    {
        // Kısmi atama: candidate eksik slot ile gönderildiğinde H12 violation üretmez.
        // Düzenleyici 3T+2L dersin önce Teori bloğunu (3 saat) atayabilir; Lab parçası sonra
        // ayrı drag ile eklenir.
        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "D101", 40);
        var course = AddCourse(1, "CENG403", theoryHours: 3);
        AddSection(1, course.Id, null, null, 30);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 1,
            InstructorId = instructor.Id,
            ClassroomId = classroom.Id,
            ScheduleSlots = Slots((DayOfWeek.Monday, 9, 0, 10, 0))
        });

        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == CreditHourMismatch);
    }

    [Fact]
    public async Task ValidateAsync_OverAssignedCandidate_ReturnsCreditHourMismatch()
    {
        // Fazla atama hâlâ HARD fail (assignedHours > expected).
        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "D101", 40);
        var course = AddCourse(1, "CENG420", theoryHours: 2);
        AddSection(1, course.Id, null, null, 30);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 1,
            InstructorId = instructor.Id,
            ClassroomId = classroom.Id,
            ScheduleSlots = Slots((DayOfWeek.Monday, 9, 0, 12, 0))
        });

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == CreditHourMismatch);
    }

    [Fact]
    public async Task ValidateAsync_TimeSlotGridMatchesLongRange_DoesNotReturnCreditHourMismatch()
    {
        // H16 disabled (threshold=0) so we isolate the CreditHour constraint only
        SetSystemConfig("Schedule.TheoryLabSplitThreshold", "0");

        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 9, 0, 9, 50, 1);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 9, 55, 10, 45, 2);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 10, 50, 11, 40, 3);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 12, 20, 13, 10, 4);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 13, 15, 14, 5, 5);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 14, 10, 15, 0, 6);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 15, 5, 15, 55, 7);

        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "D101", 60);
        var course = AddCourse(1, "CENG404", theoryHours: 3, labHours: 4);
        AddSection(1, course.Id, null, null, 30);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 1,
            InstructorId = instructor.Id,
            ClassroomId = classroom.Id,
            ScheduleSlots = Slots((DayOfWeek.Monday, 9, 0, 15, 55))
        });

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == CreditHourMismatch);
    }
}
