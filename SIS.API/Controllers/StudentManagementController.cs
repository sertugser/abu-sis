using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIS.API.Authorization;
using SIS.API.Filters;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Application.Common.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/students")]
[Authorize]
public class StudentManagementController : ControllerBase
{
    private readonly IAdminStudentService _service;
    private readonly AppDbContext _context;
    private readonly ISystemConfigProvider _systemConfig;
    private readonly IAcademicScopeGuard _scopeGuard;

    public StudentManagementController(
        IAdminStudentService service,
        AppDbContext context,
        ISystemConfigProvider systemConfig)
        : this(service, context, systemConfig, new AcademicScopeGuard(context))
    {
    }

    public StudentManagementController(
        IAdminStudentService service,
        AppDbContext context,
        IAcademicScopeGuard scopeGuard)
        : this(service, context, new NullSystemConfigProvider(), scopeGuard)
    {
    }

    // Birden fazla constructor DI tarafından çözülebildiği için ambiguity oluşuyordu;
    // DI'nin kullanacağı kanonik constructor bu (diğerleri test kolaylığı için).
    [ActivatorUtilitiesConstructor]
    public StudentManagementController(
        IAdminStudentService service,
        AppDbContext context,
        ISystemConfigProvider systemConfig,
        IAcademicScopeGuard scopeGuard)
    {
        _service = service;
        _context = context;
        _systemConfig = systemConfig;
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
        if (!await ShouldMaskTranscriptSummaryAsync())
            return Ok(result);

        var items = result.Items
            .Select(item => new
            {
                item.Id,
                item.StudentNumber,
                item.FirstName,
                item.LastName,
                item.Email,
                Cgpa = (decimal?)null,
                CompletedSemesters = (int?)null,
                item.Status,
                item.ProgramName,
                item.ProgramCode,
                item.DepartmentName,
                item.AdvisorName,
                TranscriptAccessible = false,
                TranscriptAccessMessage = "Akademisyen transkript erisimi sistem ayari nedeniyle kapali."
            })
            .Cast<object>()
            .ToList();

        return Ok(new PagedResult<object>(items, result.TotalCount, result.Page, result.PageSize));
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

        if (!await ShouldMaskTranscriptSummaryAsync())
            return Ok(result);

        return Ok(new
        {
            result.Id,
            result.UserId,
            result.StudentNumber,
            result.FirstName,
            result.LastName,
            result.Email,
            Cgpa = (decimal?)null,
            Sgpa = (decimal?)null,
            CompletedSemesters = (int?)null,
            FrozenSemesterCount = (int?)null,
            result.Status,
            result.IsScholarshipStudent,
            result.HasFinancialBlock,
            result.ProgramName,
            result.ProgramCode,
            result.DepartmentName,
            result.AdvisorName,
            result.AdvisorId,
            result.ProgramId,
            result.CurriculumId,
            result.ProgramType,
            TranscriptAccessible = false,
            TranscriptAccessMessage = "Akademisyen transkript erisimi sistem ayari nedeniyle kapali."
        });
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

    [HttpPut("{id:int}/academic")]
    [RequirePermission("menu.ogrenci-rehberi.edit-academic")]
    public async Task<IActionResult> UpdateAcademic(int id, [FromBody] UpdateStudentAcademicDto dto)
    {
        try
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var currentDeptId = await GetStudentMainDepartmentIdAsync(id);
            if (currentDeptId == null) return NotFound(new { message = "Öğrenci bulunamadı." });
            if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, currentDeptId.Value)) return Forbid();

            var targetDeptId = await _context.AcademicPrograms
                .Where(p => p.Id == dto.ProgramId)
                .Select(p => (int?)p.DepartmentId)
                .FirstOrDefaultAsync();
            if (targetDeptId == null) return NotFound(new { message = $"Program bulunamadı: {dto.ProgramId}" });
            if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, targetDeptId.Value)) return Forbid();

            await _service.UpdateAcademicAsync(id, dto);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}/student-number")]
    [RequirePermission("menu.ogrenci-no-degistirme.change")]
    public async Task<IActionResult> ChangeStudentNumber(int id, [FromBody] ChangeStudentNumberDto dto)
    {
        try
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var deptId = await GetStudentMainDepartmentIdAsync(id);
            if (deptId == null) return NotFound(new { message = "Öğrenci bulunamadı." });
            if (!await _scopeGuard.CanAccessDepartmentAsync(User, userId, deptId.Value)) return Forbid();
            await _service.ChangeStudentNumberAsync(id, dto.NewStudentNumber);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim, out userId);
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

    private async Task<bool> ShouldMaskTranscriptSummaryAsync(CancellationToken ct = default)
    {
        var transcriptClosed = await _systemConfig.GetBoolAsync("Instructor.TranscriptClosed", false, ct);
        if (!transcriptClosed)
            return false;

        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasCentralAdminRole = roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff");
        return !hasCentralAdminRole && roles.Overlaps(new[] { "Instructor", "AcademicAdvisor", "Dean", "DepartmentManager" });
    }
}

public record AssignAdvisorDto(int AdvisorId);
