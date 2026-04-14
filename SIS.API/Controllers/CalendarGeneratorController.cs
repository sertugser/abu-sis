using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.CalendarGenerator.DTOs;
using SIS.Application.CalendarGenerator.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/calendar-generator")]
[Authorize(Roles = "SystemAdministrator,AdministrativeStaff")]
public class CalendarGeneratorController : ControllerBase
{
    private readonly ICalendarGeneratorService _generatorService;
    private readonly ICalendarTemplateService _templateService;

    public CalendarGeneratorController(ICalendarGeneratorService generatorService, ICalendarTemplateService templateService)
    {
        _generatorService = generatorService;
        _templateService = templateService;
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates(CancellationToken ct) =>
        Ok(await _templateService.GetTemplatesAsync(ct));

    [HttpPut("templates")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> ReplaceTemplates([FromBody] IReadOnlyList<UpsertCalendarTemplateDto> templates, CancellationToken ct) =>
        Ok(await _templateService.ReplaceTemplatesAsync(templates, ct));

    [HttpGet("holidays/definitions")]
    public async Task<IActionResult> GetHolidayDefinitions(CancellationToken ct) =>
        Ok(await _templateService.GetHolidayDefinitionsAsync(ct));

    [HttpPost("holidays/definitions")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> UpsertHolidayDefinition([FromQuery] int? id, [FromBody] UpsertHolidayDefinitionDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _templateService.UpsertHolidayDefinitionAsync(id, dto, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateCalendarRequestDto request, CancellationToken ct)
    {
        try
        {
            return Ok(await _generatorService.GenerateAsync(request, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("drafts/{id:int}")]
    public async Task<IActionResult> GetDraft(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await _generatorService.GetDraftAsync(id, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("drafts/{id:int}/events/{eventId}")]
    public async Task<IActionResult> UpdateDraftEvent(int id, string eventId, [FromBody] UpdateDraftEventDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _generatorService.UpdateDraftEventAsync(id, eventId, dto, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("drafts/{id:int}/what-if")]
    public async Task<IActionResult> WhatIf(int id, [FromBody] WhatIfRequestDto request, CancellationToken ct)
    {
        try
        {
            return Ok(await _generatorService.WhatIfAsync(id, request, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("drafts/{id:int}/apply")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> Apply(int id, [FromBody] ApplyCalendarDraftRequestDto request, CancellationToken ct)
    {
        try
        {
            return Ok(await _generatorService.ApplyAsync(id, request, GetUserId(), ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("drafts/{id:int}/rollback")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> Rollback(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await _generatorService.RollbackAsync(id, GetUserId(), ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}
