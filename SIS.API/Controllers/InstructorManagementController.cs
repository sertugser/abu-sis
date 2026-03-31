using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/instructors")]
[Authorize(Roles = "SystemAdministrator,Dean,DepartmentManager,AdministrativeStaff")]
public class InstructorManagementController : ControllerBase
{
    private readonly IAdminInstructorService _service;
    private readonly AppDbContext _context;

    public InstructorManagementController(IAdminInstructorService service, AppDbContext context)
    {
        _service = service;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetInstructors([FromQuery] AdminInstructorFilterDto filter)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var enforcedFilter = await EnforceDepartmentFilterAsync(userId, filter.DepartmentId);
        if (enforcedFilter.IsForbidden) return Forbid();

        var appliedFilter = enforcedFilter.DepartmentId != filter.DepartmentId
            ? filter with { DepartmentId = enforcedFilter.DepartmentId }
            : filter;

        var result = await _service.GetInstructorsAsync(appliedFilter);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetInstructor(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var result = await _service.GetInstructorByIdAsync(id);
        if (result == null) return NotFound(new { message = "Öğretim üyesi bulunamadı." });

        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (!roles.Contains("SystemAdministrator") && !roles.Contains("AdministrativeStaff"))
        {
            var targetInstructor = await _context.Instructors
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id);

            if (targetInstructor == null) return NotFound(new { message = "Öğretim üyesi bulunamadı." });
            if (!await HasDepartmentAccessAsync(userId, targetInstructor.DepartmentId)) return Forbid();
        }

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdministrator,AdministrativeStaff")]
    public async Task<IActionResult> CreateInstructor([FromBody] CreateInstructorDto dto)
    {
        try
        {
            var result = await _service.CreateInstructorAsync(dto);
            return CreatedAtAction(nameof(GetInstructor), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim, out userId);
    }

    private async Task<(int? DepartmentId, bool IsForbidden)> EnforceDepartmentFilterAsync(int userId, int? requestedDepartmentId)
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        if (roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff"))
            return (requestedDepartmentId, false);

        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.UserId == userId);

        if (instructor == null) return (null, true);

        if (roles.Contains("DepartmentManager"))
        {
            if (requestedDepartmentId.HasValue && requestedDepartmentId.Value != instructor.DepartmentId)
                return (null, true);
            return (instructor.DepartmentId, false);
        }

        if (roles.Contains("Dean"))
        {
            if (requestedDepartmentId.HasValue)
            {
                var dept = await _context.Departments.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == requestedDepartmentId.Value);
                if (dept == null || dept.FacultyId != instructor.Department.FacultyId)
                    return (null, true);
            }
            return (requestedDepartmentId, false);
        }

        return (null, true);
    }

    private async Task<bool> HasDepartmentAccessAsync(int userId, int departmentId)
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        if (roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff"))
            return true;

        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.UserId == userId);

        if (instructor == null) return false;

        if (roles.Contains("Dean"))
        {
            var department = await _context.Departments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == departmentId);
            return department != null && department.FacultyId == instructor.Department.FacultyId;
        }

        if (roles.Contains("DepartmentManager"))
            return instructor.DepartmentId == departmentId;

        return false;
    }
}
