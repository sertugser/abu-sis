using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Scheduling.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/scheduling/student/me")]
[Authorize]
public sealed class StudentTimetableController : ControllerBase
{
    private readonly IStudentTimetableService _service;
    private readonly AppDbContext _db;

    public StudentTimetableController(IStudentTimetableService service, AppDbContext db)
    {
        _service = service;
        _db = db;
    }

    [HttpGet("timetable/terms")]
    [RequirePermission("menu.ders-programim.view")]
    public async Task<IActionResult> GetTerms(CancellationToken cancellationToken)
    {
        var studentId = await TryResolveStudentIdAsync(cancellationToken);
        if (studentId is null)
            return Unauthorized(new { error = "Öğrenci bilgisi bulunamadı." });
        var terms = await _service.GetEnrolledTermsAsync(studentId.Value, cancellationToken);
        return Ok(terms);
    }

    [HttpGet("timetable")]
    [RequirePermission("menu.ders-programim.view")]
    public async Task<IActionResult> GetTimetable([FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (termId.HasValue && termId.Value <= 0)
            return BadRequest(new { error = "Geçerli bir termId gönderilmelidir." });

        var studentId = await TryResolveStudentIdAsync(cancellationToken);
        if (studentId is null)
            return Unauthorized(new { error = "Öğrenci bilgisi bulunamadı." });

        var data = await _service.GetTimetableAsync(studentId.Value, termId, cancellationToken);
        return Ok(data);
    }

    [HttpGet("timetable/pdf")]
    [RequirePermission("menu.ders-programim.view")]
    public async Task<IActionResult> GetPdf([FromQuery] int termId, CancellationToken cancellationToken)
    {
        if (termId <= 0)
            return BadRequest(new { error = "Geçerli bir termId gönderilmelidir." });

        var studentId = await TryResolveStudentIdAsync(cancellationToken);
        if (studentId is null)
            return Unauthorized(new { error = "Öğrenci bilgisi bulunamadı." });

        var bytes = await _service.GeneratePdfAsync(studentId.Value, termId, cancellationToken);
        return File(bytes, "application/pdf", "ders-programi.pdf");
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
