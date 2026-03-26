using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Controllers;

namespace SIS.Tests.API;

public class AdvisorControllerTests
{
    [Fact]
    public void AdvisorController_HasCorrectRouteAndAuthorization()
    {
        var controllerType = typeof(AdvisorController);

        var routeAttr = controllerType
            .GetCustomAttributes(typeof(RouteAttribute), true)
            .Cast<RouteAttribute>()
            .Single();
        routeAttr.Template.Should().Be("api/advisor");

        var authAttr = controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Single();
        authAttr.Roles.Should().Be("AcademicAdvisor,Instructor");
    }

    [Fact]
    public void GetPendingApprovals_HasHttpGetAttribute()
    {
        var method = typeof(AdvisorController).GetMethod("GetPendingApprovals");
        method.Should().NotBeNull();

        var httpGet = method!.GetCustomAttributes(typeof(HttpGetAttribute), true)
            .Cast<HttpGetAttribute>()
            .Single();
        httpGet.Template.Should().Be("pending-approvals");
    }

    [Fact]
    public void Approve_HasHttpPostAttribute()
    {
        var method = typeof(AdvisorController).GetMethod("Approve");
        method.Should().NotBeNull();

        var httpPost = method!.GetCustomAttributes(typeof(HttpPostAttribute), true)
            .Cast<HttpPostAttribute>()
            .Single();
        httpPost.Template.Should().Be("approve");
    }

    [Fact]
    public void Reject_HasHttpPostAttribute()
    {
        var method = typeof(AdvisorController).GetMethod("Reject");
        method.Should().NotBeNull();

        var httpPost = method!.GetCustomAttributes(typeof(HttpPostAttribute), true)
            .Cast<HttpPostAttribute>()
            .Single();
        httpPost.Template.Should().Be("reject");
    }
}
