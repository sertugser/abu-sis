using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SIS.API.Controllers;
using SIS.Application.EnrollmentForecast.DTOs;
using SIS.Application.EnrollmentForecast.Services;

namespace SIS.Tests.API;

public class ForecastControllerTests
{
    [Fact]
    public void ForecastController_HasExpectedRouteAndAuthorization()
    {
        var controllerType = typeof(ForecastController);

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
    public async Task GetCourseDemand_InvalidCourseId_ReturnsBadRequest()
    {
        var serviceMock = new Mock<IEnrollmentForecastService>(MockBehavior.Strict);
        var controller = new ForecastController(serviceMock.Object);

        var result = await controller.GetCourseDemand(0);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetCourseDemand_ServiceReturnsNull_ReturnsNotFound()
    {
        var serviceMock = new Mock<IEnrollmentForecastService>();
        serviceMock
            .Setup(service => service.GetCourseDemandForecastAsync(42))
            .ReturnsAsync((CourseDemandForecastDto?)null);

        var controller = new ForecastController(serviceMock.Object);

        var result = await controller.GetCourseDemand(42);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetDepartmentSummary_ServiceReturnsDto_ReturnsOkWithExpectedShape()
    {
        var response = new DepartmentForecastSummaryDto
        {
            DepartmentId = 7,
            DepartmentName = "Computer Engineering",
            TotalCoursesAnalyzed = 1,
            TotalPredictedDemand = 12,
            TotalRecommendedSections = 1,
            CourseSummaries =
            {
                new CourseDemandForecastDto
                {
                    CourseId = 11,
                    CourseCode = "CENG301",
                    CourseName = "Software Engineering",
                    PredictedEligibleCount = 7,
                    PredictedRetakeCount = 3,
                    PredictedOverloadOrUpperClassCount = 2,
                    TotalPredictedDemand = 12,
                    RecommendedSectionCount = 1,
                    CapacityBasis = "Aktif donem course section ortalamasi",
                    Notes = { "Sample note" }
                }
            },
            Notes = { "Department note" }
        };

        var serviceMock = new Mock<IEnrollmentForecastService>();
        serviceMock
            .Setup(service => service.GetDepartmentSummaryAsync(7))
            .ReturnsAsync(response);

        var controller = new ForecastController(serviceMock.Object);

        var result = await controller.GetDepartmentSummary(7);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(response);
    }
}
