using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.Announcement.DTOs;
using SIS.Application.Announcement.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/announcement-categories")]
[Authorize]
public class AnnouncementCategoryController : ControllerBase
{
    private readonly IAnnouncementCategoryService _service;

    public AnnouncementCategoryController(IAnnouncementCategoryService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequireAnyPermission("menu.duyuru-kategorileri.view", "menu.duyuru-yonetimi.view", "menu.duyurular.view")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var items = await _service.GetAllAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("menu.duyuru-kategorileri.view")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        if (item is null)
            return NotFound(new { error = "Kategori bulunamadı." });
        return Ok(item);
    }

    [HttpPost]
    [RequirePermission("menu.duyuru-kategorileri.create")]
    public async Task<IActionResult> Create([FromBody] CreateAnnouncementCategoryDto dto, CancellationToken ct)
    {
        try
        {
            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("menu.duyuru-kategorileri.edit")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAnnouncementCategoryDto dto, CancellationToken ct)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, dto, ct);
            if (updated is null)
                return NotFound(new { error = "Kategori bulunamadı." });
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("menu.duyuru-kategorileri.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var success = await _service.DeleteAsync(id, ct);
            if (!success)
                return NotFound(new { error = "Kategori bulunamadı." });
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
