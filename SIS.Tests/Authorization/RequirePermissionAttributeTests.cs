using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SIS.API.Authorization;
using SIS.Application.Identity.Services;

namespace SIS.Tests.Authorization;

public class RequirePermissionAttributeTests
{
    private static AuthorizationFilterContext BuildContext(
        ClaimsPrincipal user,
        IPermissionService? permissionService = null)
    {
        var services = new ServiceCollection();
        if (permissionService is not null)
        {
            services.AddSingleton(permissionService);
        }
        var http = new DefaultHttpContext
        {
            User = user,
            RequestServices = services.BuildServiceProvider(),
        };
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal AuthUser(int userId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, $"user{userId}"),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    [Fact]
    public async Task Returns401_WhenUnauthenticated()
    {
        var attr = new RequirePermissionAttribute("menu.x.view");
        var ctx = BuildContext(Anonymous());

        await attr.OnAuthorizationAsync(ctx);

        ctx.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SkipsCheck_WhenUserIsSystemAdmin()
    {
        var permSvc = new Mock<IPermissionService>(MockBehavior.Strict); // çağrılmamalı
        var attr = new RequirePermissionAttribute("menu.anything.delete");
        var ctx = BuildContext(AuthUser(99, "SystemAdministrator"), permSvc.Object);

        await attr.OnAuthorizationAsync(ctx);

        ctx.Result.Should().BeNull(); // hiç set edilmedi → 200 yolu
        permSvc.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SkipsCheck_ForLookupPermission_EvenWhenUserHasNone()
    {
        var permSvc = new Mock<IPermissionService>(MockBehavior.Strict);
        var attr = new RequirePermissionAttribute("lookup.faculty");
        var ctx = BuildContext(AuthUser(7, "Instructor"), permSvc.Object);

        await attr.OnAuthorizationAsync(ctx);

        ctx.Result.Should().BeNull();
        permSvc.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Returns403_WhenUserMissingPermission()
    {
        var permSvc = new Mock<IPermissionService>();
        permSvc.Setup(p => p.HasPermissionAsync(5, "menu.x.delete", It.IsAny<CancellationToken>()))
               .ReturnsAsync(false);
        var attr = new RequirePermissionAttribute("menu.x.delete");
        var ctx = BuildContext(AuthUser(5, "Student"), permSvc.Object);

        await attr.OnAuthorizationAsync(ctx);

        ctx.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Allows_WhenUserHasPermission()
    {
        var permSvc = new Mock<IPermissionService>();
        permSvc.Setup(p => p.HasPermissionAsync(5, "menu.x.view", It.IsAny<CancellationToken>()))
               .ReturnsAsync(true);
        var attr = new RequirePermissionAttribute("menu.x.view");
        var ctx = BuildContext(AuthUser(5, "Student"), permSvc.Object);

        await attr.OnAuthorizationAsync(ctx);

        ctx.Result.Should().BeNull(); // çağrı pass eder
    }

    [Fact]
    public async Task Returns401_WhenUserIdClaimMissingOrInvalid()
    {
        var permSvc = new Mock<IPermissionService>(MockBehavior.Strict);
        var attr = new RequirePermissionAttribute("menu.x.view");
        // NameIdentifier claim'i olmayan authenticated user
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "u") }, "Test"));
        var ctx = BuildContext(principal, permSvc.Object);

        await attr.OnAuthorizationAsync(ctx);

        ctx.Result.Should().BeOfType<UnauthorizedResult>();
    }
}
