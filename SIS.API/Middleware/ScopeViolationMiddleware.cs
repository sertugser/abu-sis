using SIS.Application.Identity.Exceptions;

namespace SIS.API.Middleware;

public class ScopeViolationMiddleware
{
    private readonly RequestDelegate _next;

    public ScopeViolationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ScopeViolationException ex)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "scope_violation",
                message = ex.Message,
            });
        }
    }
}
