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
/// Tests for exam-regulation enforcement in GradeController.Finalize:
///   - weight-bounds enforcement (midterm + final sums)
///   - final-floor enforcement (raw final average below FinalExamFloor → FF)
///   - passing-grade enforcement (roundedScore below PassingGrade → FF)
///   - no regulation → normal grade preserved
/// </summary>
public class GradeFinalizeRegulationTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<IGpaCalculationService> _gpaMock;

    // Fixed IDs used across the test class
    private const int FacultyId = 1;
    private const int DeptId = 1;
    private const int ProgramId = 1;
    private const int CurriculumId = 1;
    private const int CourseId = 1;
    private const int TermId = 1;
    private const int SectionId = 1;
    private const int InstructorId = 1;
    private const int InstructorUserId = 100;
    private const int StudentId = 1;
    private const int StudentUserId = 200;
    private const int RegistrationId = 1;
    private const int MidtermComponentId = 1;
    private const int FinalComponentId = 2;
    private const int ExamRegulationId = 1;
    private const int GradingCriteriaId = 1;

    public GradeFinalizeRegulationTests()
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

    // ── private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Builds the minimal object graph that Finalize needs without an exam regulation.
    /// Weights: midterm=40, final=60 (sum=100). Student scores: midterm=70, final=70.
    /// GradingCriteria defaults (AA≥91, DD≥45) → roundedScore=70 → BB.
    /// </summary>
    private void SeedBaseScenario(
        decimal midtermWeight = 40m,
        decimal finalWeight = 60m,
        decimal midtermScore = 70m,
        decimal finalScore = 70m)
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
        _context.Courses.Add(new Course { Id = CourseId, Code = "TST101", Name = "Test Course", Ects = 3 });
        _context.Curricula.Add(new Curriculum
        {
            Id = CurriculumId,
            ProgramId = ProgramId,
            Name = "Müfredat",
            Version = "2024"
        });
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId,
            CourseId = CourseId,
            IsCompulsory = true,
            SuggestedSemester = 1
        });

        // Academic term (Fall 2024)
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

        // Instructor
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
            MaxWeeklyHours = 12
        });

        // Section
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

        // Grade components
        _context.GradeComponents.Add(new GradeComponent
        {
            Id = MidtermComponentId,
            CourseId = CourseId,
            AcademicTermId = TermId,
            Name = "Vize",
            Weight = midtermWeight,
            Order = 1,
            Type = GradeComponentType.Midterm
        });
        _context.GradeComponents.Add(new GradeComponent
        {
            Id = FinalComponentId,
            CourseId = CourseId,
            AcademicTermId = TermId,
            Name = "Final",
            Weight = finalWeight,
            Order = 2,
            Type = GradeComponentType.Final
        });

        // Grading criteria (defaults: AA≥91, BA≥81, BB≥71, CB≥61, CC≥55, DC≥50, DD≥45)
        _context.GradingCriteria.Add(new GradingCriteria
        {
            Id = GradingCriteriaId,
            CourseId = CourseId,
            AcademicTermId = TermId
        });

        // Student
        _context.Users.Add(new ApplicationUser
        {
            Id = StudentUserId,
            UserName = "ogrenci@test.edu",
            NormalizedUserName = "OGRENCI@TEST.EDU",
            Email = "ogrenci@test.edu",
            NormalizedEmail = "OGRENCI@TEST.EDU",
            FirstName = "Test",
            LastName = "Öğrenci"
        });
        _context.Students.Add(new Student
        {
            Id = StudentId,
            UserId = StudentUserId,
            StudentNumber = "2024001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 2.50m,
            Sgpa = 2.50m,
            CompletedSemesters = 1
        });
        _context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = StudentId,
            ProgramId = ProgramId,
            CurriculumId = CurriculumId,
            Type = StudentProgramType.Main,
            IsActive = true,
            EnrollmentDate = new DateTime(2024, 9, 15)
        });

        // Registration
        _context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = RegistrationId,
            StudentId = StudentId,
            CourseSectionId = SectionId,
            Status = RegistrationStatus.Active
        });

        // Student grades
        _context.StudentGrades.Add(new StudentGrade
        {
            Id = 1,
            CourseRegistrationId = RegistrationId,
            GradeComponentId = MidtermComponentId,
            Score = midtermScore
        });
        _context.StudentGrades.Add(new StudentGrade
        {
            Id = 2,
            CourseRegistrationId = RegistrationId,
            GradeComponentId = FinalComponentId,
            Score = finalScore
        });

        _context.SaveChanges();
    }

    private void SeedExamRegulation(
        decimal minMid = 40m, decimal maxMid = 60m,
        decimal minFin = 40m, decimal maxFin = 60m,
        decimal finalExamFloor = 50m,
        decimal passingGrade = 50m)
    {
        _context.ExamRegulations.Add(new ExamRegulation
        {
            Id = ExamRegulationId,
            ProgramId = ProgramId,
            DegreeLevel = DegreeLevel.Bachelor,
            TermType = TermType.Fall,
            Year = 2024,
            IsActive = true,
            Name = "Test Yönerge",
            MidtermCount = 1,
            FinalCount = 1,
            MinMidtermWeightPercent = minMid,
            MaxMidtermWeightPercent = maxMid,
            MinFinalWeightPercent = minFin,
            MaxFinalWeightPercent = maxFin,
            FinalExamFloor = finalExamFloor,
            PassingGrade = passingGrade,
            CreatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();
    }

    private GradeController CreateController()
    {
        var resolver = new ExamRegulationResolver(_context);
        var controller = new GradeController(
            _context,
            _gpaMock.Object,
            new SystemDateTimeProvider(),
            webModuleControls: new NullWebModuleControlService(),
            examRegulationResolver: resolver);

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
    /// Midterm weight sum (70) exceeds MaxMidterm (60) → Finalize must return 400.
    /// </summary>
    [Fact]
    public async Task Finalize_Blocks_WhenMidtermWeightOutOfBounds()
    {
        // Arrange: midterm weight=70, final=30 → midtermSum=70 > maxMid=60
        SeedBaseScenario(midtermWeight: 70m, finalWeight: 30m);
        SeedExamRegulation(minMid: 40m, maxMid: 60m, minFin: 40m, maxFin: 60m);

        var controller = CreateController();

        // Act
        var result = await controller.Finalize(SectionId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>("midterm weight 70 exceeds MaxMidterm 60");
    }

    /// <summary>
    /// Student raw final score (40) is below FinalExamFloor (50) → grade must be FF
    /// even though the weighted average would otherwise yield a passing grade.
    /// </summary>
    [Fact]
    public async Task Finalize_AssignsFF_WhenFinalScoreBelowFloor()
    {
        // Arrange: midterm=70, final=40 → weightedScore = 70*0.4 + 40*0.6 = 28+24 = 52 → normally DD
        // but FinalExamFloor=50 and finalScore=40 < 50 → FF
        SeedBaseScenario(midtermScore: 70m, finalScore: 40m);
        SeedExamRegulation(
            minMid: 40m, maxMid: 60m,
            minFin: 40m, maxFin: 60m,
            finalExamFloor: 50m,
            passingGrade: 0m); // passingGrade disabled so only floor matters

        var controller = CreateController();

        // Act
        var result = await controller.Finalize(SectionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>("finalize itself succeeds");

        var registration = await _context.CourseRegistrations.FindAsync(RegistrationId);
        registration!.Grade.Should().Be(GradeCode.FF, "final score 40 < FinalExamFloor 50");
    }

    /// <summary>
    /// Rounded weighted average (45) is below PassingGrade (50) → grade must be FF.
    /// roundedScore: midterm=60*0.4=24, final=55*0.6=33 → 57 → normally CC
    /// but let's pick scores that land below passing:
    /// midterm=50*0.4=20, final=50*0.6=30 → 50 → roundedScore=50 → normally DC (≥50)
    /// To get roundedScore < 50: midterm=40*0.4=16, final=48*0.6=28.8 → 44.8 → round=45 < 50 → FF
    /// </summary>
    [Fact]
    public async Task Finalize_AssignsFF_WhenAverageBelowPassingGrade()
    {
        // midterm=40, final=48 → weightedScore = 40*0.4 + 48*0.6 = 16 + 28.8 = 44.8 → rounded=45
        // PassingGrade=50 → 45 < 50 → FF
        // FinalExamFloor=0 so floor check is skipped
        SeedBaseScenario(midtermScore: 40m, finalScore: 48m);
        SeedExamRegulation(
            minMid: 40m, maxMid: 60m,
            minFin: 40m, maxFin: 60m,
            finalExamFloor: 0m,   // floor disabled
            passingGrade: 50m);

        var controller = CreateController();

        // Act
        var result = await controller.Finalize(SectionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>("finalize itself succeeds");

        var registration = await _context.CourseRegistrations.FindAsync(RegistrationId);
        registration!.Grade.Should().Be(GradeCode.FF, "roundedScore 45 < PassingGrade 50");
    }

    /// <summary>
    /// No ExamRegulation row → student gets the letter grade from GradingCriteria, not forced FF.
    /// midterm=70, final=70 → weightedScore=70 → roundedScore=70 → BB (BBMin=71? no, default BB≥71)
    /// Actually default: AA≥91, BA≥81, BB≥71, CB≥61, CC≥55, DC≥50, DD≥45, FF below.
    /// 70 is between DD(45) and BB(71): score=70 → 70 < 71 so not BB → 70 ≥ 61 → CB.
    /// </summary>
    [Fact]
    public async Task Finalize_PreservesNormalGrade_WhenNoRegulation()
    {
        // midterm=70, final=70 → weightedScore=70 → roundedScore=70 → CB (≥61, <71)
        SeedBaseScenario(midtermScore: 70m, finalScore: 70m);
        // No ExamRegulation seeded

        var controller = CreateController();

        // Act
        var result = await controller.Finalize(SectionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>("no regulation → normal finalization");

        var registration = await _context.CourseRegistrations.FindAsync(RegistrationId);
        registration!.Grade.Should().NotBe(GradeCode.FF, "no regulation means no forced FF");
        // weightedScore = 70*0.4 + 70*0.6 = 28 + 42 = 70 → CB
        registration.Grade.Should().Be(GradeCode.CB);
    }

    public void Dispose() => _context.Dispose();
}
