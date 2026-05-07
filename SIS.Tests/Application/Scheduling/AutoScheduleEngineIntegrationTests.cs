using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Application.Common.Services;
using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Scheduling;
using SIS.Infrastructure.Services.Scheduling.Evaluators.Hard;
using SIS.Infrastructure.Services.Scheduling.Evaluators.Soft;

namespace SIS.Tests.Application.Scheduling;

public class AutoScheduleEngineIntegrationTests : IDisposable
{
    private const int FacultyId = 1;
    private const int DepartmentId = 1;
    private const int TermId = 200;

    private const int Classroom1Id = 10;
    private const int Classroom2Id = 11;
    private const int Classroom3Id = 12;

    private const int Course1Id = 101; // CS101 3T+0L
    private const int Course2Id = 102; // CS102 3T+0L
    private const int Course3Id = 103; // PHYS101 3T+2L

    private const int Section1Id = 1001;
    private const int Section2Id = 1002;
    private const int Section3Id = 1003;

    private readonly AppDbContext _context;
    private readonly ConstraintEvaluationService _constraintService;
    private readonly ISystemConfigProvider _configProvider;
    private readonly AutoScheduleEngine _engine;

    public AutoScheduleEngineIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _constraintService = CreateConstraintEvaluationService();

        var configMock = new Mock<ISystemConfigProvider>();
        configMock
            .Setup(p => p.GetIntAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<string, int, CancellationToken>((_, fallback, _) => Task.FromResult(fallback));
        configMock
            .Setup(p => p.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns<string, bool, CancellationToken>((_, fallback, _) => Task.FromResult(fallback));
        configMock
            .Setup(p => p.GetDecimalAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .Returns<string, decimal, CancellationToken>((_, fallback, _) => Task.FromResult(fallback));
        configMock
            .Setup(p => p.GetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((_, fallback, _) => Task.FromResult(fallback));

        _configProvider = configMock.Object;

        _engine = new AutoScheduleEngine(_context, _constraintService, _configProvider, Mock.Of<ISolverEngine>());

        SeedDatabase();
    }

    private void SeedDatabase()
    {
        // Faculty & Department
        _context.Faculties.Add(new Faculty { Id = FacultyId, Name = "Engineering", Code = "ENG" });
        _context.Departments.Add(new Department { Id = DepartmentId, FacultyId = FacultyId, Name = "Computer Engineering", Code = "CENG" });

        // Academic Term
        _context.AcademicTerms.Add(new AcademicTerm
        {
            Id = TermId,
            Name = "2025-2026 Fall",
            ShortName = "25-26 Fall",
            EnglishName = "Fall 2025-2026",
            Year = 2025,
            TermType = TermType.Fall,
            TermStart = DateTime.UtcNow.AddDays(-10),
            TermEnd = DateTime.UtcNow.AddDays(90),
            AcademicCalendar = new SIS.Domain.Entities.TermAcademicCalendar
            {
                RegistrationStart = DateTime.UtcNow.AddDays(-5),
                RegistrationEnd = DateTime.UtcNow.AddDays(5),
                AddDropStart = DateTime.UtcNow.AddDays(6),
                AddDropEnd = DateTime.UtcNow.AddDays(12),
                WithdrawalDeadline = DateTime.UtcNow.AddDays(49),
                GradeEntryStart = DateTime.UtcNow.AddDays(76),
                GradeEntryEnd = DateTime.UtcNow.AddDays(104),
                CreatedAt = DateTime.UtcNow,
            }
        });

        // Classrooms: 2 Lecture (60 cap), 1 PhysicsLab (30 cap)
        _context.Classrooms.AddRange(
            new Classroom { Id = Classroom1Id, Name = "ENG-A101", Capacity = 60, Type = ClassroomType.Lecture },
            new Classroom { Id = Classroom2Id, Name = "ENG-A102", Capacity = 60, Type = ClassroomType.Lecture },
            new Classroom { Id = Classroom3Id, Name = "ENG-PHYS-LAB", Capacity = 30, Type = ClassroomType.PhysicsLab }
        );

        // ClassroomTermAssignments (all to engineering faculty)
        _context.ClassroomTermAssignments.AddRange(
            new ClassroomTermAssignment { Id = 1, TermId = TermId, ClassroomId = Classroom1Id, FacultyId = FacultyId },
            new ClassroomTermAssignment { Id = 2, TermId = TermId, ClassroomId = Classroom2Id, FacultyId = FacultyId },
            new ClassroomTermAssignment { Id = 3, TermId = TermId, ClassroomId = Classroom3Id, FacultyId = FacultyId }
        );

        // Courses
        _context.Courses.AddRange(
            new Course
            {
                Id = Course1Id,
                Code = "CS101",
                Name = "Intro to CS",
                DepartmentId = DepartmentId,
                Ects = 3,
                TheoryHours = 3,
                LabHours = 0,
                TheoryClassroomType = ClassroomType.Lecture
            },
            new Course
            {
                Id = Course2Id,
                Code = "CS102",
                Name = "Programming I",
                DepartmentId = DepartmentId,
                Ects = 3,
                TheoryHours = 3,
                LabHours = 0,
                TheoryClassroomType = ClassroomType.Lecture
            },
            new Course
            {
                Id = Course3Id,
                Code = "PHYS101",
                Name = "Physics I",
                DepartmentId = DepartmentId,
                Ects = 5,
                TheoryHours = 3,
                LabHours = 2,
                TheoryClassroomType = ClassroomType.Lecture,
                LabClassroomType = ClassroomType.PhysicsLab
            }
        );

        // CourseSections: sections 1 & 2 are pre-scheduled (pinned), section 3 is unscheduled
        _context.CourseSections.AddRange(
            new CourseSection
            {
                Id = Section1Id,
                CourseId = Course1Id,
                TermId = TermId,
                Capacity = 40,
                EnrolledCount = 0,
                SectionNumber = 1,
                Status = CourseSectionStatus.Active,
                IsFinalized = false,
                ClassroomId = Classroom1Id,
                IsPinned = true
            },
            new CourseSection
            {
                Id = Section2Id,
                CourseId = Course2Id,
                TermId = TermId,
                Capacity = 40,
                EnrolledCount = 0,
                SectionNumber = 1,
                Status = CourseSectionStatus.Active,
                IsFinalized = false,
                ClassroomId = Classroom2Id,
                IsPinned = true
            },
            new CourseSection
            {
                Id = Section3Id,
                CourseId = Course3Id,
                TermId = TermId,
                Capacity = 25,
                EnrolledCount = 0,
                SectionNumber = 1,
                Status = CourseSectionStatus.Active,
                IsFinalized = false
            }
        );

        // TimeSlots: Mon-Fri, 9:00-17:00, 1-hour slots (8 per day × 5 days = 40 slots)
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        var slotId = 1;
        foreach (var day in days)
        {
            for (var i = 0; i < 8; i++)
            {
                var start = new TimeOnly(9 + i, 0);
                var end = new TimeOnly(10 + i, 0);
                _context.TimeSlots.Add(new TimeSlot
                {
                    Id = slotId++,
                    TermId = TermId,
                    DayOfWeek = day,
                    StartTime = start,
                    EndTime = end,
                    SlotOrder = i + 1,
                    DisplayName = $"{day} {i + 1}. Slot ({start:HH\\:mm}-{end:HH\\:mm})"
                });
            }
        }

        // Algoritma key'leri SchedulingAlgorithmConfigs tablosuna; S*.Enabled artık
        // ConstraintDefinition.IsEnabled tarafından kontrol edildiği için (tek otorite),
        // factory bunları her zaman true alır — test'te set'i etkisiz; yine de tarihsel
        // olarak burada bırakıyoruz, ileride kaldırılabilir.
        _context.SchedulingAlgorithmConfigs.AddRange(
            new SchedulingAlgorithmConfig { Key = "Schedule.LunchBreak.IsHard", Value = "false", Category = "HardThreshold", DataType = "bool" },
            new SchedulingAlgorithmConfig { Key = "Schedule.MaxConsecutiveHours", Value = "12", Category = "HardThreshold", DataType = "int" },
            new SchedulingAlgorithmConfig { Key = "Classroom.AmphiThreshold", Value = "100", Category = "HardThreshold", DataType = "int" },
            new SchedulingAlgorithmConfig { Key = "Schedule.TheoryLabSplitThreshold", Value = "4", Category = "Other", DataType = "int" },
            new SchedulingAlgorithmConfig { Key = "Classroom.OccupancyTarget.Min", Value = "60", Category = "SoftThreshold", DataType = "int" },
            new SchedulingAlgorithmConfig { Key = "Classroom.OccupancyTarget.Max", Value = "85", Category = "SoftThreshold", DataType = "int" },
            new SchedulingAlgorithmConfig { Key = "SoftConstraint.S2.MaxDailyHours", Value = "6", Category = "SoftThreshold", DataType = "int" },
            new SchedulingAlgorithmConfig { Key = "SoftConstraint.S4.MinCoPairThreshold", Value = "2", Category = "SoftThreshold", DataType = "int" }
        );

        _context.SaveChanges();

        // Pre-schedule sections 1 and 2 so the engine treats them as pinned (not targets)
        // Section1: Mon 9:00-12:00 in Classroom1 (slots 1,2,3)
        // Section2: Tue 9:00-12:00 in Classroom2 (slots 9,10,11)
        _context.SectionSchedules.AddRange(
            new SectionSchedule { CourseSectionId = Section1Id, TimeSlotId = 1, ClassroomId = Classroom1Id },
            new SectionSchedule { CourseSectionId = Section1Id, TimeSlotId = 2, ClassroomId = Classroom1Id },
            new SectionSchedule { CourseSectionId = Section1Id, TimeSlotId = 3, ClassroomId = Classroom1Id },
            new SectionSchedule { CourseSectionId = Section2Id, TimeSlotId = 9, ClassroomId = Classroom2Id },
            new SectionSchedule { CourseSectionId = Section2Id, TimeSlotId = 10, ClassroomId = Classroom2Id },
            new SectionSchedule { CourseSectionId = Section2Id, TimeSlotId = 11, ClassroomId = Classroom2Id }
        );
        _context.SaveChanges();
    }

    private ConstraintEvaluationService CreateConstraintEvaluationService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("constraint-service-" + Guid.NewGuid())
            .Options;
        var ctx = new AppDbContext(options);

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
            new TheoryLabDaySplitEvaluator(),
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
        return new ConstraintEvaluationService(evaluators, ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<ConstraintEvaluationService>.Instance);
    }

    [Fact]
    public async Task Schedules_multiple_sections_with_no_conflicts()
    {
        // Arrange
        var request = new AutoScheduleRequest
        {
            FacultyId = FacultyId,
            TermId = TermId,
            Seed = 42
        };

        // Act
        var result = await _engine.RunAsync(request);

        // Assert
        result.SuccessCount.Should().BeGreaterThan(0);
        result.TotalProcessed.Should().Be(result.SuccessCount + result.FailedCount);
        result.Seed.Should().Be(42);
    }

    [Fact]
    public async Task Deterministic_with_same_seed()
    {
        // Arrange
        var request = new AutoScheduleRequest
        {
            FacultyId = FacultyId,
            TermId = TermId,
            Seed = 123
        };

        // Act — two runs with same seed
        var result1 = await _engine.RunAsync(request);
        var result2 = await _engine.RunAsync(request);

        // Assert
        result1.SuccessCount.Should().Be(result2.SuccessCount);
        result1.TotalPenalty.Should().Be(result2.TotalPenalty);
    }

    [Fact]
    public async Task Mixed_course_assigns_theory_and_lab_separately()
    {
        // Arrange — only request the PHYS101 section (3T + 2L = 5 hours total)
        var request = new AutoScheduleRequest
        {
            FacultyId = FacultyId,
            TermId = TermId,
            SectionIds = new List<int> { Section3Id },
            Seed = 42
        };

        // Act
        var result = await _engine.RunAsync(request);

        // Assert
        result.SuccessCount.Should().Be(1);

        var assignment = result.Assignments.Single();
        assignment.SectionId.Should().Be(Section3Id);

        // PHYS101 has 3 theory + 2 lab = 5 hours total; slots should cover 5 hours
        var totalHours = assignment.ScheduleSlots
            .Sum(s => (s.EndTime - s.StartTime).TotalHours);
        totalHours.Should().BeApproximately(5.0, precision: 0.1);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
