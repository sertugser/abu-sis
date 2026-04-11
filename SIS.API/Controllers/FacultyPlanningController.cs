using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.FacultyPlanning.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/faculty-planning")]
[Authorize(Roles = "DepartmentManager,Dean,AdministrativeStaff,SystemAdministrator")]
public class FacultyPlanningController : ControllerBase
{
    private readonly IFacultyPlanningService _service;
    private readonly AppDbContext _context;

    public FacultyPlanningController(IFacultyPlanningService service, AppDbContext context)
    {
        _service = service;
        _context = context;
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim, out userId);
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] int facultyId, [FromQuery] int termId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasFacultyAccessAsync(userId, facultyId, ct)) return Forbid();

        var result = await _service.GetOverviewAsync(facultyId, termId, ct);
        return Ok(result);
    }

    [HttpGet("sections")]
    public async Task<IActionResult> GetSections(
        [FromQuery] int facultyId, [FromQuery] int termId,
        [FromQuery] int? departmentId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasFacultyAccessAsync(userId, facultyId, ct)) return Forbid();

        pageSize = Math.Clamp(pageSize, 1, 200);
        var result = await _service.GetSectionsAsync(facultyId, termId, departmentId, status, page, pageSize, ct);
        return Ok(result);
    }

    [HttpPost("assign")]
    public async Task<IActionResult> AssignSection([FromBody] AssignSectionRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _service.AssignSectionAsync(request, userId.ToString(), ct);
        return Ok(result);
    }

    [HttpDelete("unassign/{sectionId}")]
    public async Task<IActionResult> UnassignSection(int sectionId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await _service.UnassignSectionAsync(sectionId, userId.ToString(), ct);
        return NoContent();
    }

    [HttpPost("auto-schedule")]
    public async Task<IActionResult> AutoSchedule([FromBody] AutoScheduleRequest request, CancellationToken ct)
    {
        var result = await _service.AutoSchedulePreviewAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("apply-schedule")]
    public async Task<IActionResult> ApplySchedule([FromBody] ApplyScheduleRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _service.ApplyScheduleAsync(request, userId.ToString(), ct);
        return Ok(result);
    }

    [HttpGet("available-classrooms")]
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
    public async Task<IActionResult> GetTimeSlots([FromQuery] int termId, CancellationToken ct)
    {
        var slots = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == termId)
            .OrderBy(ts => ts.DayOfWeek)
            .ThenBy(ts => ts.SlotOrder)
            .Select(ts => new FacultyPlanningTimeSlotDto
            {
                Day = (int)ts.DayOfWeek,
                SlotOrder = ts.SlotOrder,
                StartTime = ts.StartTime,
                EndTime = ts.EndTime,
                DisplayName = ts.DisplayName
            })
            .ToListAsync(ct);

        return Ok(slots);
    }

    [HttpGet("conflicts")]
    public async Task<IActionResult> GetConflicts([FromQuery] int facultyId, [FromQuery] int termId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await HasFacultyAccessAsync(userId, facultyId, ct)) return Forbid();

        var result = await _service.GetConflictsAsync(facultyId, termId, ct);
        return Ok(result);
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
