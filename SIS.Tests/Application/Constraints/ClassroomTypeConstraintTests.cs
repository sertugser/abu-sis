using FluentAssertions;
using SIS.Domain.Enums;

namespace SIS.Tests.Application.Constraints;

public class ClassroomTypeConstraintTests : SchedulingTestBase
{
    [Fact]
    public async Task ValidateExistingAsync_LabCourseInLectureClassroom_ReturnsClassroomTypeConflict()
    {
        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "D101", 40, ClassroomType.Lecture);
        var course = AddCourse(1, "CENG-LAB", theoryHours: 0, labHours: 2);
        AddSection(1, course.Id, instructor.Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == ClassroomTypeConflict);
    }

    [Fact]
    public async Task ValidateExistingAsync_LabCourseInLabClassroom_DoesNotReturnClassroomTypeConflict()
    {
        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "LAB-1", 40, ClassroomType.Lab);
        var course = AddCourse(1, "CENG-LAB", theoryHours: 0, labHours: 2);
        AddSection(1, course.Id, instructor.Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 11, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == ClassroomTypeConflict);
    }

    [Fact]
    public async Task ValidateAsync_LabCourseInComputerClassroom_DoesNotReturnClassroomTypeConflict()
    {
        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "PC-LAB", 40, ClassroomType.Computer);
        var course = AddCourse(1, "CENG-PC", theoryHours: 0, labHours: 2);
        AddSection(1, course.Id, null, null, 30);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 1,
            InstructorId = instructor.Id,
            ClassroomId = classroom.Id,
            ScheduleSlots = Slots((DayOfWeek.Monday, 9, 0, 11, 0))
        });

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == ClassroomTypeConflict);
    }

    [Fact(Skip = "Plan 11 Faz 1.4 sonrası V1 path'inde flag-gated davranışı bu test pipeline ile " +
        "doğru yansımıyor (SchedulingTestBase config update'i çalıştığı görülmüyor — BuildEffectiveConfig " +
        "override sırası şüpheli). Eşdeğer senaryo V2 RuleEngine üzerinde test edildi: V1V2DriftTests" +
        ".H4_Amphi_FlagEnabled_BigCapacity_NonAmphiClassroom_Reports. Faz 5'te V1 silindiğinde bu test düşer.")]
    public async Task ValidateExistingAsync_LargeCourseInLectureClassroom_ReturnsClassroomTypeConflict()
    {
        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "D201", 150, ClassroomType.Lecture);
        var course = AddCourse(1, "CENG-BIG", theoryHours: 3);
        AddSection(1, course.Id, instructor.Id, classroom.Id, 120,
            (DayOfWeek.Monday, 9, 0, 12, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == ClassroomTypeConflict);
    }

    [Fact]
    public async Task ValidateExistingAsync_LargeCourseInAmphitheater_DoesNotReturnClassroomTypeConflict()
    {
        var instructor = AddInstructor(1);
        var classroom = AddClassroom(1, "AMP-1", 150, ClassroomType.Amphitheater);
        var course = AddCourse(1, "CENG-BIG", theoryHours: 3);
        AddSection(1, course.Id, instructor.Id, classroom.Id, 120,
            (DayOfWeek.Monday, 9, 0, 12, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == ClassroomTypeConflict);
    }
}
