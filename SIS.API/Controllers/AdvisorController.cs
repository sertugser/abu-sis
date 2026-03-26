using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.CourseRegistration.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/advisor")]
[Authorize(Roles = "AcademicAdvisor,Instructor")]
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
    public async Task<IActionResult> GetPendingApprovals()
    {
        var instructorId = await GetInstructorIdAsync();
        if (instructorId == null)
            return Unauthorized(new { error = "Ogretim uyesi bilgisi bulunamadi." });

        var result = await _registrationService.GetPendingApprovalsAsync(instructorId.Value);
        return Ok(result);
    }

    [HttpPost("approve")]
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
