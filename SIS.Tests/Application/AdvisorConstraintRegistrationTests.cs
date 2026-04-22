using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Time;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class AdvisorConstraintRegistrationTests : IDisposable
{
    private const int FacultyId = 1;
    private const int DepartmentId = 1;
    private const int ProgramId = 1;
    private const int CurriculumId = 1;
    private const int ActiveTermId = 1;
    private const int AdvisorId = 10;
    private const int AdvisorUserId = 100;
    private const int StudentId = 1;
    private const int StudentUserId = 200;

    private readonly AppDbContext _context;
    private readonly CourseRegistrationService _service;

    public AdvisorConstraintRegistrationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        SeedBaseData();

        var clock = new FakeClock();
        var validationService = new RegistrationValidationService(_context, clock);
        var constraintService = new AdvisorConstraintService(_context);
        _service = new CourseRegistrationService(_context, validationService, clock, advisorConstraintService: constraintService);
    }

    [Fact]
    public async Task SubmitRegistration_BlockConstraint_PreventsRegistration()
    {
        var course = AddCourse(100, "CENG201", "Data Structures");
        var section = AddSection(1000, course.Id);
        AddConstraint(course.Id, AdvisorConstraintType.Block, "Bu donem alinmamali.");

        var result = await _service.SubmitRegistrationAsync(StudentId, new RegistrationSubmitDto
        {
            CourseSectionIds = [section.Id]
        });

        result.Success.Should().BeFalse();
        result.ValidationResult.Should().NotBeNull();
        result.ValidationResult!.Errors.Should().ContainSingle(error =>
            error.Rule == "AdvisorConstraintBlock"
            && error.Message.Contains("CENG201"));
    }

    [Fact]
    public async Task SubmitRegistration_WarningConstraint_AllowsRegistrationAndReturnsWarning()
    {
        var course = AddCourse(101, "CENG202", "Algorithms");
        var section = AddSection(1001, course.Id);
        AddConstraint(course.Id, AdvisorConstraintType.Warning, "Yuku agir olabilir.");

        var result = await _service.SubmitRegistrationAsync(StudentId, new RegistrationSubmitDto
        {
            CourseSectionIds = [section.Id]
        });

        result.Success.Should().BeTrue();
        result.ValidationResult.Should().NotBeNull();
        result.ValidationResult!.Warnings.Should().ContainSingle(warning =>
            warning.Rule == "AdvisorConstraintWarning"
            && warning.Message.Contains("CENG202"));
        result.Registrations.Should().ContainSingle(r => r.Status == RegistrationStatus.PendingAdvisorApproval.ToString());
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
        var termEnd = DateTime.UtcNow.AddDays(90);
        _context.AcademicTerms.Add(new AcademicTerm
        {
            Id = ActiveTermId,
            Name = "2025-2026 Fall",
            Year = 2025,
            TermType = TermType.Fall,
            RegistrationStart = DateTime.UtcNow.AddDays(-2),
            RegistrationEnd = DateTime.UtcNow.AddDays(5),
            AddDropStart = DateTime.UtcNow.AddDays(6),
            AddDropEnd = DateTime.UtcNow.AddDays(12),
            WithdrawalDeadline = DateTime.UtcNow.AddDays(50),
            TermStart = DateTime.UtcNow.AddDays(-10),
            TermEnd = termEnd,
            GradeEntryStart = termEnd.AddDays(-14),
            GradeEntryEnd = termEnd.AddDays(14)
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
        _context.Users.Add(new ApplicationUser
        {
            Id = AdvisorUserId,
            UserName = "advisor",
            NormalizedUserName = "ADVISOR",
            Email = "advisor@example.com",
            FirstName = "Ayse",
            LastName = "Danisman"
        });
        _context.Instructors.Add(new Instructor
        {
            Id = AdvisorId,
            UserId = AdvisorUserId,
            DepartmentId = DepartmentId,
            Title = "Dr.",
            MaxWeeklyHours = 20
        });
        _context.Users.Add(new ApplicationUser
        {
            Id = StudentUserId,
            UserName = "student",
            NormalizedUserName = "STUDENT",
            Email = "student@example.com",
            FirstName = "Ali",
            LastName = "Ogrenci"
        });
        _context.Students.Add(new Student
        {
            Id = StudentId,
            UserId = StudentUserId,
            StudentNumber = "20240001",
            NationalId = "12345678901",
            Status = StudentStatus.Active,
            Cgpa = 2.50m,
            Sgpa = 2.60m,
            CompletedSemesters = 3,
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
            EnrollmentDate = DateTime.UtcNow.AddYears(-2),
            IsActive = true
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
            Ects = 6,
            TheoryHours = 3,
            LabHours = 0
        };
        _context.Courses.Add(course);
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId,
            CourseId = id,
            IsCompulsory = true,
            SuggestedSemester = 4
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
            TermId = ActiveTermId,
            SectionNumber = 1,
            Capacity = 40,
            EnrolledCount = 0,
            Status = CourseSectionStatus.Active
        };
        _context.CourseSections.Add(section);
        _context.SaveChanges();
        return section;
    }

    private void AddConstraint(int courseId, AdvisorConstraintType type, string note)
    {
        _context.AdvisorCourseConstraints.Add(new AdvisorCourseConstraint
        {
            StudentId = StudentId,
            CourseId = courseId,
            TermId = ActiveTermId,
            AdvisorId = AdvisorId,
            ConstraintType = type,
            Note = note,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
