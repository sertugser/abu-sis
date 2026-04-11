using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Filters;

public class SensitiveDataAccessFilter : IAsyncActionFilter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executedContext = await next();

        if (context.HttpContext.Request.Method != "GET")
            return;

        if (executedContext.Result is not ObjectResult { StatusCode: null or (>= 200 and < 300) })
            return;

        var attr = context.ActionDescriptor.EndpointMetadata
            .OfType<SensitiveDataAccessAttribute>()
            .FirstOrDefault();

        if (attr is null)
            return;

        try
        {
            using var scope = context.HttpContext.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var entityId = attr.EntityIdRouteParam != null
                && context.RouteData.Values.TryGetValue(attr.EntityIdRouteParam, out var routeVal)
                    ? routeVal?.ToString()
                    : null;

            var endpoint = context.HttpContext.Request.Path.Value;
            var method = context.HttpContext.Request.Method;

            db.AuditLogs.Add(new AuditLog
            {
                UserId = userId != null && int.TryParse(userId, out var uid) ? uid : null,
                Action = "DataAccess",
                EntityType = attr.EntityType,
                EntityId = entityId,
                OldValue = null,
                NewValue = JsonSerializer.Serialize(
                    new { endpoint, method }, JsonOptions),
                Timestamp = DateTime.UtcNow,
                IpAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            });

            await db.SaveChangesAsync();
        }
        catch
        {
            // Loglama hatasi ana istegi bozmasin
        }
    }
}
