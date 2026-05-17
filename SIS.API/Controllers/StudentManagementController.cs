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
    private readonly IAcademicScopeGuard _scopeGuard;

    public StudentManagementController(
        IAdminStudentService service,
        AppDbContext context,
        IAcademicScopeGuard scopeGuard)
    {
        _service = service;
        _context = context;
        _scopeGuard = scopeGuard;
    }

    [HttpGet]
    [RequirePermission("menu.ogrenci-rehberi.view")]
    [ServiceFilter(typeof(SensitiveDataAccessFilter))]
    [SensitiveDataAccess(EntityType = "Student")]
    public async Task<IActionResult> GetStudents([FromQuery] AdminStudentFilterDto filter)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        // Dean/DepartmentManager: departmentId filtresi zorunlu kıl
        var scope = await _scopeGuard.ResolveAsync(User, userId);
        if (scope.IsForbidden) return Forbid();
        if (filter.DepartmentId.HasValue && !scope.IsGlobal && !scope.DepartmentIds.Contains(filter.DepartmentId.Value))
            return Forbid();

        var appliedFilter = scope.IsGlobal
            ? filter
            : filter with { DepartmentIds = scope.DepartmentIds };

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
            if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, studentDeptId.Value)) return Forbid();
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
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var studentDepartmentId = await GetStudentMainDepartmentIdAsync(id);
        if (studentDepartmentId == null)
            return NotFound(new { message = "Öğrenci bulunamadı." });

        if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, studentDepartmentId.Value))
            return Forbid();

        var advisorDepartmentId = await GetAdvisorDepartmentIdAsync(dto.AdvisorId);
        if (advisorDepartmentId == null)
            return NotFound(new { message = $"Danışman bulunamadı: {dto.AdvisorId}" });

        if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, advisorDepartmentId.Value))
            return Forbid();

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

        if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, dto.DepartmentId))
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
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await CanAccessAllBulkAssignmentTargetsAsync(userId, dto))
            return Forbid();

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
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await CanAccessAllAdvisorSettingTargetsAsync(userId, dto))
            return Forbid();

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

    private async Task<bool> CanAccessAllBulkAssignmentTargetsAsync(
        int userId,
        BulkAdvisorAssignmentApplyRequestDto dto)
    {
        if (dto.Assignments == null || dto.Assignments.Count == 0)
            return true;

        var studentIds = dto.Assignments.Select(item => item.StudentId).Distinct().ToList();
        var advisorIds = dto.Assignments.Select(item => item.AdvisorId).Distinct().ToList();

        var studentDepartmentIds = await _context.StudentPrograms
            .AsNoTracking()
            .Include(sp => sp.Program)
            .Where(sp => studentIds.Contains(sp.StudentId)
                && sp.IsActive
                && sp.Type == Domain.Enums.StudentProgramType.Main)
            .Select(sp => sp.Program.DepartmentId)
            .Distinct()
            .ToListAsync();

        foreach (var departmentId in studentDepartmentIds)
        {
            if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, departmentId))
                return false;
        }

        var advisorDepartmentIds = await _context.Instructors
            .AsNoTracking()
            .Where(instructor => advisorIds.Contains(instructor.Id))
            .Select(instructor => instructor.DepartmentId)
            .Distinct()
            .ToListAsync();

        foreach (var departmentId in advisorDepartmentIds)
        {
            if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, departmentId))
                return false;
        }

        return true;
    }

    private async Task<bool> CanAccessAllAdvisorSettingTargetsAsync(
        int userId,
        BulkAdvisorSettingsUpdateRequestDto dto)
    {
        if (dto.Advisors == null || dto.Advisors.Count == 0)
            return true;

        var advisorIds = dto.Advisors.Select(item => item.AdvisorId).Distinct().ToList();
        var advisorDepartmentIds = await _context.Instructors
            .AsNoTracking()
            .Where(instructor => advisorIds.Contains(instructor.Id))
            .Select(instructor => instructor.DepartmentId)
            .Distinct()
            .ToListAsync();

        foreach (var departmentId in advisorDepartmentIds)
        {
            if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, departmentId))
                return false;
        }

        return true;
    }

    private async Task<int?> GetStudentMainDepartmentIdAsync(int studentId)
    {
        return await _context.StudentPrograms
            .AsNoTracking()
            .Include(sp => sp.Program)
            .Where(sp => sp.StudentId == studentId
                && sp.IsActive
                && sp.Type == Domain.Enums.StudentProgramType.Main)
            .Select(sp => (int?)sp.Program.DepartmentId)
            .FirstOrDefaultAsync();
    }

    private async Task<int?> GetAdvisorDepartmentIdAsync(int advisorId)
    {
        return await _context.Instructors
            .AsNoTracking()
            .Where(instructor => instructor.Id == advisorId)
            .Select(instructor => (int?)instructor.DepartmentId)
            .FirstOrDefaultAsync();
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
