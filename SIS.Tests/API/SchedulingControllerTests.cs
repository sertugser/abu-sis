using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SIS.API.Controllers;
using SIS.Application.AcademicOperations.Services;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;

namespace SIS.Tests.API;

public class SchedulingControllerTests
{
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
        authorizeAttribute.Roles.Should().Be("AdministrativeStaff,DepartmentManager,SystemAdministrator");
    }

    [Fact]
    public async Task GetConflicts_InvalidTermId_ReturnsBadRequest()
    {
        var serviceMock = new Mock<ISchedulingService>(MockBehavior.Strict);
        var academicOperationsServiceMock = new Mock<IAcademicOperationsService>(MockBehavior.Strict);
        var controller = new SchedulingController(serviceMock.Object, academicOperationsServiceMock.Object);

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

        var academicOperationsServiceMock = new Mock<IAcademicOperationsService>(MockBehavior.Strict);
        var controller = new SchedulingController(serviceMock.Object, academicOperationsServiceMock.Object);

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

        var academicOperationsServiceMock = new Mock<IAcademicOperationsService>(MockBehavior.Strict);
        var controller = new SchedulingController(serviceMock.Object, academicOperationsServiceMock.Object);

        var result = await controller.Suggest(new ScheduleSuggestionRequestDto { SectionId = 7 }, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(response);
    }
}
