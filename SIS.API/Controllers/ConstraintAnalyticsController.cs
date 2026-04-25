using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.Scheduling.Constraints;
using SIS.Domain.Enums;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/constraints/analytics")]
[Authorize]
public class ConstraintAnalyticsController : ControllerBase
{
    private readonly IConstraintSnapshotService _snapshotService;

    public ConstraintAnalyticsController(IConstraintSnapshotService snapshotService)
    {
        _snapshotService = snapshotService;
    }

    [HttpPost("snapshot")]
    [RequirePermission("menu.ders-programi-kisitlamalari.snapshot")]
    public async Task<IActionResult> TakeSnapshot([FromQuery] int termId, CancellationToken ct)
    {
        if (termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var snapshot = await _snapshotService.TakeSnapshotAsync(termId, SnapshotType.Manual, userId, ct);

        return Ok(new SnapshotDto(
            snapshot.Id, snapshot.TermId, snapshot.SnapshotAt, snapshot.SnapshotType.ToString(),
            snapshot.TotalHardViolations, snapshot.TotalSoftPenalty, snapshot.IsFeasible));
    }

    [HttpGet("snapshots")]
    [RequirePermission("menu.ders-programi-kisitlamalari.view")]
    public async Task<IActionResult> GetSnapshots(
        [FromQuery] int termId, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        if (termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        var snapshots = await _snapshotService.GetSnapshotsAsync(termId, limit, ct);
        return Ok(snapshots);
    }

    [HttpGet("trend")]
    [RequirePermission("menu.ders-programi-kisitlamalari.view")]
    public async Task<IActionResult> GetTrend([FromQuery] int termId, CancellationToken ct)
    {
        if (termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        var trend = await _snapshotService.GetTrendDataAsync(termId, ct);
        return Ok(trend);
    }

    [HttpGet("compare")]
    [RequirePermission("menu.ders-programi-kisitlamalari.view")]
    public async Task<IActionResult> Compare([FromQuery] int id1, [FromQuery] int id2, CancellationToken ct)
    {
        if (id1 <= 0 || id2 <= 0)
            return BadRequest(new { error = "Gecerli snapshot ID'leri gonderilmelidir." });

        try
        {
            var comparison = await _snapshotService.CompareSnapshotsAsync(id1, id2, ct);
            return Ok(comparison);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("heatmap")]
    [RequirePermission("menu.ders-programi-kisitlamalari.view")]
    public async Task<IActionResult> GetHeatmap([FromQuery] int termId, CancellationToken ct)
    {
        if (termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        var heatmap = await _snapshotService.GetHeatmapAsync(termId, ct);
        return Ok(heatmap);
    }

    [HttpGet("bottlenecks")]
    [RequirePermission("menu.ders-programi-kisitlamalari.view")]
    public async Task<IActionResult> GetBottlenecks([FromQuery] int termId, CancellationToken ct)
    {
        if (termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        var bottlenecks = await _snapshotService.GetBottlenecksAsync(termId, ct);
        return Ok(bottlenecks);
    }
}
