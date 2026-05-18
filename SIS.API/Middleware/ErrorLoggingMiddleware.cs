using System.Security.Claims;
using System.Text.Json;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Middleware;

/// <summary>
/// Sadece 401/403 status code'larını AuditLog'a yazar. Unhandled exception'lar
/// pipeline'da daha üstteki <see cref="GlobalExceptionMiddleware"/> tarafından
/// yakalanır; burada exception handling yapılmaz.
/// </summary>
public class ErrorLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorLoggingMiddleware> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public ErrorLoggingMiddleware(RequestDelegate next, ILogger<ErrorLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        await _next(httpContext);

        if (httpContext.Response.StatusCode is 401 or 403)
        {
            // Login endpoint kendi LoginFailed logunu yaziyor, cifte log olmasin
            var path = httpContext.Request.Path.Value;
            if (path != null && path.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
                return;

            await LogStatusCodeAsync(httpContext);
        }
    }

    private async Task LogStatusCodeAsync(HttpContext httpContext)
    {
        try
        {
            using var scope = httpContext.RequestServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var userId = GetUserId(httpContext);
            var action = httpContext.Response.StatusCode == 401 ? "Unauthorized" : "Forbidden";

            context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityType = "System",
                EntityId = null,
                OldValue = null,
                NewValue = JsonSerializer.Serialize(new
                {
                    endpoint = httpContext.Request.Path.Value,
                    method = httpContext.Request.Method,
                }, JsonOptions),
                Timestamp = DateTime.UtcNow,
                IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            });

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Loglama hatasi ana istegi bozmasin
            _logger.LogWarning(ex, "ErrorLoggingMiddleware: status code log yazılamadı.");
        }
    }

    private static int? GetUserId(HttpContext httpContext)
    {
        var claim = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim, out var uid) ? uid : null;
    }
}
