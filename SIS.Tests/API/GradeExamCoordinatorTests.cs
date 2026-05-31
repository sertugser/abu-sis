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
/// Sınav koordinatörü guard'ı: bir ders aynı dönemde birden fazla hoca tarafından
/// veriliyorsa, ortak GradeComponent ayarını yalnızca koordinatör section'ın hocası
/// değiştirebilir.
///
///   - Koordinatör atanmamış / başka section koordinatör → UpsertComponent 403
///   - Bu section IsExamCoordinator = true → guard geçer (403 DEĞİL; ileri validasyondan
///     dönen Ok/BadRequest olabilir, ama 403 olmaz)
/// </summary>
public class GradeExamCoordinatorTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<IGpaCalculationService> _gpaMock;

    private const int FacultyId = 1;
    private const int DeptId = 1;
    private const int ProgramId = 1;
    private const int CurriculumId = 1;
    private const int CourseId = 1;
    private const int TermId = 1;

    // Section A — koordinatör adayı, giriş yapan hoca buna atanmış
    private const int SectionAId = 1;
    private const int InstructorAId = 1;
    private const int InstructorAUserId = 100;

    // Section B — farklı hoca (çok-hocalı ders olduğunu tetikler)
    private const int SectionBId = 2;
    private const int InstructorBId = 2;
    private const int InstructorBUserId = 101;

    public GradeExamCoordinatorTests()
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

    /// <summary>
    /// Seeds a course+term with TWO sections taught by DIFFERENT instructors.
    /// Section A (the logged-in instructor's section) starts NOT coordinator.
    /// </summary>
    private void SeedMultiInstructorCourse()
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
        _context.Courses.Add(new Course { Id = CourseId, Code = "BIO101", Name = "Coordinator Test", Ects = 3 });
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
            Id = InstructorAUserId,
            UserName = "hocaA@test.edu",
            NormalizedUserName = "HOCAA@TEST.EDU",
            Email = "hocaA@test.edu",
            NormalizedEmail = "HOCAA@TEST.EDU",
            FirstName = "Hoca",
            LastName = "A"
        });
        _context.Users.Add(new ApplicationUser
        {
            Id = InstructorBUserId,
            UserName = "hocaB@test.edu",
            NormalizedUserName = "HOCAB@TEST.EDU",
            Email = "hocaB@test.edu",
            NormalizedEmail = "HOCAB@TEST.EDU",
            FirstName = "Hoca",
            LastName = "B"
        });
        _context.Instructors.Add(new Instructor
        {
            Id = InstructorAId,
            UserId = InstructorAUserId,
            DepartmentId = DeptId,
            Title = "Dr.",
            MaxWeeklyHours = 12
        });
        _context.Instructors.Add(new Instructor
        {
            Id = InstructorBId,
            UserId = InstructorBUserId,
            DepartmentId = DeptId,
            Title = "Dr.",
            MaxWeeklyHours = 12
        });

        _context.CourseSections.Add(new CourseSection
        {
            Id = SectionAId,
            CourseId = CourseId,
            TermId = TermId,
            InstructorId = InstructorAId,
            SectionNumber = 1,
            Capacity = 30,
            Status = CourseSectionStatus.Active,
            IsExamCoordinator = false
        });
        _context.CourseSections.Add(new CourseSection
        {
            Id = SectionBId,
            CourseId = CourseId,
            TermId = TermId,
            InstructorId = InstructorBId,
            SectionNumber = 2,
            Capacity = 30,
            Status = CourseSectionStatus.Active,
            IsExamCoordinator = false
        });

        _context.SaveChanges();
    }

    private GradeController CreateControllerAs(int instructorUserId)
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
                    new Claim(ClaimTypes.NameIdentifier, instructorUserId.ToString())
                ], "TestAuth"))
            }
        };

        return controller;
    }

    private static GradeController.ComponentDto MidtermComponent() => new()
    {
        Id = null,
        Name = "Vize",
        Weight = 40m,
        Order = 1,
        Type = GradeComponentType.Midterm
    };

    /// <summary>
    /// Non-coordinator instructor in a multi-instructor course must be blocked from
    /// mutating the shared component config, with a 403.
    /// </summary>
    [Fact]
    public async Task UpsertComponent_NonCoordinatorInMultiInstructorCourse_Returns403()
    {
        SeedMultiInstructorCourse();

        var controller = CreateControllerAs(InstructorAUserId);
        var result = await controller.UpsertComponent(SectionAId, MidtermComponent());

        var objectResult = result.Should().BeOfType<ObjectResult>(
            "the non-coordinator section must be blocked from shared config mutation").Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// Once the logged-in instructor's section is designated exam coordinator, the
    /// shared-config guard passes — the response must NOT be 403 (it may be Ok or a later
    /// BadRequest from downstream validation, but the coordinator guard no longer blocks).
    /// </summary>
    [Fact]
    public async Task UpsertComponent_CoordinatorSection_IsNot403()
    {
        SeedMultiInstructorCourse();

        var section = await _context.CourseSections.FirstAsync(cs => cs.Id == SectionAId);
        section.IsExamCoordinator = true;
        await _context.SaveChangesAsync();

        var controller = CreateControllerAs(InstructorAUserId);
        var result = await controller.UpsertComponent(SectionAId, MidtermComponent());

        if (result is ObjectResult objectResult && objectResult.StatusCode == StatusCodes.Status403Forbidden)
            objectResult.StatusCode.Should().NotBe(StatusCodes.Status403Forbidden,
                "the coordinator section must pass the shared-config guard");
    }

    public void Dispose() => _context.Dispose();
}
