using System.Security.Claims;
using System.Text.Json;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Middleware;

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
        try
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
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception for {Method} {Path}. TraceId={TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path.Value,
                httpContext.TraceIdentifier);
            await LogExceptionAsync(httpContext, ex);
            throw;
        }
    }

    private async Task LogExceptionAsync(HttpContext httpContext, Exception ex)
    {
        try
        {
            using var scope = httpContext.RequestServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var userId = GetUserId(httpContext);
            var endpoint = httpContext.Request.Path.Value;
            var method = httpContext.Request.Method;

            context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "SystemError",
                EntityType = "System",
                EntityId = null,
                OldValue = null,
                NewValue = JsonSerializer.Serialize(new
                {
                    exceptionType = ex.GetType().Name,
                    traceId = httpContext.TraceIdentifier,
                    endpoint,
                    method,
                }, JsonOptions),
                Timestamp = DateTime.UtcNow,
                IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            });

            await context.SaveChangesAsync();
        }
        catch
        {
            // Loglama hatasi ana istegi bozmasin
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
        catch
        {
            // Loglama hatasi ana istegi bozmasin
        }
    }

    private static int? GetUserId(HttpContext httpContext)
    {
        var claim = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim, out var uid) ? uid : null;
    }
}
