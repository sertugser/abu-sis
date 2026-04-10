using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

public record AuditLogSummaryDto(
    int Id,
    int? UserId,
    string? UserName,
    string Action,
    string EntityType,
    string? EntityId,
    DateTime Timestamp,
    string? IpAddress);

public record AuditLogDetailDto(
    int Id,
    int? UserId,
    string? UserName,
    string Action,
    string EntityType,
    string? EntityId,
    string? OldValue,
    string? NewValue,
    DateTime Timestamp,
    string? IpAddress);

public record AuditLogPagedResponse(
    List<AuditLogSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

[ApiController]
[Route("api/admin/audit-logs")]
[Authorize(Roles = "SystemAdministrator")]
public class AuditLogController(AppDbContext context) : ControllerBase
{
    private readonly AppDbContext _context = context;

    /// <summary>
    /// Filtrelenebilir, sayfalanmış audit log listesi döner.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] int? userId,
        [FromQuery] string? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var query = BuildFilteredQuery(entityType, entityId, userId, action, from, to);
        var totalCount = await query.CountAsync(ct);

        var rawItems = await query
            .OrderByDescending(al => al.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var userNames = await LoadUserNamesAsync(rawItems, ct);
        var items = rawItems.Select(al => ToSummaryDto(al, userNames)).ToList();

        return Ok(new AuditLogPagedResponse(items, totalCount, page, pageSize));
    }

    /// <summary>
    /// Filtrelenmiş audit log kayıtlarını CSV olarak dışa aktarır.
    /// </summary>
    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] int? userId,
        [FromQuery] string? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
    {
        var logs = await BuildFilteredQuery(entityType, entityId, userId, action, from, to)
            .OrderByDescending(al => al.Timestamp)
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

    private IQueryable<AuditLog> BuildFilteredQuery(
        string? entityType,
        string? entityId,
        int? userId,
        string? action,
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

        if (from.HasValue)
            query = query.Where(al => al.Timestamp >= from.Value.ToUniversalTime());

        if (to.HasValue)
            query = query.Where(al => al.Timestamp <= to.Value.ToUniversalTime());

        return query;
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

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
