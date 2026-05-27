using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.API.Filters;
using SIS.Application.Admin.DTOs;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/audit-logs")]
[Authorize]
public class AuditLogController(AppDbContext context) : ControllerBase
{
    private readonly AppDbContext _context = context;

    private const int MaxExportRows = 50_000;

    // Whitelist for sortable columns — prevents injection via dynamic ordering
    private static readonly HashSet<string> AllowedSortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Timestamp", "Action", "EntityType", "UserId"
    };

    /// <summary>
    /// Filtrelenebilir, sayfalanmış, sıralanabilir audit log listesi döner.
    /// </summary>
    // Not: Rutin listeleme her sayfa render/yenileme'de çağrıldığı için "hassas veri erişimi"
    // olarak loglanmaz (gürültü olur). Erişim loglaması tek-kayıt detay ve toplu export'ta yapılır.
    [HttpGet]
    [RequirePermission("menu.audit-log.view")]
    [EnableRateLimiting("AuditLogRead")]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] int? userId,
        [FromQuery] string? action,
        [FromQuery] string? search,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var query = BuildFilteredQuery(entityType, entityId, userId, action, search, from, to);
        var totalCount = await query.CountAsync(ct);

        query = ApplySorting(query, sortBy, sortDir);

        var rawItems = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var userNames = await LoadUserNamesAsync(rawItems, ct);
        var items = rawItems.Select(al => ToSummaryDto(al, userNames)).ToList();

        return Ok(new AuditLogPagedResponse(items, totalCount, page, pageSize));
    }

    /// <summary>
    /// Filtrelenmiş audit log kayıtlarını CSV olarak dışa aktarır (max 50.000 satır).
    /// </summary>
    [HttpGet("export/csv")]
    [RequirePermission("menu.audit-log.export")]
    [EnableRateLimiting("AuditLogExport")]
    [ServiceFilter(typeof(SensitiveDataAccessFilter))]
    [SensitiveDataAccess("AuditLog")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] int? userId,
        [FromQuery] string? action,
        [FromQuery] string? search,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
    {
        var query = BuildFilteredQuery(entityType, entityId, userId, action, search, from, to);

        // Pre-check count to avoid loading too much data
        var totalCount = await query.CountAsync(ct);
        if (totalCount > MaxExportRows)
        {
            return BadRequest(new
            {
                error = $"Toplam {totalCount:N0} kayıt var. CSV export en fazla {MaxExportRows:N0} satır destekler. " +
                        "Lütfen filtreleri daraltın veya tarih aralığını kısaltın."
            });
        }

        var logs = await query
            .OrderByDescending(al => al.Timestamp)
            .Take(MaxExportRows)
            .ToListAsync(ct);

        var userNames = await LoadUserNamesAsync(logs, ct);

        var csv = new StringBuilder();
        csv.AppendLine("Id,Timestamp,UserId,UserName,Action,EntityType,EntityId,IpAddress,OldValue,NewValue");

        foreach (var log in logs)
        {
            csv.AppendLine(string.Join(",",
                EscapeCsv(log.Id.ToString()),
                EscapeCsv(log.Timestamp.ToString("O")),
                EscapeCsv(log.UserId?.ToString()),
                EscapeCsv(log.UserId.HasValue && userNames.TryGetValue(log.UserId.Value, out var userName) ? userName : null),
                EscapeCsv(log.Action),
                EscapeCsv(log.EntityType),
                EscapeCsv(log.EntityId),
                EscapeCsv(log.IpAddress),
                EscapeCsv(log.OldValue),
                EscapeCsv(log.NewValue)));
        }

        var fileName = $"audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", fileName);
    }

    /// <summary>
    /// Tek bir audit log kaydını tam OldValue/NewValue JSON'ıyla döner.
    /// </summary>
    [HttpGet("{id:int}")]
    [RequirePermission("menu.audit-log.view")]
    [EnableRateLimiting("AuditLogRead")]
    [ServiceFilter(typeof(SensitiveDataAccessFilter))]
    [SensitiveDataAccess("AuditLog", EntityIdRouteParam = "id")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var al = await _context.AuditLogs.AsNoTracking()
            .Where(a => a.Id == id)
            .FirstOrDefaultAsync(ct);

        if (al is null)
            return NotFound(new { error = "Audit log kaydı bulunamadı." });

        string? userName = null;
        if (al.UserId.HasValue)
        {
            userName = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == al.UserId.Value)
                .Select(u => u.FirstName + " " + u.LastName)
                .FirstOrDefaultAsync(ct);
        }

        var log = new AuditLogDetailDto(
            al.Id, al.UserId, userName, al.Action, al.EntityType,
            al.EntityId, al.OldValue, al.NewValue, al.Timestamp, al.IpAddress);

        return Ok(log);
    }

    /// <summary>
    /// Son 24 saatte tespit edilen anomalileri döner.
    /// </summary>
    [HttpGet("anomalies")]
    [RequirePermission("menu.audit-log.view")]
    [EnableRateLimiting("AuditLogRead")]
    public async Task<IActionResult> GetAnomalies(CancellationToken ct)
    {
        var anomalies = new List<AuditLogAnomalyDto>();
        var since = DateTime.UtcNow.AddHours(-24);

        // 1. Ardışık başarısız giriş: Aynı IP'den 5+ başarısız giriş
        var failedLoginsByIp = await _context.AuditLogs
            .AsNoTracking()
            .Where(al => al.Timestamp >= since && al.Action == "LoginFailed" && al.IpAddress != null)
            .GroupBy(al => al.IpAddress)
            .Where(g => g.Count() >= 5)
            .Select(g => new { Ip = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        foreach (var item in failedLoginsByIp)
        {
            anomalies.Add(new AuditLogAnomalyDto(
                "ConsecutiveFailedLogins",
                $"{item.Ip} adresinden {item.Count} başarısız giriş denemesi",
                item.Count >= 10 ? "critical" : "warning",
                item.Count,
                DateTime.UtcNow));
        }

        // 2. Toplu silme: Aynı kullanıcıdan 1 saatte 10+ silme
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        var massDeletes = await _context.AuditLogs
            .AsNoTracking()
            .Where(al => al.Timestamp >= oneHourAgo && al.Action == "Deleted" && al.UserId != null)
            .GroupBy(al => al.UserId)
            .Where(g => g.Count() >= 10)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        foreach (var item in massDeletes)
        {
            anomalies.Add(new AuditLogAnomalyDto(
                "MassDeletion",
                $"Kullanıcı #{item.UserId} son 1 saatte {item.Count} kayıt sildi",
                "critical",
                item.Count,
                DateTime.UtcNow));
        }

        // 3. Yüksek sistem hatası: Son 1 saatte 5+ hata
        var recentErrors = await _context.AuditLogs
            .AsNoTracking()
            .Where(al => al.Timestamp >= oneHourAgo && al.Action == "SystemError")
            .CountAsync(ct);

        if (recentErrors >= 5)
        {
            anomalies.Add(new AuditLogAnomalyDto(
                "HighErrorRate",
                $"Son 1 saatte {recentErrors} sistem hatası",
                recentErrors >= 20 ? "critical" : "warning",
                recentErrors,
                DateTime.UtcNow));
        }

        return Ok(anomalies);
    }

    /* ── Private Helpers ── */

    private IQueryable<AuditLog> BuildFilteredQuery(
        string? entityType,
        string? entityId,
        int? userId,
        string? action,
        string? search,
        DateTime? from,
        DateTime? to)
    {
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(al => al.EntityType == entityType);

        if (!string.IsNullOrWhiteSpace(entityId))
            query = query.Where(al => al.EntityId == entityId);

        if (userId.HasValue)
            query = query.Where(al => al.UserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(al => al.Action == action);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Case-insensitive arama (ILike) + LIKE wildcard'larını ('%', '_', '\') escape et,
            // böylece kullanıcının girdiği karakterler literal olarak aranır.
            var pattern = $"%{EscapeLikePattern(search)}%";
            query = query.Where(al =>
                (al.OldValue != null && EF.Functions.ILike(al.OldValue, pattern)) ||
                (al.NewValue != null && EF.Functions.ILike(al.NewValue, pattern)));
        }

        // Tarih filtreleri kullanıcının seçtiği Türkiye takvim günü olarak yorumlanır (sunucu TZ'inden
        // bağımsız). from → günün Türkiye 00:00'ı (UTC), to → ertesi günün Türkiye 00:00'ı (UTC, dışlayan).
        if (from.HasValue)
            query = query.Where(al => al.Timestamp >= TurkeyDateStartUtc(from.Value));

        if (to.HasValue)
            query = query.Where(al => al.Timestamp < TurkeyDateStartUtc(to.Value.AddDays(1)));

        return query;
    }

    // Türkiye kalıcı olarak UTC+3 (2016'dan beri yaz saati uygulaması yok).
    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);

    /// <summary>
    /// Verilen tarihin Türkiye yerel gün başlangıcını (00:00 +03:00) UTC'ye çevirir.
    /// Sunucu saat diliminden bağımsız deterministik sonuç verir.
    /// </summary>
    private static DateTime TurkeyDateStartUtc(DateTime date) =>
        new DateTimeOffset(DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified), TurkeyOffset).UtcDateTime;

    /// <summary>
    /// LIKE/ILIKE özel karakterlerini (\, %, _) escape eder — kullanıcı girdisi literal aranır.
    /// </summary>
    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static IQueryable<AuditLog> ApplySorting(IQueryable<AuditLog> query, string? sortBy, string? sortDir)
    {
        var column = AllowedSortColumns.Contains(sortBy ?? "") ? sortBy! : "Timestamp";
        var ascending = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

        return column switch
        {
            "Action" => ascending ? query.OrderBy(al => al.Action) : query.OrderByDescending(al => al.Action),
            "EntityType" => ascending ? query.OrderBy(al => al.EntityType) : query.OrderByDescending(al => al.EntityType),
            "UserId" => ascending ? query.OrderBy(al => al.UserId) : query.OrderByDescending(al => al.UserId),
            _ => ascending ? query.OrderBy(al => al.Timestamp) : query.OrderByDescending(al => al.Timestamp),
        };
    }

    private async Task<Dictionary<int, string>> LoadUserNamesAsync(IEnumerable<AuditLog> logs, CancellationToken ct)
    {
        var userIds = logs
            .Where(al => al.UserId.HasValue)
            .Select(al => al.UserId!.Value)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
            return new Dictionary<int, string>();

        return await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.FirstName + " " + u.LastName).Trim(), ct);
    }

    private static AuditLogSummaryDto ToSummaryDto(AuditLog log, IReadOnlyDictionary<int, string> userNames)
    {
        var userName = log.UserId.HasValue && userNames.TryGetValue(log.UserId.Value, out var name)
            ? name
            : null;

        return new AuditLogSummaryDto(
            log.Id,
            log.UserId,
            userName,
            log.Action,
            log.EntityType,
            log.EntityId,
            log.Timestamp,
            log.IpAddress);
    }

    /// <summary>
    /// CSV injection korumalı escape fonksiyonu.
    /// =, +, -, @, tab, CR ile başlayan değerlere tek tırnak prefix'i ekler.
    /// </summary>
    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        var escaped = value.Replace("\"", "\"\"");

        // CSV injection protection: prefix formula-triggering characters
        if (escaped.Length > 0 && "=+-@\t\r".Contains(escaped[0]))
            escaped = "'" + escaped;

        return $"\"{escaped}\"";
    }
}
