using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.FacultyPlanning.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/faculty-planning")]
[Authorize(Roles = "DepartmentManager,Dean,AdministrativeStaff,SystemAdministrator")]
public class FacultyPlanningController : ControllerBase
{
    private readonly IFacultyPlanningService _service;

    public FacultyPlanningController(IFacultyPlanningService service)
    {
        _service = service;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException();

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] int facultyId, [FromQuery] int termId, CancellationToken ct)
    {
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
        pageSize = Math.Clamp(pageSize, 1, 200);
        var result = await _service.GetSectionsAsync(facultyId, termId, departmentId, status, page, pageSize, ct);
        return Ok(result);
    }

    [HttpPost("assign")]
    public async Task<IActionResult> AssignSection([FromBody] AssignSectionRequest request, CancellationToken ct)
    {
        var result = await _service.AssignSectionAsync(request, GetUserId(), ct);
        return Ok(result);
    }

    [HttpDelete("unassign/{sectionId}")]
    public async Task<IActionResult> UnassignSection(int sectionId, CancellationToken ct)
    {
        await _service.UnassignSectionAsync(sectionId, GetUserId(), ct);
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
        var result = await _service.ApplyScheduleAsync(request, GetUserId(), ct);
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

    [HttpGet("conflicts")]
    public async Task<IActionResult> GetConflicts([FromQuery] int facultyId, [FromQuery] int termId, CancellationToken ct)
    {
        var result = await _service.GetConflictsAsync(facultyId, termId, ct);
        return Ok(result);
    }
}
