using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/academic-structure")]
[Authorize(Roles = "SystemAdministrator")]
public class AcademicStructureController(IAdminAcademicStructureService service) : ControllerBase
{
    // ─── Faculty ───────────────────────────────────────────────────

    [HttpGet("faculties")]
    public async Task<IActionResult> GetFaculties()
        => Ok(await service.GetFacultiesAsync());

    [HttpPost("faculties")]
    public async Task<IActionResult> CreateFaculty([FromBody] CreateFacultyDto dto)
    {
        try { return Ok(await service.CreateFacultyAsync(dto)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("faculties/{id:int}")]
    public async Task<IActionResult> UpdateFaculty(int id, [FromBody] UpdateFacultyDto dto)
    {
        try { return Ok(await service.UpdateFacultyAsync(id, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("faculties/{id:int}")]
    public async Task<IActionResult> DeleteFaculty(int id)
    {
        try { await service.DeleteFacultyAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ─── Department ────────────────────────────────────────────────

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments([FromQuery] int? facultyId)
        => Ok(await service.GetDepartmentsAsync(facultyId));

    [HttpPost("departments")]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentDto dto)
    {
        try { return Ok(await service.CreateDepartmentAsync(dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("departments/{id:int}")]
    public async Task<IActionResult> UpdateDepartment(int id, [FromBody] UpdateDepartmentDto dto)
    {
        try { return Ok(await service.UpdateDepartmentAsync(id, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("departments/{id:int}")]
    public async Task<IActionResult> DeleteDepartment(int id)
    {
        try { await service.DeleteDepartmentAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ─── Program ───────────────────────────────────────────────────

    [HttpGet("programs")]
    public async Task<IActionResult> GetPrograms([FromQuery] int? departmentId)
        => Ok(await service.GetProgramsAsync(departmentId));

    [HttpPost("programs")]
    public async Task<IActionResult> CreateProgram([FromBody] CreateProgramDto dto)
    {
        try { return Ok(await service.CreateProgramAsync(dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("programs/{id:int}")]
    public async Task<IActionResult> UpdateProgram(int id, [FromBody] UpdateProgramDto dto)
    {
        try { return Ok(await service.UpdateProgramAsync(id, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("programs/{id:int}")]
    public async Task<IActionResult> DeleteProgram(int id)
    {
        try { await service.DeleteProgramAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
