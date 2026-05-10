using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Announcement.DTOs;
using SIS.Application.Announcement.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/internal-announcements")]
[Authorize]
public class InternalAnnouncementController : ControllerBase
{
    private readonly IInternalAnnouncementService _service;
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public InternalAnnouncementController(
        IInternalAnnouncementService service,
        AppDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _service     = service;
        _context     = context;
        _userManager = userManager;
    }

    // ── Duyuru okuma (tüm roller) ─────────────────────────────���───────────────

    [HttpGet("active")]
    public async Task<IActionResult> GetActive([FromQuery] int? max, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        int?    departmentId  = null;
        int?    facultyId     = null;
        var     sectionIds    = new List<int>();
        string? advisorUserId = null;

        if (int.TryParse(userId, out var uid))
        {
            var instructor = await _context.Instructors
                .AsNoTracking()
                .Include(i => i.Department)
                .FirstOrDefaultAsync(i => i.UserId == uid, ct);

            if (instructor is not null)
            {
                departmentId = instructor.DepartmentId;
                facultyId    = instructor.Department?.FacultyId;
            }
            else
            {
                var student = await _context.Students
                    .AsNoTracking()
                    .Include(s => s.StudentPrograms)
                        .ThenInclude(sp => sp.Program)
                    .Include(s => s.Advisor)
                    .FirstOrDefaultAsync(s => s.UserId == uid, ct);

                if (student is not null)
                {
                    var program = student.StudentPrograms.FirstOrDefault()?.Program;
                    departmentId = program?.DepartmentId;
                    if (departmentId.HasValue)
                    {
                        var dept = await _context.Departments
                            .AsNoTracking()
                            .FirstOrDefaultAsync(d => d.Id == departmentId.Value, ct);
                        facultyId = dept?.FacultyId;
                    }

                    sectionIds    = await _context.CourseRegistrations
                        .AsNoTracking()
                        .Where(r => r.StudentId == student.Id)
                        .Select(r => r.CourseSectionId)
                        .ToListAsync(ct);

                    advisorUserId = student.Advisor?.UserId.ToString();
                }
            }
        }

        var items = await _service.GetActiveForUserAsync(userId, roles, departmentId, facultyId, sectionIds, advisorUserId, max, ct);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        if (item is null)
            return NotFound(new { error = "Duyuru bulunamadı." });
        return Ok(item);
    }

    // ── Yönetim listesi ───────────────────────────────────────────────────────

    /// <summary>
    /// SysAdmin + AdministrativeStaff: tüm duyurular.
    /// Diğer roller (DeptManager, Dean, Instructor): yalnızca kendi oluşturdukları.
    /// </summary>
    [HttpGet("all")]
    [RequirePermission("menu.duyuru-yonetimi.view")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var isAdminView = User.IsInRole("SystemAdministrator")
                       || User.IsInRole("AdministrativeStaff");

        List<AnnouncementListDto> items;
        if (isAdminView)
            items = await _service.GetAllAsync(ct);
        else
            items = await _service.GetMyCreatedAsync(userId, ct);

        return Ok(items);
    }

    // ── Form için create context ──────────────────────────────────────────────

    [HttpGet("create-context")]
    [RequirePermission("menu.duyuru-yonetimi.create")]
    public async Task<IActionResult> GetCreateContext(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value);

        var context = await _service.GetCreateContextAsync(userId, roles, ct);
        return Ok(context);
    }

    // ── CRUD (duyuru oluşturma / düzenleme / silme) ───────────────────────────

    [HttpPost]
    [RequirePermission("menu.duyuru-yonetimi.create")]
    public async Task<IActionResult> Create([FromBody] CreateAnnouncementDto dto, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Unauthorized();

        var name = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrEmpty(name)) name = user.UserName ?? "Bilinmeyen";

        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        int? senderDepartmentId = null;
        int? senderFacultyId    = null;

        if (int.TryParse(userIdClaim, out var uid))
        {
            var instructor = await _context.Instructors
                .AsNoTracking()
                .Include(i => i.Department)
                .FirstOrDefaultAsync(i => i.UserId == uid, ct);

            if (instructor is not null)
            {
                senderDepartmentId = instructor.DepartmentId;
                senderFacultyId    = instructor.Department?.FacultyId;
            }
        }

        try
        {
            var created = await _service.CreateAsync(dto, userIdClaim, name, roles, senderDepartmentId, senderFacultyId, ct);
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return BadRequest(new { error = msg });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("menu.duyuru-yonetimi.edit")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAnnouncementDto dto, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var isAdminOverride = User.IsInRole("SystemAdministrator") || User.IsInRole("AdministrativeStaff");

        try
        {
            var updated = await _service.UpdateAsync(id, dto, userId, isAdminOverride, ct);
            if (updated is null) return NotFound(new { error = "Duyuru bulunamadı." });
            return Ok(updated);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("menu.duyuru-yonetimi.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var isAdminOverride = User.IsInRole("SystemAdministrator") || User.IsInRole("AdministrativeStaff");

        try
        {
            var success = await _service.DeleteAsync(id, userId, isAdminOverride, ct);
            if (!success) return NotFound(new { error = "Duyuru bulunamadı." });
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [HttpDelete("{id:int}/permanent")]
    [RequirePermission("menu.duyuru-yonetimi.delete")]
    public async Task<IActionResult> PermanentDelete(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var isAdminOverride = User.IsInRole("SystemAdministrator") || User.IsInRole("AdministrativeStaff");

        try
        {
            var success = await _service.PermanentDeleteAsync(id, userId, isAdminOverride, ct);
            if (!success) return NotFound(new { error = "Duyuru bulunamadı." });
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return BadRequest(new { error = msg });
        }
    }

    [HttpPost("{id:int}/reactivate")]
    [RequirePermission("menu.duyuru-yonetimi.edit")]
    public async Task<IActionResult> Reactivate(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var isAdminOverride = User.IsInRole("SystemAdministrator") || User.IsInRole("AdministrativeStaff");

        try
        {
            var success = await _service.ReactivateAsync(id, userId, isAdminOverride, ct);
            if (!success) return NotFound(new { error = "Duyuru bulunamadı." });
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }
}
