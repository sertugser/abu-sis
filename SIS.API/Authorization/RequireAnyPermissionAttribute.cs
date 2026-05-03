using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SIS.Application.Identity.Services;

namespace SIS.API.Authorization;

/// <summary>
/// Allows access when the authenticated user has at least one of the supplied permissions.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RequireAnyPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private const string SystemAdminRole = "SystemAdministrator";
    private const string LookupPrefix = "lookup.";
    private readonly string[] _permissionKeys;

    public RequireAnyPermissionAttribute(params string[] permissionKeys)
    {
        if (permissionKeys.Length == 0)
            throw new ArgumentException("At least one permission key is required.", nameof(permissionKeys));

        _permissionKeys = permissionKeys;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (!(user.Identity?.IsAuthenticated ?? false))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (user.IsInRole(SystemAdminRole))
            return;

        if (_permissionKeys.Any(key => key.StartsWith(LookupPrefix, StringComparison.Ordinal)))
            return;

        var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var permissionService = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();
        foreach (var key in _permissionKeys)
        {
            if (await permissionService.HasPermissionAsync(userId, key))
                return;
        }

        context.Result = new ForbidResult();
    }
}
