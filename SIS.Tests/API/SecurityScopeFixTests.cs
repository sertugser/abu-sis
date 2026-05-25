using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.API.Authorization;
using SIS.API.Controllers;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Application.Common.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.API;

public class SecurityScopeFixTests
{
    [Fact]
    public async Task StudentDirectory_DeanWithoutDepartment_IsConstrainedToDeanFacultyDepartments()
    {
        await using var context = CreateContext();
        SeedFacultyDepartmentGraph(context);
        AddInstructor(context, id: 10, userId: 100, departmentId: 1);
        await context.SaveChangesAsync();

        var service = new Mock<IAdminStudentService>();
        service
            .Setup(item => item.GetStudentsAsync(It.Is<AdminStudentFilterDto>(filter =>
                filter.DepartmentId == null &&
                filter.DepartmentIds != null &&
                filter.DepartmentIds.OrderBy(id => id).SequenceEqual(new[] { 1, 2 }))))
            .ReturnsAsync(new PagedResult<AdminStudentListItemDto>([], 0, 1, 25));

        var controller = CreateStudentController(context, service.Object, userId: 100, "Dean");

        var result = await controller.GetStudents(new AdminStudentFilterDto(null, null, null, null, null, null));

        result.Should().BeOfType<OkObjectResult>();
        service.VerifyAll();
    }

