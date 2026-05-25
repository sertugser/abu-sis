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
/// Tests for absence-based NA grade assignment in GradeController.Finalize.
///
/// Scenarios covered:
///   1. Student with attendance records OVER the theory absence limit → NA grade.
///   2. Student with attendance records UNDER the theory absence limit → normal letter grade.
///   3. Student with NO attendance records → normal letter grade (not auto-NA).
/// </summary>
public class GradeFinalizeAttendanceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<IGpaCalculationService> _gpaMock;
    private readonly Mock<ISystemConfigProvider> _configMock;

    // Fixed IDs — each test gets an isolated in-memory DB via Guid seed
    private const int FacultyId = 1;
    private const int DeptId = 1;
    private const int ProgramId = 1;
    private const int CurriculumId = 1;
    private const int CourseId = 1;
    private const int TermId = 1;
    private const int SectionId = 1;
    private const int InstructorId = 1;
    private const int InstructorUserId = 100;

    // Student IDs
    private const int StudentOverLimitId = 1;
    private const int StudentUnderLimitId = 2;
    private const int StudentNoAttendanceId = 3;

    private const int StudentOverLimitUserId = 201;
    private const int StudentUnderLimitUserId = 202;
    private const int StudentNoAttendanceUserId = 203;

    private const int RegOverLimitId = 1;
    private const int RegUnderLimitId = 2;
    private const int RegNoAttendanceId = 3;

    private const int MidtermComponentId = 1;
    private const int FinalComponentId = 2;
    private const int GradingCriteriaId = 1;
    private const int AttendanceSessionId = 1;

    // Config: 14 weeks, theory limit 30%, lab limit 20%
    // TheoryHours = 3  → totalTheoryHours = 14 * 3 = 42
    // theoryLimitHours = ceil(42 * 30 / 100) = ceil(12.6) = 13
    // To fail: 14+ theory absences. To pass: ≤13 theory absences.
    private const int CourseTheoryHours = 3;
    private const int CourseLabHours = 0;

    public GradeFinalizeAttendanceTests()
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
        // Ensure unrelated config calls return a safe default
        _configMock
            .Setup(c => c.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(false);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds the full graph: faculty, dept, program, curriculum, course, term, instructor,
    /// section, grade components, grading criteria, and three students with registrations
    /// and grades. Does NOT seed attendance records — callers do that selectively.
    /// </summary>
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
            Code = "ATT101",
            Name = "Attendance Test Course",
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

        // Grade components (sum = 100)
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

        // Grading criteria (AA≥91 … DD≥45)
        _context.GradingCriteria.Add(new GradingCriteria
        {
            Id = GradingCriteriaId,
            CourseId = CourseId,
            AcademicTermId = TermId
        });

        // ── Students ──────────────────────────────────────────────────────────

        AddStudent(StudentOverLimitId, StudentOverLimitUserId, "overlimit@test.edu", "2024001");
        AddStudent(StudentUnderLimitId, StudentUnderLimitUserId, "underlimit@test.edu", "2024002");
        AddStudent(StudentNoAttendanceId, StudentNoAttendanceUserId, "noattendance@test.edu", "2024003");

        // Registrations
        AddRegistration(RegOverLimitId, StudentOverLimitId);
        AddRegistration(RegUnderLimitId, StudentUnderLimitId);
        AddRegistration(RegNoAttendanceId, StudentNoAttendanceId);

        // Grade entries for all three students (so no missing-grade 400)
        // midterm=70, final=70 → weightedScore=70 → CB
        foreach (var (regId, baseId) in new[] {
            (RegOverLimitId, 10),
            (RegUnderLimitId, 20),
            (RegNoAttendanceId, 30)
        })
        {
            _context.StudentGrades.Add(new StudentGrade { Id = baseId,     CourseRegistrationId = regId, GradeComponentId = MidtermComponentId, Score = 70m });
            _context.StudentGrades.Add(new StudentGrade { Id = baseId + 1, CourseRegistrationId = regId, GradeComponentId = FinalComponentId,   Score = 70m });
        }

        // Attendance session (required FK for AttendanceRecord)
        _context.AttendanceSessions.Add(new AttendanceSession
        {
            Id = AttendanceSessionId,
            CourseSectionId = SectionId,
            InstructorId = InstructorId,
            WeekNumber = 1,
            SessionDate = new DateTime(2024, 10, 7),
            CreatedAt = DateTime.UtcNow
        });

        _context.SaveChanges();
    }

    private void AddStudent(int studentId, int userId, string email, string studentNumber)
    {
        _context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = "Test",
            LastName = $"Student{studentId}"
        });
        _context.Students.Add(new Student
        {
            Id = studentId,
            UserId = userId,
            StudentNumber = studentNumber,
            NationalId = $"1111111111{studentId}",
            Status = StudentStatus.Active,
            Cgpa = 2.50m,
            Sgpa = 2.50m,
            CompletedSemesters = 1
        });
        _context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = studentId,
            ProgramId = ProgramId,
            CurriculumId = CurriculumId,
            Type = StudentProgramType.Main,
            IsActive = true,
            EnrollmentDate = new DateTime(2024, 9, 15)
        });
    }

    private void AddRegistration(int regId, int studentId)
    {
        _context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = regId,
            StudentId = studentId,
            CourseSectionId = SectionId,
            Status = RegistrationStatus.Active
        });
    }

    /// <summary>
    /// Adds <paramref name="absentWeeks"/> fully-absent attendance records for the given registration.
    /// Each record represents one week (TotalSlotCount = CourseTheoryHours slots per session).
    /// </summary>
    private void AddAbsenceRecords(int registrationId, int studentId, int absentWeeks, int startRecordId)
    {
        for (var week = 0; week < absentWeeks; week++)
        {
            _context.AttendanceRecords.Add(new AttendanceRecord
            {
                Id = startRecordId + week,
                AttendanceSessionId = AttendanceSessionId,
                CourseRegistrationId = registrationId,
                StudentId = studentId,
                Status = AttendanceStatus.Absent,
                TotalSlotCount = CourseTheoryHours, // 3 theory slots per session
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
    /// A student with absence records exceeding the theory limit receives NA.
    ///
    /// Config: 14 weeks, 30% theory limit.
    /// Course: TheoryHours=3 → totalTheoryHours = 14×3 = 42.
    /// theoryLimitHours = ceil(42 × 30 / 100) = ceil(12.6) = 13.
    /// Over-limit student: 5 sessions × 3 slots = 15 absence hours > 13 → NA.
    /// </summary>
    [Fact]
    public async Task Finalize_AssignsNA_WhenStudentExceedsAbsenceLimit()
    {
        // Arrange
        SeedBase();

        // 5 absent sessions × 3 slots = 15 theory absence hours (> limit of 13)
        AddAbsenceRecords(RegOverLimitId, StudentOverLimitId, absentWeeks: 5, startRecordId: 100);

        var controller = CreateController();

        // Act
        var result = await controller.Finalize(SectionId);

        // Assert — finalize should succeed (200 OK)
        result.Should().BeOfType<OkObjectResult>("finalize returns 200 when NA student is handled");

        var regOverLimit = await _context.CourseRegistrations.FindAsync(RegOverLimitId);
        regOverLimit!.Grade.Should().Be(GradeCode.NA, "student exceeded absence limit");
    }

    /// <summary>
    /// A student with absence records UNDER the limit keeps a normal letter grade (CB).
    ///
    /// Under-limit student: 4 sessions × 3 slots = 12 absence hours (≤ limit 13) → normal.
    /// </summary>
    [Fact]
    public async Task Finalize_PreservesNormalGrade_WhenStudentUnderAbsenceLimit()
    {
        // Arrange
        SeedBase();

        // 4 absent sessions × 3 slots = 12 theory absence hours (≤ limit of 13)
        AddAbsenceRecords(RegUnderLimitId, StudentUnderLimitId, absentWeeks: 4, startRecordId: 200);

        var controller = CreateController();

        // Act
        var result = await controller.Finalize(SectionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        var regUnderLimit = await _context.CourseRegistrations.FindAsync(RegUnderLimitId);
        regUnderLimit!.Grade.Should().NotBe(GradeCode.NA, "student is within attendance limit");
        // midterm=70*0.4=28, final=70*0.6=42 → 70 → CB (≥61, <71)
        regUnderLimit.Grade.Should().Be(GradeCode.CB);
    }

    /// <summary>
    /// A student with NO attendance records is NOT auto-NA — normal grade is computed.
    /// </summary>
    [Fact]
    public async Task Finalize_PreservesNormalGrade_WhenStudentHasNoAttendanceRecords()
    {
        // Arrange — no attendance records for StudentNoAttendanceId
        SeedBase();

        var controller = CreateController();

        // Act
        var result = await controller.Finalize(SectionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        var regNoAttendance = await _context.CourseRegistrations.FindAsync(RegNoAttendanceId);
        regNoAttendance!.Grade.Should().NotBe(GradeCode.NA, "no attendance records → not auto-NA");
        regNoAttendance.Grade.Should().Be(GradeCode.CB, "normal grade should be CB based on scores");
    }

    /// <summary>
    /// When one student is NA and two have normal scores, the non-NA students still get their
    /// correct letter grades — NA student is excluded from the weighted-average loop.
    /// </summary>
    [Fact]
    public async Task Finalize_BothNaAndNormalStudentsCoexist_GradesAreCorrect()
    {
        // Arrange
        SeedBase();

        // Over-limit → NA
        AddAbsenceRecords(RegOverLimitId, StudentOverLimitId, absentWeeks: 5, startRecordId: 100);
        // Under-limit → has records but within limit
        AddAbsenceRecords(RegUnderLimitId, StudentUnderLimitId, absentWeeks: 4, startRecordId: 200);
        // No attendance → normal

        var controller = CreateController();

        // Act
        var result = await controller.Finalize(SectionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        var regOver    = await _context.CourseRegistrations.FindAsync(RegOverLimitId);
        var regUnder   = await _context.CourseRegistrations.FindAsync(RegUnderLimitId);
        var regNoAtt   = await _context.CourseRegistrations.FindAsync(RegNoAttendanceId);

        regOver!.Grade.Should().Be(GradeCode.NA);
        regUnder!.Grade.Should().Be(GradeCode.CB);
        regNoAtt!.Grade.Should().Be(GradeCode.CB);
    }

    public void Dispose() => _context.Dispose();
}
