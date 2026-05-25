using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.API.Controllers;
using SIS.Application.Common.Services;
using SIS.Application.GpaCalculation.DTOs;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Time;

namespace SIS.Tests.API;

/// <summary>
/// Verifies that when Attendance.Method = "Passive" (or "MarkOnly"), no student is auto-NA'd
/// by absence during GradeController.Finalize, even when their absence hours exceed the limit.
/// </summary>
public class GradeFinalizePassiveMethodTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<IGpaCalculationService> _gpaMock;
    private readonly Mock<ISystemConfigProvider> _configMock;

    private const int FacultyId        = 1;
    private const int DeptId           = 1;
    private const int ProgramId        = 1;
    private const int CurriculumId     = 1;
    private const int CourseId         = 1;
    private const int TermId           = 1;
    private const int SectionId        = 1;
    private const int InstructorId     = 1;
    private const int InstructorUserId = 100;

    private const int StudentOverLimitId     = 1;
    private const int StudentOverLimitUserId = 201;
    private const int RegOverLimitId         = 1;

    private const int MidtermComponentId = 1;
    private const int FinalComponentId   = 2;
    private const int GradingCriteriaId  = 1;
    private const int AttendanceSessionId = 1;

    // 3 theory hours/week × 14 weeks = 42 total, limit = ceil(42 × 30%) = 13
    // Student will get 5 × 3 = 15 absence hours (over the 13-hour limit)
    private const int CourseTheoryHours = 3;
    private const int CourseLabHours    = 0;

    public GradeFinalizePassiveMethodTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);

        _gpaMock = new Mock<IGpaCalculationService>();
        _gpaMock
            .Setup(g => g.RecalculateAndUpdateAsync(It.IsAny<int>()))
            .ReturnsAsync(new GpaResultDto());

        _configMock = new Mock<ISystemConfigProvider>();
        _configMock.Setup(c => c.GetIntAsync("Attendance.TermWeekCount", 14)).ReturnsAsync(14);
        _configMock.Setup(c => c.GetDecimalAsync("Attendance.TheoryAbsenceLimitPercent", 30m)).ReturnsAsync(30m);
        _configMock.Setup(c => c.GetDecimalAsync("Attendance.LabAbsenceLimitPercent", 20m)).ReturnsAsync(20m);
        _configMock.Setup(c => c.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(false);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private void SetAttendanceMethod(string method)
    {
        _configMock
            .Setup(c => c.GetStringAsync("Attendance.Method", "Separate", default))
            .ReturnsAsync(method);
    }

    private void SeedBase()
    {
        _context.Faculties.Add(new Faculty { Id = FacultyId, Name = "Test Faculty", Code = "TF" });
        _context.Departments.Add(new Department { Id = DeptId, Name = "Test Dept", Code = "TD", FacultyId = FacultyId });
        _context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId,
            Name = "Test Program",
            Code = "TP",
            DepartmentId = DeptId,
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 8,
            TotalRequiredEcts = 240
        });
        _context.Courses.Add(new Course
        {
            Id = CourseId,
            Code = "PSVM101",
            Name = "Passive Method Test Course",
            Ects = 3,
            TheoryHours = CourseTheoryHours,
            LabHours = CourseLabHours
        });
        _context.Curricula.Add(new Curriculum
        {
            Id = CurriculumId,
            ProgramId = ProgramId,
            Name = "Test Müfredat",
            Version = "2024"
        });
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId,
            CourseId = CourseId,
            IsCompulsory = true,
            SuggestedSemester = 1
        });
        _context.AcademicTerms.Add(new AcademicTerm
        {
            Id = TermId,
            Name = "2024-2025 Güz",
            ShortEnglishName = "Fall 2024",
            TermType = TermType.Fall,
            Year = 2024,
            IsActivated = true,
            TermStart = new DateTime(2024, 10, 1),
            TermEnd = new DateTime(2025, 1, 31)
        });
        _context.Users.Add(new ApplicationUser
        {
            Id = InstructorUserId,
            UserName = "hoca@test.edu",
            NormalizedUserName = "HOCA@TEST.EDU",
            Email = "hoca@test.edu",
            NormalizedEmail = "HOCA@TEST.EDU",
            FirstName = "Test",
            LastName = "Hoca"
        });
        _context.Instructors.Add(new Instructor
        {
            Id = InstructorId,
            UserId = InstructorUserId,
            DepartmentId = DeptId,
            Title = "Dr.",
            MaxWeeklyHours = 20
        });
        _context.CourseSections.Add(new CourseSection
        {
            Id = SectionId,
            CourseId = CourseId,
            TermId = TermId,
            InstructorId = InstructorId,
            Capacity = 30,
            Status = CourseSectionStatus.Active,
            IsFinalized = false
        });

        _context.GradeComponents.Add(new GradeComponent
        {
            Id = MidtermComponentId,
            CourseId = CourseId,
            AcademicTermId = TermId,
            Name = "Vize",
            Weight = 40m,
            Order = 1,
            Type = GradeComponentType.Midterm
        });
        _context.GradeComponents.Add(new GradeComponent
        {
            Id = FinalComponentId,
            CourseId = CourseId,
            AcademicTermId = TermId,
            Name = "Final",
            Weight = 60m,
            Order = 2,
            Type = GradeComponentType.Final
        });

        _context.GradingCriteria.Add(new GradingCriteria
        {
            Id = GradingCriteriaId,
            CourseId = CourseId,
            AcademicTermId = TermId
        });

        // Student over the absence limit
        _context.Users.Add(new ApplicationUser
        {
            Id = StudentOverLimitUserId,
            UserName = "overlimit@test.edu",
            NormalizedUserName = "OVERLIMIT@TEST.EDU",
            Email = "overlimit@test.edu",
            NormalizedEmail = "OVERLIMIT@TEST.EDU",
            FirstName = "Over",
            LastName = "Limit"
        });
        _context.Students.Add(new Student
        {
            Id = StudentOverLimitId,
            UserId = StudentOverLimitUserId,
            StudentNumber = "2024001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 2.50m,
            Sgpa = 2.50m,
            CompletedSemesters = 1
        });
        _context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = StudentOverLimitId,
            ProgramId = ProgramId,
            CurriculumId = CurriculumId,
            Type = StudentProgramType.Main,
            IsActive = true,
            EnrollmentDate = new DateTime(2024, 9, 15)
        });
        _context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = RegOverLimitId,
            StudentId = StudentOverLimitId,
            CourseSectionId = SectionId,
            Status = RegistrationStatus.Active
        });

        // Scores: midterm=70, final=70 → CB
        _context.StudentGrades.Add(new StudentGrade { Id = 10, CourseRegistrationId = RegOverLimitId, GradeComponentId = MidtermComponentId, Score = 70m });
        _context.StudentGrades.Add(new StudentGrade { Id = 11, CourseRegistrationId = RegOverLimitId, GradeComponentId = FinalComponentId,   Score = 70m });

        _context.AttendanceSessions.Add(new AttendanceSession
        {
            Id = AttendanceSessionId,
            CourseSectionId = SectionId,
            InstructorId = InstructorId,
            WeekNumber = 1,
            SessionDate = new DateTime(2024, 10, 7),
            CreatedAt = DateTime.UtcNow
        });

        // 5 absent sessions × 3 theory slots = 15 absence hours > limit (13) → would be NA with Separate
        for (var week = 0; week < 5; week++)
        {
            _context.AttendanceRecords.Add(new AttendanceRecord
            {
                Id = 100 + week,
                AttendanceSessionId = AttendanceSessionId,
                CourseRegistrationId = RegOverLimitId,
                StudentId = StudentOverLimitId,
                Status = AttendanceStatus.Absent,
                TotalSlotCount = CourseTheoryHours,
                CreatedAt = DateTime.UtcNow
            });
        }

        _context.SaveChanges();
    }

    private GradeController CreateController()
    {
        var resolver = new ExamRegulationResolver(_context);
        var controller = new GradeController(
            _context,
            _gpaMock.Object,
            new SystemDateTimeProvider(),
            examRegulationResolver: resolver,
            systemConfig: _configMock.Object,
            webModuleControls: new NullWebModuleControlService());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, InstructorUserId.ToString())
                ], "TestAuth"))
            }
        };

        return controller;
    }

    // ── tests ────────────────────────────────────────────────────────────────

    /// <summary>
    /// With Attendance.Method = "Passive", a student who is over the absence limit
    /// must NOT receive an NA grade — normal letter grade is computed instead.
    /// </summary>
    [Fact]
    public async Task Finalize_WithPassiveMethod_DoesNotAutoNaOverLimitStudent()
    {
        SetAttendanceMethod("Passive");
        SeedBase();

        var result = await CreateController().Finalize(SectionId);

        result.Should().BeOfType<OkObjectResult>("finalize should succeed");

        var reg = await _context.CourseRegistrations.FindAsync(RegOverLimitId);
        reg!.Grade.Should().NotBe(GradeCode.NA, "Passive method disables absence-based NA");
        reg.Grade.Should().Be(GradeCode.CB, "normal grade computed from scores");
    }

    /// <summary>
    /// With Attendance.Method = "MarkOnly", same expectation as Passive.
    /// </summary>
    [Fact]
    public async Task Finalize_WithMarkOnlyMethod_DoesNotAutoNaOverLimitStudent()
    {
        SetAttendanceMethod("MarkOnly");
        SeedBase();

        var result = await CreateController().Finalize(SectionId);

        result.Should().BeOfType<OkObjectResult>();

        var reg = await _context.CourseRegistrations.FindAsync(RegOverLimitId);
        reg!.Grade.Should().NotBe(GradeCode.NA, "MarkOnly method disables absence-based NA");
        reg.Grade.Should().Be(GradeCode.CB, "normal grade computed from scores");
    }

    /// <summary>
    /// With Attendance.Method = "Separate" (default), a student over the theory limit IS auto-NA'd.
    /// This is a regression guard to confirm Separate behaviour is preserved.
    /// </summary>
    [Fact]
    public async Task Finalize_WithSeparateMethod_AutoNasOverLimitStudent()
    {
        SetAttendanceMethod("Separate");
        SeedBase();

        var result = await CreateController().Finalize(SectionId);

        result.Should().BeOfType<OkObjectResult>();

        var reg = await _context.CourseRegistrations.FindAsync(RegOverLimitId);
        reg!.Grade.Should().Be(GradeCode.NA, "Separate method auto-NAs when theory absence exceeds limit");
    }

    public void Dispose() => _context.Dispose();
}
