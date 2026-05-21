using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SIS.Application.Common.Services;
using SIS.Application.Common.Time;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class CourseRegistrationManualOpenOverrideTests : IDisposable
{
    private const int FacultyId = 1;
    private const int DepartmentId = 1;
    private const int ProgramId = 1;
    private const int CurriculumId = 1;
    private const int TermId = 1;
    private const int StudentId = 1;
    private const int AdvisorId = 10;

    private readonly AppDbContext _context;
    private readonly FakeClock _clock;
    private readonly MemoryCache _memoryCache;

    public CourseRegistrationManualOpenOverrideTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _clock = new FakeClock { UtcNow = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Utc) };
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        SeedBaseData();
    }

    [Fact]
    public async Task AddToPlan_WhenRegistrationModuleIsManuallyOpen_AllowsOutsideRegistrationWindow()
    {
        ConfigureModuleMode("StudentCourseRegistration", WebModuleControlModes.Open);
        var service = CreateService();
        var course = AddCourse(100, "CENG201", "Algorithms");
        var section = AddSection(1000, course.Id);

        var result = await service.AddToPlanAsync(StudentId, section.Id);

        result.Success.Should().BeTrue();
        _context.CourseRegistrations.Should().ContainSingle(registration =>
            registration.StudentId == StudentId
            && registration.CourseSectionId == section.Id
            && registration.Status == RegistrationStatus.Draft);
    }

    [Fact]
    public async Task DropCourse_WhenAddDropModuleIsManuallyOpen_AllowsOutsideAddDropWindow()
    {
        ConfigureModuleMode("StudentAddDrop", WebModuleControlModes.Open);
        var service = CreateService();
        var course = AddCourse(101, "CENG202", "Operating Systems");
        var section = AddSection(1001, course.Id);
        var registration = AddRegistration(5001, section.Id, RegistrationStatus.Active);

        var result = await service.DropCourseAsync(StudentId, registration.Id);

        result.Success.Should().BeTrue();
        _context.CourseRegistrations.Single(item => item.Id == registration.Id).Status.Should().Be(RegistrationStatus.Dropped);
    }

    [Fact]
    public async Task ApproveStudentRegistrations_WhenApprovalModuleIsManuallyOpen_AllowsOutsideRegistrationWindow()
    {
        ConfigureModuleMode("AdvisorRegistrationApproval", WebModuleControlModes.Open);
        var service = CreateService();
        var course = AddCourse(102, "CENG203", "Databases");
        var section = AddSection(1002, course.Id);
        AddRegistration(5002, section.Id, RegistrationStatus.PendingAdvisorApproval);

        var result = await service.ApproveStudentRegistrationsAsync(AdvisorId, new ApprovalActionDto
        {
            StudentId = StudentId
        });

        result.Success.Should().BeTrue();
        _context.CourseRegistrations.Single(item => item.CourseSectionId == section.Id).Status.Should().Be(RegistrationStatus.Active);
    }

    private CourseRegistrationService CreateService()
    {
        var validationService = new RegistrationValidationService(_context, _clock);
        var settingsProvider = new WebModuleSettingsProvider(_context, _memoryCache, _clock);
        var controlService = new WebModuleControlService(_context, settingsProvider, _clock);
        return new CourseRegistrationService(_context, validationService, _clock, webSettings: settingsProvider, webModuleControls: controlService);
    }

    private void SeedBaseData()
    {
        _context.Faculties.Add(new Faculty { Id = FacultyId, Name = "Engineering", Code = "ENG" });
        _context.Departments.Add(new Department
        {
            Id = DepartmentId,
            Name = "Computer Engineering",
            Code = "CENG",
            FacultyId = FacultyId
        });

        _context.AcademicTerms.Add(new AcademicTerm
        {
            Id = TermId,
            Name = "2025-2026 Bahar",
            ShortName = "2025 Bahar",
            EnglishName = "2025-2026 Spring",
            ShortEnglishName = "2025 Spring",
            Year = 2026,
            TermType = TermType.Spring,
            TermStart = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc),
            IsActivated = true,
            AcademicCalendar = new TermAcademicCalendar
            {
                RegistrationStart = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc),
                RegistrationEnd = new DateTime(2026, 2, 15, 23, 59, 59, DateTimeKind.Utc),
                AddDropStart = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc),
                AddDropEnd = new DateTime(2026, 2, 20, 23, 59, 59, DateTimeKind.Utc),
                WithdrawalDeadline = new DateTime(2026, 2, 25, 23, 59, 59, DateTimeKind.Utc),
                GradeEntryStart = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
                GradeEntryEnd = new DateTime(2026, 7, 10, 23, 59, 59, DateTimeKind.Utc),
                CreatedAt = DateTime.UtcNow,
            }
        });

        _context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId,
            Name = "Computer Engineering",
            Code = "CENG-BSC",
            DepartmentId = DepartmentId,
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240
        });

        _context.Curricula.Add(new Curriculum
        {
            Id = CurriculumId,
            ProgramId = ProgramId,
            Version = "2024",
            IsActive = true
        });

        _context.Instructors.Add(new Instructor
        {
            Id = AdvisorId,
            UserId = 100,
            DepartmentId = DepartmentId,
            Title = "Dr.",
            MaxWeeklyHours = 20
        });

        _context.Students.Add(new Student
        {
            Id = StudentId,
            UserId = 200,
            StudentNumber = "20250001",
            NationalId = "12345678901",
            Status = StudentStatus.Active,
            Cgpa = 2.72m,
            Sgpa = 2.72m,
            CompletedSemesters = 2,
            IsScholarshipStudent = false,
            HasFinancialBlock = false,
            AdvisorId = AdvisorId
        });

        _context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = StudentId,
            ProgramId = ProgramId,
            CurriculumId = CurriculumId,
            Type = StudentProgramType.Main,
            EnrollmentDate = DateTime.UtcNow.AddYears(-1),
            IsActive = true
        });

        _context.SaveChanges();
    }

    private void ConfigureModuleMode(string moduleKey, string mode)
    {
        _context.WebModuleSettings.Add(new WebModuleSetting
        {
            ModuleKey = moduleKey,
            Key = "Mode",
            Value = mode
        });
        _context.SaveChanges();
    }

    private Course AddCourse(int id, string code, string name)
    {
        var course = new Course
        {
            Id = id,
            Code = code,
            Name = name,
            DepartmentId = DepartmentId,
            Ects = 5,
            TheoryHours = 3,
            LabHours = 0
        };
        _context.Courses.Add(course);
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId,
            CourseId = id,
            IsCompulsory = true,
            SuggestedSemester = 2
        });
        _context.SaveChanges();
        return course;
    }

    private CourseSection AddSection(int id, int courseId)
    {
        var section = new CourseSection
        {
            Id = id,
            CourseId = courseId,
            TermId = TermId,
            SectionNumber = 1,
            Capacity = 40,
            EnrolledCount = 1,
            Status = CourseSectionStatus.Active,
            InstructorId = AdvisorId
        };
        _context.CourseSections.Add(section);
        _context.SaveChanges();
        return section;
    }

    private CourseRegistration AddRegistration(int id, int sectionId, RegistrationStatus status)
    {
        var registration = new CourseRegistration
        {
            Id = id,
            StudentId = StudentId,
            CourseSectionId = sectionId,
            Status = status,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow.AddHours(-1)
        };
        _context.CourseRegistrations.Add(registration);
        _context.SaveChanges();
        return registration;
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
        _context.Dispose();
    }
}
