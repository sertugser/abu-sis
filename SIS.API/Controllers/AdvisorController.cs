using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Common.Services;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/advisor")]
[Authorize]
public class AdvisorController : ControllerBase
{
    private readonly ICourseRegistrationService _registrationService;
    private readonly AppDbContext _context;
    private readonly ISystemConfigProvider _systemConfig;

    public AdvisorController(ICourseRegistrationService registrationService, AppDbContext context, ISystemConfigProvider systemConfig)
    {
        _registrationService = registrationService;
        _context = context;
        _systemConfig = systemConfig;
    }

    [HttpGet("pending-approvals")]
    [RequirePermission("menu.ders-kayit-duzenleme.view")]
    public async Task<IActionResult> GetPendingApprovals()
    {
        var calculationMethod = await _systemConfig.GetStringAsync("Instructor.AdvisingCalculationMethod", "standard");
        var instructorId = await GetInstructorIdAsync();
        if (instructorId == null)
            // Ogretim uyesi profili olmayan kullanicilar (orn. SystemAdmin) icin bos liste don.
            // 401 doner isek frontend axios interceptor JWT expired sanip logout yapar.
            return Ok(new PendingApprovalListDto
            {
                CalculationMethod = calculationMethod,
                CalculationMethodLabel = ResolveCalculationMethodLabel(calculationMethod),
                Items = new List<PendingApprovalDto>(),
            });

        var result = await _registrationService.GetPendingApprovalsAsync(instructorId.Value);
        return Ok(new PendingApprovalListDto
        {
            CalculationMethod = calculationMethod,
            CalculationMethodLabel = ResolveCalculationMethodLabel(calculationMethod),
            Items = result,
        });
    }

    [HttpGet("students")]
    [RequirePermission("menu.ders-kayit-duzenleme.view")]
    public async Task<IActionResult> GetAdvisorStudents(CancellationToken cancellationToken = default)
    {
        var instructorId = await GetInstructorIdAsync();
        if (instructorId == null)
            // Ogretim uyesi profili olmayan kullanicilar icin bos liste don (anonymous projection).
            return Ok(Array.Empty<object>());

        var result = await (
            from student in _context.Students.AsNoTracking()
            join user in _context.Users.AsNoTracking() on student.UserId equals user.Id
            join program in _context.StudentPrograms.AsNoTracking() on student.Id equals program.StudentId
            join academicProgram in _context.AcademicPrograms.AsNoTracking() on program.ProgramId equals academicProgram.Id
            where student.AdvisorId == instructorId.Value
                && student.Status == StudentStatus.Active
                && program.IsActive
                && program.Type == StudentProgramType.Main
            orderby user.LastName, user.FirstName
            select new
            {
                StudentId = student.Id,
                student.StudentNumber,
                StudentName = user.FirstName + " " + user.LastName,
                ProgramName = academicProgram.Name,
                student.CompletedSemesters,
                student.Cgpa
            })
            .ToListAsync(cancellationToken);

        return Ok(result);
    }

    [HttpPost("approve")]
    [RequirePermission("menu.ders-kayit-duzenleme.approve")]
    public async Task<IActionResult> Approve([FromBody] ApprovalActionDto dto)
    {
        var instructorId = await GetInstructorIdAsync();
        if (instructorId == null)
            // Mutating action: 403 Forbid — semantik olarak dogru, axios logout etmez.
            return StatusCode(403, new { error = "Bu islem icin ogretim uyesi profili gerekli." });

        var result = await _registrationService.ApproveStudentRegistrationsAsync(instructorId.Value, dto);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("reject")]
    [RequirePermission("menu.ders-kayit-duzenleme.reject")]
    public async Task<IActionResult> Reject([FromBody] ApprovalActionDto dto)
    {
        var instructorId = await GetInstructorIdAsync();
        if (instructorId == null)
            // Mutating action: 403 Forbid — semantik olarak dogru, axios logout etmez.
            return StatusCode(403, new { error = "Bu islem icin ogretim uyesi profili gerekli." });

        var result = await _registrationService.RejectStudentRegistrationsAsync(instructorId.Value, dto);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    private async Task<int?> GetInstructorIdAsync()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return null;

        var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.UserId == userId);
        return instructor?.Id;
    }

    private static string ResolveCalculationMethodLabel(string? value) =>
        string.Equals(value, "load-based", StringComparison.OrdinalIgnoreCase)
            ? "Yük Bazlı"
            : "Standart";
}
