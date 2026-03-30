using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/instructors")]
[Authorize(Roles = "SystemAdministrator,Dean,DepartmentManager,AdministrativeStaff")]
public class InstructorManagementController : ControllerBase
{
    private readonly IAdminInstructorService _service;

    public InstructorManagementController(IAdminInstructorService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetInstructors([FromQuery] AdminInstructorFilterDto filter)
    {
        var result = await _service.GetInstructorsAsync(filter);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetInstructor(int id)
    {
        var result = await _service.GetInstructorByIdAsync(id);
        if (result == null) return NotFound(new { message = "Öğretim üyesi bulunamadı." });
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdministrator,AdministrativeStaff")]
    public async Task<IActionResult> CreateInstructor([FromBody] CreateInstructorDto dto)
    {
        try
        {
            var result = await _service.CreateInstructorAsync(dto);
            return CreatedAtAction(nameof(GetInstructor), new { id = result.Id }, result);
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
}
