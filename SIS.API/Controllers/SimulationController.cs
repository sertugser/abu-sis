using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/constraints/simulate")]
[Authorize(Roles = "DepartmentManager,Dean,AdministrativeStaff,SystemAdministrator")]
public class SimulationController : ControllerBase
{
    private readonly ISimulationService _simulationService;

    public SimulationController(ISimulationService simulationService)
    {
        _simulationService = simulationService;
    }

    /// <summary>
    /// Simulate moving a section to a different day/time/classroom.
    /// Returns before/after constraint evaluation diff.
    /// </summary>
    [HttpPost("move")]
    public async Task<IActionResult> SimulateMove(
        [FromBody] SimulateMoveRequest request, CancellationToken ct)
    {
        var result = await _simulationService.SimulateMoveAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get best alternative slot/classroom suggestions for a section.
    /// </summary>
    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestions(
        [FromQuery] int sectionId,
        [FromQuery] int termId,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        if (limit < 1) limit = 1;
        if (limit > 50) limit = 50;

        var result = await _simulationService.GetSuggestionsAsync(sectionId, termId, limit, ct);
        return Ok(result);
    }

    /// <summary>
    /// Simulate constraint definition changes (enable/disable, weight, parameters).
    /// Returns before/after constraint evaluation diff.
    /// </summary>
    [HttpPost("constraint-change")]
    public async Task<IActionResult> SimulateConstraintChange(
        [FromBody] SimulateConstraintChangeRequest request, CancellationToken ct)
    {
        var result = await _simulationService.SimulateConstraintChangeAsync(request, ct);
        return Ok(result);
    }
}
