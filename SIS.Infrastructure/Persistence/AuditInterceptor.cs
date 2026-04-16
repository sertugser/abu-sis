using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence;

/// <summary>
/// EF Core SaveChanges interceptor that automatically logs changes to critical entities
/// into the AuditLog table. Captures old/new values for Modified entities, new values
/// for Added, and old values for Deleted.
/// </summary>
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    // Entity types that should be audited
    private static readonly HashSet<string> AuditedEntityTypes = new(StringComparer.Ordinal)
    {
        nameof(ApplicationUser),
        nameof(AcademicTerm),
        nameof(CourseSection),
        nameof(CourseRegistration),
        nameof(Student),
        nameof(SystemConfig),
        nameof(FacultyScheduleApproval),
        nameof(PromotionRule),
        nameof(RegistrationPlan),
        nameof(CourseAccessPolicy),
        nameof(ClassroomTermAssignment),
        nameof(Course),
        nameof(Curriculum),
        nameof(StudentGrade),       // Not degisiklikleri
        nameof(GradeComponent),     // Not bilesenleri
        nameof(GradingCriteria),    // Degerlendirme kriterleri
    };

    // Properties that must never appear in audit logs (security)
    private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp",
        "NormalizedEmail",
        "NormalizedUserName",
        "RowVersion",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // AsyncLocal ensures thread-safety: each async flow gets its own list
    private static readonly AsyncLocal<List<AuditEntry>?> _pendingAuditEntries = new();

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            _pendingAuditEntries.Value = CollectAuditEntries(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            _pendingAuditEntries.Value = CollectAuditEntries(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(
        SaveChangesCompletedEventData eventData,
        int result)
    {
        if (eventData.Context is not null && _pendingAuditEntries.Value is { Count: > 0 })
        {
            WriteAuditLogs(eventData.Context);
        }

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null && _pendingAuditEntries.Value is { Count: > 0 })
        {
            await WriteAuditLogsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private List<AuditEntry> CollectAuditEntries(DbContext context)
    {
        var entries = new List<AuditEntry>();
        var userId = GetCurrentUserId();
        var ipAddress = GetCurrentIpAddress();
        var timestamp = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            var entityTypeName = entry.Entity.GetType().Name;
            if (!AuditedEntityTypes.Contains(entityTypeName))
                continue;

            // Skip AuditLog entity itself to avoid infinite recursion
            if (entry.Entity is AuditLog)
                continue;

            var auditEntry = new AuditEntry
            {
                EntityType = entityTypeName,
                EntityId = GetPrimaryKeyValue(entry),
                Action = entry.State switch
                {
                    EntityState.Added => "Created",
                    EntityState.Modified => "Modified",
                    EntityState.Deleted => "Deleted",
                    _ => entry.State.ToString()
                },
                UserId = userId,
                IpAddress = ipAddress,
                Timestamp = timestamp,
            };

            switch (entry.State)
            {
                case EntityState.Added:
                    auditEntry.NewValues = GetPropertyValues(entry, e => e.CurrentValues);
                    break;

                case EntityState.Deleted:
                    auditEntry.OldValues = GetPropertyValues(entry, e => e.OriginalValues);
                    break;

                case EntityState.Modified:
                    var oldValues = new Dictionary<string, object?>();
                    var newValues = new Dictionary<string, object?>();

                    foreach (var prop in entry.Properties)
                    {
                        if (SensitiveProperties.Contains(prop.Metadata.Name))
                            continue;

                        if (!prop.IsModified)
                            continue;

                        var propName = prop.Metadata.Name;
                        oldValues[propName] = prop.OriginalValue;
                        newValues[propName] = prop.CurrentValue;
                    }

                    if (oldValues.Count > 0)
                    {
                        auditEntry.OldValues = oldValues;
                        auditEntry.NewValues = newValues;
                    }
                    else
                    {
                        // No actual property changes worth logging
                        continue;
                    }
                    break;
            }

            entries.Add(auditEntry);
        }

        return entries;
    }

    private static Dictionary<string, object?> GetPropertyValues(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        Func<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry, Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues> valuesSelector)
    {
        var values = new Dictionary<string, object?>();
        var propertyValues = valuesSelector(entry);

        foreach (var prop in entry.Properties)
        {
            if (SensitiveProperties.Contains(prop.Metadata.Name))
                continue;

            // Skip navigation properties — only scalar/owned
            if (prop.Metadata.IsShadowProperty())
                continue;

            values[prop.Metadata.Name] = propertyValues[prop.Metadata];
        }

        return values;
    }

    private static string? GetPrimaryKeyValue(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var keyProperties = entry.Metadata.FindPrimaryKey()?.Properties;
        if (keyProperties is null || keyProperties.Count == 0)
            return null;

        if (keyProperties.Count == 1)
        {
            var value = entry.Property(keyProperties[0].Name).CurrentValue;
            return value?.ToString();
        }

        // Composite key
        var parts = keyProperties.Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "null");
        return string.Join("_", parts);
    }

    private int? GetCurrentUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
            return null;

        var userIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                       ?? httpContext.User.FindFirst("sub");

        if (userIdClaim is not null && int.TryParse(userIdClaim.Value, out var userId))
            return userId;

        return null;
    }

    private string? GetCurrentIpAddress()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        return httpContext?.Connection?.RemoteIpAddress?.ToString();
    }

    private void WriteAuditLogs(DbContext context)
    {
        if (_pendingAuditEntries.Value is null)
            return;

        var auditLogs = _pendingAuditEntries.Value.Select(ToAuditLog).ToList();
        _pendingAuditEntries.Value = null;

        context.Set<AuditLog>().AddRange(auditLogs);
        // Use base SaveChanges to avoid re-triggering interceptor for audit entries
        // The interceptor will fire again but AuditLog entities are skipped via the type check
        context.SaveChanges();
    }

    private async Task WriteAuditLogsAsync(DbContext context, CancellationToken ct)
    {
        if (_pendingAuditEntries.Value is null)
            return;

        var auditLogs = _pendingAuditEntries.Value.Select(ToAuditLog).ToList();
        _pendingAuditEntries.Value = null;

        context.Set<AuditLog>().AddRange(auditLogs);
        await context.SaveChangesAsync(ct);
    }

    private static AuditLog ToAuditLog(AuditEntry entry)
    {
        return new AuditLog
        {
            UserId = entry.UserId,
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            OldValue = entry.OldValues is not null ? JsonSerializer.Serialize(entry.OldValues, JsonOptions) : null,
            NewValue = entry.NewValues is not null ? JsonSerializer.Serialize(entry.NewValues, JsonOptions) : null,
            Timestamp = entry.Timestamp,
            IpAddress = entry.IpAddress,
        };
    }

    /// <summary>
    /// Temporary holder for audit data collected before SaveChanges completes.
    /// </summary>
    private sealed class AuditEntry
    {
        public string EntityType { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public string Action { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string? IpAddress { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object?>? OldValues { get; set; }
        public Dictionary<string, object?>? NewValues { get; set; }
    }
}
