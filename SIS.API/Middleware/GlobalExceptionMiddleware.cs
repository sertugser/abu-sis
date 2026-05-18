using System.Security.Claims;
using System.Text.Json;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Middleware;

/// <summary>
/// Tüm pipeline'da yakalanmayan exception'ları HTTP response'a anlamlı bir JSON gövde olarak
/// çevirir ve AuditLog'a tam bağlam (ex.Message + StackTrace + request meta + userId) yazar.
/// Dev/prod ayrımı sadece response'ta details alanını gizler — mesaj her durumda gönderilir.
///
/// Eşleme (Exception → HTTP status):
///   KeyNotFoundException      → 404
///   ArgumentException         → 400
///   InvalidOperationException → 400  (iş kuralı ihlali)
///   UnauthorizedAccessException → 403
///   diğerleri                 → 500
///
/// Response body: { error, status, type, traceId, details? }
/// Frontend extractError mevcut haliyle data.error'ı okuyor; başka değişiklik gerekmez.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private const int StackTraceMaxLength = 4000;

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var status = MapStatusCode(ex);

        _logger.LogError(
            ex,
            "Unhandled exception for {Method} {Path}. Status={Status}. TraceId={TraceId}",
            context.Request.Method,
            context.Request.Path.Value,
            status,
            context.TraceIdentifier);

        await TryWriteAuditLogAsync(context, ex, status);

        if (context.Response.HasStarted)
        {
            // Response already flushed — sadece logla, body yazma denemesi yapma.
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";

        var body = new Dictionary<string, object?>
        {
            ["error"] = ex.Message,
            ["status"] = status,
            ["type"] = ex.GetType().Name,
            ["traceId"] = context.TraceIdentifier,
        };

        if (_environment.IsDevelopment())
        {
            var stackTrace = ex.StackTrace ?? string.Empty;
            body["details"] = stackTrace.Length > StackTraceMaxLength
                ? stackTrace[..StackTraceMaxLength]
                : stackTrace;
        }

        await context.Response.WriteAsJsonAsync(body, JsonOptions);
    }

    private static int MapStatusCode(Exception ex) => ex switch
    {
        KeyNotFoundException => StatusCodes.Status404NotFound,
        UnauthorizedAccessException => StatusCodes.Status403Forbidden,
        ArgumentException => StatusCodes.Status400BadRequest,
        InvalidOperationException => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status500InternalServerError,
    };

    private async Task TryWriteAuditLogAsync(HttpContext context, Exception ex, int status)
    {
        try
        {
            using var scope = context.RequestServices.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var stackTrace = ex.StackTrace ?? string.Empty;
            var truncatedStack = stackTrace.Length > StackTraceMaxLength
                ? stackTrace[..StackTraceMaxLength]
                : stackTrace;

            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = GetUserId(context),
                Action = "SystemError",
                EntityType = "System",
                EntityId = null,
                OldValue = null,
                NewValue = JsonSerializer.Serialize(new
                {
                    exceptionType = ex.GetType().Name,
                    message = ex.Message,
                    stackTrace = truncatedStack,
                    status,
                    traceId = context.TraceIdentifier,
                    endpoint = context.Request.Path.Value,
                    method = context.Request.Method,
                    queryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
                }, JsonOptions),
                Timestamp = DateTime.UtcNow,
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            });

            await dbContext.SaveChangesAsync();
        }
        catch (Exception logEx)
        {
            // Loglama hatası asıl response'u bozmasın; sadece ILogger'a düş.
            _logger.LogWarning(logEx, "AuditLog yazımı başarısız (asıl exception zaten loglandı).");
        }
    }

    private static int? GetUserId(HttpContext context)
    {
        var claim = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim, out var uid) ? uid : null;
    }
}
