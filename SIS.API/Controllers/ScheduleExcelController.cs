using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
