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
/// Tests for exam-attendance marking integration in GradeController.Finalize:
///   1. Unexcused Absent → component score treated as 0 (null Score is not a gap).
///   2. Excused → GradeCode.I assigned; other students finalize normally.
///   3. Present/NotMarked with scores → normal letter (existing behaviour preserved).
///   4. Withdrawn registration is excluded from the finalize roster.
/// </summary>
public class GradeFinalizeAttendanceMarkTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<IGpaCalculationService> _gpaMock;
    private readonly Mock<ISystemConfigProvider> _configMock;

    // ── shared IDs ────────────────────────────────────────────────────────────
    private const int FacultyId        = 1;
    private const int DeptId           = 1;
    private const int ProgramId        = 1;
    private const int CurriculumId     = 1;
    private const int CourseId         = 1;
    private const int TermId           = 1;
    private const int SectionId        = 1;
    private const int InstructorId     = 1;
    private const int InstructorUserId = 100;

    // Students A, B, C, D
    private const int StudentAId       = 1;   // used per-test — role varies
    private const int StudentBId       = 2;
    private const int StudentCId       = 3;
    private const int StudentDId       = 4;   // Withdrawn

    private const int StudentAUserId   = 201;
    private const int StudentBUserId   = 202;
    private const int StudentCUserId   = 203;
    private const int StudentDUserId   = 204;

    private const int RegAId           = 1;
    private const int RegBId           = 2;
    private const int RegCId           = 3;
    private const int RegDId           = 4;   // Withdrawn

    // Components: use GradeComponentType.Other so weight-bounds enforcement is skipped.
    private const int VizeComponentId  = 1;   // weight 40
    private const int FinalComponentId = 2;   // weight 60

    private const int GradingCriteriaId = 1;

    // ── constructor ───────────────────────────────────────────────────────────

    public GradeFinalizeAttendanceMarkTests()
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
        _configMock
            .Setup(c => c.GetIntAsync("Attendance.TermWeekCount", 14))
            .ReturnsAsync(14);
        _configMock
            .Setup(c => c.GetDecimalAsync("Attendance.TheoryAbsenceLimitPercent", 30m))
            .ReturnsAsync(30m);
        _configMock
            .Setup(c => c.GetDecimalAsync("Attendance.LabAbsenceLimitPercent", 20m))
            .ReturnsAsync(20m);
        _configMock
            .Setup(c => c.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(false);
    }

    // ── seed helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds shared infrastructure: faculty, dept, program, curriculum, course, term,
    /// instructor, section, grade components (Type=Other, 40+60), grading criteria.
    /// Call BEFORE adding students and registrations.
    /// </summary>
    private void SeedInfrastructure()
    {
        _context.Faculties.Add(new Faculty { Id = FacultyId, Name = "Test Fak", Code = "TF" });
        _context.Departments.Add(new Department
        {
            Id = DeptId, Name = "Test Bolum", Code = "TB", FacultyId = FacultyId
        });
        _context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId, Name = "Test Program", Code = "TP",
            DepartmentId = DeptId,
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 8,
            TotalRequiredEcts = 240
        });
        _context.Courses.Add(new Course
        {
            Id = CourseId, Code = "ATM101", Name = "Attendance Mark Test",
            Ects = 3, TheoryHours = 0, LabHours = 0 // No theory/lab → NA path is never triggered
        });
        _context.Curricula.Add(new Curriculum
        {
            Id = CurriculumId, ProgramId = ProgramId, Name = "Mufredat", Version = "2024"
        });
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId, CourseId = CourseId,
            IsCompulsory = true, SuggestedSemester = 1
        });
        _context.AcademicTerms.Add(new AcademicTerm
        {
            Id = TermId, Name = "2024-2025 Guz", ShortEnglishName = "Fall 2024",
            TermType = TermType.Fall, Year = 2024,
            IsActivated = true,
            TermStart = new DateTime(2024, 10, 1),
            TermEnd   = new DateTime(2025,  1, 31)
        });
        _context.Users.Add(new ApplicationUser
        {
            Id = InstructorUserId, UserName = "hoca@test.edu",
            NormalizedUserName = "HOCA@TEST.EDU",
            Email = "hoca@test.edu", NormalizedEmail = "HOCA@TEST.EDU",
            FirstName = "Test", LastName = "Hoca"
        });
        _context.Instructors.Add(new Instructor
        {
            Id = InstructorId, UserId = InstructorUserId,
            DepartmentId = DeptId, Title = "Dr.", MaxWeeklyHours = 20
        });
        _context.CourseSections.Add(new CourseSection
        {
            Id = SectionId, CourseId = CourseId, TermId = TermId,
            InstructorId = InstructorId, Capacity = 30,
            Status = CourseSectionStatus.Active, IsFinalized = false
        });

        // Type = Other → weight-bounds enforcement is bypassed entirely
        _context.GradeComponents.Add(new GradeComponent
        {
            Id = VizeComponentId, CourseId = CourseId, AcademicTermId = TermId,
            Name = "Vize", Weight = 40m, Order = 1, Type = GradeComponentType.Other
        });
        _context.GradeComponents.Add(new GradeComponent
        {
            Id = FinalComponentId, CourseId = CourseId, AcademicTermId = TermId,
            Name = "Final", Weight = 60m, Order = 2, Type = GradeComponentType.Other
        });

        // Defaults: AA≥91, DD≥45 → score 40 → FF, score 70 → CB
        _context.GradingCriteria.Add(new GradingCriteria
        {
            Id = GradingCriteriaId, CourseId = CourseId, AcademicTermId = TermId
        });
    }

    private void AddStudent(int studentId, int userId, string email, string number)
    {
        _context.Users.Add(new ApplicationUser
        {
            Id = userId, UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email, NormalizedEmail = email.ToUpperInvariant(),
            FirstName = "Test", LastName = $"Student{studentId}"
        });
        _context.Students.Add(new Student
        {
            Id = studentId, UserId = userId,
            StudentNumber = number, NationalId = $"1111111111{studentId}",
            Status = StudentStatus.Active, Cgpa = 2.50m, Sgpa = 2.50m,
            CompletedSemesters = 1
        });
        _context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = studentId, ProgramId = ProgramId,
            CurriculumId = CurriculumId, Type = StudentProgramType.Main,
            IsActive = true, EnrollmentDate = new DateTime(2024, 9, 15)
        });
    }

    private void AddRegistration(int regId, int studentId,
        RegistrationStatus status = RegistrationStatus.Active)
    {
        _context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = regId, StudentId = studentId, CourseSectionId = SectionId, Status = status
        });
    }

    /// <summary>
    /// Adds StudentGrade rows for a registration.
    /// vizeAttendance / finalAttendance = ExamAttendance for each component.
    /// vizeScore / finalScore = null means "no score entered" (simulates absent with null score).
    /// </summary>
    private void AddGrades(
        int regId,
        int gradeIdBase,
        decimal? vizeScore,
        ExamAttendance vizeAttendance,
        decimal? finalScore,
        ExamAttendance finalAttendance)
    {
        _context.StudentGrades.Add(new StudentGrade
        {
            Id = gradeIdBase,
            CourseRegistrationId = regId,
            GradeComponentId = VizeComponentId,
            Score = vizeScore,
            ExamAttendance = vizeAttendance
        });
        _context.StudentGrades.Add(new StudentGrade
        {
            Id = gradeIdBase + 1,
            CourseRegistrationId = regId,
            GradeComponentId = FinalComponentId,
            Score = finalScore,
            ExamAttendance = finalAttendance
        });
    }

    private GradeController CreateController()
    {
        var controller = new GradeController(
            _context,
            _gpaMock.Object,
            new SystemDateTimeProvider(),
            examRegulationResolver: new ExamRegulationResolver(_context),
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

    // ── tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Student A: Vize 100 (Present), Final null (Absent).
    /// Weighted = 100*0.4 + 0*0.6 = 40 → below DDMin(45) → FF.
    /// Student B: Vize 70 (Present), Final 70 (Present) → CB.
    /// Finalize must NOT block due to Student A's null Final score.
    /// </summary>
    [Fact]
    public async Task Finalize_TreatsUnexcusedAbsentAsZero()
    {
        // Arrange
        SeedInfrastructure();
        AddStudent(StudentAId, StudentAUserId, "a@test.edu", "2024001");
        AddStudent(StudentBId, StudentBUserId, "b@test.edu", "2024002");
        AddRegistration(RegAId, StudentAId);
        AddRegistration(RegBId, StudentBId);

        // Student A: Vize 100 Present, Final null Absent
        AddGrades(RegAId, gradeIdBase: 10,
            vizeScore: 100m, vizeAttendance: ExamAttendance.Present,
            finalScore: null, finalAttendance: ExamAttendance.Absent);

        // Student B: all present, passing scores
        AddGrades(RegBId, gradeIdBase: 20,
            vizeScore: 70m, vizeAttendance: ExamAttendance.Present,
            finalScore: 70m, finalAttendance: ExamAttendance.Present);

        await _context.SaveChangesAsync();

        // Act — must not throw or return 400 due to null Final score of Student A
        var result = await CreateController().Finalize(SectionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>("finalize must succeed despite null Absent score");

        var regA = await _context.CourseRegistrations.FindAsync(RegAId);
        var regB = await _context.CourseRegistrations.FindAsync(RegBId);

        // 100*0.4 + 0*0.6 = 40 → FF (below DDMin=45)
        regA!.Grade.Should().Be(GradeCode.FF, "absent final counts as 0 → weighted score 40 → FF");

        // 70*0.4 + 70*0.6 = 70 → CB (≥61)
        regB!.Grade.Should().Be(GradeCode.CB, "normal student gets CB");
    }

    /// <summary>
    /// Student with Final ExamAttendance=Excused (Score null) receives GradeCode.I.
    /// Other students (all present) finalize normally.
    /// Finalize must return 200 OK — it is not blocked by the excused student.
    /// </summary>
    [Fact]
    public async Task Finalize_AssignsIncomplete_ForExcusedStudent()
    {
        // Arrange
        SeedInfrastructure();
        AddStudent(StudentAId, StudentAUserId, "excused@test.edu", "2024001");
        AddStudent(StudentBId, StudentBUserId, "normal@test.edu",  "2024002");
        AddRegistration(RegAId, StudentAId);
        AddRegistration(RegBId, StudentBId);

        // Student A: Vize scored Present, Final null Excused (awaiting makeup)
        AddGrades(RegAId, gradeIdBase: 10,
            vizeScore: 80m, vizeAttendance: ExamAttendance.Present,
            finalScore: null, finalAttendance: ExamAttendance.Excused);

        // Student B: fully present with passing scores
        AddGrades(RegBId, gradeIdBase: 20,
            vizeScore: 70m, vizeAttendance: ExamAttendance.Present,
            finalScore: 70m, finalAttendance: ExamAttendance.Present);

        await _context.SaveChangesAsync();

        // Act
        var result = await CreateController().Finalize(SectionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>("finalize completes even with an excused student");

        var regA = await _context.CourseRegistrations.FindAsync(RegAId);
        var regB = await _context.CourseRegistrations.FindAsync(RegBId);

        regA!.Grade.Should().Be(GradeCode.I, "excused student gets Incomplete, not a letter grade");
        regB!.Grade.Should().Be(GradeCode.CB, "normal student gets CB");
    }

    /// <summary>
    /// All students have Present/NotMarked attendance with valid scores.
    /// Existing behaviour is preserved — normal letter grades are assigned.
    /// </summary>
    [Fact]
    public async Task Finalize_NormalGrade_WhenPresentWithScores()
    {
        // Arrange
        SeedInfrastructure();
        AddStudent(StudentAId, StudentAUserId, "pa@test.edu", "2024001");
        AddStudent(StudentBId, StudentBUserId, "pb@test.edu", "2024002");
        AddRegistration(RegAId, StudentAId);
        AddRegistration(RegBId, StudentBId);

        // Student A: Present, good scores → CB
        AddGrades(RegAId, gradeIdBase: 10,
            vizeScore: 70m, vizeAttendance: ExamAttendance.Present,
            finalScore: 70m, finalAttendance: ExamAttendance.Present);

        // Student B: NotMarked (default), good scores → CB
        AddGrades(RegBId, gradeIdBase: 20,
            vizeScore: 70m, vizeAttendance: ExamAttendance.NotMarked,
            finalScore: 70m, finalAttendance: ExamAttendance.NotMarked);

        await _context.SaveChangesAsync();

        // Act
        var result = await CreateController().Finalize(SectionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        var regA = await _context.CourseRegistrations.FindAsync(RegAId);
        var regB = await _context.CourseRegistrations.FindAsync(RegBId);

        regA!.Grade.Should().Be(GradeCode.CB);
        regB!.Grade.Should().Be(GradeCode.CB);
    }

    /// <summary>
    /// A Withdrawn registration must NOT be in the finalize roster:
    /// it is not graded, not blocked on missing grades, and its Grade stays null.
    /// The active registration still finalises correctly.
    /// </summary>
    [Fact]
    public async Task Finalize_ExcludesWithdrawnFromRoster()
    {
        // Arrange
        SeedInfrastructure();
        AddStudent(StudentAId, StudentAUserId, "active@test.edu",    "2024001");
        AddStudent(StudentDId, StudentDUserId, "withdrawn@test.edu", "2024004");
        AddRegistration(RegAId, StudentAId, RegistrationStatus.Active);
        AddRegistration(RegDId, StudentDId, RegistrationStatus.Withdrawn);

        // Active student: fully scored
        AddGrades(RegAId, gradeIdBase: 10,
            vizeScore: 70m, vizeAttendance: ExamAttendance.Present,
            finalScore: 70m, finalAttendance: ExamAttendance.Present);

        // Withdrawn student: no grades at all (would normally trigger missing-grade 400 if included)
        // Deliberately omit to prove exclusion — if Withdrawn were included, finalize would block.

        await _context.SaveChangesAsync();

        // Act
        var result = await CreateController().Finalize(SectionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>(
            "finalize must succeed; withdrawn student is not in the roster");

        var regA = await _context.CourseRegistrations.FindAsync(RegAId);
        var regD = await _context.CourseRegistrations.FindAsync(RegDId);

        regA!.Grade.Should().Be(GradeCode.CB, "active student gets normal grade");
        regD!.Grade.Should().BeNull("withdrawn student is not touched by finalize");
    }

    public void Dispose() => _context.Dispose();
}
