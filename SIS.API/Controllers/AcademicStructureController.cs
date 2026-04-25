using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/academic-structure")]
[Authorize]
public class AcademicStructureController(IAdminAcademicStructureService service) : ControllerBase
{
    // ─── Faculty ───────────────────────────────────────────────────

    [HttpGet("faculties")]
    [RequirePermission("menu.fakulte-tanimlari.view")]
    public async Task<IActionResult> GetFaculties()
        => Ok(await service.GetFacultiesAsync());

    [HttpPost("faculties")]
    [RequirePermission("menu.fakulte-tanimlari.create")]
    public async Task<IActionResult> CreateFaculty([FromBody] CreateFacultyDto dto)
    {
        try { return Ok(await service.CreateFacultyAsync(dto)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("faculties/{id:int}")]
    [RequirePermission("menu.fakulte-tanimlari.edit")]
    public async Task<IActionResult> UpdateFaculty(int id, [FromBody] UpdateFacultyDto dto)
    {
        try { return Ok(await service.UpdateFacultyAsync(id, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("faculties/{id:int}")]
    [RequirePermission("menu.fakulte-tanimlari.delete")]
    public async Task<IActionResult> DeleteFaculty(int id)
    {
        try { await service.DeleteFacultyAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ─── Department ────────────────────────────────────────────────

    [HttpGet("departments")]
    [RequirePermission("menu.bolum-tanimlari.view")]
    public async Task<IActionResult> GetDepartments([FromQuery] int? facultyId)
        => Ok(await service.GetDepartmentsAsync(facultyId));

    [HttpPost("departments")]
    [RequirePermission("menu.bolum-tanimlari.create")]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentDto dto)
    {
        try { return Ok(await service.CreateDepartmentAsync(dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("departments/{id:int}")]
    [RequirePermission("menu.bolum-tanimlari.edit")]
    public async Task<IActionResult> UpdateDepartment(int id, [FromBody] UpdateDepartmentDto dto)
    {
        try { return Ok(await service.UpdateDepartmentAsync(id, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("departments/{id:int}")]
    [RequirePermission("menu.bolum-tanimlari.delete")]
    public async Task<IActionResult> DeleteDepartment(int id)
    {
        try { await service.DeleteDepartmentAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ─── Program ───────────────────────────────────────────────────

    [HttpGet("programs")]
    [RequirePermission("menu.program-tanimlari.view")]
    public async Task<IActionResult> GetPrograms([FromQuery] int? departmentId)
        => Ok(await service.GetProgramsAsync(departmentId));

    [HttpPost("programs")]
    [RequirePermission("menu.program-tanimlari.create")]
    public async Task<IActionResult> CreateProgram([FromBody] CreateProgramDto dto)
    {
        try { return Ok(await service.CreateProgramAsync(dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("programs/{id:int}")]
    [RequirePermission("menu.program-tanimlari.edit")]
    public async Task<IActionResult> UpdateProgram(int id, [FromBody] UpdateProgramDto dto)
    {
        try { return Ok(await service.UpdateProgramAsync(id, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("programs/{id:int}")]
    [RequirePermission("menu.program-tanimlari.delete")]
    public async Task<IActionResult> DeleteProgram(int id)
    {
        try { await service.DeleteProgramAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
