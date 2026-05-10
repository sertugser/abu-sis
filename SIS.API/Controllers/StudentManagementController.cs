using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.API.Filters;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/students")]
[Authorize]
public class StudentManagementController : ControllerBase
{
    private readonly IAdminStudentService _service;
    private readonly AppDbContext _context;

    public StudentManagementController(IAdminStudentService service, AppDbContext context)
    {
        _service = service;
        _context = context;
    }

    [HttpGet]
    [RequirePermission("menu.ogrenci-rehberi.view")]
    [ServiceFilter(typeof(SensitiveDataAccessFilter))]
    [SensitiveDataAccess(EntityType = "Student")]
    public async Task<IActionResult> GetStudents([FromQuery] AdminStudentFilterDto filter)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        // Dean/DepartmentManager: departmentId filtresi zorunlu kıl
        var enforcedFilter = await EnforceDepartmentFilterAsync(userId, filter.DepartmentId);
        if (enforcedFilter.IsForbidden) return Forbid();

        var appliedFilter = enforcedFilter.DepartmentId != filter.DepartmentId
            ? filter with { DepartmentId = enforcedFilter.DepartmentId }
            : filter;

        var result = await _service.GetStudentsAsync(appliedFilter);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("menu.ogrenci-rehberi.view")]
    [ServiceFilter(typeof(SensitiveDataAccessFilter))]
    [SensitiveDataAccess(EntityType = "Student", EntityIdRouteParam = "id")]
    public async Task<IActionResult> GetStudent(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var result = await _service.GetStudentByIdAsync(id);
        if (result == null) return NotFound(new { message = "Öğrenci bulunamadı." });

        // Dean/DepartmentManager erişim kontrolü
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (!roles.Contains("SystemAdministrator") && !roles.Contains("AdministrativeStaff"))
        {
            var studentDeptId = await _context.StudentPrograms
                .AsNoTracking()
                .Include(sp => sp.Program)
                .Where(sp => sp.StudentId == id && sp.IsActive && sp.Type == Domain.Enums.StudentProgramType.Main)
                .Select(sp => (int?)sp.Program.DepartmentId)
                .FirstOrDefaultAsync();

            if (studentDeptId == null) return NotFound(new { message = "Öğrenci bulunamadı." });
            if (!await HasDepartmentAccessAsync(userId, studentDeptId.Value)) return Forbid();
        }

        return Ok(result);
    }

    [HttpPost]
    [RequirePermission("menu.ogrenci-rehberi.create")]
    public async Task<IActionResult> CreateStudent([FromBody] CreateStudentDto dto)
    {
        try
        {
            var result = await _service.CreateStudentAsync(dto);
            return CreatedAtAction(nameof(GetStudent), new { id = result.Id }, result);
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

[HttpPut("{id:int}/advisor")]
[RequirePermission("menu.toplu-danisman-atama.assign")]
public async Task<IActionResult> AssignAdvisor(int id, [FromBody] AssignAdvisorDto dto)
{
    try
    {
        await _service.AssignAdvisorAsync(id, dto.AdvisorId);
        return NoContent();
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

[HttpPost("bulk-advisor-assignment/preview")]
[RequirePermission("menu.toplu-danisman-atama.assign")]
public async Task<IActionResult> PreviewBulkAdvisorAssignment(
    [FromBody] BulkAdvisorAssignmentPreviewRequestDto dto)
{
    try
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var enforcedFilter = await EnforceDepartmentFilterAsync(userId, dto.DepartmentId);

        if (enforcedFilter.IsForbidden)
            return Forbid();

        if (enforcedFilter.DepartmentId.HasValue &&
            enforcedFilter.DepartmentId.Value != dto.DepartmentId)
            return Forbid();

        var result = await _service.PreviewBulkAdvisorAssignmentAsync(dto);
        return Ok(result);
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

[HttpPost("bulk-advisor-assignment/apply")]
[RequirePermission("menu.toplu-danisman-atama.assign")]
public async Task<IActionResult> ApplyBulkAdvisorAssignment(
    [FromBody] BulkAdvisorAssignmentApplyRequestDto dto)
{
    try
    {
        var result = await _service.ApplyBulkAdvisorAssignmentAsync(dto);
        return Ok(result);
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
[HttpPut("bulk-advisor-assignment/advisor-settings")]
[RequirePermission("menu.toplu-danisman-atama.assign")]
public async Task<IActionResult> UpdateBulkAdvisorAssignmentSettings(
    [FromBody] BulkAdvisorSettingsUpdateRequestDto dto)
{
    try
    {
        var result = await _service.UpdateBulkAdvisorAssignmentSettingsAsync(dto);
        return Ok(result);
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
            // Dean: filtre olmadan kendi fakültesinin tüm bölümleri gelir (servis tarafında filtrelenir)
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

public record AssignAdvisorDto(int AdvisorId);
