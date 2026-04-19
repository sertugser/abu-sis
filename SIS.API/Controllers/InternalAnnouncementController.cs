using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        _service = service;
        _context = context;
        _userManager = userManager;
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive([FromQuery] int? max, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Unauthorized();

        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        int? departmentId = null;
        if (int.TryParse(userId, out var uid))
        {
            var student = await _context.Students
                .AsNoTracking()
                .Include(s => s.StudentPrograms)
                    .ThenInclude(sp => sp.Program)
                .FirstOrDefaultAsync(s => s.UserId == uid, ct);

            if (student != null)
            {
                var program = student.StudentPrograms.FirstOrDefault()?.Program;
                departmentId = program?.DepartmentId;
            }
            else
            {
                var instructor = await _context.Instructors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.UserId == uid, ct);
                departmentId = instructor?.DepartmentId;
            }
        }

        var items = await _service.GetActiveForUserAsync(userId, roles, departmentId, max, ct);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        if (item == null)
            return NotFound(new { error = "Duyuru bulunamadi." });

        return Ok(item);
    }

    [HttpGet("all")]
    [Authorize(Roles = "SystemAdministrator,Dean,DepartmentManager,AdministrativeStaff")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var items = await _service.GetAllAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdministrator,Dean,DepartmentManager,AdministrativeStaff")]
    public async Task<IActionResult> Create([FromBody] CreateAnnouncementDto dto, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return Unauthorized();

        var name = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrEmpty(name))
            name = user.UserName ?? "Bilinmeyen";

        try
        {
            var created = await _service.CreateAsync(dto, userIdClaim, name, ct);
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "SystemAdministrator,Dean,DepartmentManager,AdministrativeStaff")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAnnouncementDto dto, CancellationToken ct)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, dto, ct);
            if (updated == null)
                return NotFound(new { error = "Duyuru bulunamadi." });
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "SystemAdministrator,Dean,DepartmentManager,AdministrativeStaff")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var success = await _service.DeleteAsync(id, ct);
        if (!success)
            return NotFound(new { error = "Duyuru bulunamadi." });

        return NoContent();
    }
}
