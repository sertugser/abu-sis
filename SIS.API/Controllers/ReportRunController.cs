using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.Reports;

namespace SIS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/me/reports")]
public class ReportRunController : ControllerBase
{
    private readonly IReportExecutionService _exec;
    private readonly IReportExportService _export;

    public ReportRunController(IReportExecutionService exec, IReportExportService export)
    {
        _exec = exec;
        _export = export;
    }

    [HttpGet("{code}/schema")]
    [RequirePermission("menu.raporlarim.view")]
    public async Task<IActionResult> GetSchema(string code, CancellationToken ct)
    {
        if (!TryGetUserId(out var uid)) return Unauthorized();
        var schema = await _exec.GetSchemaAsync(uid, code, ct);
        if (schema is null) return Forbid();
        return Ok(schema);
    }

    [HttpPost("{code}/run")]
    [RequirePermission("menu.raporlarim.view")]
    public async Task<IActionResult> Run(string code, [FromBody] ReportRunRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var uid)) return Unauthorized();
        try { return Ok(await _exec.RunAsync(uid, code, request, ct)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("{code}/export")]
    [RequirePermission("menu.raporlarim.view")]
    public async Task<IActionResult> Export(string code, [FromQuery] string format, [FromBody] ReportRunRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var uid)) return Unauthorized();
        ReportResult result;
        try { result = await _exec.RunForExportAsync(uid, code, request, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }

        var title = await _exec.GetReportTitleAsync(code, ct) ?? code;
        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
            return File(_export.ToPdf(title, result), "application/pdf", $"{title}.pdf");

        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
            return File(_export.ToXlsx(title, result),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{title}.xlsx");

        return BadRequest(new { message = "Geçersiz format. 'xlsx' veya 'pdf' olmalı." });
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out userId);
    }
}
