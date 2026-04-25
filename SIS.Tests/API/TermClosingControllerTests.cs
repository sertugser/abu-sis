using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Controllers;

namespace SIS.Tests.API;

public class TermClosingControllerTests
{
    [Fact]
    public void Controller_HasCorrectRouteAndAuthorization()
    {
        var controllerType = typeof(TermClosingController);

        var routeAttribute = controllerType.GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Single();

        var authorizeAttribute = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        routeAttribute.Template.Should().Be("api/term-closing");
        // Sprint 5: [Authorize(Roles=...)] -> [Authorize] + method-level [RequirePermission]
        authorizeAttribute.Roles.Should().BeNull();
    }

    [Theory]
    [InlineData("GetStatus", "GET")]
    [InlineData("LockGrades", "POST")]
    [InlineData("UnlockGrades", "POST")]
    [InlineData("CalculateGpa", "POST")]
    [InlineData("PreviewPromotion", "GET")]
    [InlineData("Promote", "POST")]
    [InlineData("RollbackPromotion", "POST")]
    [InlineData("OpenNewTerm", "POST")]
    public void Controller_HasExpectedHttpMethods(string methodName, string expectedMethod)
    {
        var method = typeof(TermClosingController).GetMethod(methodName);
        method.Should().NotBeNull($"method {methodName} should exist");

        var hasCorrectAttribute = expectedMethod switch
        {
            "GET" => method!.GetCustomAttributes(typeof(HttpGetAttribute), false).Length > 0,
            "POST" => method!.GetCustomAttributes(typeof(HttpPostAttribute), false).Length > 0,
            _ => false
        };

        hasCorrectAttribute.Should().BeTrue($"{methodName} should have Http{expectedMethod} attribute");
    }
}
