using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/courses")]
[Authorize(Roles = "SystemAdministrator,AdministrativeStaff")]
public class CourseCatalogAdminController(IAdminCourseCatalogService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCourses([FromQuery] CourseFilterDto filter)
        => Ok(await service.GetCoursesAsync(filter));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCourse(int id)
    {
        var result = await service.GetCourseByIdAsync(id);
        return result == null ? NotFound(new { message = "Ders bulunamadı." }) : Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> CreateCourse([FromBody] CreateCourseDto dto)
    {
        try { return Ok(await service.CreateCourseAsync(dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> UpdateCourse(int id, [FromBody] UpdateCourseDto dto)
    {
        try { return Ok(await service.UpdateCourseAsync(id, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> DeleteCourse(int id)
    {
        try { await service.DeleteCourseAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/prerequisites")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> AddPrerequisite(int id, [FromBody] AddPrerequisiteDto dto)
    {
        try { await service.AddPrerequisiteAsync(id, dto); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}/prerequisites/{requiredCourseId:int}")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> RemovePrerequisite(int id, int requiredCourseId)
    {
        try { await service.RemovePrerequisiteAsync(id, requiredCourseId); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/equivalencies")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> AddEquivalency(int id, [FromBody] AddEquivalencyDto dto)
    {
        try { await service.AddEquivalencyAsync(id, dto); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}/equivalencies/{equivalentCourseId:int}")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> RemoveEquivalency(int id, int equivalentCourseId)
    {
        try { await service.RemoveEquivalencyAsync(id, equivalentCourseId); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

}
