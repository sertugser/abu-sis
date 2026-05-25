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

    /// <summary>
    /// Seeds an exam regulation with letter-range governance fields.
    /// Weight bounds are set permissively (0-100) so weight-bounds enforcement never blocks.
    /// </summary>
    private void SeedExamRegulationWithLetterPolicy(
        bool letterRangesEditable,
        int aaMin = 90, int baMin = 80, int bbMin = 70,
        int cbMin = 60, int ccMin = 55, int dcMin = 50, int ddMin = 40)
    {
        _context.ExamRegulations.Add(new ExamRegulation
        {
            Id = ExamRegulationId,
            ProgramId = ProgramId,
            DegreeLevel = DegreeLevel.Bachelor,
            TermType = TermType.Fall,
            Year = 2024,
            IsActive = true,
            Name = "Letter Policy Yönerge",
            MidtermCount = 1,
            FinalCount = 1,
            // Permissive bounds so weight-bounds check is never the blocking factor
            MinMidtermWeightPercent = 0m,
            MaxMidtermWeightPercent = 100m,
            MinFinalWeightPercent = 0m,
            MaxFinalWeightPercent = 100m,
            FinalExamFloor = 0m,
            PassingGrade = 0m,
            LetterRangesEditable = letterRangesEditable,
            AAMin = aaMin, BAMin = baMin, BBMin = bbMin,
            CBMin = cbMin, CCMin = ccMin, DCMin = dcMin, DDMin = ddMin,
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

    /// <summary>
    /// A section WITH an ExamRegulation (MinMidterm=40) but ALL GradeComponents typed as Other
    /// (midtermSum==0, finalSum==0) → Finalize must SUCCEED, not return 400.
    /// Preserves legacy behavior for courses whose components were never classified.
    /// </summary>
    [Fact]
    public async Task Finalize_DoesNotBlock_WhenNoComponentsClassified()
    {
        // Arrange: two components, both Type=Other (the migration default)
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var ctx = new AppDbContext(options);

        const int fId = 10, dId = 10, pId = 10, currId = 10;
        const int courseId = 10, termId = 10, sectionId = 10;
        const int instrId = 10, instrUserId = 1010;
        const int studId = 10, studUserId = 2020, regId = 10;
        const int comp1Id = 10, comp2Id = 11, gcId = 10;

        ctx.Faculties.Add(new Faculty { Id = fId, Name = "F", Code = "F" });
        ctx.Departments.Add(new Department { Id = dId, Name = "D", Code = "D", FacultyId = fId });
        ctx.AcademicPrograms.Add(new AcademicProgram
        {
            Id = pId, Name = "P", Code = "P", DepartmentId = dId,
            DegreeLevel = DegreeLevel.Bachelor, EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 8, TotalRequiredEcts = 240
        });
        ctx.Courses.Add(new Course { Id = courseId, Code = "NCC101", Name = "No Classification", Ects = 3 });
        ctx.Curricula.Add(new Curriculum { Id = currId, ProgramId = pId, Name = "M", Version = "2024" });
        ctx.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = currId, CourseId = courseId, IsCompulsory = true, SuggestedSemester = 1
        });
        ctx.AcademicTerms.Add(new AcademicTerm
        {
            Id = termId, Name = "2024-2025 Güz", ShortEnglishName = "Fall 2024",
            TermType = TermType.Fall, Year = 2024, IsActivated = true,
            TermStart = new DateTime(2024, 10, 1), TermEnd = new DateTime(2025, 1, 31)
        });
        ctx.Users.Add(new ApplicationUser
        {
            Id = instrUserId, UserName = "instr@t.edu", NormalizedUserName = "INSTR@T.EDU",
            Email = "instr@t.edu", NormalizedEmail = "INSTR@T.EDU", FirstName = "I", LastName = "T"
        });
        ctx.Instructors.Add(new Instructor
        {
            Id = instrId, UserId = instrUserId, DepartmentId = dId, Title = "Dr.", MaxWeeklyHours = 12
        });
        ctx.CourseSections.Add(new CourseSection
        {
            Id = sectionId, CourseId = courseId, TermId = termId,
            InstructorId = instrId, Capacity = 30,
            Status = CourseSectionStatus.Active, IsFinalized = false
        });

        // Both components are Type=Other (default after migration — never classified)
        ctx.GradeComponents.Add(new GradeComponent
        {
            Id = comp1Id, CourseId = courseId, AcademicTermId = termId,
            Name = "Ödev", Weight = 40m, Order = 1, Type = GradeComponentType.Other
        });
        ctx.GradeComponents.Add(new GradeComponent
        {
            Id = comp2Id, CourseId = courseId, AcademicTermId = termId,
            Name = "Proje", Weight = 60m, Order = 2, Type = GradeComponentType.Other
        });

        // GradingCriteria with defaults (AA≥91 … DD≥45)
        ctx.GradingCriteria.Add(new GradingCriteria
        {
            Id = gcId, CourseId = courseId, AcademicTermId = termId
        });

        ctx.Users.Add(new ApplicationUser
        {
            Id = studUserId, UserName = "s@t.edu", NormalizedUserName = "S@T.EDU",
            Email = "s@t.edu", NormalizedEmail = "S@T.EDU", FirstName = "S", LastName = "T"
        });
        ctx.Students.Add(new Student
        {
            Id = studId, UserId = studUserId, StudentNumber = "2024010",
            NationalId = "22222222222", Status = StudentStatus.Active,
            Cgpa = 3.0m, Sgpa = 3.0m, CompletedSemesters = 1
        });
        ctx.StudentPrograms.Add(new StudentProgram
        {
            StudentId = studId, ProgramId = pId, CurriculumId = currId,
            Type = StudentProgramType.Main, IsActive = true,
            EnrollmentDate = new DateTime(2024, 9, 15)
        });
        ctx.CourseRegistrations.Add(new CourseRegistration
        {
            Id = regId, StudentId = studId,
            CourseSectionId = sectionId, Status = RegistrationStatus.Active
        });
        ctx.StudentGrades.Add(new StudentGrade
        {
            Id = 10, CourseRegistrationId = regId, GradeComponentId = comp1Id, Score = 80m
        });
        ctx.StudentGrades.Add(new StudentGrade
        {
            Id = 11, CourseRegistrationId = regId, GradeComponentId = comp2Id, Score = 75m
        });

        // ExamRegulation WITH MinMidterm=40 — would block if midtermSum (0) were checked
        ctx.ExamRegulations.Add(new ExamRegulation
        {
            Id = 10, ProgramId = pId, DegreeLevel = DegreeLevel.Bachelor,
            TermType = TermType.Fall, Year = 2024, IsActive = true,
            Name = "Yönerge NC", MidtermCount = 1, FinalCount = 1,
            MinMidtermWeightPercent = 40m, MaxMidtermWeightPercent = 60m,
            MinFinalWeightPercent = 40m, MaxFinalWeightPercent = 60m,
            FinalExamFloor = 0m, PassingGrade = 0m,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var gpaMock = new Mock<IGpaCalculationService>();
        gpaMock.Setup(g => g.RecalculateAndUpdateAsync(It.IsAny<int>()))
               .ReturnsAsync(new GpaResultDto());

        var resolver = new ExamRegulationResolver(ctx);
        var controller = new GradeController(
            ctx, gpaMock.Object, new SystemDateTimeProvider(),
            webModuleControls: new NullWebModuleControlService(),
            examRegulationResolver: resolver);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, instrUserId.ToString())
                ], "TestAuth"))
            }
        };

        // Act
        var result = await controller.Finalize(sectionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>(
            "all components are Type=Other (midtermSum==0, finalSum==0) → weight-bounds check must be skipped");
    }

    // ── Letter-range governance (new) ────────────────────────────────────────

    /// <summary>
    /// UpdateGradingCriteria must return 400 when the regulation has LetterRangesEditable=false.
    /// </summary>
    [Fact]
    public async Task UpdateGradingCriteria_Blocked_WhenRegulationLocksLetterRanges()
    {
        // Arrange
        SeedBaseScenario();
        SeedExamRegulationWithLetterPolicy(letterRangesEditable: false);

        var controller = CreateController();
        var dto = new GradeController.GradingCriteriaDto
        {
            AAMin = 90, BAMin = 80, BBMin = 70,
            CBMin = 60, CCMin = 55, DCMin = 50, DDMin = 45
        };

        // Act
        var result = await controller.UpdateGradingCriteria(SectionId, dto);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>(
            "regulation has LetterRangesEditable=false → editing must be blocked");
    }

    /// <summary>
    /// UpdateGradingCriteria must succeed when the regulation has LetterRangesEditable=true.
    /// </summary>
    [Fact]
    public async Task UpdateGradingCriteria_Allowed_WhenRegulationEditable()
    {
        // Arrange
        SeedBaseScenario();
        SeedExamRegulationWithLetterPolicy(letterRangesEditable: true);

        var controller = CreateController();
        var dto = new GradeController.GradingCriteriaDto
        {
            AAMin = 92, BAMin = 82, BBMin = 72,
            CBMin = 62, CCMin = 56, DCMin = 51, DDMin = 46
        };

        // Act
        var result = await controller.UpdateGradingCriteria(SectionId, dto);

        // Assert
        result.Should().BeOfType<OkObjectResult>(
            "regulation has LetterRangesEditable=true → update must succeed");
    }

    /// <summary>
    /// Finalize must use regulation thresholds when the regulation is locked AND no section
    /// GradingCriteria row exists.
    /// Regulation thresholds: AAMin=90, BAMin=80, BBMin=70, CBMin=60, CCMin=55, DCMin=50, DDMin=45
    /// (strictly descending — valid).
    /// Components are both Type=Other so weight-bounds enforcement is skipped.
    /// Student 1: scores=92 → weightedScore=92 → AA (≥90 per regulation).
    /// Student 2: scores=44 → weightedScore=44 → FF (44 < DDMin=45 per regulation).
    /// </summary>
    [Fact]
    public async Task Finalize_UsesRegulationThresholds_WhenLockedAndNoSectionCriteria()
    {
        // Arrange: isolated DB, Type=Other components (skip weight-bounds check),
        // no GradingCriteria row, locked regulation with AAMin=90 / DDMin=50.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var ctx = new AppDbContext(options);

        const int fId = 20, dId = 20, pId = 20, currId = 20;
        const int courseId = 20, termId = 20, sectionId = 20;
        const int instrId = 20, instrUserId = 3030;
        const int studId1 = 20, studUserId1 = 4040;
        const int studId2 = 21, studUserId2 = 4041;
        const int reg1Id = 20, reg2Id = 21;
        const int comp1Id = 20, comp2Id = 21;

        ctx.Faculties.Add(new Faculty { Id = fId, Name = "F2", Code = "F2" });
        ctx.Departments.Add(new Department { Id = dId, Name = "D2", Code = "D2", FacultyId = fId });
        ctx.AcademicPrograms.Add(new AcademicProgram
        {
            Id = pId, Name = "P2", Code = "P2", DepartmentId = dId,
            DegreeLevel = DegreeLevel.Bachelor, EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 8, TotalRequiredEcts = 240
        });
        ctx.Courses.Add(new Course { Id = courseId, Code = "LKD201", Name = "Locked Course", Ects = 3 });
        ctx.Curricula.Add(new Curriculum { Id = currId, ProgramId = pId, Name = "M2", Version = "2024" });
        ctx.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = currId, CourseId = courseId, IsCompulsory = true, SuggestedSemester = 1
        });
        ctx.AcademicTerms.Add(new AcademicTerm
        {
            Id = termId, Name = "2024-2025 Güz", ShortEnglishName = "Fall 2024",
            TermType = TermType.Fall, Year = 2024, IsActivated = true,
            TermStart = new DateTime(2024, 10, 1), TermEnd = new DateTime(2025, 1, 31)
        });
        ctx.Users.Add(new ApplicationUser
        {
            Id = instrUserId, UserName = "ins2@t.edu", NormalizedUserName = "INS2@T.EDU",
            Email = "ins2@t.edu", NormalizedEmail = "INS2@T.EDU", FirstName = "I", LastName = "2"
        });
        ctx.Instructors.Add(new Instructor
        {
            Id = instrId, UserId = instrUserId, DepartmentId = dId, Title = "Dr.", MaxWeeklyHours = 12
        });
        ctx.CourseSections.Add(new CourseSection
        {
            Id = sectionId, CourseId = courseId, TermId = termId,
            InstructorId = instrId, Capacity = 30,
            Status = CourseSectionStatus.Active, IsFinalized = false
        });

        // Both components Type=Other (skip weight-bounds)
        ctx.GradeComponents.Add(new GradeComponent
        {
            Id = comp1Id, CourseId = courseId, AcademicTermId = termId,
            Name = "Ödev", Weight = 40m, Order = 1, Type = GradeComponentType.Other
        });
        ctx.GradeComponents.Add(new GradeComponent
        {
            Id = comp2Id, CourseId = courseId, AcademicTermId = termId,
            Name = "Proje", Weight = 60m, Order = 2, Type = GradeComponentType.Other
        });

        // NO GradingCriteria row → regulation thresholds must be used

        // Locked regulation: AAMin=90 … DDMin=45 (strictly descending, valid)
        ctx.ExamRegulations.Add(new ExamRegulation
        {
            Id = 20, ProgramId = pId, DegreeLevel = DegreeLevel.Bachelor,
            TermType = TermType.Fall, Year = 2024, IsActive = true,
            Name = "Locked Yönerge",
            MidtermCount = 1, FinalCount = 1,
            MinMidtermWeightPercent = 0m, MaxMidtermWeightPercent = 100m,
            MinFinalWeightPercent = 0m, MaxFinalWeightPercent = 100m,
            FinalExamFloor = 0m, PassingGrade = 0m,
            LetterRangesEditable = false,
            AAMin = 90, BAMin = 80, BBMin = 70, CBMin = 60, CCMin = 55, DCMin = 50, DDMin = 45,
            CreatedAt = DateTime.UtcNow
        });

        // Student 1: both component scores = 92 → weightedScore=92 → AA (≥90)
        ctx.Users.Add(new ApplicationUser
        {
            Id = studUserId1, UserName = "s1@t.edu", NormalizedUserName = "S1@T.EDU",
            Email = "s1@t.edu", NormalizedEmail = "S1@T.EDU", FirstName = "S", LastName = "1"
        });
        ctx.Students.Add(new Student
        {
            Id = studId1, UserId = studUserId1, StudentNumber = "20240201",
            NationalId = "33333333333", Status = StudentStatus.Active,
            Cgpa = 3.0m, Sgpa = 3.0m, CompletedSemesters = 1
        });
        ctx.StudentPrograms.Add(new StudentProgram
        {
            StudentId = studId1, ProgramId = pId, CurriculumId = currId,
            Type = StudentProgramType.Main, IsActive = true,
            EnrollmentDate = new DateTime(2024, 9, 15)
        });
        ctx.CourseRegistrations.Add(new CourseRegistration
        {
            Id = reg1Id, StudentId = studId1, CourseSectionId = sectionId,
            Status = RegistrationStatus.Active
        });
        ctx.StudentGrades.Add(new StudentGrade
        {
            Id = 20, CourseRegistrationId = reg1Id, GradeComponentId = comp1Id, Score = 92m
        });
        ctx.StudentGrades.Add(new StudentGrade
        {
            Id = 21, CourseRegistrationId = reg1Id, GradeComponentId = comp2Id, Score = 92m
        });

        // Student 2: both component scores = 44 → weightedScore=44 → FF (< DDMin=45)
        ctx.Users.Add(new ApplicationUser
        {
            Id = studUserId2, UserName = "s2@t.edu", NormalizedUserName = "S2@T.EDU",
            Email = "s2@t.edu", NormalizedEmail = "S2@T.EDU", FirstName = "S", LastName = "2"
        });
        ctx.Students.Add(new Student
        {
            Id = studId2, UserId = studUserId2, StudentNumber = "20240202",
            NationalId = "44444444444", Status = StudentStatus.Active,
            Cgpa = 2.0m, Sgpa = 2.0m, CompletedSemesters = 1
        });
        ctx.StudentPrograms.Add(new StudentProgram
        {
            StudentId = studId2, ProgramId = pId, CurriculumId = currId,
            Type = StudentProgramType.Main, IsActive = true,
            EnrollmentDate = new DateTime(2024, 9, 15)
        });
        ctx.CourseRegistrations.Add(new CourseRegistration
        {
            Id = reg2Id, StudentId = studId2, CourseSectionId = sectionId,
            Status = RegistrationStatus.Active
        });
        ctx.StudentGrades.Add(new StudentGrade
        {
            Id = 22, CourseRegistrationId = reg2Id, GradeComponentId = comp1Id, Score = 44m
        });
        ctx.StudentGrades.Add(new StudentGrade
        {
            Id = 23, CourseRegistrationId = reg2Id, GradeComponentId = comp2Id, Score = 44m
        });

        await ctx.SaveChangesAsync();

        var gpaMock = new Mock<IGpaCalculationService>();
        gpaMock.Setup(g => g.RecalculateAndUpdateAsync(It.IsAny<int>()))
               .ReturnsAsync(new GpaResultDto());

        var resolver = new ExamRegulationResolver(ctx);
        var controller = new GradeController(
            ctx, gpaMock.Object, new SystemDateTimeProvider(),
            webModuleControls: new NullWebModuleControlService(),
            examRegulationResolver: resolver);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, instrUserId.ToString())
                ], "TestAuth"))
            }
        };

        // Act
        var result = await controller.Finalize(sectionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>(
            "locked regulation with HasUniqueThresholds=true and no section criteria → must use regulation thresholds");

        var reg1 = await ctx.CourseRegistrations.FindAsync(reg1Id);
        reg1!.Grade.Should().Be(GradeCode.AA,
            "student 1 scored 92 which is ≥ AAMin=90 per regulation thresholds");

        var reg2 = await ctx.CourseRegistrations.FindAsync(reg2Id);
        reg2!.Grade.Should().Be(GradeCode.FF,
            "student 2 scored 44 which is < DDMin=45 per regulation thresholds");
    }

    /// <summary>
    /// Finalize must use section GradingCriteria (existing behavior) when no regulation exists.
    /// midterm=70, final=70 → weightedScore=70 → CB via default GradingCriteria (BBMin=71, CBMin=61).
    /// </summary>
    [Fact]
    public async Task Finalize_UsesSectionCriteria_WhenNoRegulation()
    {
        // Arrange: no regulation seeded — existing path
        SeedBaseScenario(midtermScore: 70m, finalScore: 70m);
        // No SeedExamRegulationWithLetterPolicy call

        var controller = CreateController();

        // Act
        var result = await controller.Finalize(SectionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>("no regulation → normal finalization with section criteria");

        var registration = await _context.CourseRegistrations.FindAsync(RegistrationId);
        // weightedScore = 70*0.4 + 70*0.6 = 70 → CB (default: BBMin=71 > 70 ≥ CBMin=61)
        registration!.Grade.Should().Be(GradeCode.CB,
            "no regulation → section GradingCriteria used; score=70 → CB");
    }

    /// <summary>
    /// Deadlock edge: regulation is LOCKED (LetterRangesEditable=false) but programs have
    /// CONFLICTING thresholds (HasUniqueThresholds=false) and NO section GradingCriteria row.
    /// UpdateGradingCriteria is blocked by the lock, and without this fix Finalize falls
    /// through to the generic "girilmeden" message — an unresolvable deadlock.
    /// After the fix, Finalize must return 400 with a message containing "çelişen".
    /// All components are Type=Other so the weight-bounds check is bypassed.
    /// </summary>
    [Fact]
    public async Task Finalize_ReturnsConflictError_WhenLockedRegulationsHaveConflictingThresholds()
    {
        // Arrange: isolated DB — course belongs to TWO programs, each with a locked regulation
        // but DIFFERENT thresholds, so HasUniqueThresholds=false.
        // No GradingCriteria row.  Components are Type=Other (weight-bounds skipped).
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var ctx = new AppDbContext(options);

        const int fId = 50, dId1 = 50, dId2 = 51;
        const int pId1 = 50, pId2 = 51;
        const int currId1 = 50, currId2 = 51;
        const int courseId = 50, termId = 50, sectionId = 50;
        const int instrId = 50, instrUserId = 5050;
        const int studId = 50, studUserId = 6060, regId = 50;
        const int comp1Id = 50, comp2Id = 51;

        // Faculty
        ctx.Faculties.Add(new Faculty { Id = fId, Name = "Conflict Faculty", Code = "CF" });

        // Two departments (both in same faculty)
        ctx.Departments.Add(new Department { Id = dId1, Name = "Dept A", Code = "DA", FacultyId = fId });
        ctx.Departments.Add(new Department { Id = dId2, Name = "Dept B", Code = "DB", FacultyId = fId });

        // Two programs
        ctx.AcademicPrograms.Add(new AcademicProgram
        {
            Id = pId1, Name = "Program A", Code = "PA", DepartmentId = dId1,
            DegreeLevel = DegreeLevel.Bachelor, EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 8, TotalRequiredEcts = 240
        });
        ctx.AcademicPrograms.Add(new AcademicProgram
        {
            Id = pId2, Name = "Program B", Code = "PB", DepartmentId = dId2,
            DegreeLevel = DegreeLevel.Bachelor, EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 8, TotalRequiredEcts = 240
        });

        // Course
        ctx.Courses.Add(new Course { Id = courseId, Code = "CON301", Name = "Conflict Course", Ects = 3 });

        // Two curricula, each linking the same course → resolver sees two programs
        ctx.Curricula.Add(new Curriculum { Id = currId1, ProgramId = pId1, Name = "Curr A", Version = "2024" });
        ctx.Curricula.Add(new Curriculum { Id = currId2, ProgramId = pId2, Name = "Curr B", Version = "2024" });
        ctx.CurriculumCourses.Add(new CurriculumCourse
            { CurriculumId = currId1, CourseId = courseId, IsCompulsory = true, SuggestedSemester = 3 });
        ctx.CurriculumCourses.Add(new CurriculumCourse
            { CurriculumId = currId2, CourseId = courseId, IsCompulsory = true, SuggestedSemester = 3 });

        // Term
        ctx.AcademicTerms.Add(new AcademicTerm
        {
            Id = termId, Name = "2024-2025 Güz", ShortEnglishName = "Fall 2024",
            TermType = TermType.Fall, Year = 2024, IsActivated = true,
            TermStart = new DateTime(2024, 10, 1), TermEnd = new DateTime(2025, 1, 31)
        });

        // Instructor + section
        ctx.Users.Add(new ApplicationUser
        {
            Id = instrUserId, UserName = "instr50@t.edu", NormalizedUserName = "INSTR50@T.EDU",
            Email = "instr50@t.edu", NormalizedEmail = "INSTR50@T.EDU", FirstName = "I", LastName = "50"
        });
        ctx.Instructors.Add(new Instructor
        {
            Id = instrId, UserId = instrUserId, DepartmentId = dId1, Title = "Dr.", MaxWeeklyHours = 12
        });
        ctx.CourseSections.Add(new CourseSection
        {
            Id = sectionId, CourseId = courseId, TermId = termId,
            InstructorId = instrId, Capacity = 30,
            Status = CourseSectionStatus.Active, IsFinalized = false
        });

        // Components: both Type=Other → weight-bounds check is skipped entirely
        ctx.GradeComponents.Add(new GradeComponent
        {
            Id = comp1Id, CourseId = courseId, AcademicTermId = termId,
            Name = "Ödev", Weight = 50m, Order = 1, Type = GradeComponentType.Other
        });
        ctx.GradeComponents.Add(new GradeComponent
        {
            Id = comp2Id, CourseId = courseId, AcademicTermId = termId,
            Name = "Proje", Weight = 50m, Order = 2, Type = GradeComponentType.Other
        });

        // NO GradingCriteria row → the section has no manually entered criteria

        // Locked regulation for Program A — AAMin=90 (set 1)
        ctx.ExamRegulations.Add(new ExamRegulation
        {
            Id = 50, ProgramId = pId1, DegreeLevel = DegreeLevel.Bachelor,
            TermType = TermType.Fall, Year = 2024, IsActive = true,
            Name = "Locked Reg A", MidtermCount = 1, FinalCount = 1,
            MinMidtermWeightPercent = 0m, MaxMidtermWeightPercent = 100m,
            MinFinalWeightPercent = 0m, MaxFinalWeightPercent = 100m,
            FinalExamFloor = 0m, PassingGrade = 0m,
            LetterRangesEditable = false,
            AAMin = 90, BAMin = 80, BBMin = 70, CBMin = 60, CCMin = 55, DCMin = 50, DDMin = 45,
            CreatedAt = DateTime.UtcNow
        });

        // Locked regulation for Program B — AAMin=95 (DIFFERENT → HasUniqueThresholds=false)
        ctx.ExamRegulations.Add(new ExamRegulation
        {
            Id = 51, ProgramId = pId2, DegreeLevel = DegreeLevel.Bachelor,
            TermType = TermType.Fall, Year = 2024, IsActive = true,
            Name = "Locked Reg B", MidtermCount = 1, FinalCount = 1,
            MinMidtermWeightPercent = 0m, MaxMidtermWeightPercent = 100m,
            MinFinalWeightPercent = 0m, MaxFinalWeightPercent = 100m,
            FinalExamFloor = 0m, PassingGrade = 0m,
            LetterRangesEditable = false,
            AAMin = 95, BAMin = 85, BBMin = 75, CBMin = 65, CCMin = 58, DCMin = 52, DDMin = 47,
            CreatedAt = DateTime.UtcNow
        });

        // Student enrolled in Program A (main program)
        ctx.Users.Add(new ApplicationUser
        {
            Id = studUserId, UserName = "s50@t.edu", NormalizedUserName = "S50@T.EDU",
            Email = "s50@t.edu", NormalizedEmail = "S50@T.EDU", FirstName = "S", LastName = "50"
        });
        ctx.Students.Add(new Student
        {
            Id = studId, UserId = studUserId, StudentNumber = "20240501",
            NationalId = "55555555555", Status = StudentStatus.Active,
            Cgpa = 3.0m, Sgpa = 3.0m, CompletedSemesters = 2
        });
        ctx.StudentPrograms.Add(new StudentProgram
        {
            StudentId = studId, ProgramId = pId1, CurriculumId = currId1,
            Type = StudentProgramType.Main, IsActive = true,
            EnrollmentDate = new DateTime(2024, 9, 15)
        });
        ctx.CourseRegistrations.Add(new CourseRegistration
        {
            Id = regId, StudentId = studId, CourseSectionId = sectionId,
            Status = RegistrationStatus.Active
        });

        // Valid scores for both components
        ctx.StudentGrades.Add(new StudentGrade
        {
            Id = 50, CourseRegistrationId = regId, GradeComponentId = comp1Id, Score = 75m
        });
        ctx.StudentGrades.Add(new StudentGrade
        {
            Id = 51, CourseRegistrationId = regId, GradeComponentId = comp2Id, Score = 75m
        });

        await ctx.SaveChangesAsync();

        var gpaMock = new Mock<IGpaCalculationService>();
        gpaMock.Setup(g => g.RecalculateAndUpdateAsync(It.IsAny<int>()))
               .ReturnsAsync(new GpaResultDto());

        var resolver = new ExamRegulationResolver(ctx);
        var controller = new GradeController(
            ctx, gpaMock.Object, new SystemDateTimeProvider(),
            webModuleControls: new NullWebModuleControlService(),
            examRegulationResolver: resolver);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, instrUserId.ToString())
                ], "TestAuth"))
            }
        };

        // Act
        var result = await controller.Finalize(sectionId);

        // Assert: must be a 400 whose error message mentions "çelişen"
        result.Should().BeOfType<BadRequestObjectResult>(
            "locked regulations with conflicting thresholds and no section criteria → deadlock → must return 400");

        var badRequest = (BadRequestObjectResult)result;

        // The error value is an anonymous object; serialize with Unicode un-escaped so Turkish
        // characters are preserved as literals (not \uXXXX escapes).
        var jsonOptions = new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var json = System.Text.Json.JsonSerializer.Serialize(badRequest.Value, jsonOptions);
        json.Should().Contain("çelişen",
            "the error must specifically identify the conflicting-thresholds deadlock, not the generic 'girilmeden' message");

        // Explicitly verify it is NOT the generic no-criteria message
        json.Should().NotContain("girilmeden",
            "the generic 'girilmeden' message is misleading when the lock itself prevents entry");
    }

    /// <summary>
    /// GetGradingCriteria must return regulation thresholds when the regulation has
    /// HasUniqueThresholds=true and LetterRangesEditable=true, but NO section GradingCriteria
    /// row exists yet. This mirrors the Finalize path: (!policy.Editable || criteria == null).
    /// The response must surface the regulation thresholds (not 91/81/71/61/55/50/45 defaults)
    /// and must set Editable=true so the instructor knows they can override.
    /// </summary>
    [Fact]
    public async Task GetGradingCriteria_ReturnsRegulationThresholds_WhenEditableButNoSectionCriteria()
    {
        // Arrange: regulation with HasUniqueThresholds=true, LetterRangesEditable=true.
        // No GradingCriteria row → criteria == null.
        // Under the fixed condition (!policy.Editable || criteria == null):
        //   Editable=true, criteria=null → criteria==null branch is TRUE → use regulation thresholds.
        // Under the OLD condition (!policy.Editable && HasUniqueThresholds):
        //   Editable=true → !policy.Editable=false → condition is FALSE → falls back to defaults.
        SeedBaseScenario(); // seeds GradingCriteria row with Id=GradingCriteriaId

        // Remove the seeded GradingCriteria row so criteria == null
        var seedCriteria = await _context.GradingCriteria.FindAsync(GradingCriteriaId);
        if (seedCriteria != null)
        {
            _context.GradingCriteria.Remove(seedCriteria);
            await _context.SaveChangesAsync();
        }

        // Seed a regulation that is editable but has unique thresholds
        SeedExamRegulationWithLetterPolicy(
            letterRangesEditable: true,
            aaMin: 93, baMin: 83, bbMin: 73, cbMin: 63, ccMin: 57, dcMin: 52, ddMin: 47);

        var controller = CreateController();

        // Act
        var result = await controller.GetGradingCriteria(SectionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>(
            "GetGradingCriteria must succeed when regulation exists");

        var ok = (OkObjectResult)result;
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        var doc = System.Text.Json.JsonDocument.Parse(json).RootElement;

        doc.GetProperty("AAMin").GetInt32().Should().Be(93,
            "with Editable=true and no section criteria, regulation AAMin=93 must be surfaced (not default 91)");
        doc.GetProperty("BAMin").GetInt32().Should().Be(83);
        doc.GetProperty("BBMin").GetInt32().Should().Be(73);
        doc.GetProperty("CBMin").GetInt32().Should().Be(63);
        doc.GetProperty("CCMin").GetInt32().Should().Be(57);
        doc.GetProperty("DCMin").GetInt32().Should().Be(52);
        doc.GetProperty("DDMin").GetInt32().Should().Be(47);
        doc.GetProperty("Editable").GetBoolean().Should().BeTrue(
            "regulation is editable → instructor must be able to override");
    }

    public void Dispose() => _context.Dispose();
}
