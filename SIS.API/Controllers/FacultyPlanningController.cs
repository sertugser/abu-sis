using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.FacultyPlanning.Services;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/faculty-planning")]
[Authorize]
public class FacultyPlanningController : ControllerBase
{
    private readonly IFacultyPlanningService _service;
    private readonly ISchedulingService _schedulingService;
    private readonly AppDbContext _context;
    private readonly ILogger<FacultyPlanningController> _logger;

    public FacultyPlanningController(
        IFacultyPlanningService service,
        ISchedulingService schedulingService,
        AppDbContext context,
        ILogger<FacultyPlanningController> logger)
    {
        _service = service;
        _schedulingService = schedulingService;
        _context = context;
        _logger = logger;
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim, out userId);
    }

    [HttpGet("overview")]
    [RequirePermission("menu.ders-programi-olusturma.view")]
    public async Task<IActionResult> GetOverview([FromQuery] int facultyId, [FromQuery] int termId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasFacultyAccessAsync(userId, facultyId, ct)) return Forbid();

        var scopeDeptId = await EnforceDepartmentScopeAsync(userId, null, ct);
        var result = await _service.GetOverviewAsync(facultyId, termId, scopeDeptId, ct);
        return Ok(result);
    }

    [HttpGet("sections")]
    [RequirePermission("menu.ders-programi-olusturma.view")]
    public async Task<IActionResult> GetSections(
        [FromQuery] int facultyId, [FromQuery] int termId,
        [FromQuery] int? departmentId, [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasFacultyAccessAsync(userId, facultyId, ct)) return Forbid();

        departmentId = await EnforceDepartmentScopeAsync(userId, departmentId, ct);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var result = await _service.GetSectionsAsync(facultyId, termId, departmentId, status, page, pageSize, search, ct);
        return Ok(result);
    }

    [HttpPost("assign")]
    [RequirePermission("menu.ders-programi-olusturma.assign")]
    public async Task<IActionResult> AssignSection([FromBody] AssignSectionRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var facultyId = await _service.ResolveFacultyIdForSectionAsync(request.SectionId, ct);
        if (facultyId == null) return NotFound("Bölüm bulunamadı.");
        if (!await HasFacultyAccessAsync(userId, facultyId.Value, ct)) return Forbid();

        var result = await _service.AssignSectionAsync(request, userId.ToString(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Düzenleyici drag-start akışında çağrılır. Bir section için N aday hücreyi
    /// tek HTTP çağrısında RuleEngine ile değerlendirir; per-cell {isValid, reason}
    /// döndürür. Sadece okuma — herhangi bir değişiklik yapmaz, dolayısıyla
    /// .view permission'ı yeterli.
    /// </summary>
    [HttpPost("validate-cells")]
    [RequirePermission("menu.ders-programi-olusturma.view")]
    public async Task<IActionResult> ValidateCellsBulk(
        [FromBody] BulkValidateCellsRequestDto request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var facultyId = await _service.ResolveFacultyIdForSectionAsync(request.SectionId, ct);
        if (facultyId == null) return NotFound("Bölüm bulunamadı.");
        if (!await HasFacultyAccessAsync(userId, facultyId.Value, ct)) return Forbid();

        var result = await _schedulingService.ValidateCellsBulkAsync(request, ct);
        return Ok(result);
    }

    [HttpDelete("unassign/{sectionId}")]
    [RequirePermission("menu.ders-programi-olusturma.unassign")]
    public async Task<IActionResult> UnassignSection(int sectionId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var facultyId = await _service.ResolveFacultyIdForSectionAsync(sectionId, ct);
        if (facultyId == null) return NotFound("Bölüm bulunamadı.");
        if (!await HasFacultyAccessAsync(userId, facultyId.Value, ct)) return Forbid();

        await _service.UnassignSectionAsync(sectionId, userId.ToString(), ct);
        return NoContent();
    }

    [HttpDelete("reset-all/{facultyId}/{termId}")]
    [RequirePermission("menu.ders-programi-olusturma.reset-all")]
    public async Task<IActionResult> ResetAllSchedules(int facultyId, int termId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasFacultyAccessAsync(userId, facultyId, ct)) return Forbid();

        var count = await _service.ResetAllSchedulesAsync(facultyId, termId, userId.ToString(), ct);
        return Ok(new { resetCount = count });
    }

    [HttpPost("auto-schedule")]
    [RequirePermission("menu.ders-programi-olusturma.auto-schedule")]
    public async Task<IActionResult> AutoSchedule([FromBody] AutoScheduleRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasFacultyAccessAsync(userId, request.FacultyId, ct)) return Forbid();

        try
        {
            var result = await _service.AutoSchedulePreviewAsync(request, userId.ToString(), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException or NotSupportedException)
        {
            _logger.LogWarning(
                ex,
                "Auto-schedule rejected for FacultyId={FacultyId}, TermId={TermId}, DepartmentId={DepartmentId}",
                request.FacultyId,
                request.TermId,
                request.DepartmentId);
            return BadRequest(new
            {
                error = "Ders programi olusturulamadi. Derslik, zaman dilimi ve kisit ayarlarini kontrol edin."
            });
        }
    }

    [HttpPost("apply-schedule")]
    [RequirePermission("menu.ders-programi-olusturma.apply")]
    public async Task<IActionResult> ApplySchedule([FromBody] ApplyScheduleRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        if (request.Assignments.Count > 0)
        {
            var facultyId = await _service.ResolveFacultyIdForSectionAsync(request.Assignments[0].SectionId, ct);
            if (facultyId == null) return NotFound("Bölüm bulunamadı.");
            if (!await HasFacultyAccessAsync(userId, facultyId.Value, ct)) return Forbid();
        }

        var result = await _service.ApplyScheduleAsync(request, userId.ToString(), ct);
        return Ok(result);
    }

    [HttpGet("available-classrooms")]
    [RequirePermission("menu.ders-programi-olusturma.view")]
    public async Task<IActionResult> GetAvailableClassrooms(
        [FromQuery] int termId, [FromQuery] DayOfWeek? day,
        [FromQuery] TimeOnly? startTime, [FromQuery] TimeOnly? endTime,
        [FromQuery] int? minCapacity, [FromQuery] string? type,
        CancellationToken ct)
    {
        var result = await _service.GetAvailableClassroomsAsync(termId, day, startTime, endTime, minCapacity, type, ct);
        return Ok(result);
    }

    [HttpGet("time-slots")]
    [RequirePermission("menu.ders-programi-olusturma.view")]
    public async Task<IActionResult> GetTimeSlots([FromQuery] int termId, CancellationToken ct)
    {
        var slots = await _service.GetTimeSlotsAsync(termId, ct);
        return Ok(slots);
    }

    [HttpGet("conflicts")]
    [RequirePermission("menu.ders-programi-olusturma.view")]
    public async Task<IActionResult> GetConflicts([FromQuery] int facultyId, [FromQuery] int termId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasFacultyAccessAsync(userId, facultyId, ct)) return Forbid();

        var result = await _service.GetConflictsAsync(facultyId, termId, ct);
        return Ok(result);
    }

    [HttpGet("schedule-runs")]
    [RequirePermission("menu.ders-programi-olusturma.view")]
    public async Task<IActionResult> GetScheduleRuns(
        [FromQuery] int facultyId, [FromQuery] int termId, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasFacultyAccessAsync(userId, facultyId, ct)) return Forbid();

        var runs = await _service.GetScheduleRunsAsync(facultyId, termId, ct);
        return Ok(runs);
    }

    [HttpGet("schedule-runs/{id}")]
    [RequirePermission("menu.ders-programi-olusturma.view")]
    public async Task<IActionResult> GetScheduleRun(int id, CancellationToken ct = default)
    {
        if (!TryGetUserId(out _)) return Unauthorized();

        var run = await _service.GetScheduleRunAsync(id, ct);
        if (run == null) return NotFound();
        return Ok(run);
    }

    private async Task<int?> EnforceDepartmentScopeAsync(int userId, int? requestedDepartmentId, CancellationToken ct)
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff") || roles.Contains("Dean"))
            return requestedDepartmentId;

        if (roles.Contains("DepartmentManager"))
        {
            var deptId = await _context.Instructors
                .AsNoTracking()
                .Where(i => i.UserId == userId)
                .Select(i => (int?)i.DepartmentId)
                .FirstOrDefaultAsync(ct);
            return deptId;
        }

        return requestedDepartmentId;
    }

    private async Task<bool> HasFacultyAccessAsync(int userId, int facultyId, CancellationToken ct)
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        if (roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff"))
            return true;

        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.UserId == userId, ct);

        if (instructor == null) return false;

        // Dean: kendi fakültesine erişebilir
        if (roles.Contains("Dean"))
            return instructor.Department.FacultyId == facultyId;

        // DepartmentManager: kendi bölümünün fakültesine erişebilir
        if (roles.Contains("DepartmentManager"))
            return instructor.Department.FacultyId == facultyId;

        return false;
    }
}
