using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SIS.Application.Common.Services;
using SIS.Application.CourseOpening.DTOs;
using SIS.Application.EnrollmentForecast.DTOs;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.CourseOpening;

namespace SIS.Tests.Application;

/// <summary>
/// Faz 2.A/B/C/D/E/3.B kapsamı: Geçmiş dönemden ders aktarımı testleri.
/// </summary>
public class TermCourseTransferServiceTests
{
    private const int FacultyId = 1;
    private const int DepartmentId = 1;
    private const int OtherDepartmentId = 2;
    private const int SourceTermId = 100;
    private const int TargetTermId = 101;
    private const int InstructorId = 50;

    [Fact]
    public async Task TransferFromTerm_WithInternalExternalQuota_PropagatesToOpenedSection()
    {
        using var context = CreateContext();
        SeedBaseData(context);
        var course = AddCourse(context, 1, "CENG101");
        AddCurriculum(context, 700, 500, course.Id);
        AddSourceSection(context, 1, course.Id, internalQuota: 15, externalQuota: 10);

        var service = CreateService(context);

        var result = await service.TransferFromTermAsync(new TransferFromTermRequest
        {
            DepartmentId = DepartmentId,
            SourceTermId = SourceTermId,
            TargetTermId = TargetTermId,
            Courses = [new TransferCourseItem { CourseId = course.Id }],
        }, CancellationToken.None);

        result.SuccessCount.Should().Be(1);
        var newSection = await context.CourseSections
            .AsNoTracking()
            .FirstAsync(cs => cs.CourseId == course.Id && cs.TermId == TargetTermId);
        newSection.InternalQuota.Should().Be(15);
        newSection.ExternalQuota.Should().Be(10);
    }

    [Fact(Skip = "OpenCourseAsync cross-list query (.Select(cs => cs.CrossListGroupId!.Value)) " +
                 "InMemory provider tarafından çevrilemiyor. Real PostgreSQL gerekiyor — integration test'e taşınacak.")]
    public async Task TransferFromTerm_WithCrossList_OpensParallelSections()
    {
        using var context = CreateContext();
        SeedBaseData(context);
        var mainCourse = AddCourse(context, 10, "YZM301");
        var linkedCourse = AddCourse(context, 11, "BIL301");
        AddCurriculum(context, 700, 500, mainCourse.Id);
        AddSourceSection(context, 10, mainCourse.Id);
        context.CourseCrossLists.Add(new CourseCrossList
        {
            CourseId = mainCourse.Id,
            LinkedCourseId = linkedCourse.Id,
        });
        context.SaveChanges();

        var service = CreateService(context);

        var result = await service.TransferFromTermAsync(new TransferFromTermRequest
        {
            DepartmentId = DepartmentId,
            SourceTermId = SourceTermId,
            TargetTermId = TargetTermId,
            Courses = [new TransferCourseItem { CourseId = mainCourse.Id, IncludeCrossLists = true }],
        }, CancellationToken.None);

        result.Results[0].Success.Should().BeTrue("transfer error: {0}", result.Results[0].Error);
        var targetSections = await context.CourseSections.AsNoTracking()
            .Where(cs => cs.TermId == TargetTermId)
            .ToListAsync();
        targetSections.Should().HaveCount(2);
        targetSections.Should().Contain(s => s.CourseId == linkedCourse.Id);
        targetSections.Select(s => s.CrossListGroupId).Distinct().Should().HaveCount(1);
    }

    [Fact]
    public async Task TransferFromTerm_WithSchedule_CopiesTimeSlots()
    {
        using var context = CreateContext();
        SeedBaseData(context);
        var course = AddCourse(context, 20, "CENG201");
        AddCurriculum(context, 700, 500, course.Id);
        var sourceSection = AddSourceSection(context, 20, course.Id);
        var sourceSlot = AddTimeSlot(context, 1000, SourceTermId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(9, 50));
        var targetSlot = AddTimeSlot(context, 2000, TargetTermId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(9, 50));
        AddSectionSchedule(context, sourceSection.Id, sourceSlot.Id, classroomId: null);

        var service = CreateService(context);

        var result = await service.TransferFromTermAsync(new TransferFromTermRequest
        {
            DepartmentId = DepartmentId,
            SourceTermId = SourceTermId,
            TargetTermId = TargetTermId,
            Courses = [new TransferCourseItem { CourseId = course.Id, IncludeSchedule = true }],
        }, CancellationToken.None);

        result.SuccessCount.Should().Be(1);
        result.Results[0].Warnings.Should().BeEmpty();
        var newSection = await context.CourseSections.AsNoTracking()
            .FirstAsync(cs => cs.CourseId == course.Id && cs.TermId == TargetTermId);
        var newSchedule = await context.SectionSchedules.AsNoTracking()
            .FirstAsync(ss => ss.CourseSectionId == newSection.Id);
        newSchedule.TimeSlotId.Should().Be(targetSlot.Id);
    }

