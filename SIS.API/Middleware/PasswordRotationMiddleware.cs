using SIS.Application.Auth;

namespace SIS.API.Middleware;

public class PasswordRotationMiddleware
{
    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/change-password",
        "/api/me/permissions",
    };

    private readonly RequestDelegate _next;

    public PasswordRotationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var isAuthenticated = context.User.Identity?.IsAuthenticated == true;
        var requiresPasswordChange = string.Equals(
            context.User.FindFirst(AuthClaimTypes.PasswordChangeRequired)?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (isAuthenticated && requiresPasswordChange && !AllowedPaths.Contains(context.Request.Path.Value ?? string.Empty))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Sifre degisikligi zorunlu. Devam etmek icin sifrenizi guncelleyin.",
                code = "PasswordChangeRequired"
            });
            return;
        }

        await _next(context);
    }
}
