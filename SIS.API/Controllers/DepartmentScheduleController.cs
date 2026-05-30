using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Scheduling.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

/// <summary>
/// Bölüm Ders Programı — öğrencinin kendi programının (program × sınıf × dönem)
/// haftalık ders programı görünümü. Yayın gate'i (Active + !Suspended +
/// FacultyScheduleApproval) RegistrationGate ile aynı.
/// </summary>
[ApiController]
[Route("api/scheduling/department")]
[Authorize]
public sealed class DepartmentScheduleController : ControllerBase
{
    private readonly IDepartmentScheduleService _service;
    private readonly AppDbContext _db;

    public DepartmentScheduleController(IDepartmentScheduleService service, AppDbContext db)
    {
        _service = service;
        _db = db;
    }

    /// <summary>Sayfa açılış context'i — default term/program/classYear + dropdown'lar.</summary>
    [HttpGet("context")]
    [RequirePermission("menu.bolum-ders-programi.view")]
    public async Task<IActionResult> GetContext(CancellationToken cancellationToken)
    {
        var studentId = await TryResolveStudentIdAsync(cancellationToken);
        if (studentId is null)
            return Unauthorized(new { error = "Öğrenci bilgisi bulunamadı." });

        try
        {
            var context = await _service.GetContextAsync(studentId.Value, cancellationToken);
            return Ok(context);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Belirli (term, program, classYear) için haftalık ders programı.</summary>
    [HttpGet("schedule")]
    [RequirePermission("menu.bolum-ders-programi.view")]
    public async Task<IActionResult> GetSchedule(
        [FromQuery] int termId,
        [FromQuery] int programId,
        [FromQuery] int classYear,
        CancellationToken cancellationToken)
    {
        if (termId <= 0 || programId <= 0)
            return BadRequest(new { error = "Geçerli termId ve programId gönderilmelidir." });

        var studentId = await TryResolveStudentIdAsync(cancellationToken);
        if (studentId is null)
            return Unauthorized(new { error = "Öğrenci bilgisi bulunamadı." });

        try
        {
            var data = await _service.GetScheduleAsync(studentId.Value, termId, programId, classYear, cancellationToken);
            return Ok(data);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>PDF render.</summary>
    [HttpGet("schedule/pdf")]
    [RequirePermission("menu.bolum-ders-programi.export")]
    public async Task<IActionResult> GetPdf(
        [FromQuery] int termId,
        [FromQuery] int programId,
        [FromQuery] int classYear,
        CancellationToken cancellationToken)
    {
        if (termId <= 0 || programId <= 0)
            return BadRequest(new { error = "Geçerli termId ve programId gönderilmelidir." });

        var studentId = await TryResolveStudentIdAsync(cancellationToken);
        if (studentId is null)
            return Unauthorized(new { error = "Öğrenci bilgisi bulunamadı." });

        try
        {
            var bytes = await _service.GeneratePdfAsync(studentId.Value, termId, programId, classYear, cancellationToken);
            return File(bytes, "application/pdf", "bolum-ders-programi.pdf");
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private async Task<int?> TryResolveStudentIdAsync(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !int.TryParse(userIdClaim, out var userId))
            return null;
        return await _db.Students
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
