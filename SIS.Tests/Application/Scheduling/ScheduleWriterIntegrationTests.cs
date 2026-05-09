using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling;
using SIS.Tests.Application.Constraints;

namespace SIS.Tests.Application.Scheduling;

/// <summary>
/// 2026-05-10 — IScheduleWriter integration tests. Verifies what gets written to SectionSchedule
/// after Apply. Catches the "DTO/wire format correct, DB record wrong field-by-field" bug class
/// (BlockType bug 2026-05-10 went unnoticed for ~6 months because no test asserted DB state).
/// </summary>
public class ScheduleWriterIntegrationTests : SchedulingTestBase
{
    private readonly ScheduleWriter _writer;

    public ScheduleWriterIntegrationTests()
    {
        _writer = new ScheduleWriter(Context);
    }

    [Fact]
    public async Task WriteAsync_TheoryOnlySection_AllRecordsAreTheory()
    {
        // Arrange: 3T+0L course, 1 section, 3 Mon slots seeded
        var course = AddCourse(id: 100, code: "THE101", theoryHours: 3, labHours: 0);
        var classroom = AddClassroom(id: 50, name: "L1", capacity: 30);
        var section = AddSection(id: 1, courseId: course.Id, instructorId: null, classroomId: classroom.Id, capacity: 30);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 9, 0, 9, 50);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 9, 55, 10, 45);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 10, 50, 11, 40);

        var slots = new List<ScheduleSlotDto>
        {
            new() { Day = (int)DayOfWeek.Monday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(11, 40) }
        };

        // Act
        await _writer.WriteAsync(section.Id, ActiveTermId, classroom.Id, slots);
        await Context.SaveChangesAsync();

        // Assert
        var written = await Context.SectionSchedules.AsNoTracking()
            .Where(s => s.CourseSectionId == section.Id).ToListAsync();
        written.Should().HaveCount(3);
        written.Should().OnlyContain(s => s.BlockType == ScheduleBlockType.Theory);
        written.Should().OnlyContain(s => s.ClassroomId == classroom.Id);
    }

    [Fact]
    public async Task WriteAsync_LabOnlySection_AllRecordsAreLab()
    {
        var course = AddCourse(id: 101, code: "LAB101", theoryHours: 0, labHours: 2);
        var classroom = AddClassroom(id: 51, name: "Lab1", capacity: 30, type: ClassroomType.Lab);
        var section = AddSection(id: 2, courseId: course.Id, instructorId: null, classroomId: classroom.Id, capacity: 30);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 9, 0, 9, 50);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 9, 55, 10, 45);

        var slots = new List<ScheduleSlotDto>
        {
            new() { Day = (int)DayOfWeek.Monday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 45) }
        };

        await _writer.WriteAsync(section.Id, ActiveTermId, classroom.Id, slots);
        await Context.SaveChangesAsync();

        var written = await Context.SectionSchedules.AsNoTracking()
            .Where(s => s.CourseSectionId == section.Id).ToListAsync();
        written.Should().HaveCount(2);
        written.Should().OnlyContain(s => s.BlockType == ScheduleBlockType.Lab);
    }

    [Fact]
    public async Task WriteAsync_MixedSection_FirstTheoryHoursAreTheoryRestAreLab()
    {
        // 3T+2L course = 5 total. First 3 → Theory, next 2 → Lab.
        var course = AddCourse(id: 102, code: "MIX101", theoryHours: 3, labHours: 2);
        var classroom = AddClassroom(id: 52, name: "C1", capacity: 30);
        var section = AddSection(id: 3, courseId: course.Id, instructorId: null, classroomId: classroom.Id, capacity: 30);
        // Mon: 3 slots (Theory)
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 9, 0, 9, 50);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 9, 55, 10, 45);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 10, 50, 11, 40);
        // Tue: 2 slots (Lab)
        AddTimeSlot(ActiveTermId, DayOfWeek.Tuesday, 9, 0, 9, 50);
        AddTimeSlot(ActiveTermId, DayOfWeek.Tuesday, 9, 55, 10, 45);

        var slots = new List<ScheduleSlotDto>
        {
            new() { Day = (int)DayOfWeek.Monday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(11, 40) },
            new() { Day = (int)DayOfWeek.Tuesday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 45) },
        };

        await _writer.WriteAsync(section.Id, ActiveTermId, classroom.Id, slots);
        await Context.SaveChangesAsync();

        var written = await Context.SectionSchedules
            .Include(s => s.TimeSlot)
            .AsNoTracking()
            .Where(s => s.CourseSectionId == section.Id)
            .OrderBy(s => s.TimeSlot.DayOfWeek).ThenBy(s => s.TimeSlot.SlotOrder)
            .ToListAsync();

        written.Should().HaveCount(5);
        written.Count(s => s.BlockType == ScheduleBlockType.Theory).Should().Be(3);
        written.Count(s => s.BlockType == ScheduleBlockType.Lab).Should().Be(2);

        // Mon = Theory, Tue = Lab
        var monBlocks = written.Where(s => s.TimeSlot.DayOfWeek == DayOfWeek.Monday).ToList();
        var tueBlocks = written.Where(s => s.TimeSlot.DayOfWeek == DayOfWeek.Tuesday).ToList();
        monBlocks.Should().OnlyContain(s => s.BlockType == ScheduleBlockType.Theory);
        tueBlocks.Should().OnlyContain(s => s.BlockType == ScheduleBlockType.Lab);
    }

    [Fact]
    public async Task WriteAsync_CalledTwice_ReplacesPreviousRecords()
    {
        var course = AddCourse(id: 103, code: "IDM101", theoryHours: 3, labHours: 0);
        var classroom = AddClassroom(id: 53, name: "C2", capacity: 30);
        var section = AddSection(id: 4, courseId: course.Id, instructorId: null, classroomId: classroom.Id, capacity: 30);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 9, 0, 9, 50);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 9, 55, 10, 45);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 10, 50, 11, 40);
        AddTimeSlot(ActiveTermId, DayOfWeek.Tuesday, 9, 0, 9, 50);
        AddTimeSlot(ActiveTermId, DayOfWeek.Tuesday, 9, 55, 10, 45);
        AddTimeSlot(ActiveTermId, DayOfWeek.Tuesday, 10, 50, 11, 40);

        var firstApply = new List<ScheduleSlotDto>
        {
            new() { Day = (int)DayOfWeek.Monday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(11, 40) }
        };
        var secondApply = new List<ScheduleSlotDto>
        {
            new() { Day = (int)DayOfWeek.Tuesday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(11, 40) }
        };

        // 1st apply
        await _writer.WriteAsync(section.Id, ActiveTermId, classroom.Id, firstApply);
        await Context.SaveChangesAsync();
        (await Context.SectionSchedules.AsNoTracking().CountAsync(s => s.CourseSectionId == section.Id))
            .Should().Be(3);

        // 2nd apply: replaces previous
        await _writer.WriteAsync(section.Id, ActiveTermId, classroom.Id, secondApply);
        await Context.SaveChangesAsync();

        var written = await Context.SectionSchedules
            .Include(s => s.TimeSlot)
            .AsNoTracking()
            .Where(s => s.CourseSectionId == section.Id)
            .ToListAsync();
        written.Should().HaveCount(3, because: "second apply replaces all previous (idempotency)");
        written.Should().OnlyContain(s => s.TimeSlot.DayOfWeek == DayOfWeek.Tuesday);
    }

    [Fact]
    public async Task WriteAsync_OnlineSection_ClassroomIdRemainsNull()
    {
        // Online ders: classroom yok. Apply hâlâ çalışmalı, ClassroomId null kalmalı.
        var course = AddCourse(id: 104, code: "ONL101", theoryHours: 2, labHours: 0);
        course.TeachingMethod = TeachingMethod.Online;
        Context.SaveChanges();
        var section = AddSection(id: 5, courseId: course.Id, instructorId: null, classroomId: null, capacity: 30);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 9, 0, 9, 50);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, 9, 55, 10, 45);

        var slots = new List<ScheduleSlotDto>
        {
            new() { Day = (int)DayOfWeek.Monday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 45) }
        };

        // defaultClassroomId null + slot.ClassroomId null
        await _writer.WriteAsync(section.Id, ActiveTermId, defaultClassroomId: null, slots);
        await Context.SaveChangesAsync();

        var written = await Context.SectionSchedules.AsNoTracking()
            .Where(s => s.CourseSectionId == section.Id).ToListAsync();
        written.Should().HaveCount(2);
        written.Should().OnlyContain(s => s.ClassroomId == null);
        written.Should().OnlyContain(s => s.BlockType == ScheduleBlockType.Theory);
    }
}
