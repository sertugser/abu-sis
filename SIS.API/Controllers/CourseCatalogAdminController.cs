using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

public record CrossListItemDto(int CourseId, string Code, string Name, int Ects);

public record CourseCurriculumUsageDto(
    int CurriculumId, string ProgramName, string CurriculumVersion, bool IsActive);

[ApiController]
[Route("api/admin/courses")]
[Authorize]
public class CourseCatalogAdminController(IAdminCourseCatalogService service, AppDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission("menu.ders-katalogu.view")]
    public async Task<IActionResult> GetCourses([FromQuery] CourseFilterDto filter)
        => Ok(await service.GetCoursesAsync(filter));

    [HttpGet("{id:int}")]
    [RequirePermission("menu.ders-katalogu.view")]
    public async Task<IActionResult> GetCourse(int id)
    {
        var result = await service.GetCourseByIdAsync(id);
        return result == null ? NotFound(new { message = "Ders bulunamadı." }) : Ok(result);
    }

    [HttpPost]
    [RequirePermission("menu.ders-katalogu.create")]
    public async Task<IActionResult> CreateCourse([FromBody] CreateCourseDto dto)
    {
        try { return Ok(await service.CreateCourseAsync(dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("menu.ders-katalogu.edit")]
    public async Task<IActionResult> UpdateCourse(int id, [FromBody] UpdateCourseDto dto)
    {
        try { return Ok(await service.UpdateCourseAsync(id, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("menu.ders-katalogu.delete")]
    public async Task<IActionResult> DeleteCourse(int id)
    {
        try { await service.DeleteCourseAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/prerequisites")]
    [RequirePermission("menu.ders-katalogu.prerequisite")]
    public async Task<IActionResult> AddPrerequisite(int id, [FromBody] AddPrerequisiteDto dto)
    {
        try { await service.AddPrerequisiteAsync(id, dto); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}/prerequisites/{requiredCourseId:int}")]
    [RequirePermission("menu.ders-katalogu.prerequisite")]
    public async Task<IActionResult> RemovePrerequisite(int id, int requiredCourseId)
    {
        try { await service.RemovePrerequisiteAsync(id, requiredCourseId); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/equivalencies")]
    [RequirePermission("menu.ders-katalogu.equivalency")]
    public async Task<IActionResult> AddEquivalency(int id, [FromBody] AddEquivalencyDto dto)
    {
        try { await service.AddEquivalencyAsync(id, dto); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}/equivalencies/{equivalentCourseId:int}")]
    [RequirePermission("menu.ders-katalogu.equivalency")]
    public async Task<IActionResult> RemoveEquivalency(int id, int equivalentCourseId)
    {
        try { await service.RemoveEquivalencyAsync(id, equivalentCourseId); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    // Proliz "Diğer Ön Koşul Tanımları" — sadece course-level kural alanları (Min kredi/AKTS, Alttan, Tekil Alma)
    [HttpPut("{id:int}/prereq-rules")]
    [RequirePermission("menu.ders-katalogu.edit")]
    public async Task<IActionResult> UpdatePrereqRules(int id, [FromBody] UpdateCoursePrereqRulesDto dto)
    {
        try { return Ok(await service.UpdateCoursePrereqRulesAsync(id, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── Curriculum usage — bu dersi kullanan müfredatların listesi (yan etki uyarısı için) ──
    [HttpGet("{id:int}/curriculum-usage")]
    [RequirePermission("menu.mufredat-islemleri.view")]
    public async Task<IActionResult> GetCurriculumUsage(int id, CancellationToken ct)
    {
        var rows = await db.CurriculumCourses
            .AsNoTracking()
            .Where(cc => cc.CourseId == id)
            .Select(cc => new CourseCurriculumUsageDto(
                cc.CurriculumId,
                cc.Curriculum.Program.Name,
                cc.Curriculum.Version,
                cc.Curriculum.IsActive))
            .OrderByDescending(r => r.IsActive)
            .ThenBy(r => r.ProgramName)
            .ThenBy(r => r.CurriculumVersion)
            .ToListAsync(ct);
        return Ok(rows);
    }

    // ── Madde 14: Cross-Listing CRUD ─────────────────────────────────
    // İki ders kodunun aynı dersin farklı bölümlerdeki görünümü olduğunu kaydeder.
    // Örn: YZM301 ≡ BIL301 — aynı hoca, aynı sınıf, iki kod.

    [HttpGet("{id:int}/cross-lists")]
    [RequirePermission("menu.ders-katalogu.view")]
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
    [RequirePermission("menu.ders-katalogu.cross-list")]
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
    [RequirePermission("menu.ders-katalogu.cross-list")]
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
