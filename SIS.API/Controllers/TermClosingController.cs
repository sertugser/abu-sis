using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.TermClosing.DTOs;
using SIS.Application.TermClosing.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/term-closing")]
[Authorize(Roles = "DepartmentManager,SystemAdministrator")]
public class TermClosingController : ControllerBase
{
    private readonly ITermClosingService _service;

    public TermClosingController(ITermClosingService service)
    {
        _service = service;
    }

    [HttpGet("{departmentId}/status")]
    public async Task<IActionResult> GetStatus(int departmentId, CancellationToken ct)
    {
        var result = await _service.GetStatusAsync(departmentId, ct);
        return Ok(result);
    }

    [HttpPost("{departmentId}/lock-grades")]
    public async Task<IActionResult> LockGrades(int departmentId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _service.LockGradesAsync(departmentId, userId.Value, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{departmentId}/unlock-grades")]
    public async Task<IActionResult> UnlockGrades(int departmentId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _service.UnlockGradesAsync(departmentId, userId.Value, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{departmentId}/calculate-gpa")]
    public async Task<IActionResult> CalculateGpa(int departmentId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _service.CalculateGpaAsync(departmentId, userId.Value, ct);
        return Ok(result);
    }

    [HttpGet("{departmentId}/preview-promote")]
    public async Task<IActionResult> PreviewPromotion(int departmentId, CancellationToken ct)
    {
        var result = await _service.PreviewPromotionAsync(departmentId, ct);
        return Ok(result);
    }

    [HttpPost("{departmentId}/promote")]
    public async Task<IActionResult> Promote(int departmentId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _service.PromoteAsync(departmentId, userId.Value, ct);
        return Ok(result);
    }

    [HttpPost("{departmentId}/rollback-promote")]
    public async Task<IActionResult> RollbackPromotion(int departmentId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _service.RollbackPromotionAsync(departmentId, userId.Value, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{departmentId}/open-new-term")]
    public async Task<IActionResult> OpenNewTerm(int departmentId, [FromBody] NewTermRequestDto request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _service.OpenNewTermAsync(departmentId, userId.Value, request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim, out var id) ? id : null;
    }
}
