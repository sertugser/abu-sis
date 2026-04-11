using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Scheduling;
using SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;
using SIS.Infrastructure.Services.Scheduling.Evaluators.Soft;

namespace SIS.Tests.Application.Constraints;

public abstract class SchedulingTestBase : IDisposable
{
    protected const int EngineeringFacultyId = 1;
    protected const int ScienceFacultyId = 2;
    protected const int ComputerEngineeringDepartmentId = 1;
    protected const int MathematicsDepartmentId = 2;
    protected const int ActiveTermId = 100;
    protected const int PreviousTermId = 90;

    protected const string ClassroomConflict = "ClassroomConflict";
    protected const string InstructorConflict = "InstructorConflict";
    protected const string CapacityConflict = "CapacityConflict";
    protected const string WeeklyLoadConflict = "WeeklyLoadConflict";
    protected const string ClassroomTypeConflict = "ClassroomTypeConflict";
    protected const string FacultyClassroomConflict = "FacultyClassroomConflict";
    protected const string SameCourseComponentConflict = "SameCourseComponentConflict";
    protected const string SameCourseSectionConflict = "SameCourseSectionConflict";
    protected const string CreditHourMismatch = "CreditHourMismatch";

    protected AppDbContext Context { get; }
    protected SchedulingService Service { get; }

    protected SchedulingTestBase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        Context = new AppDbContext(options);
        Service = new SchedulingService(Context, CreateConstraintEvaluationService());

