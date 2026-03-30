using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/students")]
[Authorize(Roles = "SystemAdministrator,Dean,DepartmentManager,AdministrativeStaff")]
public class StudentManagementController : ControllerBase
{
    private readonly IAdminStudentService _service;

    public StudentManagementController(IAdminStudentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetStudents([FromQuery] AdminStudentFilterDto filter)
    {
        var result = await _service.GetStudentsAsync(filter);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetStudent(int id)
    {
        var result = await _service.GetStudentByIdAsync(id);
        if (result == null) return NotFound(new { message = "Öğrenci bulunamadı." });
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdministrator,AdministrativeStaff")]
    public async Task<IActionResult> CreateStudent([FromBody] CreateStudentDto dto)
    {
        try
        {
            var result = await _service.CreateStudentAsync(dto);
            return CreatedAtAction(nameof(GetStudent), new { id = result.Id }, result);
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

    [HttpPut("{id:int}/advisor")]
    [Authorize(Roles = "SystemAdministrator,DepartmentManager,AdministrativeStaff")]
    public async Task<IActionResult> AssignAdvisor(int id, [FromBody] AssignAdvisorDto dto)
    {
        try
        {
            await _service.AssignAdvisorAsync(id, dto.AdvisorId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}

public record AssignAdvisorDto(int AdvisorId);
