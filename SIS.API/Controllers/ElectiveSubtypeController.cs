using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

/// <summary>
/// Faz B B3a — DB-driven seçmeli ders tipleri yönetimi.
/// CRUD yetkisi sadece SystemAdministrator'da (Karar #1). Code immutable (Karar #14).
/// Hard delete YOK — sadece soft delete (IsActive=false) (Karar #2).
/// </summary>
[ApiController]
[Route("api/elective-subtypes")]
[Authorize]
public class ElectiveSubtypeController(AppDbContext context) : ControllerBase
{
    public record ElectiveSubtypeDto(
        int Id, string Code, string Name,
        bool RequiresFaculty, bool ForbidsFaculty,
        bool RequiresDepartment, bool ForbidsDepartment,
        bool IsActive, int SortOrder);

    public record CreateElectiveSubtypeRequest(
        string Code, string Name,
        bool RequiresFaculty = false, bool ForbidsFaculty = false,
        bool RequiresDepartment = false, bool ForbidsDepartment = false,
        int SortOrder = 0);

    public record UpdateElectiveSubtypeRequest(
        string? Name = null,
        bool? RequiresFaculty = null, bool? ForbidsFaculty = null,
        bool? RequiresDepartment = null, bool? ForbidsDepartment = null,
        bool? IsActive = null,
        int? SortOrder = null);
    // NOT: Code immutable (Karar #14) — Update DTO'da Code alanı YOK.

    [HttpGet]
    // Faz B B3c-3: Patch 2A pattern — read-only lookup multi-role'a açık.
    // Karar #1 (memory) sadece CRUD'u SysAdmin'e kısıtlar; List sadece okuma,
    // ElectiveGroup form'u açan herkes (Dean, BölümBaşkanı) tip dropdown'unu görür.
    public async Task<IActionResult> List([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var query = context.ElectiveSubtypes.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        var list = await query
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Code)
            .Select(s => new ElectiveSubtypeDto(
                s.Id, s.Code, s.Name,
                s.RequiresFaculty, s.ForbidsFaculty,
                s.RequiresDepartment, s.ForbidsDepartment,
                s.IsActive, s.SortOrder))
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("menu.secmeli-tipleri.create")]
    public async Task<IActionResult> Create([FromBody] CreateElectiveSubtypeRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Code))
        {
            return BadRequest(new { message = "Kod boş olamaz." });
        }
        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return BadRequest(new { message = "Ad boş olamaz." });
        }

        var trimmedCode = req.Code.Trim();
        var codeExists = await context.ElectiveSubtypes.AnyAsync(s => s.Code == trimmedCode, ct);
        if (codeExists)
        {
            return BadRequest(new { message = $"'{trimmedCode}' kodu zaten mevcut." });
        }

        var entity = new ElectiveSubtypeEntity
        {
            Code = trimmedCode,
            Name = req.Name.Trim(),
            RequiresFaculty = req.RequiresFaculty,
            ForbidsFaculty = req.ForbidsFaculty,
            RequiresDepartment = req.RequiresDepartment,
            ForbidsDepartment = req.ForbidsDepartment,
            SortOrder = req.SortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        context.ElectiveSubtypes.Add(entity);
        await context.SaveChangesAsync(ct);

        return Ok(new ElectiveSubtypeDto(
            entity.Id, entity.Code, entity.Name,
            entity.RequiresFaculty, entity.ForbidsFaculty,
            entity.RequiresDepartment, entity.ForbidsDepartment,
            entity.IsActive, entity.SortOrder));
    }

    [HttpPut("{id:int}")]
    [RequirePermission("menu.secmeli-tipleri.edit")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateElectiveSubtypeRequest req, CancellationToken ct)
    {
        var entity = await context.ElectiveSubtypes.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (entity == null)
        {
            return NotFound(new { message = "Tip bulunamadı." });
        }

        if (req.Name != null)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return BadRequest(new { message = "Ad boş olamaz." });
            }
            entity.Name = req.Name.Trim();
        }
        if (req.RequiresFaculty.HasValue) entity.RequiresFaculty = req.RequiresFaculty.Value;
        if (req.ForbidsFaculty.HasValue) entity.ForbidsFaculty = req.ForbidsFaculty.Value;
        if (req.RequiresDepartment.HasValue) entity.RequiresDepartment = req.RequiresDepartment.Value;
        if (req.ForbidsDepartment.HasValue) entity.ForbidsDepartment = req.ForbidsDepartment.Value;
        if (req.IsActive.HasValue) entity.IsActive = req.IsActive.Value;
        if (req.SortOrder.HasValue) entity.SortOrder = req.SortOrder.Value;

        entity.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        return Ok(new ElectiveSubtypeDto(
            entity.Id, entity.Code, entity.Name,
            entity.RequiresFaculty, entity.ForbidsFaculty,
            entity.RequiresDepartment, entity.ForbidsDepartment,
            entity.IsActive, entity.SortOrder));
    }

    // NOT: DELETE endpoint YOK (Karar #2 — soft delete via Update IsActive=false).
    // menu.secmeli-tipleri.delete permission'ı ileri kullanım için hazırlandı.
}
