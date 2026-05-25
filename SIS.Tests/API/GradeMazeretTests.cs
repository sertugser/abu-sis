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
/// Tests for the mazeret (excuse) makeup flow in GradeController:
///   GET  section/{id}/makeup/mazeret          — eligible student roster (Grade=I, G=on)
///   PUT  section/{id}/makeup/mazeret-scores   — store MakeupScore on the excused component
///   POST section/{id}/makeup/mazeret-finalize — recompute grades, take students out of I
///
/// Toggle semantics:
///   G = AutoTransferExcusedToMakeup — I-students appear in roster (false → excluded)
///
/// Skipping rule: if an excused component still has neither Score nor MakeupScore when
/// mazeret-finalize runs, that student is NOT ready — left as I, no error thrown.
/// </summary>
public class GradeMazeretTests : IDisposable
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

    // Excused (I) student — main one used in most tests
    private const int ExcusedStudentId = 1;
    private const int ExcusedStudentUserId = 201;
    private const int ExcusedRegistrationId = 1;

    // Normal-grade student — should NOT appear in mazeret roster
    private const int PassStudentId = 2;
    private const int PassStudentUserId = 202;
    private const int PassRegistrationId = 2;

    private const int MidtermComponentId = 10;
    private const int FinalComponentId = 11;
    private const int ExamRegulationId = 1;
    private const int GradingCriteriaId = 1;

    public GradeMazeretTests()
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
    ///   - Section (IsFinalized = true — mazeret is post-finalize)
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
        _context.Courses.Add(new Course { Id = CourseId, Code = "MAZ101", Name = "Mazeret Test", Ects = 3 });
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
            IsFinalized = true  // mazeret is post-finalize
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

    /// <summary>
    /// Seeds the excused student:
    ///   - Vize score as specified (default 80, Present)
    ///   - Final: ExamAttendance=Excused (score null), MakeupScore optionally set
    ///   - Grade = GradeCode.I (set by main Finalize because of Excused attendance)
    /// </summary>
    private void SeedExcusedStudent(
        decimal vizeScore = 80m,
        decimal? finalMakeupScore = null)
    {
        _context.Users.Add(new ApplicationUser
        {
            Id = ExcusedStudentUserId,
            UserName = "excused@test.edu",
            NormalizedUserName = "EXCUSED@TEST.EDU",
            Email = "excused@test.edu",
            NormalizedEmail = "EXCUSED@TEST.EDU",
            FirstName = "Excused",
            LastName = "Student"
        });
        _context.Students.Add(new Student
        {
            Id = ExcusedStudentId,
            UserId = ExcusedStudentUserId,
            StudentNumber = "2024001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 2.50m,
            Sgpa = 2.50m,
            CompletedSemesters = 1
        });
        _context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = ExcusedStudentId,
            ProgramId = ProgramId,
            CurriculumId = CurriculumId,
            Type = StudentProgramType.Main,
            IsActive = true,
            EnrollmentDate = new DateTime(2024, 9, 15)
        });
        _context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = ExcusedRegistrationId,
            StudentId = ExcusedStudentId,
            CourseSectionId = SectionId,
            Status = RegistrationStatus.Active,
            Grade = GradeCode.I   // set by main finalize — student had an Excused component
        });
        // Vize: attended and scored
        _context.StudentGrades.Add(new StudentGrade
        {
            Id = 100,
            CourseRegistrationId = ExcusedRegistrationId,
            GradeComponentId = MidtermComponentId,
            Score = vizeScore,
            ExamAttendance = ExamAttendance.Present
        });
        // Final: Excused — Score is null (not taken), MakeupScore optionally pre-loaded
        _context.StudentGrades.Add(new StudentGrade
        {
            Id = 101,
            CourseRegistrationId = ExcusedRegistrationId,
            GradeComponentId = FinalComponentId,
            Score = null,
            ExamAttendance = ExamAttendance.Excused,
            MakeupScore = finalMakeupScore
        });
    }

    /// <summary>Seeds a normal-grade (non-I) student — should NOT appear in the mazeret roster.</summary>
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
            Grade = GradeCode.BB   // normal passing grade — NOT I
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
    /// G (AutoTransferExcusedToMakeup) is the key toggle for mazeret.
    /// </summary>
    private void SeedExamRegulation(bool autoTransferExcusedToMakeup = true)
    {
        _context.ExamRegulations.Add(new ExamRegulation
        {
            Id = ExamRegulationId,
            ProgramId = ProgramId,
            DegreeLevel = DegreeLevel.Bachelor,
            TermType = TermType.Fall,
            Year = 2024,
            IsActive = true,
            Name = "Mazeret Test Yonerge",
            MidtermCount = 1,
            FinalCount = 1,
            MinMidtermWeightPercent = 0m,
            MaxMidtermWeightPercent = 100m,
            MinFinalWeightPercent = 0m,
            MaxFinalWeightPercent = 100m,
            FinalExamFloor = 0m,
            PassingGrade = 0m,
            BelowFloorGrade = GradeCode.FF,
            AutoTransferExcusedToMakeup = autoTransferExcusedToMakeup,
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
    /// Test 1: Mazeret_ListsIncompleteStudents
    ///
    /// A finalized section has two students:
    ///   - Excused student (Grade=I) with G=true → MUST appear in the mazeret roster
    ///   - Normal-grade student (Grade=BB) → must NOT appear
    ///
    /// Roster returns only I-students when AutoTransferExcusedToMakeup=true.
    /// </summary>
    [Fact]
    public async Task Mazeret_ListsIncompleteStudents()
    {
        SeedBase();
        SeedExcusedStudent();
        SeedPassStudent();
        _context.SaveChanges();
        SeedExamRegulation(autoTransferExcusedToMakeup: true);

        var controller = CreateController();
        var result = await controller.GetMazeretRoster(SectionId);

        result.Should().BeOfType<OkObjectResult>("the roster endpoint must succeed for a finalized section");
        var ok = (OkObjectResult)result;
        var body = ok.Value!;

        var studentsProperty = body.GetType().GetProperty("Students");
        studentsProperty.Should().NotBeNull("response must have a Students property");
        var students = (System.Collections.IEnumerable)studentsProperty!.GetValue(body)!;
        var studentList = students.Cast<object>().ToList();

        studentList.Should().HaveCount(1, "only the I-student should appear in the mazeret roster");

        var first = studentList[0];
        var studentIdProp = first.GetType().GetProperty("StudentId");
        var studentId = (int)studentIdProp!.GetValue(first)!;
        studentId.Should().Be(ExcusedStudentId, "the Excused/I student must be in the roster");
    }

    /// <summary>
    /// Test 2: Mazeret_RecomputesOutOfIncomplete
    ///
    /// Student has Vize=80 (weight=40%), Final excused (I, no score).
    /// Instructor enters mazeret score=70 on the Final component via mazeret-scores.
    /// After mazeret-finalize:
    ///   weighted = 80*0.4 + 70*0.6 = 32 + 42 = 74 → CB (≥61) or BB (≥71)?
    ///   Default criteria: BB≥71, CB≥61. 74 falls in BB range (≥71 and <81).
    /// Assertions:
    ///   - New Grade is NOT I
    ///   - New Grade is BB (74 points → BB with default criteria)
    ///   - Original Final Score (null) remains null
    ///   - MakeupScore=70 is stored on the Final component row
    /// </summary>
    [Fact]
    public async Task Mazeret_RecomputesOutOfIncomplete()
    {
        SeedBase();
        SeedExcusedStudent(vizeScore: 80m, finalMakeupScore: null);
        _context.SaveChanges();
        SeedExamRegulation(autoTransferExcusedToMakeup: true);

        var controller = CreateController();

        // Step 1: enter mazeret score for the Final component via PUT
        var putBody = new GradeController.MazeretScoresRequest
        {
            Items =
            [
                new GradeController.MazeretScoreItem
                {
                    StudentId = ExcusedStudentId,
                    ComponentId = FinalComponentId,
                    MakeupScore = 70m
                }
            ]
        };
        var putResult = await controller.SetMazeretScores(SectionId, putBody);
        putResult.Should().BeOfType<OkObjectResult>("storing mazeret scores must succeed");

        // Verify MakeupScore was stored on the Final component row
        var finalGradeRow = await _context.StudentGrades
            .FirstOrDefaultAsync(sg => sg.CourseRegistrationId == ExcusedRegistrationId
                && sg.GradeComponentId == FinalComponentId);
        finalGradeRow.Should().NotBeNull();
        finalGradeRow!.MakeupScore.Should().Be(70m, "MakeupScore must be persisted");
        finalGradeRow.Score.Should().BeNull("original Score must remain null — it was never taken");

        // Step 2: finalize mazeret
        var finalizeResult = await controller.FinalizeMazeret(SectionId);
        finalizeResult.Should().BeOfType<OkObjectResult>("mazeret-finalize must succeed");

        // Check the updated grade
        var reg = await _context.CourseRegistrations.FindAsync(ExcusedRegistrationId);
        reg!.Grade.Should().NotBe(GradeCode.I, "after mazeret score=70, grade must no longer be I");

        // 80*0.4 + 70*0.6 = 32 + 42 = 74 → BB (default criteria: BB≥71, CB≥61)
        reg.Grade.Should().Be(GradeCode.BB,
            "mazeret weighted=74 → BB with default grading criteria (BB≥71, CB≥61)");

        // GPA must be recalculated
        _gpaMock.Verify(g => g.RecalculateAndUpdateAsync(ExcusedStudentId), Times.AtLeastOnce);
    }

    /// <summary>
    /// Test 3: Mazeret_SkipsStudentWithNoMakeupScore
    ///
    /// The I-student has no mazeret score entered on their excused Final component.
    /// mazeret-finalize must leave them as I (skip, no throw).
    /// </summary>
    [Fact]
    public async Task Mazeret_SkipsStudentWithNoMakeupScore()
    {
        SeedBase();
        SeedExcusedStudent(finalMakeupScore: null); // no mazeret score pre-seeded
        _context.SaveChanges();
        SeedExamRegulation(autoTransferExcusedToMakeup: true);

        var controller = CreateController();

        // Do NOT call SetMazeretScores — no score entered
        var finalizeResult = await controller.FinalizeMazeret(SectionId);
        finalizeResult.Should().BeOfType<OkObjectResult>(
            "mazeret-finalize must succeed (return Ok) even when no mazeret score was entered");

        // Grade must still be I — student was skipped
        var reg = await _context.CourseRegistrations.FindAsync(ExcusedRegistrationId);
        reg!.Grade.Should().Be(GradeCode.I,
            "student with no makeup score on their excused component must remain as I");

        // GPA recalculation must NOT have been called (no recompute happened)
        _gpaMock.Verify(g => g.RecalculateAndUpdateAsync(ExcusedStudentId), Times.Never);
    }

    /// <summary>
    /// Test 4: Mazeret_ExcludedWhenGOff
    ///
    /// G=AutoTransferExcusedToMakeup=false → even though the student is Grade=I,
    /// they must NOT appear in the mazeret roster.
    /// </summary>
    [Fact]
    public async Task Mazeret_ExcludedWhenGOff()
    {
        SeedBase();
        SeedExcusedStudent();
        _context.SaveChanges();
        SeedExamRegulation(autoTransferExcusedToMakeup: false); // G = OFF

        var controller = CreateController();
        var result = await controller.GetMazeretRoster(SectionId);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var body = ok.Value!;

        var studentsProperty = body.GetType().GetProperty("Students");
        var students = (System.Collections.IEnumerable)studentsProperty!.GetValue(body)!;
        var studentList = students.Cast<object>().ToList();

        studentList.Should().BeEmpty(
            "G=false: I-student must be excluded from mazeret roster when AutoTransferExcusedToMakeup=false");
    }

    public void Dispose() => _context.Dispose();
}
