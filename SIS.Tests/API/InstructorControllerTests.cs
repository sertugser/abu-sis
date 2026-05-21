using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.API.Controllers;
using SIS.Application.Common.Services;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.API;

public class InstructorControllerTests
{
    [Fact]
    public void InstructorController_HasExpectedRouteAndAuthorization()
    {
        var controllerType = typeof(InstructorController);

        var routeAttribute = controllerType.GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Single();
        var authorizeAttribute = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        routeAttribute.Template.Should().Be("api/[controller]");
        // Sprint 5: [Authorize(Roles=...)] -> [Authorize] + method-level [RequirePermission]
        authorizeAttribute.Roles.Should().BeNull();
    }

    [Fact]
    public async Task ValidateSectionSchedule_SectionNotOwned_ReturnsNotFound()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);

        var serviceMock = new Mock<ISchedulingService>(MockBehavior.Strict);
        var controller = CreateController(context, serviceMock.Object, userId: 42);

        var result = await controller.ValidateSectionSchedule(999, null, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ValidateSectionSchedule_ForcesOwnedSectionAndInstructorId()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);
        SeedSection(context, sectionId: 11, instructorId: 7);

        var expected = new ScheduleValidationResultDto { IsValid = true };
        var serviceMock = new Mock<ISchedulingService>();
        serviceMock
            .Setup(service => service.ValidateAsync(
                It.Is<ScheduleValidationRequestDto>(request =>
                    request.SectionId == 11
                    && request.InstructorId == 7
                    && request.ClassroomId == 55),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController(context, serviceMock.Object, userId: 42);

        var result = await controller.ValidateSectionSchedule(
            11,
            new ScheduleValidationRequestDto
            {
                SectionId = 999,
                InstructorId = 999,
                ClassroomId = 55
            },
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task SuggestSectionSchedule_ForcesOwnedSectionAndInstructorId()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);
        SeedSection(context, sectionId: 15, instructorId: 7);

        var expected = new ScheduleSuggestionDto
        {
            SectionId = 15,
            IsFeasible = true
        };

        var serviceMock = new Mock<ISchedulingService>();
        serviceMock
            .Setup(service => service.SuggestAsync(
                It.Is<ScheduleSuggestionRequestDto>(request =>
                    request.SectionId == 15
                    && request.InstructorId == 7),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController(context, serviceMock.Object, userId: 42);

        var result = await controller.SuggestSectionSchedule(
            15,
            new ScheduleSuggestionRequestDto
            {
                SectionId = 501,
                InstructorId = 501
            },
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task UpdateProfile_RequireOfficeHoursEnabled_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);

        var systemConfigMock = CreateSystemConfigMock();
        systemConfigMock
            .Setup(provider => provider.GetBoolAsync("Instructor.RequireOfficeHoursOnProfileUpdate", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = CreateController(context, Mock.Of<ISchedulingService>(), userId: 42, systemConfigMock);

        var result = await controller.UpdateProfile(
            new InstructorController.UpdateInstructorProfileDto("ogretmen@ankarabilim.edu.tr", 1, "", false),
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateProfile_PrimaryEmailImmutable_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);

        var systemConfigMock = CreateSystemConfigMock();
        systemConfigMock
            .Setup(provider => provider.GetBoolAsync("Instructor.PrimaryEmailImmutable", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = CreateController(context, Mock.Of<ISchedulingService>(), userId: 42, systemConfigMock);

        var result = await controller.UpdateProfile(
            new InstructorController.UpdateInstructorProfileDto("yeni@ankarabilim.edu.tr", 1, "Salı 10:00-12:00", false),
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateProfile_DepartmentDeadlinePassed_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);
        SeedDepartment(context, departmentId: 2, facultyId: 1, code: "YM", name: "Yazılım Mühendisliği");

        var systemConfigMock = CreateSystemConfigMock();
        systemConfigMock
            .Setup(provider => provider.GetStringAsync("Instructor.ProfileOrgUnitUpdateDeadline", string.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync("2000-01-01");

        var controller = CreateController(context, Mock.Of<ISchedulingService>(), userId: 42, systemConfigMock);

        var result = await controller.UpdateProfile(
            new InstructorController.UpdateInstructorProfileDto("ogretmen@ankarabilim.edu.tr", 2, "Salı 10:00-12:00", false),
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateProfile_AllowedUpdate_PersistsAdvisorOptOutAndOfficeHours()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);

        var controller = CreateController(context, Mock.Of<ISchedulingService>(), userId: 42);

        var result = await controller.UpdateProfile(
            new InstructorController.UpdateInstructorProfileDto("ogretmen@ankarabilim.edu.tr", 1, "Perşembe 14:00-16:00", true),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();

        var instructor = await context.Instructors.SingleAsync(i => i.Id == 7);
        instructor.OfficeHours.Should().Be("Perşembe 14:00-16:00");
        instructor.AdvisorOptOutRequested.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAcademicCvActivity_ActiveYearMismatch_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);

        var systemConfigMock = CreateSystemConfigMock();
        systemConfigMock
            .Setup(provider => provider.GetIntAsync("Instructor.AcademicCvActiveYear", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2026);

        var controller = CreateController(context, Mock.Of<ISchedulingService>(), userId: 42, systemConfigMock);

        var result = await controller.CreateAcademicCvActivity(
            new InstructorController.CreateAcademicCvActivityDto(2025, "Makale", "Açıklama"),
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateAcademicCvActivity_AutoApproveEnabled_PersistsApprovedStatus()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);

        var systemConfigMock = CreateSystemConfigMock();
        systemConfigMock
            .Setup(provider => provider.GetIntAsync("Instructor.AcademicCvActiveYear", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2026);
        systemConfigMock
            .Setup(provider => provider.GetBoolAsync("Instructor.AcademicCvAutoApprove", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = CreateController(context, Mock.Of<ISchedulingService>(), userId: 42, systemConfigMock);

        var result = await controller.CreateAcademicCvActivity(
            new InstructorController.CreateAcademicCvActivityDto(2026, "Makale", "Açıklama"),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var activity = await context.AcademicCvActivities.SingleAsync();
        activity.Status.Should().Be(ApprovalStepStatus.Approved);
    }

    [Fact]
    public async Task UpdateAcademicCvActivity_ApprovedAndLocked_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        SeedInstructor(context, instructorId: 7, userId: 42);
        context.AcademicCvActivities.Add(new AcademicCvActivity
        {
            Id = 9,
            InstructorId = 7,
            ActivityYear = 2026,
            Title = "Makale",
            Description = "Eski",
            Status = ApprovalStepStatus.Approved,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var systemConfigMock = CreateSystemConfigMock();
        systemConfigMock
            .Setup(provider => provider.GetIntAsync("Instructor.AcademicCvActiveYear", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2026);
        systemConfigMock
            .Setup(provider => provider.GetBoolAsync("Instructor.AcademicCvApprovedActivityEditable", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = CreateController(context, Mock.Of<ISchedulingService>(), userId: 42, systemConfigMock);

        var result = await controller.UpdateAcademicCvActivity(
            9,
            new InstructorController.UpdateAcademicCvActivityDto(2026, "Yeni Başlık", "Yeni açıklama"),
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static void SeedInstructor(AppDbContext context, int instructorId, int userId)
    {
        SeedDepartment(context, departmentId: 1, facultyId: 1, code: "BM", name: "Bilgisayar Mühendisliği");
        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "ogretmen@ankarabilim.edu.tr",
            NormalizedUserName = "OGRETMEN@ANKARABILIM.EDU.TR",
            Email = "ogretmen@ankarabilim.edu.tr",
            NormalizedEmail = "OGRETMEN@ANKARABILIM.EDU.TR",
            FirstName = "Ada",
            LastName = "Lovelace",
        });
        context.Instructors.Add(new Instructor
        {
            Id = instructorId,
            UserId = userId,
            DepartmentId = 1,
            Title = "Dr.",
            MaxWeeklyHours = 12
        });

        context.SaveChanges();
    }

    private static void SeedDepartment(AppDbContext context, int departmentId, int facultyId, string code, string name)
    {
        if (!context.Faculties.Any(f => f.Id == facultyId))
        {
            context.Faculties.Add(new Faculty
            {
                Id = facultyId,
                Code = $"F{facultyId}",
                Name = $"Fakülte {facultyId}",
            });
        }

        if (!context.Departments.Any(d => d.Id == departmentId))
        {
            context.Departments.Add(new Department
            {
                Id = departmentId,
                FacultyId = facultyId,
                Code = code,
                Name = name,
            });
        }

        context.SaveChanges();
    }

    private static void SeedSection(AppDbContext context, int sectionId, int instructorId)
    {
        if (!context.Courses.Any(c => c.Id == 1))
        {
            context.Courses.Add(new Course
            {
                Id = 1,
                Code = "TEST101",
                Name = "Test Course",
                DepartmentId = null,
                Ects = 5,
                TheoryHours = 3,
                LabHours = 0
            });
        }

        context.CourseSections.Add(new CourseSection
        {
            Id = sectionId,
            CourseId = 1,
            TermId = 1,
            InstructorId = instructorId,
            Capacity = 30,
            Status = CourseSectionStatus.Active,
        });

        context.SaveChanges();
    }

    private static Mock<ISystemConfigProvider> CreateSystemConfigMock()
    {
        var systemConfigMock = new Mock<ISystemConfigProvider>();
        systemConfigMock
            .Setup(provider => provider.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, bool fallback, CancellationToken _) => fallback);
        systemConfigMock
            .Setup(provider => provider.GetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string fallback, CancellationToken _) => fallback);
        systemConfigMock
            .Setup(provider => provider.GetIntAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, int fallback, CancellationToken _) => fallback);
        return systemConfigMock;
    }

    private static InstructorController CreateController(
        AppDbContext context,
        ISchedulingService schedulingService,
        int userId,
        Mock<ISystemConfigProvider>? systemConfigMock = null)
    {
        var configMock = systemConfigMock ?? CreateSystemConfigMock();

        var controller = new InstructorController(context, schedulingService, configMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                ], "TestAuth"))
            }
        };

        return controller;
    }
}
