using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.AcademicOperations.Services;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "AdministrativeStaff,DepartmentManager,SystemAdministrator")]
public class SchedulingController : ControllerBase
{
    private readonly ISchedulingService _schedulingService;
    private readonly IAcademicOperationsService _academicOperationsService;

    public SchedulingController(
        ISchedulingService schedulingService,
        IAcademicOperationsService academicOperationsService)
    {
        _schedulingService = schedulingService;
        _academicOperationsService = academicOperationsService;
    }

    [HttpGet("conflicts")]
    public async Task<IActionResult> GetConflicts([FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (termId.HasValue && termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        var conflicts = await _schedulingService.GetConflictsAsync(termId, cancellationToken);
        return Ok(conflicts);
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ScheduleValidationRequestDto request, CancellationToken cancellationToken)
    {
        if (request.SectionId <= 0)
            return BadRequest(new { error = "Gecerli bir sectionId gonderilmelidir." });

        var result = await _schedulingService.ValidateAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("suggest")]
    public async Task<IActionResult> Suggest([FromBody] ScheduleSuggestionRequestDto request, CancellationToken cancellationToken)
    {
        if (request.SectionId <= 0)
            return BadRequest(new { error = "Gecerli bir sectionId gonderilmelidir." });

        var result = await _schedulingService.SuggestAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] ApplyScheduleRequestDto request, CancellationToken cancellationToken)
    {
        if (request.SectionId <= 0)
            return BadRequest(new { error = "Gecerli bir sectionId gonderilmelidir." });

        if (request.ScheduleSlots == null || request.ScheduleSlots.Count == 0)
            return BadRequest(new { error = "En az bir schedule slot gonderilmelidir." });

        var result = await _schedulingService.ApplyAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("validate-existing")]
    public async Task<IActionResult> ValidateExisting([FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (termId.HasValue && termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        var result = await _schedulingService.ValidateExistingAsync(termId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("batch-auto-schedule")]
    public async Task<IActionResult> BatchAutoSchedule([FromBody] BatchAutoScheduleRequestDto request, CancellationToken cancellationToken)
    {
        if (request.TermId.HasValue && request.TermId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        if (request.DepartmentId.HasValue && request.DepartmentId <= 0)
            return BadRequest(new { error = "Gecerli bir departmentId gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out var roles))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        var context = await _academicOperationsService.GetContextAsync(userId, roles, cancellationToken);

        if (request.DepartmentId.HasValue)
        {
            var hasDepartmentAccess = context.AccessibleDepartments
                .Any(department => department.DepartmentId == request.DepartmentId.Value);

            if (!hasDepartmentAccess)
                return Forbid();
        }
        else if (!context.CanViewAllDepartments)
        {
            if (!context.DefaultDepartmentId.HasValue)
                return BadRequest(new { error = "Bu kullanici icin erisilebilir bolum bulunamadi." });

            request.DepartmentId = context.DefaultDepartmentId.Value;
        }

        var result = await _schedulingService.BatchAutoScheduleAsync(request, cancellationToken);
        return Ok(result);
    }

    private bool TryGetAuthenticatedUser(out int userId, out IReadOnlyCollection<string> roles)
    {
        userId = 0;
        roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToList();

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userIdClaim != null && int.TryParse(userIdClaim, out userId);
    }
}