    [Fact]
    public async Task TransferFromTerm_WithSchedule_SkipsWhenInstructorConflict()
    {
        using var context = CreateContext();
        SeedBaseData(context);
        AddInstructor(context, InstructorId, departmentId: DepartmentId);
        var course = AddCourse(context, 30, "CENG301");
        AddCurriculum(context, 700, 500, course.Id);
        var sourceSection = AddSourceSection(context, 30, course.Id, instructorId: InstructorId);
        var sourceSlot = AddTimeSlot(context, 1010, SourceTermId, DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(10, 50));
        var targetSlot = AddTimeSlot(context, 2010, TargetTermId, DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(10, 50));
        AddSectionSchedule(context, sourceSection.Id, sourceSlot.Id);

        // Hedef dönemde aynı instructor zaten aynı slot'ta başka section'da
        var conflictCourse = AddCourse(context, 31, "CENG302");
        var conflictSection = new CourseSection
        {
            Id = 9999,
            CourseId = conflictCourse.Id,
            TermId = TargetTermId,
            SectionNumber = 1,
            Capacity = 40,
            EnrolledCount = 0,
            Status = CourseSectionStatus.Active,
            InstructorId = InstructorId,
        };
        context.CourseSections.Add(conflictSection);
        context.SectionSchedules.Add(new SectionSchedule
        {
            CourseSectionId = conflictSection.Id,
            TimeSlotId = targetSlot.Id,
            ClassroomId = null,
        });
        context.SaveChanges();

        var service = CreateService(context);

        var result = await service.TransferFromTermAsync(new TransferFromTermRequest
        {
            DepartmentId = DepartmentId,
            SourceTermId = SourceTermId,
            TargetTermId = TargetTermId,
            Courses = [new TransferCourseItem { CourseId = course.Id, InstructorId = InstructorId, IncludeSchedule = true }],
        }, CancellationToken.None);

        result.SuccessCount.Should().Be(1);
        result.Results[0].Warnings.Should().Contain(w => w.Contains("hoca"));
        var newSection = await context.CourseSections.AsNoTracking()
            .FirstAsync(cs => cs.CourseId == course.Id && cs.TermId == TargetTermId);
        var newScheduleCount = await context.SectionSchedules.AsNoTracking()
            .CountAsync(ss => ss.CourseSectionId == newSection.Id);
        newScheduleCount.Should().Be(0);
    }

    [Fact]
    public async Task TransferFromTerm_WithSchedule_SkipsWhenClassroomConflict()
    {
        using var context = CreateContext();
        SeedBaseData(context);
        AddClassroom(context, 7);
        var course = AddCourse(context, 40, "CENG401");
        AddCurriculum(context, 700, 500, course.Id);
        var sourceSection = AddSourceSection(context, 40, course.Id);
        var sourceSlot = AddTimeSlot(context, 1020, SourceTermId, DayOfWeek.Wednesday, new TimeOnly(11, 0), new TimeOnly(11, 50));
        var targetSlot = AddTimeSlot(context, 2020, TargetTermId, DayOfWeek.Wednesday, new TimeOnly(11, 0), new TimeOnly(11, 50));
        AddSectionSchedule(context, sourceSection.Id, sourceSlot.Id, classroomId: 7);

        // Hedef dönemde aynı classroom aynı slot'ta dolu
        var conflictCourse = AddCourse(context, 41, "CENG402");
        var conflictSection = new CourseSection
        {
            Id = 9998,
            CourseId = conflictCourse.Id,
            TermId = TargetTermId,
            SectionNumber = 1,
            Capacity = 40,
            EnrolledCount = 0,
            Status = CourseSectionStatus.Active,
        };
        context.CourseSections.Add(conflictSection);
        context.SectionSchedules.Add(new SectionSchedule
        {
            CourseSectionId = conflictSection.Id,
            TimeSlotId = targetSlot.Id,
            ClassroomId = 7,
        });
        context.SaveChanges();

        var service = CreateService(context);

        var result = await service.TransferFromTermAsync(new TransferFromTermRequest
        {
            DepartmentId = DepartmentId,
            SourceTermId = SourceTermId,
            TargetTermId = TargetTermId,
            Courses = [new TransferCourseItem { CourseId = course.Id, IncludeSchedule = true }],
        }, CancellationToken.None);

        result.SuccessCount.Should().Be(1);
        result.Results[0].Warnings.Should().Contain(w => w.Contains("derslik"));
    }

