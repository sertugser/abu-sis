using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
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
        var course1 = AddCourse(1, "CS101", theoryHours: 2);
        var course2 = AddCourse(2, "CS102", theoryHours: 2);

        AddSection(1, course1.Id, instructor1.Id, classroom.Id, ActiveTermId, 30,
(DayOfWeek.Monday, 9, 0, 11, 0));
        AddSection(2, course2.Id, instructor2.Id, classroom.Id, ActiveTermId, 30,
(DayOfWeek.Monday, 10, 0, 12, 0));

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
        var course1 = AddCourse(1, "CS201", theoryHours: 2);
        var course2 = AddCourse(2, "CS202", theoryHours: 2);

        AddSection(1, course1.Id, instructor.Id, classroom1.Id, ActiveTermId, 30,
(DayOfWeek.Monday, 9, 0, 11, 0));
        AddSection(2, course2.Id, instructor.Id, classroom2.Id, ActiveTermId, 30,
(DayOfWeek.Monday, 10, 0, 12, 0));

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
        var course1 = AddCourse(1, "CS301", theoryHours: 2);
        var course2 = AddCourse(2, "CS302", theoryHours: 2);

        AddSection(1, course1.Id, instructor.Id, classroom1.Id, ActiveTermId, 30,
(DayOfWeek.Monday, 9, 0, 11, 0));
        AddSection(2, course2.Id, instructor.Id, classroom2.Id, ActiveTermId, 30,
(DayOfWeek.Tuesday, 9, 0, 11, 0));

        var result = await _service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == "WeeklyLoadConflict");
    }

    [Fact]
    public async Task ValidateExistingAsync_ClassroomCapacityTooSmall_ReturnsCapacityConflict()
    {
        var instructor = AddInstructor(1, 101, maxWeeklyHours: 10);
        var classroom = AddClassroom(1, "Lab1", 20);
        var course = AddCourse(1, "CS401", theoryHours: 2);
        AddSection(1, course.Id, instructor.Id, classroom.Id, ActiveTermId, 30,
(DayOfWeek.Monday, 9, 0, 11, 0));

        var result = await _service.ValidateExistingAsync(ActiveTermId);

        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().Contain(conflict => conflict.ConflictType == "CapacityConflict");
    }

    [Fact]
    public async Task ValidateAsync_ValidScheduleProposal_ReturnsSuccess()
    {
        var instructor = AddInstructor(1, 101, maxWeeklyHours: 10);
        var classroom = AddClassroom(1, "C101", 50);
        var course = AddCourse(1, "CS403", theoryHours: 2);
        AddSection(1, course.Id, null, null, ActiveTermId, 30);

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

        // Add TimeSlots for the term (SuggestAsync uses TimeSlot grid)
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(9, 50), 1);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(10, 50), 2);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, new TimeOnly(11, 0), new TimeOnly(11, 50), 3);
        AddTimeSlot(ActiveTermId, DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(9, 50), 1);
        AddTimeSlot(ActiveTermId, DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(10, 50), 2);

        var course = AddCourse(1, "CS404", theoryHours: 2);
        AddSection(1, course.Id, instructor.Id, null, ActiveTermId, 28);

        var result = await _service.SuggestAsync(new ScheduleSuggestionRequestDto { SectionId = 1 });

        result.IsFeasible.Should().BeTrue();
        result.SuggestedClassroomId.Should().Be(bestFitClassroom.Id);
        result.SuggestedSlots.Should().ContainSingle();
        result.SuggestedSlots[0].Day.Should().Be(DayOfWeek.Monday);
        result.SuggestedSlots[0].StartTime.Should().Be(new TimeOnly(9, 0));
        result.SuggestedSlots[0].EndTime.Should().Be(new TimeOnly(10, 50));
    }

    [Fact]
    public async Task SuggestAsync_NoFeasibleSchedule_ReturnsInfeasibleResponse()
    {
        var instructor = AddInstructor(1, 101, maxWeeklyHours: 10);
        var blockedClassroom = AddClassroom(1, "C301", 40);
        var otherClassroom = AddClassroom(2, "C302", 40);

        // Only one time window available — same instructor already occupies it
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(9, 50), 1);
        AddTimeSlot(ActiveTermId, DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(10, 50), 2);

        var existingCourse = AddCourse(1, "CS405", theoryHours: 2);
        AddSection(1, existingCourse.Id, instructor.Id, otherClassroom.Id, ActiveTermId, 25,
(DayOfWeek.Monday, 9, 0, 10, 50));

        var targetCourse = AddCourse(2, "CS406", theoryHours: 2);
        AddSection(2, targetCourse.Id, instructor.Id, blockedClassroom.Id, ActiveTermId, 25);

        var result = await _service.SuggestAsync(new ScheduleSuggestionRequestDto { SectionId = 2 });

        result.IsFeasible.Should().BeFalse();
        result.Notes.Should().Contain(note => note.Contains("Çakışma olmayan", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateExistingAsync_UsesRequestedTermForPersistedAssignments()
    {
        var instructor = AddInstructor(1, 101, maxWeeklyHours: 10);
        var classroom = AddClassroom(1, "C101", 50);
        var activeCourse = AddCourse(1, "CS407", theoryHours: 2);
        AddSection(1, activeCourse.Id, instructor.Id, classroom.Id, ActiveTermId, 20,
(DayOfWeek.Monday, 8, 0, 10, 0));

        var inactiveCourse1 = AddCourse(2, "CS408", theoryHours: 2);
        var inactiveCourse2 = AddCourse(3, "CS409", theoryHours: 2);
        AddSection(2, inactiveCourse1.Id, instructor.Id, classroom.Id, SecondaryTermId, 20,
(DayOfWeek.Monday, 9, 0, 11, 0));
        AddSection(3, inactiveCourse2.Id, instructor.Id, classroom.Id, SecondaryTermId, 20,
(DayOfWeek.Monday, 10, 0, 12, 0));

        var activeResult = await _service.ValidateExistingAsync(ActiveTermId);
        var requestedTermResult = await _service.ValidateExistingAsync(SecondaryTermId);

        activeResult.IsValid.Should().BeTrue();
        requestedTermResult.IsValid.Should().BeFalse();
        requestedTermResult.Conflicts.Should().Contain(conflict => conflict.ConflictType == "ClassroomConflict");
    }

    [Fact]
    public async Task ValidateExistingAsync_NoSectionScheduleRecords_IsHandledSafely()
    {
        var instructor = AddInstructor(1, 101, maxWeeklyHours: 10);
        var classroom = AddClassroom(1, "C101", 50);
        var course = AddCourse(1, "CS411", theoryHours: 2);
        AddSection(1, course.Id, instructor.Id, classroom.Id, ActiveTermId, 20);

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
                WithdrawalDeadline = DateTime.UtcNow.AddDays(49),
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
                WithdrawalDeadline = DateTime.UtcNow.AddDays(49),
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
        params (DayOfWeek day, int startHour, int startMinute, int endHour, int endMinute)[] scheduleSlots)
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
            IsFinalized = false
        };

        _context.CourseSections.Add(section);
        _context.SaveChanges();

        // Create SectionSchedule records with matching TimeSlots
        foreach (var slot in scheduleSlots)
        {
            var startTime = new TimeOnly(slot.startHour, slot.startMinute);
            var endTime = new TimeOnly(slot.endHour, slot.endMinute);

            // Find or create a TimeSlot that covers this range
            var timeSlot = _context.TimeSlots
                .FirstOrDefault(ts => ts.TermId == termId
                    && ts.DayOfWeek == slot.day
                    && ts.StartTime == startTime
                    && ts.EndTime == endTime);

            if (timeSlot == null)
            {
                timeSlot = new TimeSlot
                {
                    TermId = termId,
                    DayOfWeek = slot.day,
                    StartTime = startTime,
                    EndTime = endTime,
                    SlotOrder = _context.TimeSlots.Count(ts => ts.TermId == termId && ts.DayOfWeek == slot.day) + 1,
                    DisplayName = $"{slot.day} ({startTime:HH\\:mm}-{endTime:HH\\:mm})"
                };
                _context.TimeSlots.Add(timeSlot);
                _context.SaveChanges();
            }

            _context.SectionSchedules.Add(new SectionSchedule
            {
                CourseSectionId = section.Id,
                TimeSlotId = timeSlot.Id,
                ClassroomId = classroomId ?? 0
            });
        }

        if (scheduleSlots.Length > 0)
            _context.SaveChanges();

        return section;
    }

    private void AddTimeSlot(int termId, DayOfWeek day, TimeOnly startTime, TimeOnly endTime, int slotOrder)
    {
        _context.TimeSlots.Add(new TimeSlot
        {
            TermId = termId,
            DayOfWeek = day,
            StartTime = startTime,
            EndTime = endTime,
            SlotOrder = slotOrder,
            DisplayName = $"{day} {slotOrder}. Ders ({startTime:HH\\:mm}-{endTime:HH\\:mm})"
        });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
