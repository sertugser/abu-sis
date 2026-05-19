using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.API.Controllers;

namespace SIS.Tests.API;

public class AdvisorInfoControllerTests
{
    [Fact]
    public void AdvisorInfoController_HasCorrectRouteAndAuthorization()
    {
        var controllerType = typeof(AdvisorInfoController);

        var routeAttr = controllerType
            .GetCustomAttributes(typeof(RouteAttribute), true)
            .Cast<RouteAttribute>()
            .Single();
        routeAttr.Template.Should().Be("api/advisor-info");

        var authAttr = controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Single();
        authAttr.Roles.Should().BeNull(); // Sprint 5 pattern: permission-based
    }

    [Fact]
    public void GetMe_HasHttpGetAttribute_AndPermissionGate()
    {
        var method = typeof(AdvisorInfoController).GetMethod("GetMe");
        method.Should().NotBeNull();

        var httpGet = method!.GetCustomAttributes(typeof(HttpGetAttribute), true)
            .Cast<HttpGetAttribute>()
            .Single();
        httpGet.Template.Should().Be("me");

        var permission = method.GetCustomAttributes(typeof(RequirePermissionAttribute), true)
            .Cast<RequirePermissionAttribute>()
            .Single();
        permission.Should().NotBeNull();
    }

    [Fact]
    public void GetSchedule_HasHttpGetAttribute_AndPermissionGate()
    {
        var method = typeof(AdvisorInfoController).GetMethod("GetSchedule");
        method.Should().NotBeNull();

        var httpGet = method!.GetCustomAttributes(typeof(HttpGetAttribute), true)
            .Cast<HttpGetAttribute>()
            .Single();
        httpGet.Template.Should().Be("schedule");

        var permission = method.GetCustomAttributes(typeof(RequirePermissionAttribute), true)
            .Cast<RequirePermissionAttribute>()
            .Single();
        permission.Should().NotBeNull();
    }
}
