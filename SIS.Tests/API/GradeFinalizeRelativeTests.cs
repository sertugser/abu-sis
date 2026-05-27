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
/// Tests for relative (bell-curve) grading integration in GradeController.Finalize.
///
/// ═══════════════════════════════════════════════════════════════════════
/// HAND-COMPUTED REFERENCE
/// ═══════════════════════════════════════════════════════════════════════
/// Single Type=Other component, weight=100 → roundedScore == raw component score.
///
/// Population design: n=8 → {60×6, 40×1, 80×1}
///   μ = (6×60 + 40 + 80) / 8 = 480 / 8 = 60
///   σ = 10  (same derivation as RelativeGradingServiceTests)
///
///   T-scores:
///     score=80 → T=70  → AA  (TAAMin=70)
///     score=60 → T=50  → CB  (TCBMin=50)
///     score=40 → T=30  → FF  (below TDDMin=35)
///
/// Absolute table (GradingCriteria defaults): AA≥91, BA≥81, BB≥71, CB≥61, CC≥55, DC≥50, DD≥45
///   score=80 → BB  (71 ≤ 80 &lt; 81)  ← DIFFERENT from curve AA
///   score=60 → CC  (55 ≤ 60 &lt; 61)  ← DIFFERENT from curve CB
///   score=40 → FF  (40 &lt; 45)
///
/// Tests use this asymmetry: if curve is active, score=80 → AA; if absolute, score=80 → BB.
/// ═══════════════════════════════════════════════════════════════════════
/// </summary>
public class GradeFinalizeRelativeTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<IGpaCalculationService> _gpaMock;

    // ── Fixed IDs ────────────────────────────────────────────────────────────
    private const int FacultyId = 1;
    private const int DeptId = 1;
    private const int ProgramId = 1;
    private const int CurriculumId = 1;
    private const int CourseId = 1;
    private const int TermId = 1;
    private const int SectionId = 1;
    private const int InstructorId = 1;
    private const int InstructorUserId = 100;
    private const int ComponentId = 1;    // single Type=Other component, weight=100
    private const int GradingCriteriaId = 1;
    private const int ExamRegulationId = 1;

    // Student/registration IDs — 8 students for the main distribution
    // IDs 10-17 for students, 20-27 for users, 30-37 for registrations
    private static int StudentId(int i) => 10 + i;
    private static int StudentUserId(int i) => 20 + i;
    private static int RegistrationId(int i) => 30 + i;

    public GradeFinalizeRelativeTests()
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds the minimal object graph needed: faculty, dept, program, course, curriculum,
    /// term, instructor, section, a single Type=Other component (weight=100), and
    /// default GradingCriteria (AA≥91 … DD≥45).
    /// Does NOT seed ExamRegulation — tests add that themselves.
    /// </summary>
    private void SeedBase()
    {
        _context.Faculties.Add(new Faculty { Id = FacultyId, Name = "Test Faculty", Code = "TF" });
        _context.Departments.Add(new Department
        {
            Id = DeptId, Name = "Test Dept", Code = "TD", FacultyId = FacultyId
        });
        _context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId, Name = "Test Program", Code = "TP", DepartmentId = DeptId,
            DegreeLevel = DegreeLevel.Bachelor, EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 8, TotalRequiredEcts = 240
        });
        _context.Courses.Add(new Course { Id = CourseId, Code = "REL101", Name = "Relative Course", Ects = 3 });
        _context.Curricula.Add(new Curriculum
        {
            Id = CurriculumId, ProgramId = ProgramId, Name = "Müfredat", Version = "2024"
        });
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId, CourseId = CourseId, IsCompulsory = true, SuggestedSemester = 1
        });
        _context.AcademicTerms.Add(new AcademicTerm
        {
            Id = TermId, Name = "2024-2025 Güz", ShortEnglishName = "Fall 2024",
            TermType = TermType.Fall, Year = 2024, IsActivated = true,
            TermStart = new DateTime(2024, 10, 1), TermEnd = new DateTime(2025, 1, 31)
        });
        _context.Users.Add(new ApplicationUser
        {
            Id = InstructorUserId, UserName = "hoca@test.edu", NormalizedUserName = "HOCA@TEST.EDU",
            Email = "hoca@test.edu", NormalizedEmail = "HOCA@TEST.EDU",
            FirstName = "Test", LastName = "Hoca"
        });
        _context.Instructors.Add(new Instructor
        {
            Id = InstructorId, UserId = InstructorUserId, DepartmentId = DeptId,
            Title = "Dr.", MaxWeeklyHours = 12
        });
        _context.CourseSections.Add(new CourseSection
        {
            Id = SectionId, CourseId = CourseId, TermId = TermId,
            InstructorId = InstructorId, Capacity = 30,
            Status = CourseSectionStatus.Active, IsFinalized = false
        });

        // Single Type=Other component, weight=100 (avoids weight-bounds enforcement)
        _context.GradeComponents.Add(new GradeComponent
        {
            Id = ComponentId, CourseId = CourseId, AcademicTermId = TermId,
            Name = "Dönem Sonu", Weight = 100m, Order = 1, Type = GradeComponentType.Other
        });

        // Default GradingCriteria: AA≥91, BA≥81, BB≥71, CB≥61, CC≥55, DC≥50, DD≥45
        // score=80 → CB absolutely; curve gives AA (T=70 ≥ TAAMin=70) — intentionally different
        _context.GradingCriteria.Add(new GradingCriteria
        {
            Id = GradingCriteriaId, CourseId = CourseId, AcademicTermId = TermId
        });

        _context.SaveChanges();
    }

    /// <summary>
    /// Seeds n students (indices 0..n-1), each assigned the provided score.
    /// Returns the studentId for each by index via StudentId(i).
    /// </summary>
    private void SeedStudentsWithScores(IReadOnlyList<decimal> scores)
    {
        for (int i = 0; i < scores.Count; i++)
        {
            _context.Users.Add(new ApplicationUser
            {
                Id = StudentUserId(i),
                UserName = $"s{i}@test.edu",
                NormalizedUserName = $"S{i}@TEST.EDU",
                Email = $"s{i}@test.edu",
                NormalizedEmail = $"S{i}@TEST.EDU",
                FirstName = "Öğrenci",
                LastName = $"{i}"
            });
            _context.Students.Add(new Student
            {
                Id = StudentId(i),
                UserId = StudentUserId(i),
                StudentNumber = $"2024{i:D3}",
                NationalId = $"{10000000000 + i}",
                Status = StudentStatus.Active,
                Cgpa = 2.5m, Sgpa = 2.5m, CompletedSemesters = 1
            });
            _context.StudentPrograms.Add(new StudentProgram
            {
                StudentId = StudentId(i), ProgramId = ProgramId, CurriculumId = CurriculumId,
                Type = StudentProgramType.Main, IsActive = true,
                EnrollmentDate = new DateTime(2024, 9, 15)
            });
            _context.CourseRegistrations.Add(new CourseRegistration
            {
                Id = RegistrationId(i), StudentId = StudentId(i),
                CourseSectionId = SectionId, Status = RegistrationStatus.Active
            });
            _context.StudentGrades.Add(new StudentGrade
            {
                Id = 1000 + i, CourseRegistrationId = RegistrationId(i),
                GradeComponentId = ComponentId, Score = scores[i]
            });
        }
        _context.SaveChanges();
    }

    /// <summary>
    /// Seeds a Relative ExamRegulation.
    /// T-thresholds: TAAMin=70, TBAMin=63, TBBMin=57, TCBMin=50, TCCMin=45, TDCMin=40, TDDMin=35.
    /// Weight bounds are 0-100 so they never block.
    /// </summary>
    private void SeedRelativeRegulation(
        int minStudentCount = 3,
        decimal inclusionLimit = 0m,
        decimal absoluteFloor = 0m,
        decimal passingGrade = 0m,
        decimal finalExamFloor = 0m,
        GradeCode belowFloorGrade = GradeCode.FF)
    {
        _context.ExamRegulations.Add(new ExamRegulation
        {
            Id = ExamRegulationId,
            ProgramId = ProgramId,
            DegreeLevel = DegreeLevel.Bachelor,
            TermType = TermType.Fall,
            Year = 2024,
            IsActive = true,
            Name = "Bağıl Yönerge",
            MidtermCount = 1,
            FinalCount = 1,
            MinMidtermWeightPercent = 0m,
            MaxMidtermWeightPercent = 100m,
            MinFinalWeightPercent = 0m,
            MaxFinalWeightPercent = 100m,
            FinalExamFloor = finalExamFloor,
            PassingGrade = passingGrade,
            BelowFloorGrade = belowFloorGrade,
            LetterRangesEditable = true,
            // Relative evaluation
            EvaluationType = GradeEvaluationType.Relative,
            RelativeMinStudentCount = minStudentCount,
            RelativeInclusionLimit = inclusionLimit,
            RelativeAbsoluteFloor = absoluteFloor,
            TAAMin = 70, TBAMin = 63, TBBMin = 57,
            TCBMin = 50, TCCMin = 45, TDCMin = 40, TDDMin = 35,
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

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Population: 8 students with scores {60, 60, 60, 60, 60, 60, 40, 80}.
    /// μ=60, σ=10 → T-scores: 80→70(AA), 60→50(CB), 40→30(FF below TDDMin=35).
    ///
    /// The absolute table gives: 80→BB(71≤80&lt;81), 60→CC(55≤60&lt;61), 40→FF.
    /// The curve gives: 80→AA, 60→CB, 40→FF.
    ///
    /// Assert that the top student (score=80) gets AA (from curve), NOT BB (from absolute table).
    /// Assert that a median student (score=60) gets CB (from curve), NOT CC (from absolute table).
    /// Assert that the bottom student (score=40) gets FF regardless.
    /// </summary>
    [Fact]
    public async Task Finalize_UsesBellCurve_WhenRelativeRegulationAndEnoughStudents()
    {
        // Arrange
        SeedBase();

        // 8 scores: 6×60, 1×40, 1×80  →  indices 0-5 get 60, index 6 gets 40, index 7 gets 80
        var scores = new decimal[] { 60m, 60m, 60m, 60m, 60m, 60m, 40m, 80m };
        SeedStudentsWithScores(scores);

        SeedRelativeRegulation(minStudentCount: 3); // 8 > 3 → curve runs

        var controller = CreateController();

        // Act
        var result = await controller.Finalize(SectionId);

        // Assert — finalize must succeed
        result.Should().BeOfType<OkObjectResult>("curve applies with 8 students > minStudentCount=3");

        // Top student (score=80) → T=70 → AA via curve; absolute would give BB (71 ≤ 80 < 81)
        var topReg = await _context.CourseRegistrations.FindAsync(RegistrationId(7));
        topReg!.Grade.Should().Be(GradeCode.AA,
            "score=80 → T=70 ≥ TAAMin=70 → AA from bell curve (absolute would give BB)");

        // Median student (score=60) → T=50 → CB via curve; absolute would give CC (55 ≤ 60 < 61)
        var medianReg = await _context.CourseRegistrations.FindAsync(RegistrationId(0));
        medianReg!.Grade.Should().Be(GradeCode.CB,
            "score=60 → T=50 ≥ TCBMin=50 → CB from bell curve (absolute would give CC)");

        // Bottom student (score=40) → T=30 < TDDMin=35 → FF (same in both paths, verifying consistency)
        var bottomReg = await _context.CourseRegistrations.FindAsync(RegistrationId(6));
        bottomReg!.Grade.Should().Be(GradeCode.FF,
            "score=40 → T=30 < TDDMin=35 → FF from curve bottom");
    }

    /// <summary>
    /// Relative regulation with MinStudentCount=10, but only 3 students → curve falls back to
    /// absolute table. score=80 → BB (absolute, 71 ≤ 80 &lt; 81), NOT AA (curve).
    /// </summary>
    [Fact]
    public async Task Finalize_FallsBackToAbsolute_WhenBelowMinStudents()
    {
        // Arrange: 3 students, MinStudentCount=10 → included.Count(3) < 10 → fallback
        SeedBase();

        // 3 students: scores 40, 60, 80
        var scores = new decimal[] { 40m, 60m, 80m };
        SeedStudentsWithScores(scores);

        SeedRelativeRegulation(minStudentCount: 10); // 3 < 10 → fallback

        var controller = CreateController();

        // Act
        var result = await controller.Finalize(SectionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>("finalize succeeds; curve falls back to absolute");

        // score=80 → absolute BB (71 ≤ 80 < 81), NOT curve AA — verifies fallback path
        var reg80 = await _context.CourseRegistrations.FindAsync(RegistrationId(2));
        reg80!.Grade.Should().Be(GradeCode.BB,
            "curve falls back to absolute (MinStudentCount=10 > 3 students) → score=80 → BB");
        reg80.Grade.Should().NotBe(GradeCode.AA,
            "curve AA must not be assigned when the engine fell back to absolute");
    }

    /// <summary>
    /// No ExamRegulation seeded → absolute path, same behavior as before.
    /// score=80 → BB via default GradingCriteria (71 ≤ 80 &lt; 81).
    /// </summary>
    [Fact]
    public async Task Finalize_UsesAbsolute_WhenNoRelativeRegulation()
    {
        // Arrange
        SeedBase();

        // 4 students
        var scores = new decimal[] { 40m, 60m, 70m, 80m };
        SeedStudentsWithScores(scores);

        // No ExamRegulation seeded at all

        var controller = CreateController();

        // Act
        var result = await controller.Finalize(SectionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>("no regulation → normal absolute finalize");

        // score=80 → absolute BB (71 ≤ 80 < 81)
        var reg80 = await _context.CourseRegistrations.FindAsync(RegistrationId(3));
        reg80!.Grade.Should().Be(GradeCode.BB,
            "no regulation → absolute path; score=80 maps to BB in default GradingCriteria");
    }

    /// <summary>
    /// Relative regulation is active and enough students → curve runs.
    /// PassingGrade=50 is also set. A student whose roundedScore=45 might get DC from the curve
    /// (if T-score lands there), but PassingGrade check must still force them to
    /// BelowFloorGrade (FF by default) regardless.
    ///
    /// Population: {70×6, 50×1, 30×1}
    ///   μ = (6×70 + 50 + 30) / 8 = 500/8 = 62.5
    ///   σ ≈ 13.919
    ///   score=50 → T ≈ 41.02 → DC (TDCMin=40 ≤ T &lt; TCCMin=45)  [curve says pass-ish]
    ///   score=30 → T ≈ 26.64 → FF (below TDDMin=35)
    ///
    /// However PassingGrade=50:
    ///   student with score=50 → roundedScore=50 → 50 ≥ 50 → NOT blocked by passing grade
    ///   We need a student with roundedScore &lt; 50, so use score=45:
    ///     T(45) = 50 + 10×(45-62.5)/13.919 ≈ 50 - 12.58 ≈ 37.42 → DD (TDDMin=35 ≤ T &lt; TDCMin=40)
    ///     Absolute: score=45 → 45 ≥ DDMin=45 → DD
    ///   Both paths give DD, but PassingGrade=50 forces to BelowFloorGrade=FF.
    ///
    /// Revised design: use score=49 instead.
    ///   T(49) = 50 + 10×(49-62.5)/13.919 ≈ 50 - 9.70 ≈ 40.30 → DC (TDCMin=40 ≤ T &lt; TCCMin=45)
    ///   Absolute: score=49 → DC (50&gt;49&gt;=45 is DD actually: DC starts at 50)
    ///   Actually: score=49 &lt; DCMin=50 → DD (absolute). Curve: T≈40.3 → DC.
    ///   PassingGrade=50 → 49 &lt; 50 → force FF regardless of DC from curve.
    ///
    /// Full population for this test: {70×6, 49×1, 30×1}
    ///   (replaced 50 with 49 to ensure roundedScore &lt; PassingGrade=50)
    ///   μ = (6×70 + 49 + 30) / 8 = 499/8 = 62.375
    ///   σ² = [6×(70-62.375)² + (49-62.375)² + (30-62.375)²] / 8
    ///      = [6×58.1406 + 178.8906 + 1048.2656] / 8
    ///      ≈ [348.844 + 178.891 + 1048.266] / 8 ≈ 1576.001 / 8 ≈ 197.0
    ///   σ ≈ 14.04
    ///   T(49) = 50 + 10×(49-62.375)/14.04 ≈ 50 + 10×(-0.9526) ≈ 40.47 → DC
    ///   PassingGrade=50, roundedScore=49 &lt; 50 → BelowFloorGrade=FF wins.
    /// </summary>
    [Fact]
    public async Task Finalize_RelativeStillEnforcesPassingGrade()
    {
        // Arrange
        SeedBase();

        // Population: {70×6, 49×1, 30×1}
        // index 0-5: score=70, index 6: score=49 (below passing=50), index 7: score=30 (FF)
        var scores = new decimal[] { 70m, 70m, 70m, 70m, 70m, 70m, 49m, 30m };
        SeedStudentsWithScores(scores);

        SeedRelativeRegulation(
            minStudentCount: 3,
            passingGrade: 50m);  // passing-grade check: roundedScore < 50 → FF

        var controller = CreateController();

        // Act
        var result = await controller.Finalize(SectionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>("curve runs; Faz 2a floors still apply");

        // score=49 → curve gives DC (T≈40.47 ≥ TDCMin=40) but roundedScore=49 < PassingGrade=50
        // → BelowFloorGrade=FF must override the curve letter
        var belowPassingReg = await _context.CourseRegistrations.FindAsync(RegistrationId(6));
        belowPassingReg!.Grade.Should().Be(GradeCode.FF,
            "curve would give DC but roundedScore=49 < PassingGrade=50 → Faz 2a forces FF");
        belowPassingReg.Grade.Should().NotBe(GradeCode.DC,
            "passing-grade enforcement must survive in relative mode");

        // score=70 → curve applies normally, not forced by passing-grade check (70 ≥ 50)
        var normalReg = await _context.CourseRegistrations.FindAsync(RegistrationId(0));
        normalReg!.Grade.Should().NotBe(GradeCode.FF,
            "score=70 ≥ PassingGrade=50 → passing-grade check must NOT force FF");
    }

    public void Dispose() => _context.Dispose();
}
