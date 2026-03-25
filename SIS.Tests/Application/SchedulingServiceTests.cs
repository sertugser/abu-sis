using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class SchedulingServiceTests : IDisposable
{
    private const int FacultyId = 1;
    private const int DepartmentId = 1;
    private const int ActiveTermId = 1;
    private const int SecondaryTermId = 2;

    private readonly AppDbContext _context;
    private readonly SchedulingService _service;

    public SchedulingServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _service = new SchedulingService(_context);

        SeedBaseData();
    }

    [Fact]
    public async Task ValidateExistingAsync_SameClassroomSameTime_ReturnsClassroomConflict()
    {
        var instructor1 = AddInstructor(1, 101, maxWeeklyHours: 10);
        var instructor2 = AddInstructor(2, 102, maxWeeklyHours: 10);
        var classroom = AddClassroom(1, "C101", 50);
        AddAvailability(instructor1.Id, DayOfWeek.Monday, 8, 0, 12, 0);
        AddAvailability(instructor2.Id, DayOfWeek.Monday, 8, 0, 12, 0);

        var course1 = AddCourse(1, "CS101", theoryHours: 2);
        var course2 = AddCourse(2, "CS102", theoryHours: 2);

        AddSection(1, course1.Id, instructor1.Id, classroom.Id, ActiveTermId, 30,
            CreateScheduleJson((DayOfWeek.Monday, 9, 0, 11, 0)));
        AddSection(2, course2.Id, instructor2.Id, classroom.Id, ActiveTermId, 30,
            CreateScheduleJson((DayOfWeek.Monday, 10, 0, 12, 0)));

        var result = await _service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == "ClassroomConflict");
    }

    [Fact]
    public async Task ValidateExistingAsync_SameInstructorSameTime_ReturnsInstructorConflict()
    {
        var instructor = AddInstructor(1, 101, maxWeeklyHours: 10);
        var classroom1 = AddClassroom(1, "C101", 50);
        var classroom2 = AddClassroom(2, "C102", 50);
        AddAvailability(instructor.Id, DayOfWeek.Monday, 8, 0, 12, 0);

        var course1 = AddCourse(1, "CS201", theoryHours: 2);
        var course2 = AddCourse(2, "CS202", theoryHours: 2);

        AddSection(1, course1.Id, instructor.Id, classroom1.Id, ActiveTermId, 30,
            CreateScheduleJson((DayOfWeek.Monday, 9, 0, 11, 0)));
        AddSection(2, course2.Id, instructor.Id, classroom2.Id, ActiveTermId, 30,
            CreateScheduleJson((DayOfWeek.Monday, 10, 0, 12, 0)));

        var result = await _service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == "InstructorConflict");
    }

    [Fact]
    public async Task ValidateExistingAsync_MaxWeeklyHoursExceeded_ReturnsWeeklyLoadConflict()
    {
        var instructor = AddInstructor(1, 101, maxWeeklyHours: 3);
        var classroom1 = AddClassroom(1, "C101", 50);
        var classroom2 = AddClassroom(2, "C102", 50);
        AddAvailability(instructor.Id, DayOfWeek.Monday, 8, 0, 12, 0);
        AddAvailability(instructor.Id, DayOfWeek.Tuesday, 8, 0, 12, 0);

        var course1 = AddCourse(1, "CS301", theoryHours: 2);
        var course2 = AddCourse(2, "CS302", theoryHours: 2);

        AddSection(1, course1.Id, instructor.Id, classroom1.Id, ActiveTermId, 30,
            CreateScheduleJson((DayOfWeek.Monday, 9, 0, 11, 0)));
        AddSection(2, course2.Id, instructor.Id, classroom2.Id, ActiveTermId, 30,
            CreateScheduleJson((DayOfWeek.Tuesday, 9, 0, 11, 0)));

        var result = await _service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == "WeeklyLoadConflict");
    }

    [Fact]
    public async Task ValidateExistingAsync_ClassroomCapacityTooSmall_ReturnsCapacityConflict()
    {
        var instructor = AddInstructor(1, 101, maxWeeklyHours: 10);
        var classroom = AddClassroom(1, "Lab1", 20);
        AddAvailability(instructor.Id, DayOfWeek.Monday, 8, 0, 12, 0);

        var course = AddCourse(1, "CS401", theoryHours: 2);
        AddSection(1, course.Id, instructor.Id, classroom.Id, ActiveTermId, 30,
            CreateScheduleJson((DayOfWeek.Monday, 9, 0, 11, 0)));

        var result = await _service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == "CapacityConflict");
    }

    [Fact]
    public async Task ValidateExistingAsync_InstructorUnavailableSlot_ReturnsAvailabilityConflict()
    {
        var instructor = AddInstructor(1, 101, maxWeeklyHours: 10);
        var classroom = AddClassroom(1, "C101", 50);
        AddAvailability(instructor.Id, DayOfWeek.Monday, 13, 0, 15, 0);

        var course = AddCourse(1, "CS402", theoryHours: 2);
        AddSection(1, course.Id, instructor.Id, classroom.Id, ActiveTermId, 30,
            CreateScheduleJson((DayOfWeek.Monday, 9, 0, 11, 0)));

        var result = await _service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == "InstructorAvailabilityConflict");
    }

    [Fact]
    public async Task ValidateAsync_ValidScheduleProposal_ReturnsSuccess()
    {
        var instructor = AddInstructor(1, 101, maxWeeklyHours: 10);
        var classroom = AddClassroom(1, "C101", 50);
        AddAvailability(instructor.Id, DayOfWeek.Monday, 8, 0, 12, 0, isPreferred: true);

        var course = AddCourse(1, "CS403", theoryHours: 2);
        AddSection(1, course.Id, null, null, ActiveTermId, 30, null);

        var request = new ScheduleValidationRequestDto
        {
            SectionId = 1,
            InstructorId = instructor.Id,
            ClassroomId = classroom.Id,
            ScheduleSlots =
            [
                new ScheduleSlotDto
                {
                    Day = DayOfWeek.Monday,
                    StartTime = new TimeOnly(9, 0),
                    EndTime = new TimeOnly(11, 0)
                }
            ]
        };

        var result = await _service.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public async Task SuggestAsync_FeasibleCandidate_ReturnsDeterministicSuggestion()
    {
        var instructor = AddInstructor(1, 101, maxWeeklyHours: 10);
        var bestFitClassroom = AddClassroom(1, "C201", 30);
        AddClassroom(2, "C202", 60);
        AddAvailability(instructor.Id, DayOfWeek.Monday, 9, 0, 12, 0, isPreferred: true);
        AddAvailability(instructor.Id, DayOfWeek.Tuesday, 9, 0, 12, 0);

        var course = AddCourse(1, "CS404", theoryHours: 2);
        AddSection(1, course.Id, instructor.Id, null, ActiveTermId, 28, null);

        var result = await _service.SuggestAsync(new ScheduleSuggestionRequestDto { SectionId = 1 });

        result.IsFeasible.Should().BeTrue();
        result.SuggestedClassroomId.Should().Be(bestFitClassroom.Id);
        result.SuggestedSlots.Should().ContainSingle();
        result.SuggestedSlots[0].Day.Should().Be(DayOfWeek.Monday);
        result.SuggestedSlots[0].StartTime.Should().Be(new TimeOnly(9, 0));
        result.SuggestedSlots[0].EndTime.Should().Be(new TimeOnly(11, 0));
    }

    [Fact]
    public async Task SuggestAsync_NoFeasibleSchedule_ReturnsInfeasibleResponse()
    {
        var instructor = AddInstructor(1, 101, maxWeeklyHours: 10);
        var blockedClassroom = AddClassroom(1, "C301", 40);
        var otherClassroom = AddClassroom(2, "C302", 40);

        AddAvailability(instructor.Id, DayOfWeek.Monday, 9, 0, 11, 0, isPreferred: true);

        var existingCourse = AddCourse(1, "CS405", theoryHours: 2);
        AddSection(1, existingCourse.Id, instructor.Id, otherClassroom.Id, ActiveTermId, 25,
            CreateScheduleJson((DayOfWeek.Monday, 9, 0, 11, 0)));

        var targetCourse = AddCourse(2, "CS406", theoryHours: 2);
        AddSection(2, targetCourse.Id, instructor.Id, blockedClassroom.Id, ActiveTermId, 25, null);

        var result = await _service.SuggestAsync(new ScheduleSuggestionRequestDto { SectionId = 2 });

        result.IsFeasible.Should().BeFalse();
        result.Notes.Should().Contain(note => note.Contains("Çakışma olmayan", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateExistingAsync_UsesRequestedTermForPersistedAssignments()
    {
        var instructor = AddInstructor(1, 101, maxWeeklyHours: 10);
        var classroom = AddClassroom(1, "C101", 50);
        AddAvailability(instructor.Id, DayOfWeek.Monday, 8, 0, 12, 0);

        var activeCourse = AddCourse(1, "CS407", theoryHours: 2);
        AddSection(1, activeCourse.Id, instructor.Id, classroom.Id, ActiveTermId, 20,
            CreateScheduleJson((DayOfWeek.Monday, 8, 0, 10, 0)));

        var inactiveCourse1 = AddCourse(2, "CS408", theoryHours: 2);
        var inactiveCourse2 = AddCourse(3, "CS409", theoryHours: 2);
        AddSection(2, inactiveCourse1.Id, instructor.Id, classroom.Id, SecondaryTermId, 20,
            CreateScheduleJson((DayOfWeek.Monday, 9, 0, 11, 0)));
        AddSection(3, inactiveCourse2.Id, instructor.Id, classroom.Id, SecondaryTermId, 20,
            CreateScheduleJson((DayOfWeek.Monday, 10, 0, 12, 0)));

        var activeResult = await _service.ValidateExistingAsync(ActiveTermId);
        var requestedTermResult = await _service.ValidateExistingAsync(SecondaryTermId);

        activeResult.IsValid.Should().BeTrue();
        requestedTermResult.IsValid.Should().BeFalse();
        requestedTermResult.Conflicts.Should().Contain(conflict => conflict.ConflictType == "ClassroomConflict");
    }

    [Fact]
    public async Task ValidateExistingAsync_InvalidScheduleJson_ReturnsParseConflict()
    {
        var instructor = AddInstructor(1, 101, maxWeeklyHours: 10);
        var classroom = AddClassroom(1, "C101", 50);
        AddAvailability(instructor.Id, DayOfWeek.Monday, 8, 0, 12, 0);

        var course = AddCourse(1, "CS410", theoryHours: 2);
        AddSection(1, course.Id, instructor.Id, classroom.Id, ActiveTermId, 20, "{invalid-json");

        var result = await _service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == "InvalidScheduleJson");
    }

    [Fact]
    public async Task ValidateExistingAsync_NullScheduleJson_IsHandledSafely()
    {
        var instructor = AddInstructor(1, 101, maxWeeklyHours: 10);
        var classroom = AddClassroom(1, "C101", 50);
        var course = AddCourse(1, "CS411", theoryHours: 2);
        AddSection(1, course.Id, instructor.Id, classroom.Id, ActiveTermId, 20, null);

        var result = await _service.ValidateExistingAsync(ActiveTermId);

        result.Conflicts.Should().NotContain(conflict => conflict.ConflictType == "InvalidScheduleJson");
    }

    private void SeedBaseData()
    {
        _context.Faculties.Add(new Faculty
        {
            Id = FacultyId,
            Name = "Engineering",
            Code = "ENG"
        });

        _context.Departments.Add(new Department
        {
            Id = DepartmentId,
            FacultyId = FacultyId,
            Name = "Computer Engineering",
            Code = "CENG"
        });

        _context.AcademicTerms.AddRange(
            new AcademicTerm
            {
                Id = ActiveTermId,
                Name = "2025-2026 Bahar",
                Year = 2025,
                Semester = SemesterType.Spring,
                RegistrationStart = DateTime.UtcNow.AddDays(-5),
                RegistrationEnd = DateTime.UtcNow.AddDays(5),
                AddDropStart = DateTime.UtcNow.AddDays(6),
                AddDropEnd = DateTime.UtcNow.AddDays(12),
                WithdrawalDeadlineWeek = 7,
                TermStart = DateTime.UtcNow.AddDays(-10),
                TermEnd = DateTime.UtcNow.AddDays(90),
                IsActive = true
            },
            new AcademicTerm
            {
                Id = SecondaryTermId,
                Name = "2024-2025 Guz",
                Year = 2024,
                Semester = SemesterType.Fall,
                RegistrationStart = DateTime.UtcNow.AddDays(-100),
                RegistrationEnd = DateTime.UtcNow.AddDays(-90),
                AddDropStart = DateTime.UtcNow.AddDays(-89),
                AddDropEnd = DateTime.UtcNow.AddDays(-85),
                WithdrawalDeadlineWeek = 7,
                TermStart = DateTime.UtcNow.AddDays(-120),
                TermEnd = DateTime.UtcNow.AddDays(-30),
                IsActive = false
            });

        _context.SaveChanges();
    }

    private Instructor AddInstructor(int id, int userId, int maxWeeklyHours)
    {
        _context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"instructor{userId}",
            NormalizedUserName = $"INSTRUCTOR{userId}",
            Email = $"instructor{userId}@example.com",
            FirstName = $"Name{userId}",
            LastName = $"Last{userId}"
        });

        var instructor = new Instructor
        {
            Id = id,
            UserId = userId,
            DepartmentId = DepartmentId,
            Title = "Dr.",
            MaxWeeklyHours = maxWeeklyHours
        };

        _context.Instructors.Add(instructor);
        _context.SaveChanges();
        return instructor;
    }

    private Classroom AddClassroom(int id, string name, int capacity)
    {
        var classroom = new Classroom
        {
            Id = id,
            Name = name,
            Capacity = capacity,
            Type = ClassroomType.Lecture
        };

        _context.Classrooms.Add(classroom);
        _context.SaveChanges();
        return classroom;
    }

    private Course AddCourse(int id, string code, int theoryHours, int labHours = 0)
    {
        var course = new Course
        {
            Id = id,
            Code = code,
            Name = code,
            DepartmentId = DepartmentId,
            Ects = theoryHours + labHours,
            TheoryHours = theoryHours,
            LabHours = labHours,
            IsNonGpa = false
        };

        _context.Courses.Add(course);
        _context.SaveChanges();
        return course;
    }

    private CourseSection AddSection(
        int id,
        int courseId,
        int? instructorId,
        int? classroomId,
        int termId,
        int capacity,
        string? scheduleJson)
    {
        var section = new CourseSection
        {
            Id = id,
            CourseId = courseId,
            InstructorId = instructorId,
            ClassroomId = classroomId,
            TermId = termId,
            Capacity = capacity,
            EnrolledCount = 0,
            IsActive = true,
            IsFinalized = false,
            ScheduleJson = scheduleJson ?? string.Empty
        };

        _context.CourseSections.Add(section);
        _context.SaveChanges();
        return section;
    }

    private void AddAvailability(
        int instructorId,
        DayOfWeek day,
        int startHour,
        int startMinute,
        int endHour,
        int endMinute,
        bool isAvailable = true,
        bool isPreferred = false)
    {
        _context.InstructorAvailabilities.Add(new InstructorAvailability
        {
            InstructorId = instructorId,
            DayOfWeek = day,
            StartTime = new TimeOnly(startHour, startMinute),
            EndTime = new TimeOnly(endHour, endMinute),
            IsAvailable = isAvailable,
            IsPreferred = isPreferred
        });

        _context.SaveChanges();
    }

    private static string CreateScheduleJson(params (DayOfWeek day, int startHour, int startMinute, int endHour, int endMinute)[] slots)
    {
        return JsonSerializer.Serialize(slots.Select(slot => new ScheduleSlot
        {
            Day = slot.day,
            StartTime = new TimeOnly(slot.startHour, slot.startMinute),
            EndTime = new TimeOnly(slot.endHour, slot.endMinute)
        }));
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
