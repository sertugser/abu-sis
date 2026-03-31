using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SIS.Domain.Entities;
using SIS.Domain.Interfaces;

namespace SIS.Infrastructure.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor'ı.
/// Görevleri:
///   1. IHasAuditFields entity'lerinde CreatedAt/UpdatedAt'i otomatik set eder.
///   2. Kritik entity değişikliklerini AuditLogs tablosuna kaydeder.
///
/// Strateji: SavingChanges'DA değişiklikleri yakala (_pendingAuditLogs'a koy)
///           → SavedChanges SONRASI ikinci bir SaveChanges ile audit kayıtlarını yaz.
///           _pendingAuditLogs null'lanarak ikinci çevrim sonsuz döngü oluşturmaz.
///
/// Kayıt tipi: Scoped — her HTTP isteği kendi interceptor örneğini alır,
///             _pendingAuditLogs instance field'ı thread-safe çalışır.
///
/// İzlenen entity'ler:
///   Student, Instructor, Faculty, Department, Course, CourseSection,
///   CourseRegistration, GradeComponent
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    // İzlenecek entity tipleri — beyaz liste stratejisi (performans)
    private static readonly HashSet<Type> TrackedEntityTypes = new()
    {
        typeof(Student),
        typeof(Instructor),
        typeof(Faculty),
        typeof(Department),
        typeof(Course),
        typeof(CourseSection),
        typeof(CourseRegistration),
        typeof(GradeComponent),
    };

    // Hassas alanlar — JSON serializasyona dahil edilmez
    private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "NationalId",
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp",
        "RowVersion",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    // SavingChanges → SavedChanges arası veri taşıyıcısı (Scoped = thread-safe)
    // Tuple ile hem logu hem de asıl entry'yi tutuyoruz ki INSERT durumlarında SavedChanges sonrası oluşan EntityId'yi çekebilelim.
    private List<(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Entry, AuditLog Log)>? _pendingAuditLogs;

    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // ─────────────────────────────────────────────────────────────
    //  SYNC PATH (fallback — bazı araçlar sync SaveChanges kullanır)
    // ─────────────────────────────────────────────────────────────

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            SetTimestamps(eventData.Context);
            _pendingAuditLogs = BuildAuditLogs(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushAuditLogs(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    // ─────────────────────────────────────────────────────────────
    //  ASYNC PATH (primary)
    // ─────────────────────────────────────────────────────────────

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            SetTimestamps(eventData.Context);
            _pendingAuditLogs = BuildAuditLogs(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await FlushAuditLogsAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────
    //  HATA DURUMUNDA TEMİZLİK
    // ─────────────────────────────────────────────────────────────

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pendingAuditLogs = null;
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _pendingAuditLogs = null;
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────
    //  TIMESTAMP OTOMATİK YÖNETİMİ
    // ─────────────────────────────────────────────────────────────

    private static void SetTimestamps(DbContext context)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<IHasAuditFields>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.CreatedAt == default)
                        entry.Entity.CreatedAt = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  AUDIT LOG OLUŞTURMA — SavingChanges SIRASINDA çağrılır
    // ─────────────────────────────────────────────────────────────

    private List<(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Entry, AuditLog Log)> BuildAuditLogs(DbContext context)
    {
        var logs = new List<(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Entry, AuditLog Log)>();
        var now = DateTime.UtcNow;
        var (userId, ipAddress) = GetRequestContext();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            var entityType = entry.Entity.GetType();

            if (!TrackedEntityTypes.Contains(entityType))
                continue;

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var action = entry.State switch
            {
                EntityState.Added    => "INSERT",
                EntityState.Modified => "UPDATE",
                EntityState.Deleted  => "DELETE",
                _                    => "UNKNOWN"
            };

            string? oldValue = null;
            string? newValue = null;

            switch (entry.State)
            {
                case EntityState.Modified:
                {
                    var original = FilterSensitive(entry.Properties
                        .Where(p => p.IsModified)
                        .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue));
                    var current = FilterSensitive(entry.Properties
                        .Where(p => p.IsModified)
                        .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));

                    oldValue = original.Count > 0 ? JsonSerializer.Serialize(original, JsonOptions) : null;
                    newValue = current.Count > 0  ? JsonSerializer.Serialize(current,  JsonOptions) : null;
                    break;
                }
                case EntityState.Added:
                {
                    var values = FilterSensitive(entry.Properties
                        .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));
                    newValue = values.Count > 0 ? JsonSerializer.Serialize(values, JsonOptions) : null;
                    break;
                }
                case EntityState.Deleted:
                {
                    var values = FilterSensitive(entry.Properties
                        .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue));
                    oldValue = values.Count > 0 ? JsonSerializer.Serialize(values, JsonOptions) : null;
                    break;
                }
            }

            var entityId = entry.Properties
                .FirstOrDefault(p => p.Metadata.IsPrimaryKey())
                ?.CurrentValue?.ToString();

            var auditLog = new AuditLog
            {
                UserId     = userId,
                Action     = action,
                EntityType = entityType.Name,
                EntityId   = entityId,
                OldValue   = oldValue,
                NewValue   = newValue,
                Timestamp  = now,
                IpAddress  = ipAddress,
            };

            logs.Add((entry, auditLog));
        }

        return logs;
    }

    // ─────────────────────────────────────────────────────────────
    //  YAZMA — SavedChanges SONRASINDA ikinci kayıt döngüsü
    // ─────────────────────────────────────────────────────────────

    private void FlushAuditLogs(DbContext? context)
    {
        if (context == null || _pendingAuditLogs is not { Count: > 0 })
            return;

        var pending = _pendingAuditLogs;
        _pendingAuditLogs = null; // Sonsuz döngüyü önle

        var logsToSave = new List<AuditLog>();
        foreach (var item in pending)
        {
            // INSERT işleminden sonra oluşan gercek ID'yi al
            if (item.Log.Action == "INSERT" && (string.IsNullOrEmpty(item.Log.EntityId) || item.Log.EntityId == "0" || item.Log.EntityId.StartsWith("-")))
            {
                item.Log.EntityId = item.Entry.Properties
                    .FirstOrDefault(p => p.Metadata.IsPrimaryKey())
                    ?.CurrentValue?.ToString();
            }
            logsToSave.Add(item.Log);
        }

        try
        {
            context.Set<AuditLog>().AddRange(logsToSave);
            context.SaveChanges();
        }
        catch
        {
            // Audit hatası uygulamayı kesmemeli — log at ve devam et
        }
    }

    private async Task FlushAuditLogsAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context == null || _pendingAuditLogs is not { Count: > 0 })
            return;

        var pending = _pendingAuditLogs;
        _pendingAuditLogs = null; // Sonsuz döngüyü önle

        var logsToSave = new List<AuditLog>();
        foreach (var item in pending)
        {
            // INSERT işleminden sonra oluşan gercek ID'yi al
            if (item.Log.Action == "INSERT" && (string.IsNullOrEmpty(item.Log.EntityId) || item.Log.EntityId == "0" || item.Log.EntityId.StartsWith("-")))
            {
                item.Log.EntityId = item.Entry.Properties
                    .FirstOrDefault(p => p.Metadata.IsPrimaryKey())
                    ?.CurrentValue?.ToString();
            }
            logsToSave.Add(item.Log);
        }

        try
        {
            context.Set<AuditLog>().AddRange(logsToSave);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Audit hatası uygulamayı kesmemeli — log at ve devam et
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  YARDIMCI METODLAR
    // ─────────────────────────────────────────────────────────────

    private (int? userId, string? ipAddress) GetRequestContext()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return (null, null);

        int? userId = null;
        var claim = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(claim, out var parsedId))
            userId = parsedId;

        var ip = httpContext.Connection.RemoteIpAddress?.ToString()
            ?? httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();

        return (userId, ip);
    }

    private static Dictionary<string, object?> FilterSensitive(Dictionary<string, object?> source)
        => source.Where(kv => !SensitiveProperties.Contains(kv.Key))
                 .ToDictionary(kv => kv.Key, kv => kv.Value);
}
