using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

public record CrossListItemDto(int CourseId, string Code, string Name, int Ects);

[ApiController]
[Route("api/admin/courses")]
[Authorize(Roles = "SystemAdministrator,AdministrativeStaff")]
public class CourseCatalogAdminController(IAdminCourseCatalogService service, AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCourses([FromQuery] CourseFilterDto filter)
        => Ok(await service.GetCoursesAsync(filter));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCourse(int id)
    {
        var result = await service.GetCourseByIdAsync(id);
        return result == null ? NotFound(new { message = "Ders bulunamadı." }) : Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> CreateCourse([FromBody] CreateCourseDto dto)
    {
        try { return Ok(await service.CreateCourseAsync(dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> UpdateCourse(int id, [FromBody] UpdateCourseDto dto)
    {
        try { return Ok(await service.UpdateCourseAsync(id, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> DeleteCourse(int id)
    {
        try { await service.DeleteCourseAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/prerequisites")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> AddPrerequisite(int id, [FromBody] AddPrerequisiteDto dto)
    {
        try { await service.AddPrerequisiteAsync(id, dto); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}/prerequisites/{requiredCourseId:int}")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> RemovePrerequisite(int id, int requiredCourseId)
    {
        try { await service.RemovePrerequisiteAsync(id, requiredCourseId); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/equivalencies")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> AddEquivalency(int id, [FromBody] AddEquivalencyDto dto)
    {
        try { await service.AddEquivalencyAsync(id, dto); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}/equivalencies/{equivalentCourseId:int}")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> RemoveEquivalency(int id, int equivalentCourseId)
    {
        try { await service.RemoveEquivalencyAsync(id, equivalentCourseId); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    // Proliz "Diğer Ön Koşul Tanımları" — sadece course-level kural alanları (Min kredi/AKTS, Alttan, Tekil Alma)
    [HttpPut("{id:int}/prereq-rules")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> UpdatePrereqRules(int id, [FromBody] UpdateCoursePrereqRulesDto dto)
    {
        try { return Ok(await service.UpdateCoursePrereqRulesAsync(id, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── Madde 14: Cross-Listing CRUD ─────────────────────────────────
    // İki ders kodunun aynı dersin farklı bölümlerdeki görünümü olduğunu kaydeder.
    // Örn: YZM301 ≡ BIL301 — aynı hoca, aynı sınıf, iki kod.

    [HttpGet("{id:int}/cross-lists")]
    public async Task<IActionResult> GetCrossLists(int id)
    {
        var items = await db.CourseCrossLists
            .Where(x => x.CourseId == id)
            .Include(x => x.LinkedCourse)
            .Select(x => new CrossListItemDto(
                x.LinkedCourseId,
                x.LinkedCourse.Code,
                x.LinkedCourse.Name,
                x.LinkedCourse.Ects))
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost("{id:int}/cross-lists/{linkedCourseId:int}")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> AddCrossList(int id, int linkedCourseId)
    {
        if (id == linkedCourseId)
            return BadRequest(new { message = "Bir dersi kendine cross-list edemezsiniz." });
        if (!await db.Courses.AnyAsync(c => c.Id == id) || !await db.Courses.AnyAsync(c => c.Id == linkedCourseId))
            return NotFound(new { message = "Ders bulunamadı." });
        if (await db.CourseCrossLists.AnyAsync(x => x.CourseId == id && x.LinkedCourseId == linkedCourseId))
            return BadRequest(new { message = "Bu cross-list kaydı zaten mevcut." });

        // Simetrik kayıt — iki yönde de eklenir
        var now = DateTime.UtcNow;
        db.CourseCrossLists.Add(new CourseCrossList { CourseId = id, LinkedCourseId = linkedCourseId, IsPrimary = true, CreatedAt = now });
        if (!await db.CourseCrossLists.AnyAsync(x => x.CourseId == linkedCourseId && x.LinkedCourseId == id))
            db.CourseCrossLists.Add(new CourseCrossList { CourseId = linkedCourseId, LinkedCourseId = id, IsPrimary = false, CreatedAt = now });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}/cross-lists/{linkedCourseId:int}")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> RemoveCrossList(int id, int linkedCourseId)
    {
        var a = await db.CourseCrossLists.FirstOrDefaultAsync(x => x.CourseId == id && x.LinkedCourseId == linkedCourseId);
        var b = await db.CourseCrossLists.FirstOrDefaultAsync(x => x.CourseId == linkedCourseId && x.LinkedCourseId == id);
        if (a != null) db.CourseCrossLists.Remove(a);
        if (b != null) db.CourseCrossLists.Remove(b);
        if (a == null && b == null)
            return NotFound(new { message = "Cross-list kaydı bulunamadı." });
        await db.SaveChangesAsync();
        return NoContent();
    }
}
