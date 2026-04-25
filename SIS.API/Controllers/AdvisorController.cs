using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
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

    public AdvisorController(ICourseRegistrationService registrationService, AppDbContext context)
    {
        _registrationService = registrationService;
        _context = context;
    }

    [HttpGet("pending-approvals")]
    [RequirePermission("menu.ders-kayit-duzenleme.view")]
    public async Task<IActionResult> GetPendingApprovals()
    {
        var instructorId = await GetInstructorIdAsync();
        if (instructorId == null)
            return Unauthorized(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        var result = await _registrationService.GetPendingApprovalsAsync(instructorId.Value);
        return Ok(result);
    }

    [HttpGet("students")]
    [RequirePermission("menu.ders-kayit-duzenleme.view")]
    public async Task<IActionResult> GetAdvisorStudents(CancellationToken cancellationToken = default)
    {
        var instructorId = await GetInstructorIdAsync();
        if (instructorId == null)
            return Unauthorized(new { error = "Ogretim uyesi bilgisi bulunamadi." });

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
            return Unauthorized(new { error = "Ogretim uyesi bilgisi bulunamadi." });

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
            return Unauthorized(new { error = "Ogretim uyesi bilgisi bulunamadi." });

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
}
