using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SIS.API.Controllers.Admin;
using SIS.Application.Identity.DTOs;
using SIS.Application.Identity.Services;
using SIS.Domain.Identity;

namespace SIS.Tests.API;

public class UserScopesControllerTests
{
    [Fact]
    public async Task Get_ReturnsNotFound_WhenScopeMissing()
    {
        var service = new Mock<IUserScopeService>();
        service.Setup(x => x.GetForUserAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserScopeDto?)null);

        var controller = new UserScopesController(service.Object);

        var result = await controller.Get(42, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Get_ReturnsOk_WhenScopeExists()
    {
        var dto = new UserScopeDto(42, ScopeKind.Faculty, 7, DateTime.UtcNow, DateTime.UtcNow);
        var service = new Mock<IUserScopeService>();
        service.Setup(x => x.GetForUserAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var controller = new UserScopesController(service.Object);

        var result = await controller.Get(42, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(dto);
    }

    [Fact]
    public async Task Set_ReturnsOk_WhenServiceSucceeds()
    {
        var request = new SetUserScopeDto(ScopeKind.Faculty, 7);
        var response = new UserScopeDto(42, ScopeKind.Faculty, 7, DateTime.UtcNow, DateTime.UtcNow);
        var service = new Mock<IUserScopeService>();
        service.Setup(x => x.SetForUserAsync(42, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var controller = new UserScopesController(service.Object);

        var result = await controller.Set(42, request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(response);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        var service = new Mock<IUserScopeService>();
        service.Setup(x => x.ClearForUserAsync(42, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new UserScopesController(service.Object);

        var result = await controller.Delete(42, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }
}
