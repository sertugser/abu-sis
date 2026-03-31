using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

public record SystemConfigDto(int Id, string Key, string Value, string? Description, string Category, string DataType);
public record UpdateSystemConfigDto(string Value);

[ApiController]
[Route("api/admin/system-config")]
[Authorize(Roles = "SystemAdministrator")]
public class SystemConfigController(AppDbContext context) : ControllerBase
{
    private readonly AppDbContext _context = context;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? category, CancellationToken ct)
    {
        var query = _context.SystemConfigs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(c => c.Category == category);

        var configs = await query
            .OrderBy(c => c.Category).ThenBy(c => c.Key)
            .Select(c => new SystemConfigDto(c.Id, c.Key, c.Value, c.Description, c.Category, c.DataType))
            .ToListAsync(ct);

        return Ok(configs);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var config = await _context.SystemConfigs.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new SystemConfigDto(c.Id, c.Key, c.Value, c.Description, c.Category, c.DataType))
            .FirstOrDefaultAsync(ct);

        if (config == null)
            return NotFound(new { error = "Konfigürasyon bulunamadı." });

        return Ok(config);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSystemConfigDto dto, CancellationToken ct)
    {
        var config = await _context.SystemConfigs.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (config == null)
            return NotFound(new { error = "Konfigürasyon bulunamadı." });

        config.Value = dto.Value;
        await _context.SaveChangesAsync(ct);

        return Ok(new SystemConfigDto(config.Id, config.Key, config.Value, config.Description, config.Category, config.DataType));
    }
}
