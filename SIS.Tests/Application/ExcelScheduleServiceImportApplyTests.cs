using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.API.Controllers;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class ExcelScheduleServiceImportApplyTests
{
    private const int UserId = 10;
    private const int OtherUserId = 11;
    private const int FacultyId = 1;
    private const int TermId = 1;

    [Fact]
    public void PublicImportContracts_DoNotExposeClientControlledScheduleIds()
    {
        typeof(ImportApplyRequest).GetProperty("Preview").Should().BeNull();
        typeof(ImportApplyRequest).GetProperty("Rows").Should().BeNull();
        typeof(ImportRowDto).GetProperty("SectionId").Should().BeNull();
        typeof(ImportRowDto).GetProperty("TimeSlotId").Should().BeNull();
        typeof(ImportRowDto).GetProperty("ClassroomId").Should().BeNull();
    }

    [Fact]
    public async Task ApplyImportAsync_ServerHeldValidRows_ReplacesSectionSchedulesOnceAndKeepsAllSelectedSlots()
    {
        await using var context = CreateContext();
        SeedScheduleGraph(context);
        var sessionId = AddSession(context, RowsJson(
            ValidRow(rowNumber: 2, timeSlotId: 1, classroomId: 1),
            ValidRow(rowNumber: 3, timeSlotId: 2, classroomId: 2)));
        await context.SaveChangesAsync();

        var service = new ExcelScheduleService(context);
        var result = await service.ApplyImportAsync(UserId, FacultyId, TermId, sessionId, [2, 3]);

        result.AppliedCount.Should().Be(2);
        result.FailedCount.Should().Be(0);

        var schedules = await context.SectionSchedules
            .AsNoTracking()
            .Where(ss => ss.CourseSectionId == 1)
            .OrderBy(ss => ss.TimeSlotId)
            .ToListAsync();

        schedules.Should().HaveCount(2);
        schedules.Select(s => s.TimeSlotId).Should().Equal(1, 2);
        schedules.Select(s => s.ClassroomId).Should().Equal(1, 2);

        var session = await context.ScheduleImportSessions.FindAsync(sessionId);
        session!.ConsumedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ApplyImportAsync_DifferentUserSession_ThrowsForbiddenWithoutWriting()
    {
        await using var context = CreateContext();
        SeedScheduleGraph(context);
        var sessionId = AddSession(context, RowsJson(ValidRow(rowNumber: 2, timeSlotId: 1, classroomId: 1)));
        await context.SaveChangesAsync();

        var service = new ExcelScheduleService(context);
        var act = () => service.ApplyImportAsync(OtherUserId, FacultyId, TermId, sessionId, [2]);

        await act.Should().ThrowAsync<ScheduleImportSessionForbiddenException>();
        await AssertExistingScheduleIsUntouched(context);
    }

    [Fact]
    public async Task ApplyImportAsync_ExpiredSession_ThrowsWithoutWriting()
    {
        await using var context = CreateContext();
        SeedScheduleGraph(context);
        var sessionId = AddSession(
            context,
            RowsJson(ValidRow(rowNumber: 2, timeSlotId: 1, classroomId: 1)),
            expiresAt: DateTime.UtcNow.AddMinutes(-1));
        await context.SaveChangesAsync();

        var service = new ExcelScheduleService(context);
        var act = () => service.ApplyImportAsync(UserId, FacultyId, TermId, sessionId, [2]);

        await act.Should().ThrowAsync<ScheduleImportSessionInvalidException>()
            .WithMessage("*expired*");
        await AssertExistingScheduleIsUntouched(context);
    }

    [Fact]
    public async Task ApplyImportAsync_TermOrFacultyMismatch_ThrowsWithoutWriting()
    {
        await using var context = CreateContext();
        SeedScheduleGraph(context);
        var sessionId = AddSession(context, RowsJson(ValidRow(rowNumber: 2, timeSlotId: 1, classroomId: 1)));
        await context.SaveChangesAsync();

        var service = new ExcelScheduleService(context);
        var act = () => service.ApplyImportAsync(UserId, facultyId: 99, TermId, sessionId, [2]);

        await act.Should().ThrowAsync<ScheduleImportSessionInvalidException>()
            .WithMessage("*faculty or term*");
        await AssertExistingScheduleIsUntouched(context);
    }

    [Fact]
    public async Task ApplyImportAsync_StaleServerHeldIds_ThrowsBeforeWriting()
    {
        await using var context = CreateContext();
        SeedScheduleGraph(context);
        var sessionId = AddSession(context, RowsJson(ValidRow(rowNumber: 2, timeSlotId: 404, classroomId: 1)));
        await context.SaveChangesAsync();

        var service = new ExcelScheduleService(context);
        var act = () => service.ApplyImportAsync(UserId, FacultyId, TermId, sessionId, [2]);

        await act.Should().ThrowAsync<ScheduleImportSessionInvalidException>()
            .WithMessage("*time slots*");
        await AssertExistingScheduleIsUntouched(context);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static void SeedScheduleGraph(AppDbContext context)
    {
        var now = DateTime.UtcNow;
        var faculty = new Faculty { Id = FacultyId, Code = "ENG", Name = "Engineering", CreatedAt = now };
        var term = new AcademicTerm
        {
            Id = TermId,
            Name = "2026 Spring",
            ShortName = "26S",
            Year = 2026,
            TermStart = now.AddDays(-30),
            TermEnd = now.AddDays(90),
            CreatedAt = now,
        };
        var department = new Department
        {
            Id = 1,
            Code = "CENG",
            Name = "Computer Engineering",
            FacultyId = FacultyId,
            Faculty = faculty,
            CreatedAt = now,
        };
        var course = new Course
        {
            Id = 1,
            Code = "CENG101",
            Name = "Intro",
            DepartmentId = department.Id,
            Department = department,
            CreatedAt = now,
        };
        var section = new CourseSection
        {
            Id = 1,
            CourseId = course.Id,
            Course = course,
            TermId = term.Id,
            Term = term,
            SectionNumber = 1,
            Capacity = 30,
            CreatedAt = now,
        };
        var slot1 = new TimeSlot { Id = 1, TermId = TermId, Term = term, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 50), SlotOrder = 1 };
        var slot2 = new TimeSlot { Id = 2, TermId = TermId, Term = term, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 50), SlotOrder = 2 };
        var oldSlot = new TimeSlot { Id = 99, TermId = TermId, Term = term, DayOfWeek = DayOfWeek.Friday, StartTime = new TimeOnly(15, 0), EndTime = new TimeOnly(15, 50), SlotOrder = 9 };
        var classroom1 = new Classroom { Id = 1, Name = "A101", Capacity = 40, CreatedAt = now };
        var classroom2 = new Classroom { Id = 2, Name = "A102", Capacity = 40, CreatedAt = now };
        var oldClassroom = new Classroom { Id = 99, Name = "OLD", Capacity = 40, CreatedAt = now };

        context.AddRange(faculty, term, department, course, section, slot1, slot2, oldSlot, classroom1, classroom2, oldClassroom);
        context.SectionSchedules.Add(new SectionSchedule
        {
            CourseSectionId = section.Id,
            CourseSection = section,
            TimeSlotId = oldSlot.Id,
            TimeSlot = oldSlot,
            ClassroomId = oldClassroom.Id,
            Classroom = oldClassroom,
        });
    }

    private static Guid AddSession(AppDbContext context, string rowsJson, DateTime? expiresAt = null)
    {
        var sessionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        context.ScheduleImportSessions.Add(new ScheduleImportSession
        {
            Id = sessionId,
            CreatedByUserId = UserId,
            FacultyId = FacultyId,
            TermId = TermId,
            RowsJson = rowsJson,
            CreatedAt = now,
            ExpiresAt = expiresAt ?? now.AddMinutes(30),
        });

        return sessionId;
    }

    private static object ValidRow(int rowNumber, int timeSlotId, int classroomId) => new
    {
        rowNumber,
        courseCode = "CENG101",
        sectionCode = "1",
        day = "Pazartesi",
        startTime = "09:00",
        endTime = "09:50",
        classroomName = $"A10{classroomId}",
        capacity = 30,
        status = "Valid",
        message = "Ready",
        sectionId = 1,
        classroomId,
        timeSlotId,
    };

    private static string RowsJson(params object[] rows) =>
        JsonSerializer.Serialize(rows, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static async Task AssertExistingScheduleIsUntouched(AppDbContext context)
    {
        var schedules = await context.SectionSchedules.AsNoTracking().ToListAsync();
        schedules.Should().ContainSingle();
        schedules.Single().TimeSlotId.Should().Be(99);
        schedules.Single().ClassroomId.Should().Be(99);
    }
}
