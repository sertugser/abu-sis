using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SIS.API.Controllers;
using SIS.Application.AcademicOperations.DTOs;
using SIS.Application.AcademicOperations.Services;

namespace SIS.Tests.API;

public class AcademicOperationsControllerTests
{
    [Fact]
    public void AcademicOperationsController_HasExpectedRouteAndAuthorization()
    {
        var controllerType = typeof(AcademicOperationsController);

        var routeAttribute = controllerType.GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Single();
        var authorizeAttribute = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        routeAttribute.Template.Should().Be("api/[controller]");
        authorizeAttribute.Roles.Should().Be("AdministrativeStaff,DepartmentManager,SystemAdministrator");
    }

    [Fact]
    public async Task GetPlanningSummary_WhenDepartmentIsOutOfScope_ReturnsForbid()
    {
        var serviceMock = new Mock<IAcademicOperationsService>(MockBehavior.Strict);
        serviceMock
            .Setup(service => service.GetContextAsync(42, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AcademicOperationsContextDto
            {
                AccessibleDepartments =
                {
                    new AccessibleDepartmentDto
                    {
                        DepartmentId = 7,
                        DepartmentName = "Bilgisayar Muhendisligi",
                        DepartmentCode = "BM"
                    }
                }
            });

        var controller = CreateController(serviceMock.Object, 42, "DepartmentManager");

        var result = await controller.GetPlanningSummary(8, 1, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetPlanningSummary_WhenDepartmentIsAccessible_ReturnsOk()
    {
        var serviceMock = new Mock<IAcademicOperationsService>();
        serviceMock
            .Setup(service => service.GetContextAsync(42, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AcademicOperationsContextDto
            {
                AccessibleDepartments =
                {
                    new AccessibleDepartmentDto
                    {
                        DepartmentId = 7,
                        DepartmentName = "Bilgisayar Muhendisligi",
                        DepartmentCode = "BM"
                    }
                }
            });

        var expected = new AcademicPlanningSummaryDto
        {
            DepartmentId = 7,
            DepartmentName = "Bilgisayar Muhendisligi",
            TermId = 3,
            TermName = "2025-2026 Bahar"
        };

        serviceMock
            .Setup(service => service.GetPlanningSummaryAsync(7, 3, 42, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController(serviceMock.Object, 42, "SystemAdministrator");

        var result = await controller.GetPlanningSummary(7, 3, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expected);
    }

    private static AcademicOperationsController CreateController(IAcademicOperationsService service, int userId, params string[] roles)
    {
        var controller = new AcademicOperationsController(service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    ..roles.Select(role => new Claim(ClaimTypes.Role, role))
                ], "TestAuth"))
            }
        };

        return controller;
    }
}
