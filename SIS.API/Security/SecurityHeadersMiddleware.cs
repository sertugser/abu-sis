using Microsoft.Extensions.Hosting;

namespace SIS.API.Security;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "DENY");
            headers.TryAdd("Referrer-Policy", "no-referrer");
            headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            headers.TryAdd("Cross-Origin-Opener-Policy", "same-origin");

            if (!_environment.IsDevelopment())
            {
                headers.TryAdd("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'; base-uri 'none'");
                headers.TryAdd("Cache-Control", "no-store");
                headers.TryAdd("Pragma", "no-cache");
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
