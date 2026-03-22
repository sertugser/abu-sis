using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "AdministrativeStaff,SystemAdministrator")]
public class SchedulingController : ControllerBase
{
    private readonly ISchedulingService _schedulingService;

    public SchedulingController(ISchedulingService schedulingService)
    {
        _schedulingService = schedulingService;
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

    [HttpGet("validate-existing")]
    public async Task<IActionResult> ValidateExisting([FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (termId.HasValue && termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        var result = await _schedulingService.ValidateExistingAsync(termId, cancellationToken);
        return Ok(result);
    }
}
