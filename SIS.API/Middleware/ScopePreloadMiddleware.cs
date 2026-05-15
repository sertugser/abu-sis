using SIS.Application.Identity.Services;

namespace SIS.API.Middleware;

/// <summary>
/// Query filter'ların doğru request-scope değerini kullanabilmesi için scope lookup'ını erken tetikler.
/// </summary>
public class ScopePreloadMiddleware
{
    private readonly RequestDelegate _next;

    public ScopePreloadMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUserScope scope)
    {
        await scope.IsActiveAsync();
        await _next(context);
    }
}