    [Fact]
    public async Task GetPreviousTermCourses_WithProgramFilter_OnlyReturnsProgramCourses()
    {
        using var context = CreateContext();
        SeedBaseData(context);
        var course1 = AddCourse(context, 50, "PRG-A");
        var course2 = AddCourse(context, 51, "PRG-B");
        AddCurriculum(context, curriculumId: 800, programId: 600, course1.Id);
        AddCurriculum(context, curriculumId: 801, programId: 601, course2.Id);
        AddSourceSection(context, 50, course1.Id);
        AddSourceSection(context, 51, course2.Id);

        var service = CreateService(context);

        var result = await service.GetPreviousTermCoursesAsync(
            DepartmentId, SourceTermId, TargetTermId, programId: 600, classLevel: null, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].CourseCode.Should().Be("PRG-A");
    }

    [Fact]
    public async Task GetPreviousTermCourses_ReturnsForecastAndEnrollment()
    {
        using var context = CreateContext();
        SeedBaseData(context);
        var course = AddCourse(context, 60, "FCT101");
        AddCurriculum(context, 802, 602, course.Id);
        AddSourceSection(context, 60, course.Id, capacity: 40, enrolledCount: 15);

        var forecastMock = new Mock<IEnrollmentForecastService>();
        forecastMock.Setup(s => s.GetDepartmentSummaryAsync(DepartmentId))
            .ReturnsAsync(new DepartmentForecastSummaryDto
            {
                DepartmentId = DepartmentId,
                DepartmentName = "Computer Engineering",
                CourseSummaries = { new CourseDemandForecastDto { CourseId = course.Id, CourseCode = "FCT101", CourseName = "Forecast", TotalPredictedDemand = 22 } },
            });

        var service = CreateService(context, forecastMock.Object);

        var result = await service.GetPreviousTermCoursesAsync(
            DepartmentId, SourceTermId, TargetTermId, programId: null, classLevel: null, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].TotalEnrolled.Should().Be(15);
        result[0].PredictedDemand.Should().Be(22);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static TermCourseTransferService CreateService(AppDbContext context, IEnrollmentForecastService? forecastService = null)
    {
        forecastService ??= Mock.Of<IEnrollmentForecastService>();
        var cacheInvalidator = Mock.Of<IEnrollmentForecastCacheInvalidator>();
        var systemConfig = new FakeSystemConfigProvider();
        var helpers = new CourseOpeningHelpers(context);
        var lifecycle = new CourseSectionLifecycleService(context, cacheInvalidator, systemConfig, helpers);
        return new TermCourseTransferService(context, forecastService, systemConfig, lifecycle, helpers, NullLogger<TermCourseTransferService>.Instance);
    }

    /// <summary>Mock yerine fallback'i doğru döndüren basit fake. Mock.Of default 0 döner ve validation'ı kırar.</summary>
    private sealed class FakeSystemConfigProvider : ISystemConfigProvider
    {
        public Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default) => Task.FromResult(fallback);
        public Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default) => Task.FromResult(fallback);
        public Task<string> GetStringAsync(string key, string fallback, CancellationToken ct = default) => Task.FromResult(fallback);
        public Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default) => Task.FromResult(fallback);
        public void InvalidateCache(string key) { }
    }

    private static void SeedBaseData(AppDbContext context)
    {
        context.Faculties.Add(new Faculty { Id = FacultyId, Name = "Engineering", Code = "ENG" });
        context.Departments.AddRange(
            new Department { Id = DepartmentId, FacultyId = FacultyId, Name = "Computer Engineering", Code = "CENG" },
            new Department { Id = OtherDepartmentId, FacultyId = FacultyId, Name = "Electrical", Code = "EEE" });

        var now = DateTime.UtcNow;
        context.AcademicTerms.AddRange(
            new AcademicTerm { Id = SourceTermId, Name = "2024 Güz", ShortName = "24G", EnglishName = "Fall 2024", Year = 2024, TermType = TermType.Fall, TermStart = now.AddDays(-200), TermEnd = now.AddDays(-100) },
            new AcademicTerm { Id = TargetTermId, Name = "2025 Güz", ShortName = "25G", EnglishName = "Fall 2025", Year = 2025, TermType = TermType.Fall, TermStart = now.AddDays(-10), TermEnd = now.AddDays(90) });

        context.SaveChanges();
    }

    private static Course AddCourse(AppDbContext context, int id, string code)
    {
        var course = new Course
        {
            Id = id,
            Code = code,
            Name = code,
            DepartmentId = DepartmentId,
            Ects = 6,
            TheoryHours = 3,
            LabHours = 0,
            IsNonGpa = false,
        };
        context.Courses.Add(course);
        context.SaveChanges();
        return course;
    }

    private static void AddCurriculum(AppDbContext context, int curriculumId, int programId, int courseId)
    {
        if (!context.AcademicPrograms.Any(p => p.Id == programId))
        {
            context.AcademicPrograms.Add(new AcademicProgram
            {
                Id = programId,
                Name = $"Program {programId}",
                Code = $"PRG-{programId}",
                DepartmentId = DepartmentId,
                DegreeLevel = DegreeLevel.Bachelor,
                EducationType = EducationType.Semester,
                MaxStudyDurationSemesters = 14,
                TotalRequiredEcts = 240,
            });
        }
        context.Curricula.Add(new Curriculum { Id = curriculumId, ProgramId = programId, Version = "2025", IsActive = true });
        context.CurriculumCourses.Add(new CurriculumCourse { CurriculumId = curriculumId, CourseId = courseId, SuggestedSemester = 1, IsCompulsory = true });
        context.SaveChanges();
    }

    private static CourseSection AddSourceSection(AppDbContext context, int id, int courseId, int capacity = 40, int enrolledCount = 0, int? internalQuota = null, int? externalQuota = null, int? instructorId = null)
    {
        var section = new CourseSection
        {
            Id = id,
            CourseId = courseId,
            TermId = SourceTermId,
            SectionNumber = 1,
            Capacity = capacity,
            EnrolledCount = enrolledCount,
            InternalQuota = internalQuota,
            ExternalQuota = externalQuota,
            InstructorId = instructorId,
            Status = CourseSectionStatus.Active,
            IsFinalized = false,
        };
        context.CourseSections.Add(section);
        context.SaveChanges();
        return section;
    }

    private static TimeSlot AddTimeSlot(AppDbContext context, int id, int termId, DayOfWeek day, TimeOnly start, TimeOnly end)
    {
        var slot = new TimeSlot
        {
            Id = id,
            TermId = termId,
            DayOfWeek = day,
            StartTime = start,
            EndTime = end,
            SlotOrder = 1,
            DisplayName = $"{day} {start:HH:mm}-{end:HH:mm}",
        };
        context.TimeSlots.Add(slot);
        context.SaveChanges();
        return slot;
    }

    private static void AddSectionSchedule(AppDbContext context, int sectionId, int timeSlotId, int? classroomId = null)
    {
        context.SectionSchedules.Add(new SectionSchedule
        {
            CourseSectionId = sectionId,
            TimeSlotId = timeSlotId,
            ClassroomId = classroomId,
        });
        context.SaveChanges();
    }

    private static void AddInstructor(AppDbContext context, int id, int departmentId)
    {
        context.Users.Add(new ApplicationUser
        {
            Id = 5000 + id,
            UserName = $"inst{id}",
            NormalizedUserName = $"INST{id}",
            Email = $"inst{id}@x.com",
            NormalizedEmail = $"INST{id}@X.COM",
            FirstName = "Test",
            LastName = $"Inst{id}",
        });
        context.Instructors.Add(new Instructor
        {
            Id = id,
            UserId = 5000 + id,
            DepartmentId = departmentId,
            Title = "Dr.",
            MaxWeeklyHours = 12,
        });
        context.SaveChanges();
    }

    private static void AddClassroom(AppDbContext context, int id)
    {
        context.Classrooms.Add(new Classroom
        {
            Id = id,
            Name = $"R-{id}",
            BuildingName = "Test",
            Floor = 1,
            Capacity = 40,
        });
        context.SaveChanges();
    }
}
