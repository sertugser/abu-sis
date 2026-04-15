using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/schedule-excel")]
[Authorize(Roles = "AdministrativeStaff,DepartmentManager,Dean,SystemAdministrator")]
public class ScheduleExcelController : ControllerBase
{
    private readonly IExcelScheduleService _excelService;

    public ScheduleExcelController(IExcelScheduleService excelService)
    {
        _excelService = excelService;
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportSchedule(
        [FromQuery] int termId,
        [FromQuery] int facultyId,
        [FromQuery] int? departmentId = null,
        CancellationToken ct = default)
    {
        var bytes = await _excelService.ExportScheduleAsync(termId, facultyId, departmentId, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ders-programi.xlsx");
    }

    [HttpPost("import-preview")]
    public async Task<ActionResult<ImportPreviewDto>> ImportPreview(
        [FromQuery] int termId,
        [FromQuery] int facultyId,
        IFormFile file,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Dosya seçilmedi.");

        using var stream = file.OpenReadStream();
        var result = await _excelService.PreviewImportAsync(stream, termId, facultyId, ct);
        return Ok(result);
    }

    [HttpPost("import-apply")]
    public async Task<ActionResult<ImportResultDto>> ImportApply(
        [FromBody] ImportApplyRequest request,
        CancellationToken ct = default)
    {
        var result = await _excelService.ApplyImportAsync(request.TermId, request.RowNumbers, request.Preview, ct);
        return Ok(result);
    }
}

public class ImportApplyRequest
{
    public int TermId { get; set; }
    public List<int> RowNumbers { get; set; } = new();
    public ImportPreviewDto Preview { get; set; } = new();
}
