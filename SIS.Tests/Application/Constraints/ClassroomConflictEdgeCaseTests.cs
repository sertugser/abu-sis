using FluentAssertions;

namespace SIS.Tests.Application.Constraints;

public class ClassroomConflictEdgeCaseTests : SchedulingTestBase
{
    [Fact]
    public async Task ValidateExistingAsync_SameClassroomPartialOverlap_ReturnsClassroomConflict()
    {
        var classroom = AddClassroom(1, "D101", 40);
        var course1 = AddCourse(1, "CENG601", theoryHours: 1);
        var course2 = AddCourse(2, "CENG602", theoryHours: 1);
        AddSection(1, course1.Id, AddInstructor(1).Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0));
        AddSection(2, course2.Id, AddInstructor(2).Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 30, 10, 30));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == ClassroomConflict);
    }

    [Fact]
    public async Task ValidateExistingAsync_SameClassroomAdjacentSlots_DoesNotReturnClassroomConflict()
    {
        var classroom = AddClassroom(1, "D101", 40);
        var course1 = AddCourse(1, "CENG603", theoryHours: 1);
        var course2 = AddCourse(2, "CENG604", theoryHours: 1);
        AddSection(1, course1.Id, AddInstructor(1).Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0));
        AddSection(2, course2.Id, AddInstructor(2).Id, classroom.Id, 30,
            (DayOfWeek.Monday, 10, 0, 11, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == ClassroomConflict);
    }

    [Fact]
    public async Task ValidateExistingAsync_SameClassroomDifferentDay_DoesNotReturnClassroomConflict()
    {
        var classroom = AddClassroom(1, "D101", 40);
        var course1 = AddCourse(1, "CENG605", theoryHours: 1);
        var course2 = AddCourse(2, "CENG606", theoryHours: 1);
        AddSection(1, course1.Id, AddInstructor(1).Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0));
        AddSection(2, course2.Id, AddInstructor(2).Id, classroom.Id, 30,
            (DayOfWeek.Tuesday, 9, 0, 10, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == ClassroomConflict);
    }

    [Fact]
    public async Task ValidateAsync_CandidateSameClassroomPartialOverlap_ReturnsClassroomConflict()
    {
        var classroom = AddClassroom(1, "D101", 40);
        var existingCourse = AddCourse(1, "CENG607", theoryHours: 1);
        var candidateCourse = AddCourse(2, "CENG608", theoryHours: 1);
        AddSection(1, existingCourse.Id, AddInstructor(1).Id, classroom.Id, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0));
        AddSection(2, candidateCourse.Id, null, null, 30);

        var result = await Service.ValidateAsync(new()
        {
            SectionId = 2,
            InstructorId = AddInstructor(2).Id,
            ClassroomId = classroom.Id,
            ScheduleSlots = Slots((DayOfWeek.Monday, 9, 30, 10, 30))
        });

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == ClassroomConflict);
    }

    // ── Plan 10 — paralel-lab aware tests ─────────────────────────────────────────────

    [Fact]
    public async Task ValidateExistingAsync_ParallelLab_DifferentRoomsSameTime_NoSelfConflict()
    {
        // Plan 10 — paralel-lab section'ında aynı saat 2 farklı oda kullanılıyor
        // (Grup A → L139, Grup B → L141). Eski evaluator section-level ClassroomId
        // üzerinden bakıp self-conflict false-positive üretiyordu. Yeni evaluator
        // SectionId == self ise atlıyor.
        var labA = AddClassroom(1, "L139", 30);
        var labB = AddClassroom(2, "L141", 30);
        var labCourse = AddCourse(1, "PARLAB", theoryHours: 0, labHours: 2);

        // Section'ı section-level ClassroomId olmadan oluştur (paralel için tek değer anlamsız).
        var section = new SIS.Domain.Entities.CourseSection
        {
            Id = 1,
            CourseId = labCourse.Id,
            ClassroomId = null,
            TermId = ActiveTermId,
            Capacity = 30,
            EnrolledCount = 0,
            SectionNumber = 1,
            Status = SIS.Domain.Enums.CourseSectionStatus.Active,
            IsFinalized = false,
        };
        Context.CourseSections.Add(section);
        Context.SaveChanges();

        // Manuel SectionSchedule satırları: aynı saat 2 farklı oda (paralel grup pattern).
        var slot = AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 9, 0, 10, 0);
        Context.SectionSchedules.Add(new SIS.Domain.Entities.SectionSchedule
        {
            CourseSectionId = section.Id, TimeSlotId = slot.Id, ClassroomId = labA.Id,
        });
        Context.SectionSchedules.Add(new SIS.Domain.Entities.SectionSchedule
        {
            CourseSectionId = section.Id, TimeSlotId = slot.Id, ClassroomId = labB.Id,
        });
        Context.SaveChanges();

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == ClassroomConflict,
            because: "paralel-lab aynı section, farklı odalar — self-conflict false-positive olmamalı");
    }

    [Fact]
    public async Task ValidateExistingAsync_ParallelLab_OverlapsWithDifferentSection_DetectsRealConflict()
    {
        // Aynı senaryo + ek section: paralel-lab odasından birinde başka bir section
        // aynı saatte yerleşmiş → GERÇEK çakışma yakalanmalı.
        var labA = AddClassroom(1, "L139", 30);
        var labB = AddClassroom(2, "L141", 30);
        var parallelCourse = AddCourse(1, "PARLAB", theoryHours: 0, labHours: 2);
        var otherCourse = AddCourse(2, "OTHER", theoryHours: 1);

        var parallel = new SIS.Domain.Entities.CourseSection
        {
            Id = 1,
            CourseId = parallelCourse.Id,
            ClassroomId = null,
            TermId = ActiveTermId,
            Capacity = 30,
            EnrolledCount = 0,
            SectionNumber = 1,
            Status = SIS.Domain.Enums.CourseSectionStatus.Active,
            IsFinalized = false,
        };
        Context.CourseSections.Add(parallel);
        Context.SaveChanges();

        var slot = AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 9, 0, 10, 0);
        Context.SectionSchedules.Add(new SIS.Domain.Entities.SectionSchedule
        {
            CourseSectionId = parallel.Id, TimeSlotId = slot.Id, ClassroomId = labA.Id,
        });
        Context.SectionSchedules.Add(new SIS.Domain.Entities.SectionSchedule
        {
            CourseSectionId = parallel.Id, TimeSlotId = slot.Id, ClassroomId = labB.Id,
        });
        Context.SaveChanges();

        // OTHER section labA içine aynı saatte yerleşsin → gerçek çakışma.
        AddSection(2, otherCourse.Id, AddInstructor(1).Id, labA.Id, 30,
            (DayOfWeek.Monday, 9, 0, 10, 0));

        var result = await Service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse(
            because: "paralel-lab odasından biri başka section ile aynı saat → real conflict");
        result.Conflicts.Should().Contain(c => c.ConflictType == ClassroomConflict);
    }
}
