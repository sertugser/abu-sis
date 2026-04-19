using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Constraints;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/constraints/profiles")]
[Authorize(Roles = "DepartmentManager,Dean,AdministrativeStaff,SystemAdministrator")]
public class ConstraintProfileController : ControllerBase
{
    private readonly AppDbContext _context;

    public ConstraintProfileController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var profiles = await _context.ConstraintProfiles
            .AsNoTracking()
            .OrderByDescending(p => p.IsDefault).ThenBy(p => p.Name)
            .Select(p => new ConstraintProfileDto(
                p.Id, p.Name, p.Description, p.IsDefault,
                p.CreatedByUserId, p.CreatedAt, p.Items.Count))
            .ToListAsync(ct);
        return Ok(profiles);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var profile = await _context.ConstraintProfiles
            .AsNoTracking()
            .Include(p => p.Items).ThenInclude(i => i.ConstraintDefinition)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (profile == null) return NotFound();

        var dto = new ConstraintProfileDetailDto(
            profile.Id, profile.Name, profile.Description, profile.IsDefault,
            profile.CreatedByUserId, profile.CreatedAt,
            profile.Items.OrderBy(i => i.ConstraintDefinition.SortOrder).Select(i => new ConstraintProfileItemDto(
                i.Id, i.ConstraintDefinitionId, i.ConstraintDefinition.Code, i.ConstraintDefinition.Name,
                i.IsEnabled, i.Category, i.Weight, i.Parameters)).ToList());
        return Ok(dto);
    }

    [HttpPost("from-current")]
    public async Task<IActionResult> CreateFromCurrent([FromBody] CreateProfileRequest request, CancellationToken ct)
    {
        var definitions = await _context.ConstraintDefinitions.AsNoTracking().ToListAsync(ct);
        var profile = new ConstraintProfile
        {
            Name = request.Name,
            Description = request.Description,
            CreatedByUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            Items = definitions.Select(d => new ConstraintProfileItem
            {
                ConstraintDefinitionId = d.Id,
                IsEnabled = d.IsEnabled,
                Category = d.Category,
                Weight = d.Weight,
                Parameters = d.Parameters,
            }).ToList(),
        };
        _context.ConstraintProfiles.Add(profile);
        await _context.SaveChangesAsync(ct);
        return Ok(new { profile.Id, profile.Name });
    }

    [HttpPost("{id:int}/apply")]
    public async Task<IActionResult> Apply(int id, CancellationToken ct)
    {
        var profile = await _context.ConstraintProfiles
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (profile == null) return NotFound();

        // Auto-save current state before applying
        var definitions = await _context.ConstraintDefinitions.ToListAsync(ct);
        var autoSave = new ConstraintProfile
        {
            Name = $"Otomatik Yedek — {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
            Description = $"'{profile.Name}' profili uygulanmadan önceki otomatik yedek",
            CreatedByUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            Items = definitions.Select(d => new ConstraintProfileItem
            {
                ConstraintDefinitionId = d.Id,
                IsEnabled = d.IsEnabled,
                Category = d.Category,
                Weight = d.Weight,
                Parameters = d.Parameters,
            }).ToList(),
        };
        _context.ConstraintProfiles.Add(autoSave);

        // Apply profile items to definitions
        var itemMap = profile.Items.ToDictionary(i => i.ConstraintDefinitionId);
        int changed = 0;
        foreach (var def in definitions)
        {
            if (!itemMap.TryGetValue(def.Id, out var item)) continue;
            if (def.IsEnabled != item.IsEnabled || def.Category != item.Category ||
                def.Weight != item.Weight || def.Parameters != item.Parameters)
            {
                def.IsEnabled = item.IsEnabled;
                def.Category = item.Category;
                def.Weight = item.Weight;
                def.Parameters = item.Parameters;
                def.UpdatedAt = DateTime.UtcNow;
                changed++;
            }
        }

        await _context.SaveChangesAsync(ct);
        return Ok(new ApplyProfileResult(changed, autoSave.Name));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var profile = await _context.ConstraintProfiles.FindAsync(new object[] { id }, ct);
        if (profile == null) return NotFound();
        if (profile.IsDefault) return BadRequest("Varsayılan profil silinemez.");

        _context.ConstraintProfiles.Remove(profile);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }
}
