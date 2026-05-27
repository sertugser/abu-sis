using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.Identity.DTOs;
using SIS.Application.Identity.Services;

namespace SIS.API.Controllers.Admin;

[ApiController]
[Authorize]
public class ReportAuthorizationController : ControllerBase
{
    private readonly IReportAuthorizationService _service;

    public ReportAuthorizationController(IReportAuthorizationService service)
    {
        _service = service;
    }

    // ── Categories ──────────────────────────────────────────

    [HttpGet("api/admin/report-categories")]
    [RequirePermission("menu.kullanici-rapor-gruplari.view")]
    public async Task<IActionResult> ListCategories(CancellationToken ct)
        => Ok(await _service.ListCategoriesAsync(ct));

    [HttpPost("api/admin/report-categories")]
    [RequirePermission("menu.kullanici-rapor-gruplari.manage")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateReportCategoryDto dto, CancellationToken ct)
    {
        try { return Ok(await _service.CreateCategoryAsync(dto, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("api/admin/report-categories/{id:int}")]
    [RequirePermission("menu.kullanici-rapor-gruplari.manage")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateReportCategoryDto dto, CancellationToken ct)
    {
        try { return Ok(await _service.UpdateCategoryAsync(id, dto, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("api/admin/report-categories/{id:int}")]
    [RequirePermission("menu.kullanici-rapor-gruplari.manage")]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
    {
        try { await _service.DeleteCategoryAsync(id, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── Reports ─────────────────────────────────────────────

    [HttpGet("api/admin/reports")]
    [RequirePermission("menu.kullanici-rapor-gruplari.view")]
    public async Task<IActionResult> ListReports([FromQuery] int? categoryId, CancellationToken ct)
        => Ok(await _service.ListReportsAsync(categoryId, ct));

    [HttpPost("api/admin/reports")]
    [RequirePermission("menu.kullanici-rapor-gruplari.manage")]
    public async Task<IActionResult> CreateReport([FromBody] CreateReportDefinitionDto dto, CancellationToken ct)
    {
        try { return Ok(await _service.CreateReportAsync(dto, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("api/admin/reports/{id:int}")]
    [RequirePermission("menu.kullanici-rapor-gruplari.manage")]
    public async Task<IActionResult> UpdateReport(int id, [FromBody] UpdateReportDefinitionDto dto, CancellationToken ct)
    {
        try { return Ok(await _service.UpdateReportAsync(id, dto, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("api/admin/reports/{id:int}")]
    [RequirePermission("menu.kullanici-rapor-gruplari.manage")]
    public async Task<IActionResult> DeleteReport(int id, CancellationToken ct)
    {
        await _service.DeleteReportAsync(id, ct);
        return NoContent();
    }

    // ── Kullanıcı grupları ──────────────────────────────────

    [HttpGet("api/admin/report-user-groups")]
    [RequirePermission("menu.kullanici-rapor-gruplari.view")]
    public async Task<IActionResult> ListGroups(CancellationToken ct) => Ok(await _service.ListGroupsAsync(ct));

    [HttpPost("api/admin/report-user-groups")]
    [RequirePermission("menu.kullanici-rapor-gruplari.manage")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateReportUserGroupDto dto, CancellationToken ct)
    {
        try { return Ok(await _service.CreateGroupAsync(dto, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("api/admin/report-user-groups/{id:int}")]
    [RequirePermission("menu.kullanici-rapor-gruplari.manage")]
    public async Task<IActionResult> UpdateGroup(int id, [FromBody] UpdateReportUserGroupDto dto, CancellationToken ct)
    {
        try { return Ok(await _service.UpdateGroupAsync(id, dto, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("api/admin/report-user-groups/{id:int}")]
    [RequirePermission("menu.kullanici-rapor-gruplari.manage")]
    public async Task<IActionResult> DeleteGroup(int id, CancellationToken ct)
    {
        await _service.DeleteGroupAsync(id, ct);
        return NoContent();
    }

    [HttpGet("api/admin/report-user-groups/{id:int}/members")]
    [RequirePermission("menu.kullanici-rapor-gruplari.view")]
    public async Task<IActionResult> ListGroupMembers(int id, CancellationToken ct)
        => Ok(await _service.ListGroupMembersAsync(id, ct));

    [HttpPost("api/admin/report-user-groups/{id:int}/members")]
    [RequirePermission("menu.kullanici-rapor-gruplari.manage")]
    public async Task<IActionResult> AddGroupMember(int id, [FromBody] AddGroupMemberDto dto, CancellationToken ct)
    {
        try { await _service.AddGroupMemberAsync(id, dto.UserId, ct); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpDelete("api/admin/report-user-groups/{id:int}/members/{userId:int}")]
    [RequirePermission("menu.kullanici-rapor-gruplari.manage")]
    public async Task<IActionResult> RemoveGroupMember(int id, int userId, CancellationToken ct)
    {
        await _service.RemoveGroupMemberAsync(id, userId, ct);
        return NoContent();
    }

    // ── Birleşik yetki (grant) ──────────────────────────────

    [HttpGet("api/admin/report-grants")]
    [RequirePermission("menu.kullanici-rapor-gruplari.view")]
    public async Task<IActionResult> ListGrants(CancellationToken ct) => Ok(await _service.ListGrantsAsync(ct));

    [HttpPost("api/admin/report-grants")]
    [RequirePermission("menu.kullanici-rapor-gruplari.manage")]
    public async Task<IActionResult> SetGrant([FromBody] SetReportAccessGrantDto dto, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var uid)) return Unauthorized();
        try { return Ok(await _service.SetGrantAsync(dto, uid, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("api/admin/report-grants/{id:int}")]
    [RequirePermission("menu.kullanici-rapor-gruplari.manage")]
    public async Task<IActionResult> DeleteGrant(int id, CancellationToken ct)
    {
        await _service.DeleteGrantAsync(id, ct);
        return NoContent();
    }

    // ── Frontend tüketim ────────────────────────────────────

    [HttpGet("api/me/reports")]
    [RequirePermission("menu.raporlarim.view")]
    public async Task<IActionResult> ListMyAccessibleReports(CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var uid)) return Unauthorized();
        return Ok(await _service.ListAccessibleReportsAsync(uid, ct));
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        userId = 0;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out userId);
    }
}
