using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

public record AuditLogSummaryDto(
    int Id,
    int? UserId,
    string Action,
    string EntityType,
    string? EntityId,
    DateTime Timestamp,
    string? IpAddress);

public record AuditLogDetailDto(
    int Id,
    int? UserId,
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

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(al => al.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(al => new AuditLogSummaryDto(
                al.Id,
                al.UserId,
                al.Action,
                al.EntityType,
                al.EntityId,
                al.Timestamp,
                al.IpAddress))
            .ToListAsync(ct);

        return Ok(new AuditLogPagedResponse(items, totalCount, page, pageSize));
    }

    /// <summary>
    /// Tek bir audit log kaydını tam OldValue/NewValue JSON'ıyla döner.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var log = await _context.AuditLogs.AsNoTracking()
            .Where(al => al.Id == id)
            .Select(al => new AuditLogDetailDto(
                al.Id,
                al.UserId,
                al.Action,
                al.EntityType,
                al.EntityId,
                al.OldValue,
                al.NewValue,
                al.Timestamp,
                al.IpAddress))
            .FirstOrDefaultAsync(ct);

        if (log is null)
            return NotFound(new { error = "Audit log kaydı bulunamadı." });

        return Ok(log);
    }
}
