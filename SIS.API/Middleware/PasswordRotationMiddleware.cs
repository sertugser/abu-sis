using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.API.Middleware;

public sealed class PasswordRotationMiddleware
{
    private static readonly string[] AllowedWhenExpired =
    [
        "/api/auth/login",
        "/api/auth/refresh",
        "/api/auth/change-password",
        "/api/me/permissions",
        "/api/me/menu",
        "/api/runtime-settings/session",
    ];

    private readonly RequestDelegate _next;

    public PasswordRotationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        PasswordRotationService passwordRotationService)
    {
        if (!ShouldValidatePasswordRotation(context))
        {
            await _next(context);
            return;
        }

        var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            await _next(context);
            return;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            await _next(context);
            return;
        }

        var status = await passwordRotationService.GetStatusAsync(user, context.RequestAborted);
        if (!status.RequiresPasswordChange)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            code = "PasswordExpired",
            error = "Sifre degistirme suresi doldu. Lutfen sifrenizi yenileyin.",
            passwordExpiresAt = status.PasswordExpiresAtUtc,
        }, context.RequestAborted);
    }

    private static bool ShouldValidatePasswordRotation(HttpContext context)
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
            return false;

        var path = context.Request.Path.Value;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            return false;

        return !AllowedWhenExpired.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
