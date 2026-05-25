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
/// Tests for the two opt-in exam-eligibility rules enforced in GradeController.Finalize:
///   Rule A — BlockFinalIfNoMidterm: student who attended no midterm has all Final scores
///             treated as 0 in the weighted-score computation.
///   Rule C — FailIfNoFinal: student whose Final component attendance is Absent receives
///             BelowFloorGrade after the letter-grade assignment step.
/// </summary>
public class GradeFinalizeExamRulesTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<IGpaCalculationService> _gpaMock;

    // Shared fixed IDs
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

    public GradeFinalizeExamRulesTests()
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

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds the minimal object graph with:
    ///   - Midterm component (weight=40) and Final component (weight=60), total=100
    ///   - GradingCriteria defaults (AA≥91, DD≥45)
    ///   - One student + one registration
    ///   - StudentGrade rows with configurable scores and ExamAttendance values
    /// </summary>
    private void SeedScenario(
        decimal midtermScore,
        ExamAttendance midtermAttendance,
        decimal finalScore,
        ExamAttendance finalAttendance)
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
        _context.Courses.Add(new Course { Id = CourseId, Code = "ERX101", Name = "Exam Rules Test", Ects = 3 });
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
            MaxWeeklyHours = 12
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

        // Midterm component (Type=Midterm, weight=40)
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

        // Final component (Type=Final, weight=60)
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

        // GradingCriteria with defaults (AA≥91, BA≥81, BB≥71, CB≥61, CC≥55, DC≥50, DD≥45)
        _context.GradingCriteria.Add(new GradingCriteria
        {
            Id = GradingCriteriaId,
            CourseId = CourseId,
            AcademicTermId = TermId
        });

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

        _context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = RegistrationId,
            StudentId = StudentId,
            CourseSectionId = SectionId,
            Status = RegistrationStatus.Active
        });

        // Midterm grade row — Score is null when attendance is Absent (deliberate 0, not a gap).
        // The missing-grades check treats Absent as a deliberate 0 (no Score needed).
        _context.StudentGrades.Add(new StudentGrade
        {
            Id = 1,
            CourseRegistrationId = RegistrationId,
            GradeComponentId = MidtermComponentId,
            Score = midtermAttendance == ExamAttendance.Absent ? (decimal?)null : midtermScore,
            ExamAttendance = midtermAttendance
        });

        // Final grade row
        _context.StudentGrades.Add(new StudentGrade
        {
            Id = 2,
            CourseRegistrationId = RegistrationId,
            GradeComponentId = FinalComponentId,
            Score = finalAttendance == ExamAttendance.Absent ? (decimal?)null : finalScore,
            ExamAttendance = finalAttendance
        });

        _context.SaveChanges();
    }

    /// <summary>
    /// Adds an ExamRegulation for the test's program/term with permissive weight bounds
    /// (0–100) so the weight-bounds check never blocks finalize.
    /// </summary>
    private void SeedExamRegulation(
        bool blockFinalIfNoMidterm = false,
        bool failIfNoFinal = false,
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
            Name = "Exam Rules Test Yönerge",
            MidtermCount = 1,
            FinalCount = 1,
            // Permissive weight bounds — never block the test
            MinMidtermWeightPercent = 0m,
            MaxMidtermWeightPercent = 100m,
            MinFinalWeightPercent = 0m,
            MaxFinalWeightPercent = 100m,
            FinalExamFloor = 0m,
            PassingGrade = 0m,
            BelowFloorGrade = belowFloorGrade,
            BlockFinalIfNoMidterm = blockFinalIfNoMidterm,
            FailIfNoFinal = failIfNoFinal,
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
    /// Rule A — BlockFinalIfNoMidterm = true:
    /// Student attended NO midterm (ExamAttendance=Absent on midterm component) but scored 100
    /// on the final. With the flag on, all Final scores are treated as 0 in the weighted
    /// computation, so the weighted average = 0 → FF.
    ///
    /// Control: same setup but BlockFinalIfNoMidterm=false → final score 100 counts → average
    /// = 100*0.6 = 60 → CC (≥55). The two outcomes must differ.
    /// </summary>
    [Fact]
    public async Task Finalize_BlockFinalIfNoMidterm_ZerosFinal_WhenOn()
    {
        // ── Scenario A1 (flag ON) ──────────────────────────────────────────────
        SeedScenario(
            midtermScore: 0m,          // irrelevant — student is Absent
            midtermAttendance: ExamAttendance.Absent,
            finalScore: 100m,          // high final score — should be zeroed out
            finalAttendance: ExamAttendance.Present);
        SeedExamRegulation(blockFinalIfNoMidterm: true, failIfNoFinal: false);

        var controllerOn = CreateController();
        var resultOn = await controllerOn.Finalize(SectionId);

        resultOn.Should().BeOfType<OkObjectResult>("finalize must succeed when rule A is on");

        var regOn = await _context.CourseRegistrations.FindAsync(RegistrationId);
        // With BlockFinalIfNoMidterm=true and no midterm attendance:
        //   midterm score treated as 0 (already Absent → 0 per existing logic)
        //   final score overridden to 0 (rule A)
        //   weightedScore = 0*0.4 + 0*0.6 = 0 → roundedScore=0 → FF
        regOn!.Grade.Should().Be(GradeCode.FF,
            "BlockFinalIfNoMidterm=true + no midterm attendance → final zeroed → weightedScore=0 → FF");

        // ── Scenario A2 (flag OFF — control) ──────────────────────────────────
        // Use a fresh, isolated DB so IDs don't collide.
        var ctxOff = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        // Mirror the full seed in ctxOff manually (same IDs, same data, flag OFF)
        ctxOff.Faculties.Add(new Faculty { Id = FacultyId, Name = "TF", Code = "TF" });
        ctxOff.Departments.Add(new Department { Id = DeptId, Name = "TD", Code = "TD", FacultyId = FacultyId });
        ctxOff.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId, Name = "TP", Code = "TP", DepartmentId = DeptId,
            DegreeLevel = DegreeLevel.Bachelor, EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 8, TotalRequiredEcts = 240
        });
        ctxOff.Courses.Add(new Course { Id = CourseId, Code = "ERX101", Name = "ERX", Ects = 3 });
        ctxOff.Curricula.Add(new Curriculum { Id = CurriculumId, ProgramId = ProgramId, Name = "M", Version = "2024" });
        ctxOff.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId, CourseId = CourseId, IsCompulsory = true, SuggestedSemester = 1
        });
        ctxOff.AcademicTerms.Add(new AcademicTerm
        {
            Id = TermId, Name = "2024-2025 Güz", ShortEnglishName = "Fall 2024",
            TermType = TermType.Fall, Year = 2024, IsActivated = true,
            TermStart = new DateTime(2024, 10, 1), TermEnd = new DateTime(2025, 1, 31)
        });
        ctxOff.Users.Add(new ApplicationUser
        {
            Id = InstructorUserId, UserName = "h@t.edu", NormalizedUserName = "H@T.EDU",
            Email = "h@t.edu", NormalizedEmail = "H@T.EDU", FirstName = "H", LastName = "T"
        });
        ctxOff.Instructors.Add(new Instructor
        {
            Id = InstructorId, UserId = InstructorUserId, DepartmentId = DeptId,
            Title = "Dr.", MaxWeeklyHours = 12
        });
        ctxOff.CourseSections.Add(new CourseSection
        {
            Id = SectionId, CourseId = CourseId, TermId = TermId,
            InstructorId = InstructorId, Capacity = 30,
            Status = CourseSectionStatus.Active, IsFinalized = false
        });
        ctxOff.GradeComponents.Add(new GradeComponent
        {
            Id = MidtermComponentId, CourseId = CourseId, AcademicTermId = TermId,
            Name = "Vize", Weight = 40m, Order = 1, Type = GradeComponentType.Midterm
        });
        ctxOff.GradeComponents.Add(new GradeComponent
        {
            Id = FinalComponentId, CourseId = CourseId, AcademicTermId = TermId,
            Name = "Final", Weight = 60m, Order = 2, Type = GradeComponentType.Final
        });
        ctxOff.GradingCriteria.Add(new GradingCriteria
        {
            Id = GradingCriteriaId, CourseId = CourseId, AcademicTermId = TermId
        });
        ctxOff.Users.Add(new ApplicationUser
        {
            Id = StudentUserId, UserName = "s@t.edu", NormalizedUserName = "S@T.EDU",
            Email = "s@t.edu", NormalizedEmail = "S@T.EDU", FirstName = "S", LastName = "T"
        });
        ctxOff.Students.Add(new Student
        {
            Id = StudentId, UserId = StudentUserId, StudentNumber = "2024001",
            NationalId = "11111111111", Status = StudentStatus.Active,
            Cgpa = 2.5m, Sgpa = 2.5m, CompletedSemesters = 1
        });
        ctxOff.StudentPrograms.Add(new StudentProgram
        {
            StudentId = StudentId, ProgramId = ProgramId, CurriculumId = CurriculumId,
            Type = StudentProgramType.Main, IsActive = true,
            EnrollmentDate = new DateTime(2024, 9, 15)
        });
        ctxOff.CourseRegistrations.Add(new CourseRegistration
        {
            Id = RegistrationId, StudentId = StudentId,
            CourseSectionId = SectionId, Status = RegistrationStatus.Active
        });
        // Midterm: Absent (score null) — same as the ON scenario
        ctxOff.StudentGrades.Add(new StudentGrade
        {
            Id = 1, CourseRegistrationId = RegistrationId,
            GradeComponentId = MidtermComponentId,
            Score = null, ExamAttendance = ExamAttendance.Absent
        });
        // Final: Present, score=100
        ctxOff.StudentGrades.Add(new StudentGrade
        {
            Id = 2, CourseRegistrationId = RegistrationId,
            GradeComponentId = FinalComponentId,
            Score = 100m, ExamAttendance = ExamAttendance.Present
        });
        // Regulation with BlockFinalIfNoMidterm=false (flag OFF)
        ctxOff.ExamRegulations.Add(new ExamRegulation
        {
            Id = ExamRegulationId, ProgramId = ProgramId,
            DegreeLevel = DegreeLevel.Bachelor, TermType = TermType.Fall, Year = 2024,
            IsActive = true, Name = "Yönerge OFF",
            MidtermCount = 1, FinalCount = 1,
            MinMidtermWeightPercent = 0m, MaxMidtermWeightPercent = 100m,
            MinFinalWeightPercent = 0m, MaxFinalWeightPercent = 100m,
            FinalExamFloor = 0m, PassingGrade = 0m,
            BelowFloorGrade = GradeCode.FF,
            BlockFinalIfNoMidterm = false,  // FLAG OFF
            FailIfNoFinal = false,
            CreatedAt = DateTime.UtcNow
        });
        await ctxOff.SaveChangesAsync();

        var gpaMockOff = new Mock<IGpaCalculationService>();
        gpaMockOff.Setup(g => g.RecalculateAndUpdateAsync(It.IsAny<int>()))
                  .ReturnsAsync(new GpaResultDto());

        var resolverOff = new ExamRegulationResolver(ctxOff);
        var controllerOff = new GradeController(
            ctxOff, gpaMockOff.Object, new SystemDateTimeProvider(),
            webModuleControls: new NullWebModuleControlService(),
            examRegulationResolver: resolverOff);
        controllerOff.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, InstructorUserId.ToString())
                ], "TestAuth"))
            }
        };

        var resultOff = await controllerOff.Finalize(SectionId);
        resultOff.Should().BeOfType<OkObjectResult>("control: flag OFF → normal finalize");

        var regOff = await ctxOff.CourseRegistrations.FindAsync(RegistrationId);
        // Flag OFF: final score 100 counts; midterm Absent=0.
        // weightedScore = 0*0.4 + 100*0.6 = 60 → roundedScore=60 → CB (≥61? no, 60 < 61 → CC ≥55)
        // Default criteria: CC≥55 → 60 ≥ 55 and 60 < 61 → CC
        regOff!.Grade.Should().NotBe(GradeCode.FF,
            "BlockFinalIfNoMidterm=false → final score 100 counts → grade is not FF");

        // The two grades must differ — proving the flag changes behavior
        regOn!.Grade.Should().NotBe(regOff.Grade,
            "flag ON vs OFF must produce different letter grades for the same raw data");

        await ctxOff.DisposeAsync();
    }

    /// <summary>
    /// Rule C — FailIfNoFinal = true:
    /// Student's midterm was Present (score=90) but Final ExamAttendance=Absent.
    /// Without the rule the weighted average = 90*0.4 + 0*0.6 = 36 → roundedScore=36 → FF
    /// (already FF from score, but that's due to the low average — not the rule).
    ///
    /// To show the rule adds something beyond the existing Absent→0 path, use midterm score
    /// high enough that the average alone would produce a passing grade WITHOUT the final,
    /// but that's impossible with a 60% final weight and Absent→0.
    ///
    /// Instead we prove: with FailIfNoFinal=true, the grade assigned is specifically
    /// BelowFloorGrade (here configured as DD) even when midterms were high; and with the
    /// flag OFF the grade is determined purely by the numeric average (FF, because 90*0.4=36).
    ///
    /// Setup: midterm=90 (Present), final=Absent, BelowFloorGrade=DD.
    ///   - Flag ON  → grade must be DD (BelowFloorGrade from FailIfNoFinal rule)
    ///   - Flag OFF → grade determined by average: 90*0.4 + 0*0.6 = 36 → FF (not DD)
    /// The two outcomes differ AND flag ON assigns the configured BelowFloorGrade.
    /// </summary>
    [Fact]
    public async Task Finalize_FailIfNoFinal_AssignsFF_WhenOn()
    {
        // ── Scenario C1 (flag ON, BelowFloorGrade=DD) ─────────────────────────
        SeedScenario(
            midtermScore: 90m,
            midtermAttendance: ExamAttendance.Present,
            finalScore: 0m,             // irrelevant — Absent → null stored
            finalAttendance: ExamAttendance.Absent);
        SeedExamRegulation(
            blockFinalIfNoMidterm: false,
            failIfNoFinal: true,
            belowFloorGrade: GradeCode.DD);   // use DD so we can distinguish from numeric FF

        var controllerOn = CreateController();
        var resultOn = await controllerOn.Finalize(SectionId);

        resultOn.Should().BeOfType<OkObjectResult>("finalize must succeed when rule C is on");

        var regOn = await _context.CourseRegistrations.FindAsync(RegistrationId);
        // FailIfNoFinal=true + final Absent → BelowFloorGrade=DD must be assigned
        regOn!.Grade.Should().Be(GradeCode.DD,
            "FailIfNoFinal=true + final ExamAttendance=Absent → BelowFloorGrade (DD) must be assigned");

        // ── Scenario C2 (flag OFF — control) ──────────────────────────────────
        var ctxOff = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        // Mirror seed with FailIfNoFinal=false
        ctxOff.Faculties.Add(new Faculty { Id = FacultyId, Name = "TF", Code = "TF" });
        ctxOff.Departments.Add(new Department { Id = DeptId, Name = "TD", Code = "TD", FacultyId = FacultyId });
        ctxOff.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId, Name = "TP", Code = "TP", DepartmentId = DeptId,
            DegreeLevel = DegreeLevel.Bachelor, EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 8, TotalRequiredEcts = 240
        });
        ctxOff.Courses.Add(new Course { Id = CourseId, Code = "ERX101", Name = "ERX", Ects = 3 });
        ctxOff.Curricula.Add(new Curriculum { Id = CurriculumId, ProgramId = ProgramId, Name = "M", Version = "2024" });
        ctxOff.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId, CourseId = CourseId, IsCompulsory = true, SuggestedSemester = 1
        });
        ctxOff.AcademicTerms.Add(new AcademicTerm
        {
            Id = TermId, Name = "2024-2025 Güz", ShortEnglishName = "Fall 2024",
            TermType = TermType.Fall, Year = 2024, IsActivated = true,
            TermStart = new DateTime(2024, 10, 1), TermEnd = new DateTime(2025, 1, 31)
        });
        ctxOff.Users.Add(new ApplicationUser
        {
            Id = InstructorUserId, UserName = "h@t.edu", NormalizedUserName = "H@T.EDU",
            Email = "h@t.edu", NormalizedEmail = "H@T.EDU", FirstName = "H", LastName = "T"
        });
        ctxOff.Instructors.Add(new Instructor
        {
            Id = InstructorId, UserId = InstructorUserId, DepartmentId = DeptId,
            Title = "Dr.", MaxWeeklyHours = 12
        });
        ctxOff.CourseSections.Add(new CourseSection
        {
            Id = SectionId, CourseId = CourseId, TermId = TermId,
            InstructorId = InstructorId, Capacity = 30,
            Status = CourseSectionStatus.Active, IsFinalized = false
        });
        ctxOff.GradeComponents.Add(new GradeComponent
        {
            Id = MidtermComponentId, CourseId = CourseId, AcademicTermId = TermId,
            Name = "Vize", Weight = 40m, Order = 1, Type = GradeComponentType.Midterm
        });
        ctxOff.GradeComponents.Add(new GradeComponent
        {
            Id = FinalComponentId, CourseId = CourseId, AcademicTermId = TermId,
            Name = "Final", Weight = 60m, Order = 2, Type = GradeComponentType.Final
        });
        ctxOff.GradingCriteria.Add(new GradingCriteria
        {
            Id = GradingCriteriaId, CourseId = CourseId, AcademicTermId = TermId
        });
        ctxOff.Users.Add(new ApplicationUser
        {
            Id = StudentUserId, UserName = "s@t.edu", NormalizedUserName = "S@T.EDU",
            Email = "s@t.edu", NormalizedEmail = "S@T.EDU", FirstName = "S", LastName = "T"
        });
        ctxOff.Students.Add(new Student
        {
            Id = StudentId, UserId = StudentUserId, StudentNumber = "2024001",
            NationalId = "11111111111", Status = StudentStatus.Active,
            Cgpa = 2.5m, Sgpa = 2.5m, CompletedSemesters = 1
        });
        ctxOff.StudentPrograms.Add(new StudentProgram
        {
            StudentId = StudentId, ProgramId = ProgramId, CurriculumId = CurriculumId,
            Type = StudentProgramType.Main, IsActive = true,
            EnrollmentDate = new DateTime(2024, 9, 15)
        });
        ctxOff.CourseRegistrations.Add(new CourseRegistration
        {
            Id = RegistrationId, StudentId = StudentId,
            CourseSectionId = SectionId, Status = RegistrationStatus.Active
        });
        // Midterm Present, score=90
        ctxOff.StudentGrades.Add(new StudentGrade
        {
            Id = 1, CourseRegistrationId = RegistrationId,
            GradeComponentId = MidtermComponentId,
            Score = 90m, ExamAttendance = ExamAttendance.Present
        });
        // Final: Absent (score null — deliberate 0)
        ctxOff.StudentGrades.Add(new StudentGrade
        {
            Id = 2, CourseRegistrationId = RegistrationId,
            GradeComponentId = FinalComponentId,
            Score = null, ExamAttendance = ExamAttendance.Absent
        });
        // Regulation with FailIfNoFinal=false, BelowFloorGrade=DD (but flag OFF means it doesn't apply)
        ctxOff.ExamRegulations.Add(new ExamRegulation
        {
            Id = ExamRegulationId, ProgramId = ProgramId,
            DegreeLevel = DegreeLevel.Bachelor, TermType = TermType.Fall, Year = 2024,
            IsActive = true, Name = "Yönerge C OFF",
            MidtermCount = 1, FinalCount = 1,
            MinMidtermWeightPercent = 0m, MaxMidtermWeightPercent = 100m,
            MinFinalWeightPercent = 0m, MaxFinalWeightPercent = 100m,
            FinalExamFloor = 0m, PassingGrade = 0m,
            BelowFloorGrade = GradeCode.DD,
            BlockFinalIfNoMidterm = false,
            FailIfNoFinal = false,           // FLAG OFF
            CreatedAt = DateTime.UtcNow
        });
        await ctxOff.SaveChangesAsync();

        var gpaMockOff = new Mock<IGpaCalculationService>();
        gpaMockOff.Setup(g => g.RecalculateAndUpdateAsync(It.IsAny<int>()))
                  .ReturnsAsync(new GpaResultDto());

        var resolverOff = new ExamRegulationResolver(ctxOff);
        var controllerOff = new GradeController(
            ctxOff, gpaMockOff.Object, new SystemDateTimeProvider(),
            webModuleControls: new NullWebModuleControlService(),
            examRegulationResolver: resolverOff);
        controllerOff.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, InstructorUserId.ToString())
                ], "TestAuth"))
            }
        };

        var resultOff = await controllerOff.Finalize(SectionId);
        resultOff.Should().BeOfType<OkObjectResult>("control: flag OFF → normal finalize");

        var regOff = await ctxOff.CourseRegistrations.FindAsync(RegistrationId);
        // Flag OFF: numeric average decides. midterm=90 (Present)→90*0.4=36, final=Absent→0*0.6=0 → total=36
        // roundedScore=36 → FF (< DDMin=45). Grade is FF, not DD.
        regOff!.Grade.Should().NotBe(GradeCode.DD,
            "FailIfNoFinal=false → BelowFloorGrade (DD) must NOT be forced; grade comes from numeric average");
        regOff.Grade.Should().Be(GradeCode.FF,
            "flag OFF: weightedScore=36 → FF from GradingCriteria; rule C does not fire");

        // The ON scenario assigned DD; the OFF scenario assigned FF — they differ
        regOn!.Grade.Should().NotBe(regOff.Grade,
            "flag ON (DD) vs flag OFF (FF) must produce different grades");

        await ctxOff.DisposeAsync();
    }

    /// <summary>
    /// Both flags false (or no regulation) → existing behavior is fully preserved; neither
    /// Rule A nor Rule C fires.
    ///
    /// Student: midterm=Absent (0), final=Present (100).
    /// weightedScore = 0*0.4 + 100*0.6 = 60 → roundedScore=60 → CC (≥55, <61 in default criteria).
    /// Neither rule fires → grade must be CC (not FF, not DD).
    /// </summary>
    [Fact]
    public async Task Finalize_RulesOff_PreserveBehavior()
    {
        // midterm Absent → 0, final Present 100 → weighted=60 → CC
        SeedScenario(
            midtermScore: 0m,
            midtermAttendance: ExamAttendance.Absent,
            finalScore: 100m,
            finalAttendance: ExamAttendance.Present);

        // Regulation with BOTH flags false
        SeedExamRegulation(blockFinalIfNoMidterm: false, failIfNoFinal: false);

        var controller = CreateController();
        var result = await controller.Finalize(SectionId);

        result.Should().BeOfType<OkObjectResult>("both flags off → normal finalize");

        var reg = await _context.CourseRegistrations.FindAsync(RegistrationId);
        // weightedScore = 0*0.4 + 100*0.6 = 60 → roundedScore=60 → CC (default criteria: CC≥55, CB≥61)
        reg!.Grade.Should().Be(GradeCode.CC,
            "both rules off → normal grade from GradingCriteria: 60 → CC");
        reg.Grade.Should().NotBe(GradeCode.FF,
            "neither rule fires when flags are false");
    }

    public void Dispose() => _context.Dispose();
}
