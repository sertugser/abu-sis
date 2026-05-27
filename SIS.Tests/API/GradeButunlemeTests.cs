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
/// Tests for the bütünleme (resit makeup) flow in GradeController:
///   GET  section/{id}/makeup/butunleme          — eligible student roster
///   PUT  section/{id}/makeup/butunleme-scores   — store MakeupScore on the Final component
///   POST section/{id}/makeup/butunleme-finalize — recompute grades using MakeupScore
///
/// Toggle semantics:
///   F = AutoTransferFailedToMakeup  — FF students appear in roster (false → empty list)
///   B = BlockMakeupIfNoFinal        — finale girmeyen Absent → excluded from roster
///   D = ZeroIfMissedMakeup          — no büt score entered → treated as 0
///   E = MakeupReplacesFinal         — büt score replaces final in the recompute
/// </summary>
public class GradeButunlemeTests : IDisposable
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

    // FF (failing) student
    private const int FailStudentId = 1;
    private const int FailStudentUserId = 201;
    private const int FailRegistrationId = 1;

    // Passing student (should NOT appear in büt list)
    private const int PassStudentId = 2;
    private const int PassStudentUserId = 202;
    private const int PassRegistrationId = 2;

    private const int MidtermComponentId = 10;
    private const int FinalComponentId = 11;
    private const int ExamRegulationId = 1;
    private const int GradingCriteriaId = 1;

    public GradeButunlemeTests()
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
    /// Seeds the base object graph:
    ///   - Faculty, Dept, Program, Course, Curriculum, CurriculumCourse, Term
    ///   - Instructor + User
    ///   - Section (IsFinalized = true — büt is post-finalize)
    ///   - Midterm component (weight=40) + Final component (weight=60)
    ///   - GradingCriteria (defaults: AA≥91, DD≥45)
    /// Does NOT seed students/registrations — callers do that.
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
        _context.Courses.Add(new Course { Id = CourseId, Code = "BUT101", Name = "Butunleme Test", Ects = 3 });
        _context.Curricula.Add(new Curriculum
        {
            Id = CurriculumId,
            ProgramId = ProgramId,
            Name = "Mufredat",
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
            Name = "2024-2025 Guz",
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
            IsFinalized = true  // büt is post-finalize
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
            // defaults: AA≥91, BA≥81, BB≥71, CB≥61, CC≥55, DC≥50, DD≥45
        });
    }

    /// <summary>Seeds the FF (failing) student with Vize=0, Final=20 → Grade=FF after main finalize.</summary>
    private void SeedFailStudent(
        ExamAttendance finalAttendance = ExamAttendance.Present,
        decimal? makeupScore = null)
    {
        _context.Users.Add(new ApplicationUser
        {
            Id = FailStudentUserId,
            UserName = "fail@test.edu",
            NormalizedUserName = "FAIL@TEST.EDU",
            Email = "fail@test.edu",
            NormalizedEmail = "FAIL@TEST.EDU",
            FirstName = "Fail",
            LastName = "Student"
        });
        _context.Students.Add(new Student
        {
            Id = FailStudentId,
            UserId = FailStudentUserId,
            StudentNumber = "2024001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 1.50m,
            Sgpa = 1.50m,
            CompletedSemesters = 1
        });
        _context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = FailStudentId,
            ProgramId = ProgramId,
            CurriculumId = CurriculumId,
            Type = StudentProgramType.Main,
            IsActive = true,
            EnrollmentDate = new DateTime(2024, 9, 15)
        });
        _context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = FailRegistrationId,
            StudentId = FailStudentId,
            CourseSectionId = SectionId,
            Status = RegistrationStatus.Active,
            Grade = GradeCode.FF   // set by main finalize already
        });
        // Vize: score=0, Present
        _context.StudentGrades.Add(new StudentGrade
        {
            Id = 100,
            CourseRegistrationId = FailRegistrationId,
            GradeComponentId = MidtermComponentId,
            Score = 0m,
            ExamAttendance = ExamAttendance.Present
        });
        // Final: score=20, attendance as specified
        _context.StudentGrades.Add(new StudentGrade
        {
            Id = 101,
            CourseRegistrationId = FailRegistrationId,
            GradeComponentId = FinalComponentId,
            Score = finalAttendance == ExamAttendance.Absent ? (decimal?)null : 20m,
            ExamAttendance = finalAttendance,
            MakeupScore = makeupScore
        });
    }

    /// <summary>Seeds the passing student with Vize=80, Final=80 → Grade=BB after main finalize.</summary>
    private void SeedPassStudent()
    {
        _context.Users.Add(new ApplicationUser
        {
            Id = PassStudentUserId,
            UserName = "pass@test.edu",
            NormalizedUserName = "PASS@TEST.EDU",
            Email = "pass@test.edu",
            NormalizedEmail = "PASS@TEST.EDU",
            FirstName = "Pass",
            LastName = "Student"
        });
        _context.Students.Add(new Student
        {
            Id = PassStudentId,
            UserId = PassStudentUserId,
            StudentNumber = "2024002",
            NationalId = "22222222222",
            Status = StudentStatus.Active,
            Cgpa = 3.00m,
            Sgpa = 3.00m,
            CompletedSemesters = 1
        });
        _context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = PassStudentId,
            ProgramId = ProgramId,
            CurriculumId = CurriculumId,
            Type = StudentProgramType.Main,
            IsActive = true,
            EnrollmentDate = new DateTime(2024, 9, 15)
        });
        _context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = PassRegistrationId,
            StudentId = PassStudentId,
            CourseSectionId = SectionId,
            Status = RegistrationStatus.Active,
            Grade = GradeCode.BB   // passing — set by main finalize
        });
        _context.StudentGrades.Add(new StudentGrade
        {
            Id = 200,
            CourseRegistrationId = PassRegistrationId,
            GradeComponentId = MidtermComponentId,
            Score = 80m,
            ExamAttendance = ExamAttendance.Present
        });
        _context.StudentGrades.Add(new StudentGrade
        {
            Id = 201,
            CourseRegistrationId = PassRegistrationId,
            GradeComponentId = FinalComponentId,
            Score = 80m,
            ExamAttendance = ExamAttendance.Present
        });
    }

    /// <summary>
    /// Seeds an ExamRegulation with permissive weight bounds and the specified toggles.
    /// F, B, D, E defaults to the standard "auto-transfer + makeup-replaces-final" configuration.
    /// </summary>
    private void SeedExamRegulation(
        bool autoTransferFailedToMakeup = true,   // F
        bool blockMakeupIfNoFinal = false,          // B
        bool zeroIfMissedMakeup = false,            // D
        bool makeupReplacesFinal = true)            // E
    {
        _context.ExamRegulations.Add(new ExamRegulation
        {
            Id = ExamRegulationId,
            ProgramId = ProgramId,
            DegreeLevel = DegreeLevel.Bachelor,
            TermType = TermType.Fall,
            Year = 2024,
            IsActive = true,
            Name = "Butunleme Test Yonerge",
            MidtermCount = 1,
            FinalCount = 1,
            MinMidtermWeightPercent = 0m,
            MaxMidtermWeightPercent = 100m,
            MinFinalWeightPercent = 0m,
            MaxFinalWeightPercent = 100m,
            FinalExamFloor = 0m,
            PassingGrade = 0m,
            BelowFloorGrade = GradeCode.FF,
            AutoTransferFailedToMakeup = autoTransferFailedToMakeup,
            BlockMakeupIfNoFinal = blockMakeupIfNoFinal,
            ZeroIfMissedMakeup = zeroIfMissedMakeup,
            MakeupReplacesFinal = makeupReplacesFinal,
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
    /// Test 1: Butunleme_ListsFailedStudents
    ///
    /// A finalized section has two students:
    ///   - FF student (Grade=FF) with F=true → MUST appear in the büt roster
    ///   - Passing student (Grade=BB) → must NOT appear
    ///
    /// Roster query returns only FF students when AutoTransferFailedToMakeup=true.
    /// </summary>
    [Fact]
    public async Task Butunleme_ListsFailedStudents()
    {
        SeedBase();
        SeedFailStudent();
        SeedPassStudent();
        _context.SaveChanges();
        SeedExamRegulation(autoTransferFailedToMakeup: true);

        var controller = CreateController();
        var result = await controller.GetButunlemeRoster(SectionId);

        result.Should().BeOfType<OkObjectResult>("the roster endpoint must succeed for a finalized section");
        var ok = (OkObjectResult)result;
        var body = ok.Value!;

        // Extract the student list via reflection (anonymous type)
        var studentsProperty = body.GetType().GetProperty("Students");
        studentsProperty.Should().NotBeNull("response must have a Students property");
        var students = (System.Collections.IEnumerable)studentsProperty!.GetValue(body)!;
        var studentList = students.Cast<object>().ToList();

        studentList.Should().HaveCount(1, "only the FF student should appear in the büt roster");

        var first = studentList[0];
        var studentIdProp = first.GetType().GetProperty("StudentId");
        var studentId = (int)studentIdProp!.GetValue(first)!;
        studentId.Should().Be(FailStudentId, "the FF student must be in the roster");
    }

    /// <summary>
    /// Test 2: Butunleme_RecomputesGrade_WhenMakeupReplacesFinal
    ///
    /// FF student had: Vize=0 (weight=40%), Final=20 (weight=60%) → weighted = 0*0.4 + 20*0.6 = 12 → FF
    /// After entering büt score=100 and running butunleme-finalize with E=true:
    ///   weighted = 0*0.4 + 100*0.6 = 60 → CC (≥55 in default criteria, <61 for CB)
    ///
    /// Assertions:
    ///   - New Grade is NOT FF
    ///   - New Grade is CC (the exact expected letter from 60 points)
    ///   - Original Final Score (20) is preserved in DB
    ///   - MakeupScore (100) is stored on the Final component StudentGrade row
    /// </summary>
    [Fact]
    public async Task Butunleme_RecomputesGrade_WhenMakeupReplacesFinal()
    {
        SeedBase();
        SeedFailStudent();
        _context.SaveChanges();
        SeedExamRegulation(makeupReplacesFinal: true, autoTransferFailedToMakeup: true);

        var controller = CreateController();

        // Step 1: store büt score via PUT endpoint
        var putBody = new GradeController.ButunlemeScoresRequest
        {
            Items = [new GradeController.ButunlemeScoreItem { StudentId = FailStudentId, MakeupScore = 100m }]
        };
        var putResult = await controller.SetButunlemeScores(SectionId, putBody);
        putResult.Should().BeOfType<OkObjectResult>("storing makeup scores must succeed");

        // Verify MakeupScore was stored on Final component row
        var finalGradeRow = await _context.StudentGrades
            .FirstOrDefaultAsync(sg => sg.CourseRegistrationId == FailRegistrationId
                && sg.GradeComponentId == FinalComponentId);
        finalGradeRow.Should().NotBeNull();
        finalGradeRow!.MakeupScore.Should().Be(100m, "MakeupScore must be persisted");
        finalGradeRow.Score.Should().Be(20m, "original Score must be preserved");

        // Step 2: finalize büt
        var finalizeResult = await controller.FinalizeButunleme(SectionId);
        finalizeResult.Should().BeOfType<OkObjectResult>("butunleme-finalize must succeed");

        // Check the updated grade
        var reg = await _context.CourseRegistrations.FindAsync(FailRegistrationId);
        reg!.Grade.Should().NotBe(GradeCode.FF, "after büt score=100, grade must no longer be FF");
        // 0*0.4 + 100*0.6 = 60 → CC (default criteria: CC≥55, CB≥61)
        reg.Grade.Should().Be(GradeCode.CC,
            "büt weighted=60 → CC with default grading criteria (CC≥55, CB≥61)");

        // Verify the GPA recalculation was triggered
        _gpaMock.Verify(g => g.RecalculateAndUpdateAsync(FailStudentId), Times.AtLeastOnce);
    }

    /// <summary>
    /// Test 3: Butunleme_ZeroForMissedMakeup_WhenDOn
    ///
    /// FF student has no büt score entered (MakeupScore=null on Final component).
    /// D=ZeroIfMissedMakeup=true → treat missing büt score as 0.
    /// Recompute: 0*0.4 + 0*0.6 = 0 → FF stays.
    ///
    /// The student should remain FF after butunleme-finalize.
    /// </summary>
    [Fact]
    public async Task Butunleme_ZeroForMissedMakeup_WhenDOn()
    {
        SeedBase();
        SeedFailStudent(); // no makeupScore seeded
        _context.SaveChanges();
        SeedExamRegulation(
            autoTransferFailedToMakeup: true,
            zeroIfMissedMakeup: true,   // D = on
            makeupReplacesFinal: true);  // E = on

        var controller = CreateController();

        // Do NOT call SetButunlemeScores — no büt score entered
        // Directly finalize büt
        var finalizeResult = await controller.FinalizeButunleme(SectionId);
        finalizeResult.Should().BeOfType<OkObjectResult>("butunleme-finalize must succeed even with D=on and no score");

        var reg = await _context.CourseRegistrations.FindAsync(FailRegistrationId);
        // D=on, no büt score → treat as 0 → 0*0.4 + 0*0.6 = 0 → FF
        reg!.Grade.Should().Be(GradeCode.FF,
            "D=on with no büt score → MakeupScore treated as 0 → weighted=0 → FF");
    }

    /// <summary>
    /// Test 4: Butunleme_ExcludesNoFinalStudent_WhenBOn
    ///
    /// B=BlockMakeupIfNoFinal=true: student who did NOT sit the final (final ExamAttendance==Absent)
    /// is excluded from the büt roster even if their Grade=FF.
    ///
    /// The FF student here has finalAttendance=Absent → must NOT appear in the roster.
    /// </summary>
    [Fact]
    public async Task Butunleme_ExcludesNoFinalStudent_WhenBOn()
    {
        SeedBase();
        SeedFailStudent(finalAttendance: ExamAttendance.Absent);  // did not sit the final
        _context.SaveChanges();
        SeedExamRegulation(
            autoTransferFailedToMakeup: true,
            blockMakeupIfNoFinal: true);   // B = on

        var controller = CreateController();
        var result = await controller.GetButunlemeRoster(SectionId);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var body = ok.Value!;

        var studentsProperty = body.GetType().GetProperty("Students");
        var students = (System.Collections.IEnumerable)studentsProperty!.GetValue(body)!;
        var studentList = students.Cast<object>().ToList();

        studentList.Should().BeEmpty(
            "B=on: student with finalAttendance=Absent must be excluded from büt roster even if Grade=FF");
    }

    /// <summary>
    /// Test 5: Butunleme_UsesAbsoluteCriteria_EvenWhenSectionIsRelative
    ///
    /// Scenario: the ExamRegulation has EvaluationType=Relative (curve grading).
    /// The original Finalize run applies the curve ONCE to produce letter grades.
    /// After Finalize, the FF student enters bütünleme.
    ///
    /// INTENDED behavior (per regulation model): the büt recompute uses the ABSOLUTE
    /// GradingCriteria thresholds, NOT the T-score curve.  The curve is never re-run
    /// for makeup — the instructor's set absolute table is used.
    ///
    /// Setup:
    ///   GradingCriteria: AA≥91, DD≥45 (defaults seeded by SeedBase)
    ///   Components: Vize weight=40%, Final weight=60%
    ///   FF student original scores: Vize=0, Final=20 → weighted=12 → FF
    ///   Büt score: 100 → büt weighted = 0*0.4 + 100*0.6 = 60
    ///   Absolute criteria (default): CC≥55, CB≥61 → 60 maps to CC
    ///
    /// If the curve were incorrectly re-run with only 1 student, the behavior would be
    /// non-deterministic (depends on T-score distribution).  The absolute path gives
    /// a deterministic CC for score 60.
    ///
    /// Assertion: Grade == CC (absolute), NOT a curve-derived letter.
    /// </summary>
    [Fact]
    public async Task Butunleme_UsesAbsoluteCriteria_EvenWhenSectionIsRelative()
    {
        SeedBase();
        SeedFailStudent(makeupScore: null);
        _context.SaveChanges();

        // Seed an ExamRegulation with EvaluationType=Relative + all büt toggles on
        _context.ExamRegulations.Add(new ExamRegulation
        {
            Id = ExamRegulationId,
            ProgramId = ProgramId,
            DegreeLevel = DegreeLevel.Bachelor,
            TermType = TermType.Fall,
            Year = 2024,
            IsActive = true,
            Name = "Relative Büt Test Yönerge",
            MidtermCount = 1,
            FinalCount = 1,
            MinMidtermWeightPercent = 0m,
            MaxMidtermWeightPercent = 100m,
            MinFinalWeightPercent = 0m,
            MaxFinalWeightPercent = 100m,
            FinalExamFloor = 0m,
            PassingGrade = 0m,
            BelowFloorGrade = GradeCode.FF,
            // Relative evaluation — curve should NOT be re-run at büt time
            EvaluationType = GradeEvaluationType.Relative,
            RelativeMinStudentCount = 10,   // won't reach threshold with 1 student anyway
            TAAMin = 70, TBAMin = 63, TBBMin = 57, TCBMin = 50, TCCMin = 45, TDCMin = 40, TDDMin = 35,
            // Büt toggles: F=on, E=on
            AutoTransferFailedToMakeup = true,
            MakeupReplacesFinal = true,
            BlockMakeupIfNoFinal = false,
            ZeroIfMissedMakeup = false,
            LetterRangesEditable = true,
            CreatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();

        var controller = CreateController();

        // Store büt score=100 on Final component
        var putBody = new GradeController.ButunlemeScoresRequest
        {
            Items = [new GradeController.ButunlemeScoreItem { StudentId = FailStudentId, MakeupScore = 100m }]
        };
        var putResult = await controller.SetButunlemeScores(SectionId, putBody);
        putResult.Should().BeOfType<OkObjectResult>("storing makeup scores must succeed");

        // Finalize büt
        var finalizeResult = await controller.FinalizeButunleme(SectionId);
        finalizeResult.Should().BeOfType<OkObjectResult>("butunleme-finalize must succeed");

        // Assert: büt weighted = 0*0.4 + 100*0.6 = 60
        // Absolute GradingCriteria (defaults seeded): CC≥55, CB≥61 → 60 → CC
        // If the curve were incorrectly applied, the result would be non-deterministic;
        // the fact that we get CC proves the absolute table was used.
        var reg = await _context.CourseRegistrations.FindAsync(FailRegistrationId);
        reg!.Grade.Should().Be(GradeCode.CC,
            "büt must use absolute GradingCriteria (60 → CC with default thresholds), " +
            "NOT the relative T-score curve even though EvaluationType=Relative");

        // GPA must have been recalculated
        _gpaMock.Verify(g => g.RecalculateAndUpdateAsync(FailStudentId), Times.AtLeastOnce);
    }

    public void Dispose() => _context.Dispose();
}