    [Fact]
    public async Task AssignAdvisor_DepartmentManagerOutsideDepartment_ForbidsBeforeServiceCall()
    {
        await using var context = CreateContext();
        SeedFacultyDepartmentGraph(context);
        AddInstructor(context, id: 10, userId: 100, departmentId: 1);
        AddInstructor(context, id: 20, userId: 200, departmentId: 3);
        AddStudentWithMainProgram(context, studentId: 30, userId: 300, departmentId: 3);
        await context.SaveChangesAsync();

        var service = new Mock<IAdminStudentService>(MockBehavior.Strict);
        var controller = CreateStudentController(context, service.Object, userId: 100, "DepartmentManager");

        var result = await controller.AssignAdvisor(30, new AssignAdvisorDto(20));

        result.Should().BeOfType<ForbidResult>();
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ScheduleExport_ForeignFaculty_ForbidsBeforeServiceCall()
    {
        await using var context = CreateContext();
        SeedFacultyDepartmentGraph(context);
        AddInstructor(context, id: 10, userId: 100, departmentId: 1);
        await context.SaveChangesAsync();

        var service = new Mock<IExcelScheduleService>(MockBehavior.Strict);
        var controller = CreateScheduleController(context, service.Object, userId: 100, "DepartmentManager");

        var result = await controller.ExportSchedule(termId: 1, facultyId: 2, departmentId: null);

        result.Should().BeOfType<ForbidResult>();
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ScheduleExport_OwnFaculty_AllowsExistingServicePath()
    {
        await using var context = CreateContext();
        SeedFacultyDepartmentGraph(context);
        AddInstructor(context, id: 10, userId: 100, departmentId: 1);
        await context.SaveChangesAsync();

        var service = new Mock<IExcelScheduleService>();
        service
            .Setup(item => item.ExportScheduleAsync(1, 1, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);

        var controller = CreateScheduleController(context, service.Object, userId: 100, "DepartmentManager");

        var result = await controller.ExportSchedule(termId: 1, facultyId: 1, departmentId: null);

        result.Should().BeOfType<FileContentResult>();
        service.VerifyAll();
    }

    [Fact]
    public async Task GradeComponentSharedConfig_DifferentInstructorInSameCourseTerm_Forbids()
    {
        await using var context = CreateContext();
        SeedCourseTerm(context, firstInstructorId: 10, secondInstructorId: 20);
        await context.SaveChangesAsync();

        var controller = CreateGradeController(context, userId: 100);

        var result = await controller.UpsertComponent(1, new GradeController.ComponentDto
        {
            Name = "Midterm",
            Weight = 40,
            Order = 1
        });

        result.Should().BeOfType<ForbidResult>();
        context.GradeComponents.Should().BeEmpty();
    }

    [Fact]
    public async Task GradeComponentSharedConfig_SameInstructorAcrossCourseTerm_Allows()
    {
        await using var context = CreateContext();
        SeedCourseTerm(context, firstInstructorId: 10, secondInstructorId: 10);
        await context.SaveChangesAsync();

        var controller = CreateGradeController(context, userId: 100);

        var result = await controller.UpsertComponent(1, new GradeController.ComponentDto
        {
            Name = "Midterm",
            Weight = 40,
            Order = 1
        });

        result.Should().BeOfType<OkObjectResult>();
        context.GradeComponents.Should().ContainSingle(component => component.Name == "Midterm");
    }

    [Fact]
    public async Task GradeSubmit_ComponentSpecificOpen_Allows_When_GenericPeriodClosed()
    {
        await using var context = CreateContext();
        SeedGradeEntryGraph(context);
        await context.SaveChangesAsync();

        var controller = CreateGradeController(
            context,
            userId: 100,
            webModuleControls: new StubWebModuleControlService(new Dictionary<string, WebModuleAccessResult>(StringComparer.OrdinalIgnoreCase)
            {
                ["instructor-grade-entry"] = new(
                    IsEnabled: true,
                    IsOpen: false,
                    SelectedMode: WebModuleControlModes.GradeEntryPeriod,
                    SelectedModeLabel: "Not Giriş Süre Tanımlarına Bağlı",
                    EffectiveStateLabel: "Kapalı",
                    Message: "Not girişi süreci şu anda not giriş süre tanımlarında açık değil."),
                ["midterm-grade-entry"] = new(
                    IsEnabled: true,
                    IsOpen: true,
                    SelectedMode: WebModuleControlModes.Open,
                    SelectedModeLabel: "Açık",
                    EffectiveStateLabel: "Açık",
                    Message: null)
            }));

        var result = await controller.SubmitGrades(1, new GradeController.GradeSubmitDto
        {
            ComponentId = 1,
            Grades = []
        });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GradeSubmit_GenericClosed_StillBlocks_Even_When_ComponentSpecificOpen()
    {
        await using var context = CreateContext();
        SeedGradeEntryGraph(context);
        await context.SaveChangesAsync();

        var controller = CreateGradeController(
            context,
            userId: 100,
            webModuleControls: new StubWebModuleControlService(new Dictionary<string, WebModuleAccessResult>(StringComparer.OrdinalIgnoreCase)
            {
                ["instructor-grade-entry"] = new(
                    IsEnabled: true,
                    IsOpen: false,
                    SelectedMode: WebModuleControlModes.Closed,
                    SelectedModeLabel: "Kapalı",
                    EffectiveStateLabel: "Kapalı",
                    Message: "Not girişi modülü kapalı."),
                ["midterm-grade-entry"] = new(
                    IsEnabled: true,
                    IsOpen: true,
                    SelectedMode: WebModuleControlModes.Open,
                    SelectedModeLabel: "Açık",
                    EffectiveStateLabel: "Açık",
                    Message: null)
            }));

        var result = await controller.SubmitGrades(1, new GradeController.GradeSubmitDto
        {
            ComponentId = 1,
            Grades = []
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeEquivalentTo(new { error = "Not girişi modülü kapalı." });
    }

    [Fact]
    public async Task QrSubmit_AnonymousUser_ReturnsUnauthorizedForActiveToken()
    {
        await using var context = CreateContext();
        SeedAttendanceGraph(context, registeredStudentId: 20, registeredStudentUserId: 200);
        await context.SaveChangesAsync();

        var controller = CreateAttendanceController(context, userId: null);

        var result = await controller.SubmitQr("qr-token");

        result.Should().BeOfType<UnauthorizedResult>();
        context.AttendanceRecords.Should().BeEmpty();
    }

    [Fact]
    public async Task QrSubmit_AuthenticatedRegisteredStudent_WritesPresentForOwnRegistration()
    {
        await using var context = CreateContext();
        SeedAttendanceGraph(context, registeredStudentId: 20, registeredStudentUserId: 200);
        await context.SaveChangesAsync();

        var controller = CreateAttendanceController(context, userId: 200);

        var result = await controller.SubmitQr("qr-token");

        result.Should().BeOfType<OkObjectResult>();
        var record = await context.AttendanceRecords.SingleAsync();
        record.StudentId.Should().Be(20);
        record.Status.Should().Be(AttendanceStatus.Present);
        record.Source.Should().Be(AttendanceEntrySource.Qr);
    }

    [Fact]
    public async Task QrSubmit_AuthenticatedDifferentStudent_DoesNotWriteForRegisteredTarget()
    {
        await using var context = CreateContext();
        SeedAttendanceGraph(context, registeredStudentId: 20, registeredStudentUserId: 200);
        context.Users.Add(new ApplicationUser { Id = 201, UserName = "other", Email = "other@example.test", FirstName = "Other", LastName = "Student" });
        context.Students.Add(new Student { Id = 21, UserId = 201, StudentNumber = "S21", Status = StudentStatus.Active });
        await context.SaveChangesAsync();

        var controller = CreateAttendanceController(context, userId: 201);

        var result = await controller.SubmitQr("qr-token");

        result.Should().BeOfType<BadRequestObjectResult>();
        context.AttendanceRecords.Should().BeEmpty();
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static StudentManagementController CreateStudentController(
        AppDbContext context,
        IAdminStudentService service,
        int userId,
        params string[] roles)
    {
        var controller = new StudentManagementController(service, context, new AcademicScopeGuard(context));
        SetUser(controller, userId, roles);
        return controller;
    }

    private static ScheduleExcelController CreateScheduleController(
        AppDbContext context,
        IExcelScheduleService service,
        int userId,
        params string[] roles)
    {
        var controller = new ScheduleExcelController(service, new AcademicScopeGuard(context));
        SetUser(controller, userId, roles);
        return controller;
    }

    private static GradeController CreateGradeController(
        AppDbContext context,
        int userId,
        IWebModuleControlService? webModuleControls = null)
    {
        var controller = new GradeController(
            context,
            Mock.Of<IGpaCalculationService>(),
            new FakeClock(),
            new ExamRegulationResolver(context),
            webModuleControls: webModuleControls);
        SetUser(controller, userId, "Instructor");
        return controller;
    }

    private static AttendanceController CreateAttendanceController(AppDbContext context, int? userId)
    {
        var controller = new AttendanceController(context);
        if (userId.HasValue)
            SetUser(controller, userId.Value, "Student");
        else
            SetAnonymousUser(controller);
        return controller;
    }

    private static void SetUser(ControllerBase controller, int userId, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };
    }

    private static void SetAnonymousUser(ControllerBase controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };
    }

    private static void SeedFacultyDepartmentGraph(AppDbContext context)
    {
        var faculty1 = new Faculty { Id = 1, Code = "F1", Name = "Faculty 1" };
        var faculty2 = new Faculty { Id = 2, Code = "F2", Name = "Faculty 2" };
        var department1 = new Department { Id = 1, Code = "D1", Name = "Department 1", FacultyId = 1, Faculty = faculty1 };
        var department2 = new Department { Id = 2, Code = "D2", Name = "Department 2", FacultyId = 1, Faculty = faculty1 };
        var department3 = new Department { Id = 3, Code = "D3", Name = "Department 3", FacultyId = 2, Faculty = faculty2 };

        context.AddRange(faculty1, faculty2, department1, department2, department3);
    }

    private static Instructor AddInstructor(AppDbContext context, int id, int userId, int departmentId)
    {
        var instructor = new Instructor
        {
            Id = id,
            UserId = userId,
            DepartmentId = departmentId,
            Title = "Dr.",
            MaxWeeklyHours = 12
        };
        context.Instructors.Add(instructor);
        return instructor;
    }

    private static Student AddStudentWithMainProgram(AppDbContext context, int studentId, int userId, int departmentId)
    {
        var program = new AcademicProgram
        {
            Id = 1000 + departmentId,
            Code = $"P{departmentId}",
            Name = $"Program {departmentId}",
            DepartmentId = departmentId,
            MaxStudyDurationSemesters = 8,
            TotalRequiredEcts = 240
        };
        var student = new Student
        {
            Id = studentId,
            UserId = userId,
            StudentNumber = $"S{studentId}",
            Status = StudentStatus.Active
        };

        context.AcademicPrograms.Add(program);
        context.Students.Add(student);
        context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = student.Id,
            Student = student,
            ProgramId = program.Id,
            Program = program,
            CurriculumId = 1,
            Type = StudentProgramType.Main,
            EnrollmentDate = DateTime.UtcNow,
            IsActive = true
        });

        return student;
    }

    private static void SeedCourseTerm(AppDbContext context, int firstInstructorId, int secondInstructorId)
    {
        SeedFacultyDepartmentGraph(context);
        context.Users.Add(new ApplicationUser { Id = 100, UserName = "instructor", Email = "instructor@example.test" });
        AddInstructor(context, id: 10, userId: 100, departmentId: 1);
        AddInstructor(context, id: 20, userId: 200, departmentId: 1);

        var term = new AcademicTerm
        {
            Id = 1,
            Name = "2026 Spring",
            ShortName = "26S",
            Year = 2026,
            TermStart = DateTime.UtcNow.AddDays(-30),
            TermEnd = DateTime.UtcNow.AddDays(30)
        };
        var course = new Course { Id = 1, Code = "CENG101", Name = "Intro", DepartmentId = 1 };

        context.AddRange(term, course);
        context.CourseSections.AddRange(
            new CourseSection { Id = 1, CourseId = course.Id, Course = course, TermId = term.Id, Term = term, InstructorId = firstInstructorId, Capacity = 30 },
            new CourseSection { Id = 2, CourseId = course.Id, Course = course, TermId = term.Id, Term = term, InstructorId = secondInstructorId, Capacity = 30 });
    }

    private static void SeedAttendanceGraph(AppDbContext context, int registeredStudentId, int registeredStudentUserId)
    {
        SeedFacultyDepartmentGraph(context);
        var term = new AcademicTerm
        {
            Id = 1,
            Name = "2026 Spring",
            ShortName = "26S",
            Year = 2026,
            TermStart = DateTime.UtcNow.AddDays(-30),
            TermEnd = DateTime.UtcNow.AddDays(30)
        };
        var course = new Course { Id = 1, Code = "CENG101", Name = "Intro", DepartmentId = 1 };
        var instructor = AddInstructor(context, id: 10, userId: 100, departmentId: 1);
        var section = new CourseSection { Id = 1, CourseId = course.Id, Course = course, TermId = term.Id, Term = term, InstructorId = instructor.Id, Capacity = 30 };
        var studentUser = new ApplicationUser { Id = registeredStudentUserId, UserName = "student", Email = "student@example.test", FirstName = "Registered", LastName = "Student" };
        var student = new Student { Id = registeredStudentId, UserId = registeredStudentUserId, StudentNumber = $"S{registeredStudentId}", Status = StudentStatus.Active };

        context.AddRange(term, course, section, studentUser, student);
        context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = 1,
            StudentId = student.Id,
            Student = student,
            CourseSectionId = section.Id,
            CourseSection = section,
            Status = RegistrationStatus.Active
        });
        context.AttendanceSessions.Add(new AttendanceSession
        {
            Id = 1,
            CourseSectionId = section.Id,
            CourseSection = section,
            InstructorId = instructor.Id,
            Instructor = instructor,
            WeekNumber = 1,
            SessionDate = DateTime.UtcNow.Date,
            Token = "qr-token",
            IsQrOpen = true,
            QrExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow
        });
    }

    private static void SeedGradeEntryGraph(AppDbContext context)
    {
        SeedFacultyDepartmentGraph(context);
        context.Users.Add(new ApplicationUser { Id = 100, UserName = "instructor", Email = "instructor@example.test" });
        var instructor = AddInstructor(context, id: 10, userId: 100, departmentId: 1);

        var term = new AcademicTerm
        {
            Id = 1,
            Name = "2026 Spring",
            ShortName = "26S",
            EnglishName = "2026 Spring",
            ShortEnglishName = "26S",
            Year = 2026,
            TermStart = DateTime.UtcNow.AddDays(-30),
            TermEnd = DateTime.UtcNow.AddDays(30)
        };

        var course = new Course
        {
            Id = 1,
            Code = "CENG101",
            Name = "Intro",
            DepartmentId = 1
        };

        var section = new CourseSection
        {
            Id = 1,
            CourseId = course.Id,
            Course = course,
            TermId = term.Id,
            Term = term,
            InstructorId = instructor.Id,
            Instructor = instructor,
            Capacity = 30
        };

        var component = new GradeComponent
        {
            Id = 1,
            CourseId = course.Id,
            Course = course,
            AcademicTermId = term.Id,
            AcademicTerm = term,
            Name = "Ara Sınav",
            ExamType = "Midterm",
            Weight = 40,
            Order = 1
        };

        context.AddRange(term, course, section, component);
    }

    private sealed class StubWebModuleControlService(IDictionary<string, WebModuleAccessResult> results) : IWebModuleControlService
    {
        public WebModuleControlDefinition? GetDefinition(string code) => null;

        public Task<IReadOnlyList<WebModuleControlSummary>> GetSummariesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WebModuleControlSummary>>([]);

        public Task<WebModuleAccessResult> EvaluateAsync(string code, int? termId = null, CancellationToken ct = default)
            => Task.FromResult(results.TryGetValue(code, out var result)
                ? result
                : new WebModuleAccessResult(
                    IsEnabled: true,
                    IsOpen: true,
                    SelectedMode: WebModuleControlModes.Open,
                    SelectedModeLabel: "Açık",
                    EffectiveStateLabel: "Açık",
                    Message: null));
    }
}
