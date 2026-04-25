using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.AdvisorConstraints.DTOs;
using SIS.Application.AdvisorConstraints.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/advisor/constraints")]
[Authorize]
public class AdvisorConstraintController : ControllerBase
{
    private readonly IAdvisorConstraintService _constraintService;
    private readonly AppDbContext _context;

    public AdvisorConstraintController(IAdvisorConstraintService constraintService, AppDbContext context)
    {
        _constraintService = constraintService;
        _context = context;
    }

    [HttpGet("student/{studentId:int}")]
    [RequirePermission("menu.danisman-ders-kisitlari.view")]
    public async Task<IActionResult> GetStudentConstraints(int studentId, [FromQuery] int? termId = null, CancellationToken cancellationToken = default)
    {
        if (studentId <= 0)
            return BadRequest(new { error = "Gecerli bir studentId gonderilmelidir." });

        var constraints = await _constraintService.GetStudentConstraintsAsync(studentId, termId, cancellationToken);
        return Ok(constraints);
    }

    [HttpPost]
    [RequirePermission("menu.danisman-ders-kisitlari.create")]
    public async Task<IActionResult> UpsertConstraint([FromBody] UpsertAdvisorCourseConstraintRequest request, CancellationToken cancellationToken = default)
    {
        var instructorId = await GetInstructorIdAsync(cancellationToken);
        if (instructorId == null)
            return Unauthorized(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        try
        {
            var result = await _constraintService.UpsertConstraintAsync(instructorId.Value, request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("menu.danisman-ders-kisitlari.edit")]
    public async Task<IActionResult> UpdateConstraint(int id, [FromBody] UpdateAdvisorCourseConstraintRequest request, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return BadRequest(new { error = "Gecerli bir constraint id gonderilmelidir." });

        var instructorId = await GetInstructorIdAsync(cancellationToken);
        if (instructorId == null)
            return Unauthorized(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        try
        {
            var result = await _constraintService.UpdateConstraintAsync(instructorId.Value, id, request, cancellationToken);
            return result == null
                ? NotFound(new { error = "Kisit bulunamadi." })
                : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("menu.danisman-ders-kisitlari.delete")]
    public async Task<IActionResult> DeleteConstraint(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return BadRequest(new { error = "Gecerli bir constraint id gonderilmelidir." });

        var instructorId = await GetInstructorIdAsync(cancellationToken);
        if (instructorId == null)
            return Unauthorized(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        try
        {
            var deleted = await _constraintService.DeleteConstraintAsync(instructorId.Value, id, cancellationToken);
            return deleted ? NoContent() : NotFound(new { error = "Kisit bulunamadi." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<int?> GetInstructorIdAsync(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return null;

        var instructor = await _context.Instructors
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.UserId == userId, cancellationToken);

        return instructor?.Id;
    }
}
