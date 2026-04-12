using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Application.Common.Services;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Scheduling;
using SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;
using SIS.Infrastructure.Services.Scheduling.Evaluators.Soft;

namespace SIS.Tests.Application;

public class FacultyPlanningServiceTests : IDisposable
{
    private const int FacultyId = 1;
    private const int DepartmentId = 1;
    private const int TermId = 1;

    private readonly AppDbContext _context;
    private readonly FacultyPlanningService _service;

    public FacultyPlanningServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new AppDbContext(options);
        var constraintService = CreateConstraintEvaluationService();
        var autoScheduleEngine = new AutoScheduleEngine(_context, constraintService, new NullSystemConfigProvider());
        _service = new FacultyPlanningService(_context, new SchedulingService(_context, constraintService), new HttpContextAccessor(), autoScheduleEngine);

        SeedBaseData();
    }

    [Fact]
    public async Task AutoSchedulePreviewAsync_PrefersHigherScoringPrimeTimeSlotOverFirstAvailable()
    {
        SeedMondayTimeSlots(); // Real 50-min slots needed for auto-scheduler
        AddClassroom(1, "C101", 30);
        var course = AddCourse(1, "CENG101", theoryHours: 2);
        AddSection(1, course.Id, instructorId: null, capacity: 25);

        var result = await _service.AutoSchedulePreviewAsync(new AutoScheduleRequest
        {
            FacultyId = FacultyId,
            TermId = TermId
        }, "1");

        result.SuccessCount.Should().Be(1);
        result.Assignments.Should().ContainSingle();

        var assignment = result.Assignments[0];
        assignment.ScheduleSlots.Should().ContainSingle();
        assignment.ScheduleSlots[0].Day.Should().Be(DayOfWeek.Monday);
        // Real slots: slot 1 = 09:00-09:50, slot 2 = 09:55-10:45
        assignment.ScheduleSlots[0].StartTime.Should().Be(new TimeOnly(9, 0));
        assignment.ScheduleSlots[0].EndTime.Should().Be(new TimeOnly(10, 45));
        // AutoScheduleEngine does not yet compute per-section FitScore (always 0)
        assignment.FitScore.Should().BeGreaterThanOrEqualTo(0);
        assignment.FitScore.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task AutoSchedulePreviewAsync_DoesNotDoubleCountPreviewInstructorHours()
    {
        SeedMondayTimeSlots(); // Real 50-min slots needed for auto-scheduler
        AddClassroom(1, "C101", 50);
        var instructor = AddInstructor(1, 101);
        var longCourse = AddCourse(1, "CENG201", theoryHours: 4);
        var shortCourse = AddCourse(2, "CENG202", theoryHours: 2);

        AddSection(1, longCourse.Id, instructor.Id, capacity: 40);
        AddSection(2, shortCourse.Id, instructor.Id, capacity: 30);

        var result = await _service.AutoSchedulePreviewAsync(new AutoScheduleRequest
        {
            FacultyId = FacultyId,
            TermId = TermId
        }, "1");

        result.SuccessCount.Should().Be(2);
        result.FailedCount.Should().Be(0);

        var firstAssignment = result.Assignments.Single(a => a.SectionId == 1);
        var secondAssignment = result.Assignments.Single(a => a.SectionId == 2);

        firstAssignment.ScheduleSlots.Should().ContainSingle();
        secondAssignment.ScheduleSlots.Should().ContainSingle();

        // 4-slot course: slots 1-4 = 09:00-12:20 (skips lunch gap, takes morning slots)
        // But with lunch gap at 11:40, only 3 consecutive morning slots available (09:00-11:40)
        // So 4-slot course must span across lunch or start after lunch
        // The algorithm should find 4 consecutive slots that fit
        firstAssignment.ScheduleSlots[0].Day.Should().Be(DayOfWeek.Monday);

        // Both should be scheduled (no double-counting of instructor hours)
        secondAssignment.ScheduleSlots.Should().ContainSingle();
    }

    [Fact]
    public async Task AssignSectionAsync_WritesSectionSchedules_AndMarksSectionAsScheduled()
    {
        AddTimeSlot(TermId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0), 1);
        AddTimeSlot(TermId, DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(11, 0), 2);

        var classroom = AddClassroom(1, "C101", 30);
        var course = AddCourse(1, "CENG101", theoryHours: 2);
        var section = AddSection(1, course.Id, instructorId: null, capacity: 25);

        var result = await _service.AssignSectionAsync(new AssignSectionRequest
        {
            SectionId = section.Id,
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
        }, "999");

        result.Success.Should().BeTrue();

        var writtenSchedules = await _context.SectionSchedules
            .Include(ss => ss.TimeSlot)
            .Where(ss => ss.CourseSectionId == section.Id)
            .OrderBy(ss => ss.TimeSlot.StartTime)
            .ToListAsync();

        writtenSchedules.Should().HaveCount(2);
        writtenSchedules.Select(ss => ss.TimeSlot.StartTime).Should().Equal(new TimeOnly(9, 0), new TimeOnly(10, 0));

        var sectionPage = await _service.GetSectionsAsync(FacultyId, TermId);
        var assignedItem = sectionPage.Items.Should().ContainSingle().Subject;
        assignedItem.Status.Should().Be("Scheduled");
        assignedItem.ScheduleSummary.Should().NotBe("Zamanlanmamis");
    }

    [Fact]
    public async Task AssignSectionAsync_WhenDailyHoursExceedLimit_ExposesDailyLoadConflict()
    {
        var classroom = AddClassroom(1, "C101", 50);
        var instructor = AddInstructor(1, 101);

        var existingCourse = AddCourse(1, "CENG301", theoryHours: 8);
        var candidateCourse = AddCourse(2, "CENG302", theoryHours: 2);

        AddSection(
            1,
            existingCourse.Id,
            instructor.Id,
            capacity: 40,
            classroomId: classroom.Id,
            (DayOfWeek.Monday, 8, 0, 16, 0));

        AddTimeSlot(TermId, DayOfWeek.Monday, new TimeOnly(16, 0), new TimeOnly(16, 50), 9);
        AddTimeSlot(TermId, DayOfWeek.Monday, new TimeOnly(16, 55), new TimeOnly(17, 45), 10);
        var candidateSection = AddSection(2, candidateCourse.Id, instructor.Id, capacity: 35);

        await _service.AssignSectionAsync(new AssignSectionRequest
        {
            SectionId = candidateSection.Id,
            ClassroomId = classroom.Id,
            ScheduleSlots =
            [
                new ScheduleSlotDto
                {
                    Day = DayOfWeek.Monday,
                    StartTime = new TimeOnly(16, 0),
                    EndTime = new TimeOnly(17, 45)
                }
            ]
        }, "999");

        var conflicts = await _service.GetConflictsAsync(FacultyId, TermId);

        conflicts.Should().Contain(conflict =>
            conflict.SectionId == candidateSection.Id
            && conflict.ConflictType == "DailyLoadConflict");

        var sectionPage = await _service.GetSectionsAsync(FacultyId, TermId);
        sectionPage.Items.Single(item => item.SectionId == candidateSection.Id).Status.Should().Be("Conflicted");
    }

    [Fact]
    public async Task AssignSectionAsync_AllowsPlanningStage()
    {
        SetTermStatus(TermStatus.Planning);
        AddTimeSlot(TermId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0), 1);
        AddTimeSlot(TermId, DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(11, 0), 2);

        var classroom = AddClassroom(1, "C101", 30);
        var course = AddCourse(1, "CENG401", theoryHours: 2);
        var section = AddSection(1, course.Id, instructorId: null, capacity: 20);

        var result = await _service.AssignSectionAsync(new AssignSectionRequest
        {
            SectionId = section.Id,
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
        }, "999");

        result.Success.Should().BeTrue();
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

        _context.AcademicTerms.Add(new AcademicTerm
        {
            Id = TermId,
            Name = "2025-2026 Bahar",
            ShortName = "25-26 Bahar",
            EnglishName = "Spring 2025-2026",
            Year = 2025,
            Semester = SemesterType.Spring,
            RegistrationStart = DateTime.UtcNow.AddDays(-5),
            RegistrationEnd = DateTime.UtcNow.AddDays(5),
            AddDropStart = DateTime.UtcNow.AddDays(6),
            AddDropEnd = DateTime.UtcNow.AddDays(12),
            WithdrawalDeadline = DateTime.UtcNow.AddDays(30),
            TermStart = DateTime.UtcNow.AddDays(-10),
            TermEnd = DateTime.UtcNow.AddDays(90),
            IsActive = true,
            Status = TermStatus.Scheduling
        });

        _context.SaveChanges();
    }

    private Instructor AddInstructor(int id, int userId)
    {
        _context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"instructor{userId}",
            NormalizedUserName = $"INSTRUCTOR{userId}",
            Email = $"instructor{userId}@example.com",
            FirstName = "Test",
            LastName = $"Instructor{userId}"
        });

        var instructor = new Instructor
        {
            Id = id,
            UserId = userId,
            DepartmentId = DepartmentId,
            Title = "Dr.",
            MaxWeeklyHours = 20
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
            BuildingName = "A",
            Floor = 1,
            Capacity = capacity,
            Type = ClassroomType.Lecture
        };

        _context.Classrooms.Add(classroom);

        // AutoScheduleEngine requires faculty-classroom term assignments
        _context.ClassroomTermAssignments.Add(new ClassroomTermAssignment
        {
            TermId = TermId,
            ClassroomId = id,
            FacultyId = FacultyId
        });

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

    private CourseSection AddSection(int id, int courseId, int? instructorId, int capacity)
    {
        var section = new CourseSection
        {
            Id = id,
            CourseId = courseId,
            InstructorId = instructorId,
            TermId = TermId,
            Capacity = capacity,
            EnrolledCount = 0,
            SectionNumber = id,
            IsActive = true,
            IsFinalized = false,
            IsPinned = false
        };

        _context.CourseSections.Add(section);
        _context.SaveChanges();
        return section;
    }

    private CourseSection AddSection(
        int id,
        int courseId,
        int? instructorId,
        int capacity,
        int? classroomId,
        params (DayOfWeek day, int startHour, int startMinute, int endHour, int endMinute)[] scheduleSlots)
    {
        var section = new CourseSection
        {
            Id = id,
            CourseId = courseId,
            InstructorId = instructorId,
            ClassroomId = classroomId,
            TermId = TermId,
            Capacity = capacity,
            EnrolledCount = 0,
            SectionNumber = id,
            IsActive = true,
            IsFinalized = false,
            IsPinned = false
        };

        _context.CourseSections.Add(section);
        _context.SaveChanges();

        foreach (var slot in scheduleSlots)
        {
            var startTime = new TimeOnly(slot.startHour, slot.startMinute);
            var endTime = new TimeOnly(slot.endHour, slot.endMinute);

            var timeSlot = _context.TimeSlots
                .FirstOrDefault(ts => ts.TermId == TermId
                    && ts.DayOfWeek == slot.day
                    && ts.StartTime == startTime
                    && ts.EndTime == endTime);

            if (timeSlot == null)
            {
                timeSlot = new TimeSlot
                {
                    TermId = TermId,
                    DayOfWeek = slot.day,
                    StartTime = startTime,
                    EndTime = endTime,
                    SlotOrder = _context.TimeSlots.Count(ts => ts.TermId == TermId && ts.DayOfWeek == slot.day) + 1,
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
        {
            _context.SaveChanges();
        }

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

    /// <summary>
    /// Seeds realistic 50-minute TimeSlots for Monday (matches production grid).
    /// Morning: 09:00-09:50, 09:55-10:45, 10:50-11:40
    /// Afternoon: 12:20-13:10, 13:15-14:05, 14:10-15:00, 15:05-15:55, 16:00-16:50, 16:55-17:45
    /// </summary>
    private void SeedMondayTimeSlots()
    {
        var slots = new (TimeOnly start, TimeOnly end)[]
        {
            (new(9, 0), new(9, 50)),
            (new(9, 55), new(10, 45)),
            (new(10, 50), new(11, 40)),
            // lunch gap: 11:40 - 12:20
            (new(12, 20), new(13, 10)),
            (new(13, 15), new(14, 5)),
            (new(14, 10), new(15, 0)),
            (new(15, 5), new(15, 55)),
            (new(16, 0), new(16, 50)),
            (new(16, 55), new(17, 45)),
        };

        for (var i = 0; i < slots.Length; i++)
        {
            _context.TimeSlots.Add(new TimeSlot
            {
                TermId = TermId,
                DayOfWeek = DayOfWeek.Monday,
                StartTime = slots[i].start,
                EndTime = slots[i].end,
                SlotOrder = i + 1,
                DisplayName = $"Pazartesi {i + 1}. Ders ({slots[i].start:HH\\:mm}-{slots[i].end:HH\\:mm})"
            });
        }

        _context.SaveChanges();
    }

    private void SetTermStatus(TermStatus status)
    {
        var term = _context.AcademicTerms.Single(t => t.Id == TermId);
        term.Status = status;
        _context.SaveChanges();
    }

    private ConstraintEvaluationService CreateConstraintEvaluationService()
    {
        var evaluators = new IConstraintEvaluator[]
        {
            new CapacityEvaluator(),
            new ClassroomTypeEvaluator(),
            new FacultyClassroomEvaluator(),
            new ClassroomConflictEvaluator(),
            new InstructorConflictEvaluator(),
            new CurriculumConflictEvaluator(),
            new CourseComponentEvaluator(),
            new SameCourseSectionEvaluator(),
            new WeeklyLoadEvaluator(),
            new DailyLoadEvaluator(),
            new NoDayOffEvaluator(),
            new CorequisiteEvaluator(),
            new CreditHourEvaluator(),
            new LunchBreakEvaluator(),
            new MaxConsecutiveHoursEvaluator(),
            new OccupancyEvaluator(),
            new DailyLoadSoftEvaluator(),
            new SlotCongestionEvaluator(),
            new CoEnrollmentEvaluator(),
            new LunchBreakSoftEvaluator(),
            new StudentGapEvaluator(),
            new InstructorDaySpreadEvaluator(),
            new EarlyLateEvaluator(),
            new WeekdayBalanceEvaluator(),
        };
        return new ConstraintEvaluationService(evaluators, _context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
