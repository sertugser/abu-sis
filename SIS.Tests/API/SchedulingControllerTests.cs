using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.API.Controllers;
using SIS.Application.AcademicOperations.Services;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.API;

public class SchedulingControllerTests
{
    private static ControllerContext CreateAuthenticatedContext(int userId = 1, string role = "SystemAdministrator")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
    [Fact]
    public void SchedulingController_HasExpectedRouteAndAuthorization()
    {
        var controllerType = typeof(SchedulingController);

        var routeAttribute = controllerType.GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Single();
        var authorizeAttribute = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        routeAttribute.Template.Should().Be("api/[controller]");
        authorizeAttribute.Roles.Should().Be("AdministrativeStaff,DepartmentManager,Dean,SystemAdministrator");
    }

    [Fact]
    public async Task GetConflicts_InvalidTermId_ReturnsBadRequest()
    {
        var serviceMock = new Mock<ISchedulingService>(MockBehavior.Strict);
        var academicOperationsServiceMock = new Mock<IAcademicOperationsService>(MockBehavior.Strict);
        var controller = new SchedulingController(serviceMock.Object, academicOperationsServiceMock.Object, null!);

        var result = await controller.GetConflicts(0, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Validate_ServiceReturnsResult_ReturnsOk()
    {
        var response = new ScheduleValidationResultDto
        {
            IsValid = true
        };

        var serviceMock = new Mock<ISchedulingService>();
        serviceMock
            .Setup(service => service.ValidateAsync(It.IsAny<ScheduleValidationRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        using var context = CreateInMemoryContext();
        // Add a section with Id=5 and a course with null DepartmentId (ortak ders, no access check needed)
        context.Courses.Add(new SIS.Domain.Entities.Course
        {
            Id = 50, Code = "TEST50", Name = "Test Course", Ects = 5,
            DepartmentId = null, TheoryHours = 3, LabHours = 0
        });
        context.CourseSections.Add(new SIS.Domain.Entities.CourseSection
        {
            Id = 5, CourseId = 50, TermId = 1, Capacity = 40, EnrolledCount = 0, IsActive = true
        });
        await context.SaveChangesAsync();

        var academicOperationsServiceMock = new Mock<IAcademicOperationsService>(MockBehavior.Strict);
        var controller = new SchedulingController(serviceMock.Object, academicOperationsServiceMock.Object, context);
        controller.ControllerContext = CreateAuthenticatedContext();

        var result = await controller.Validate(new ScheduleValidationRequestDto { SectionId = 5 }, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(response);
    }

    [Fact]
    public async Task Suggest_ServiceReturnsSuggestion_ReturnsOk()
    {
        var response = new ScheduleSuggestionDto
        {
            IsFeasible = true,
            SectionId = 7
        };

        var serviceMock = new Mock<ISchedulingService>();
        serviceMock
            .Setup(service => service.SuggestAsync(It.IsAny<ScheduleSuggestionRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        using var context = CreateInMemoryContext();
        // Add a section with Id=7 and a course with null DepartmentId (ortak ders, no access check needed)
        context.Courses.Add(new SIS.Domain.Entities.Course
        {
            Id = 70, Code = "TEST70", Name = "Test Course", Ects = 5,
            DepartmentId = null, TheoryHours = 3, LabHours = 0
        });
        context.CourseSections.Add(new SIS.Domain.Entities.CourseSection
        {
            Id = 7, CourseId = 70, TermId = 1, Capacity = 40, EnrolledCount = 0, IsActive = true
        });
        await context.SaveChangesAsync();

        var academicOperationsServiceMock = new Mock<IAcademicOperationsService>(MockBehavior.Strict);
        var controller = new SchedulingController(serviceMock.Object, academicOperationsServiceMock.Object, context);
        controller.ControllerContext = CreateAuthenticatedContext();

        var result = await controller.Suggest(new ScheduleSuggestionRequestDto { SectionId = 7 }, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(response);
    }
}
