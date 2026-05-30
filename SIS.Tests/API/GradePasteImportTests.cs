using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Controllers;
using SIS.Application.Common.Services;
using SIS.Application.GpaCalculation.DTOs;
using SIS.Application.GpaCalculation.Services;
using SIS.Application.Grading;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Time;
using Moq;

namespace SIS.Tests.API;

/// <summary>
/// Tests for paste-based bulk score import (PastePreview + PasteApply) and
/// the instructor exams-overview aggregate endpoint (ExamsOverview).
/// </summary>
public class GradePasteImportTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<IGpaCalculationService> _gpaMock;

    // ── Shared IDs ──────────────────────────────────────────────────────────

    private const int FacultyId = 1;
    private const int DeptId = 1;
    private const int ProgramId = 1;
    private const int CurriculumId = 1;
    private const int CourseId = 1;
    private const int TermId = 1;
    private const int SectionId = 1;
    private const int InstructorId = 1;
    private const int InstructorUserId = 100;

    private const int Student1Id = 10;
    private const int Student1UserId = 200;
    private const int Student1RegId = 10;
    private const string Student1Number = "2024001";

    private const int Student2Id = 11;
    private const int Student2UserId = 201;
    private const int Student2RegId = 11;
    private const string Student2Number = "2024002";

    private const int ComponentId = 5;

    // ── Constructor ──────────────────────────────────────────────────────────

    public GradePasteImportTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);

        _gpaMock = new Mock<IGpaCalculationService>();
        _gpaMock
            .Setup(g => g.RecalculateAndUpdateAsync(It.IsAny<int>()))
            .ReturnsAsync(new GpaResultDto());
    }

    public void Dispose() => _context.Dispose();

    // ── Seed helpers ─────────────────────────────────────────────────────────

    /// <summary>Seeds the base infrastructure: faculty → dept → program → course → term → instructor → section → component.</summary>
    private void SeedBase(bool sectionFinalized = false)
    {
        _context.Faculties.Add(new Faculty { Id = FacultyId, Name = "Test Faculty", Code = "TF" });
        _context.Departments.Add(new Department { Id = DeptId, Name = "Test Dept", Code = "TD", FacultyId = FacultyId });
        _context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId, Name = "Test Program", Code = "TP", DepartmentId = DeptId,
            DegreeLevel = DegreeLevel.Bachelor, EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 8, TotalRequiredEcts = 240
        });
        _context.Courses.Add(new Course { Id = CourseId, Code = "BM101", Name = "Test Course", Ects = 3 });
        _context.Curricula.Add(new Curriculum { Id = CurriculumId, ProgramId = ProgramId, Name = "Mufredat", Version = "2024" });
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId, CourseId = CourseId, IsCompulsory = true, SuggestedSemester = 1
        });
        _context.AcademicTerms.Add(new AcademicTerm
        {
            Id = TermId, Name = "2024-2025 Guz", ShortEnglishName = "Fall 2024",
            TermType = TermType.Fall, Year = 2024, IsActivated = true,
            TermStart = new DateTime(2024, 10, 1), TermEnd = new DateTime(2025, 1, 31)
        });
        _context.Users.Add(new ApplicationUser
        {
            Id = InstructorUserId, UserName = "hoca@test.edu", NormalizedUserName = "HOCA@TEST.EDU",
            Email = "hoca@test.edu", NormalizedEmail = "HOCA@TEST.EDU", FirstName = "Test", LastName = "Hoca"
        });
        _context.Instructors.Add(new Instructor
        {
            Id = InstructorId, UserId = InstructorUserId, DepartmentId = DeptId, Title = "Dr.", MaxWeeklyHours = 12
        });
        _context.CourseSections.Add(new CourseSection
        {
            Id = SectionId, CourseId = CourseId, TermId = TermId,
            InstructorId = InstructorId, Capacity = 30, SectionNumber = 1,
            Status = CourseSectionStatus.Active, IsFinalized = sectionFinalized
        });
        _context.GradeComponents.Add(new GradeComponent
        {
            Id = ComponentId, CourseId = CourseId, AcademicTermId = TermId,
            Name = "Vize", Weight = 40m, Order = 1, Type = GradeComponentType.Midterm
        });
        _context.GradingCriteria.Add(new GradingCriteria { CourseId = CourseId, AcademicTermId = TermId });
        _context.SaveChanges();
    }

    private void SeedStudents()
    {
        _context.Users.Add(new ApplicationUser
        {
            Id = Student1UserId, UserName = "s1@test.edu", NormalizedUserName = "S1@TEST.EDU",
            Email = "s1@test.edu", NormalizedEmail = "S1@TEST.EDU", FirstName = "Ali", LastName = "Yilmaz"
        });
        _context.Students.Add(new Student
        {
            Id = Student1Id, UserId = Student1UserId, StudentNumber = Student1Number,
            NationalId = "11111111111", Status = StudentStatus.Active
        });
        _context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = Student1Id, ProgramId = ProgramId, CurriculumId = CurriculumId,
            Type = StudentProgramType.Main, IsActive = true, EnrollmentDate = new DateTime(2024, 9, 15)
        });
        _context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = Student1RegId, StudentId = Student1Id,
            CourseSectionId = SectionId, Status = RegistrationStatus.Active
        });

        _context.Users.Add(new ApplicationUser
        {
            Id = Student2UserId, UserName = "s2@test.edu", NormalizedUserName = "S2@TEST.EDU",
            Email = "s2@test.edu", NormalizedEmail = "S2@TEST.EDU", FirstName = "Ayse", LastName = "Kaya"
        });
        _context.Students.Add(new Student
        {
            Id = Student2Id, UserId = Student2UserId, StudentNumber = Student2Number,
            NationalId = "22222222222", Status = StudentStatus.Active
        });
        _context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = Student2Id, ProgramId = ProgramId, CurriculumId = CurriculumId,
            Type = StudentProgramType.Main, IsActive = true, EnrollmentDate = new DateTime(2024, 9, 15)
        });
        _context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = Student2RegId, StudentId = Student2Id,
            CourseSectionId = SectionId, Status = RegistrationStatus.Active
        });

        _context.SaveChanges();
    }

    private GradeController CreateController(int userId = InstructorUserId)
    {
        var resolver = new ExamRegulationResolver(_context);
        var controller = new GradeController(
            _context,
            _gpaMock.Object,
            new SystemDateTimeProvider(),
            examRegulationResolver: resolver,
            webModuleControls: new NullWebModuleControlService());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                ], "TestAuth"))
            }
        };
        return controller;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  TASK 2 — Paste preview + apply tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PastePreview_MatchesByStudentNumber_AndReturnsDiff()
    {
        // Arrange
        SeedBase();
        SeedStudents();
        var controller = CreateController();

        var text = $"{Student1Number} 80\n{Student2Number} 55";
        var req = new GradeController.PasteScoresRequest(ComponentId, text, Separator: null);

        // Act
        var result = await controller.PastePreview(SectionId, req, default) as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        var body = result!.Value!;
        var rows = GetProp<IEnumerable<object>>(body, "Rows")!.ToList();
        rows.Should().HaveCount(2);

        var row1 = rows[0];
        GetProp<string>(row1, "StudentNumber").Should().Be(Student1Number);
        GetProp<decimal?>(row1, "NewScore").Should().Be(80m);
        GetProp<string>(row1, "Status").Should().BeOneOf("Valid", "Update");

        var row2 = rows[1];
        GetProp<string>(row2, "StudentNumber").Should().Be(Student2Number);
        GetProp<decimal?>(row2, "NewScore").Should().Be(55m);
    }

    [Fact]
    public async Task PasteApply_PersistsScores()
    {
        // Arrange
        SeedBase();
        SeedStudents();
        var controller = CreateController();

        var text = $"{Student1Number} 80\n{Student2Number} 55";
        var req = new GradeController.PasteScoresRequest(ComponentId, text, Separator: null);

        // Act
        var result = await controller.PasteApply(SectionId, req, default) as OkObjectResult;

        // Assert
        result.Should().NotBeNull();

        var grades = await _context.StudentGrades.ToListAsync();
        grades.Should().HaveCount(2);

        var grade1 = grades.Single(g => g.CourseRegistrationId == Student1RegId);
        grade1.Score.Should().Be(80m);
        grade1.GradeComponentId.Should().Be(ComponentId);

        var grade2 = grades.Single(g => g.CourseRegistrationId == Student2RegId);
        grade2.Score.Should().Be(55m);
    }

    [Fact]
    public async Task PastePreview_RespectsCustomSeparator()
    {
        // Arrange
        SeedBase();
        SeedStudents();
        var controller = CreateController();

        var text = $"{Student1Number};90";
        var req = new GradeController.PasteScoresRequest(ComponentId, text, Separator: ";");

        // Act
        var result = await controller.PastePreview(SectionId, req, default) as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        var body = result!.Value!;
        var rows = GetProp<IEnumerable<object>>(body, "Rows")!.ToList();
        rows.Should().HaveCount(1);

        var row = rows[0];
        GetProp<string>(row, "StudentNumber").Should().Be(Student1Number);
        GetProp<decimal?>(row, "NewScore").Should().Be(90m);
        GetProp<string>(row, "Status").Should().BeOneOf("Valid", "Update");
    }

    [Fact]
    public async Task PastePreview_FlagsUnknownStudentNumber()
    {
        // Arrange
        SeedBase();
        SeedStudents();
        var controller = CreateController();

        var text = "9999999 77"; // student number that doesn't exist in section
        var req = new GradeController.PasteScoresRequest(ComponentId, text, Separator: null);

        // Act
        var result = await controller.PastePreview(SectionId, req, default) as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        var body = result!.Value!;
        var rows = GetProp<IEnumerable<object>>(body, "Rows")!.ToList();
        rows.Should().HaveCount(1);

        var row = rows[0];
        GetProp<string>(row, "Status").Should().Be("Warning");
        GetProp<int>(body, "WarningCount").Should().Be(1);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  TASK 2b — Guard and fix tests (new)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PasteApply_Blocked_WhenSectionFinalized()
    {
        // Arrange
        SeedBase(sectionFinalized: true);
        SeedStudents();
        var controller = CreateController();

        var text = $"{Student1Number} 75";
        var req = new GradeController.PasteScoresRequest(ComponentId, text, Separator: null);

        // Act
        var result = await controller.PasteApply(SectionId, req, default);

        // Assert — must match the same response Excel ImportApply returns for finalized: BadRequest
        result.Should().BeOfType<BadRequestObjectResult>();
        var body = (result as BadRequestObjectResult)!.Value!;
        GetProp<string>(body, "error").Should().Contain("Sonuclandirilmis");
    }

    [Fact]
    public async Task PastePreview_Rejects_WhenComponentNotInSection()
    {
        // Arrange
        SeedBase();
        SeedStudents();
        var controller = CreateController();

        const int WrongComponentId = 9999;
        var req = new GradeController.PasteScoresRequest(WrongComponentId, $"{Student1Number} 80", Separator: null);

        // Act
        var result = await controller.PastePreview(SectionId, req, default);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var body = (result as BadRequestObjectResult)!.Value!;
        GetProp<string>(body, "error").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PasteApply_UpdatesExistingScore()
    {
        // Arrange
        SeedBase();
        SeedStudents();

        // Seed an existing grade for Student1
        _context.StudentGrades.Add(new StudentGrade
        {
            CourseRegistrationId = Student1RegId,
            GradeComponentId = ComponentId,
            Score = 50m
        });
        _context.SaveChanges();

        var controller = CreateController();
        var text = $"{Student1Number} 90";
        var req = new GradeController.PasteScoresRequest(ComponentId, text, Separator: null);

        // Act
        var result = await controller.PasteApply(SectionId, req, default) as OkObjectResult;

        // Assert
        result.Should().NotBeNull();

        var grade = await _context.StudentGrades
            .SingleAsync(g => g.CourseRegistrationId == Student1RegId && g.GradeComponentId == ComponentId);
        grade.Score.Should().Be(90m);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  TASK 3 — ExamsOverview tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExamsOverview_ReturnsAllSectionsWithComponents()
    {
        // Arrange: instructor has 2 sections — one with a component, one without
        SeedBase();

        const int Course2Id = 2;
        const int Section2Id = 2;

        _context.Courses.Add(new Course { Id = Course2Id, Code = "BM202", Name = "Course 2", Ects = 3 });
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId, CourseId = Course2Id, IsCompulsory = true, SuggestedSemester = 2
        });
        _context.CourseSections.Add(new CourseSection
        {
            Id = Section2Id, CourseId = Course2Id, TermId = TermId,
            InstructorId = InstructorId, Capacity = 30, SectionNumber = 1,
            Status = CourseSectionStatus.Active, IsFinalized = false
        });
        _context.SaveChanges();

        var controller = CreateController();

        // Act
        var result = await controller.ExamsOverview(termId: TermId, default) as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        var rows = (result!.Value as IEnumerable<GradeController.ExamOverviewRowDto>)!.ToList();
        rows.Should().HaveCount(2);

        // Section 1 (BM101) — has 1 component (Vize)
        var section1Row = rows.Single(r => r.SectionId == SectionId);
        section1Row.CourseCode.Should().Be("BM101");
        section1Row.Components.Should().HaveCount(1);
        section1Row.Components[0].Name.Should().Be("Vize");
        section1Row.Components[0].ExamDate.Should().BeNull(); // no SectionExam seeded

        // Section 2 (BM202) — no components
        var section2Row = rows.Single(r => r.SectionId == Section2Id);
        section2Row.CourseCode.Should().Be("BM202");
        section2Row.Components.Should().BeEmpty();
    }

    [Fact]
    public async Task ExamsOverview_IncludesExamDateWhenSectionExamExists()
    {
        // Arrange
        SeedBase();
        var examDate = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        _context.SectionExams.Add(new SectionExam
        {
            Id = 1,
            CourseSectionId = SectionId,
            GradeComponentId = ComponentId,
            ExamDate = examDate,
            CreatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();

        var controller = CreateController();

        // Act
        var result = await controller.ExamsOverview(termId: TermId, default) as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        var rows = (result!.Value as IEnumerable<GradeController.ExamOverviewRowDto>)!.ToList();
        rows.Should().HaveCount(1);

        var comp = rows[0].Components.Single();
        comp.ExamDate.Should().Be(examDate);
    }

    [Fact]
    public async Task ExamsOverview_UsesActiveTerm_WhenTermIdNotSpecified()
    {
        // Arrange
        SeedBase(); // term is IsActivated = true

        var controller = CreateController();

        // Act — no termId provided
        var result = await controller.ExamsOverview(termId: null, default) as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        var rows = (result!.Value as IEnumerable<GradeController.ExamOverviewRowDto>)!.ToList();
        rows.Should().HaveCount(1);
        rows[0].SectionId.Should().Be(SectionId);
    }

    // ─── Reflection helpers to inspect anonymous-object properties ───────────

    private static T? GetProp<T>(object obj, string propName)
    {
        var prop = obj.GetType().GetProperty(propName);
        if (prop == null) return default;
        var value = prop.GetValue(obj);
        if (value is T t) return t;
        if (value == null) return default;
        try { return (T)Convert.ChangeType(value, typeof(T)); }
        catch { return default; }
    }
}