        SeedBaseData();
        SeedSchedulingDefaults();
    }

    protected Instructor AddInstructor(
        int id,
        int maxWeeklyHours = 20,
        int departmentId = ComputerEngineeringDepartmentId,
        int? userId = null)
    {
        var resolvedUserId = userId ?? 1000 + id;
        Context.Users.Add(new ApplicationUser
        {
            Id = resolvedUserId,
            UserName = $"instructor{resolvedUserId}",
            NormalizedUserName = $"INSTRUCTOR{resolvedUserId}",
            Email = $"instructor{resolvedUserId}@example.com",
            FirstName = $"Name{resolvedUserId}",
            LastName = $"Last{resolvedUserId}"
        });

        var instructor = new Instructor
        {
            Id = id,
            UserId = resolvedUserId,
            DepartmentId = departmentId,
            Title = "Dr.",
            MaxWeeklyHours = maxWeeklyHours
        };

        Context.Instructors.Add(instructor);
        Context.SaveChanges();
        return instructor;
    }

    protected Classroom AddClassroom(
        int id,
        string name,
        int capacity,
        ClassroomType type = ClassroomType.Lecture)
    {
        var classroom = new Classroom
        {
            Id = id,
            Name = name,
            Capacity = capacity,
            Type = type
        };

        Context.Classrooms.Add(classroom);
        Context.SaveChanges();
        return classroom;
    }

    protected Course AddCourse(
        int id,
        string code,
        int theoryHours,
        int labHours = 0,
        int? departmentId = ComputerEngineeringDepartmentId)
    {
        var course = new Course
        {
            Id = id,
            Code = code,
            Name = code,
            DepartmentId = departmentId,
            Ects = Math.Max(1, theoryHours + labHours),
            TheoryHours = theoryHours,
            LabHours = labHours,
            IsNonGpa = false
        };

        Context.Courses.Add(course);
        Context.SaveChanges();
        return course;
    }

    protected CourseSection AddSection(
        int id,
        int courseId,
        int? instructorId,
        int? classroomId,
        int capacity,
        params (DayOfWeek Day, int StartHour, int StartMinute, int EndHour, int EndMinute)[] scheduleSlots)
    {
        return AddSection(id, courseId, instructorId, classroomId, ActiveTermId, capacity, scheduleSlots);
    }

    protected CourseSection AddSection(
        int id,
        int courseId,
        int? instructorId,
        int? classroomId,
        int termId,
        int capacity,
        params (DayOfWeek Day, int StartHour, int StartMinute, int EndHour, int EndMinute)[] scheduleSlots)
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
            SectionNumber = id,
            IsActive = true,
            IsFinalized = false
        };

        Context.CourseSections.Add(section);
        Context.SaveChanges();

        foreach (var slot in scheduleSlots)
        {
            var timeSlot = FindOrCreateTimeSlot(
                termId,
                slot.Day,
                new TimeOnly(slot.StartHour, slot.StartMinute),
                new TimeOnly(slot.EndHour, slot.EndMinute));

            Context.SectionSchedules.Add(new SectionSchedule
            {
                CourseSectionId = section.Id,
                TimeSlotId = timeSlot.Id,
                ClassroomId = classroomId ?? 0
            });
        }

        if (scheduleSlots.Length > 0)
        {
            Context.SaveChanges();
        }

        return section;
    }

    protected TimeSlot AddTimeSlot(
        int termId,
        DayOfWeek day,
        int startHour,
        int startMinute,
        int endHour,
        int endMinute,
        int? slotOrder = null)
    {
        return FindOrCreateTimeSlot(
            termId,
            day,
            new TimeOnly(startHour, startMinute),
            new TimeOnly(endHour, endMinute),
            slotOrder);
    }

    protected void AddClassroomAssignment(int id, int classroomId, int facultyId, int termId = ActiveTermId)
    {
        Context.ClassroomTermAssignments.Add(new ClassroomTermAssignment
        {
            Id = id,
            TermId = termId,
            ClassroomId = classroomId,
            FacultyId = facultyId
        });
        Context.SaveChanges();
    }

    protected void SetSystemConfig(string key, string value)
    {
        var config = Context.SystemConfigs.SingleOrDefault(item => item.Key == key);
        if (config == null)
        {
            Context.SystemConfigs.Add(new SystemConfig
            {
                Key = key,
                Value = value,
                Category = "Scheduling",
                DataType = "string"
            });
        }
        else
        {
            config.Value = value;
        }

        Context.SaveChanges();
    }

    protected static List<ScheduleSlotDto> Slots(
        params (DayOfWeek Day, int StartHour, int StartMinute, int EndHour, int EndMinute)[] slots)
    {
        return slots
            .Select(slot => new ScheduleSlotDto
            {
                Day = slot.Day,
                StartTime = new TimeOnly(slot.StartHour, slot.StartMinute),
                EndTime = new TimeOnly(slot.EndHour, slot.EndMinute)
            })
            .ToList();
    }

    protected Student AddStudent(int id)
    {
        var userId = 5000 + id;
        Context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"student{userId}",
            NormalizedUserName = $"STUDENT{userId}",
            Email = $"student{userId}@example.com",
            FirstName = $"Student{userId}",
            LastName = "Test"
        });

        var student = new Student
        {
            Id = id,
            UserId = userId,
            StudentNumber = $"S{id:000000}",
            NationalId = $"N{id:000000}",
            Status = StudentStatus.Active,
            Cgpa = 3.00m,
            Sgpa = 3.00m
        };

        Context.Students.Add(student);
        Context.SaveChanges();
        return student;
    }

    protected void AddRegistration(int id, int studentId, int courseSectionId, RegistrationStatus status = RegistrationStatus.Active)
    {
        Context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = id,
            StudentId = studentId,
            CourseSectionId = courseSectionId,
            Status = status
        });
        Context.SaveChanges();
    }

    protected void AddCoEnrollmentHistory(int courseId1, int courseId2, int studentCount)
    {
        AddSection(9001, courseId1, null, null, PreviousTermId, 30);
        AddSection(9002, courseId2, null, null, PreviousTermId, 30);

        for (var i = 1; i <= studentCount; i++)
        {
            var student = AddStudent(8000 + i);
            AddRegistration(8100 + (i * 2), student.Id, 9001);
            AddRegistration(8101 + (i * 2), student.Id, 9002);
        }
    }

    private void SeedBaseData()
    {
        Context.Faculties.AddRange(
            new Faculty
            {
                Id = EngineeringFacultyId,
                Name = "Engineering",
                Code = "ENG"
            },
            new Faculty
            {
                Id = ScienceFacultyId,
                Name = "Science",
                Code = "SCI"
            });

        Context.Departments.AddRange(
            new Department
            {
                Id = ComputerEngineeringDepartmentId,
                FacultyId = EngineeringFacultyId,
                Name = "Computer Engineering",
                Code = "CENG"
            },
            new Department
            {
                Id = MathematicsDepartmentId,
                FacultyId = ScienceFacultyId,
                Name = "Mathematics",
                Code = "MATH"
            });

        Context.AcademicTerms.AddRange(
            new AcademicTerm
            {
                Id = ActiveTermId,
                Name = "2025-2026 Spring",
                ShortName = "25-26 Spring",
                EnglishName = "Spring 2025-2026",
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
                Id = PreviousTermId,
                Name = "2024-2025 Fall",
                ShortName = "24-25 Fall",
                EnglishName = "Fall 2024-2025",
                Year = 2024,
                Semester = SemesterType.Fall,
                RegistrationStart = DateTime.UtcNow.AddDays(-180),
                RegistrationEnd = DateTime.UtcNow.AddDays(-170),
                AddDropStart = DateTime.UtcNow.AddDays(-169),
                AddDropEnd = DateTime.UtcNow.AddDays(-164),
                WithdrawalDeadline = DateTime.UtcNow.AddDays(-120),
                TermStart = DateTime.UtcNow.AddDays(-190),
                TermEnd = DateTime.UtcNow.AddDays(-100),
                IsActive = false
            });

        Context.SaveChanges();
    }

    private void SeedSchedulingDefaults()
    {
        AddConfig("SoftConstraint.S1.Enabled", "false");
        AddConfig("SoftConstraint.S2.Enabled", "false");
        AddConfig("SoftConstraint.S3.Enabled", "false");
        AddConfig("SoftConstraint.S4.Enabled", "false");
        AddConfig("SoftConstraint.S5.Enabled", "false");
        AddConfig("SoftConstraint.S7.Enabled", "false");
        AddConfig("SoftConstraint.S8.Enabled", "false");
        AddConfig("SoftConstraint.S9.Enabled", "false");
        AddConfig("SoftConstraint.S12.Enabled", "false");
        AddConfig("Classroom.OccupancyTarget.Min", "60");
        AddConfig("Classroom.OccupancyTarget.Max", "85");
        AddConfig("SoftConstraint.S2.MaxDailyHours", "6");
        AddConfig("SoftConstraint.S4.MinCoPairThreshold", "2");
        AddConfig("Schedule.LunchBreak.IsHard", "false");
        AddConfig("Schedule.MaxConsecutiveHours", "12");
        AddConfig("Schedule.BreakDurationMinutes", "5");
        AddConfig("Classroom.AmphiThreshold", "100");
        Context.SaveChanges();
    }

    private void AddConfig(string key, string value)
    {
        Context.SystemConfigs.Add(new SystemConfig
        {
            Key = key,
            Value = value,
            Category = "Scheduling",
            DataType = "string"
        });
    }

    private TimeSlot FindOrCreateTimeSlot(
        int termId,
        DayOfWeek day,
        TimeOnly startTime,
        TimeOnly endTime,
        int? slotOrder = null)
    {
        var existing = Context.TimeSlots.FirstOrDefault(ts =>
            ts.TermId == termId
            && ts.DayOfWeek == day
            && ts.StartTime == startTime
            && ts.EndTime == endTime);

        if (existing != null)
        {
            return existing;
        }

        var resolvedOrder = slotOrder
            ?? Context.TimeSlots.Count(ts => ts.TermId == termId && ts.DayOfWeek == day) + 1;

        var timeSlot = new TimeSlot
        {
            TermId = termId,
            DayOfWeek = day,
            StartTime = startTime,
            EndTime = endTime,
            SlotOrder = resolvedOrder,
            DisplayName = $"{day} {resolvedOrder}. Slot ({startTime:HH\\:mm}-{endTime:HH\\:mm})"
        };

        Context.TimeSlots.Add(timeSlot);
        Context.SaveChanges();
        return timeSlot;
    }

    private static ConstraintEvaluationService CreateConstraintEvaluationService()
    {
        var evaluators = new IConstraintEvaluator[]
        {
            // Hard
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
            // Soft
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
        return new ConstraintEvaluationService(evaluators);
    }

    public void Dispose()
    {
        Context.Dispose();
    }
}
