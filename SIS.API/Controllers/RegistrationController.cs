using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.CourseRegistration.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/registrations")]
[Authorize]
public class RegistrationController : ControllerBase
{
    private readonly ICourseRegistrationService _registrationService;
    private readonly AppDbContext _context;

    public RegistrationController(ICourseRegistrationService registrationService, AppDbContext context)
    {
        _registrationService = registrationService;
        _context = context;
    }

    /// <summary>
    /// POST api/registration/plan — add a course section to the student's plan.
    /// </summary>
    [HttpPost("plan")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> AddToPlan([FromBody] RegistrationPlanItemDto dto)
    {
        var studentId = await GetStudentIdAsync();
        if (studentId == null)
            return Unauthorized(new { error = "Ogrenci bilgisi bulunamadi." });

        var result = await _registrationService.AddToPlanAsync(studentId.Value, dto.CourseSectionId);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// POST api/registration/submit — submit the registration (validate and finalize).
    /// </summary>
    [HttpPost("submit")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> SubmitRegistration([FromBody] RegistrationSubmitDto dto)
    {
        var studentId = await GetStudentIdAsync();
        if (studentId == null)
            return Unauthorized(new { error = "Ogrenci bilgisi bulunamadi." });

        var result = await _registrationService.SubmitRegistrationAsync(studentId.Value, dto);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// GET api/registration/my — list the student's registrations for the current term.
    /// </summary>
    [HttpGet("my")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyRegistrations()
    {
        var studentId = await GetStudentIdAsync();
        if (studentId == null)
            return Unauthorized(new { error = "Ogrenci bilgisi bulunamadi." });

        var registrations = await _registrationService.GetStudentRegistrationsAsync(studentId.Value);
        return Ok(registrations);
    }

    /// <summary>
    /// GET api/registrations/history — list the student's course history with grades, grouped by term.
    /// </summary>
    [HttpGet("history")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetCourseHistory()
    {
        var studentId = await GetStudentIdAsync();
        if (studentId == null)
            return Unauthorized(new { error = "Ogrenci bilgisi bulunamadi." });

        var history = await _registrationService.GetCourseHistoryAsync(studentId.Value);
        return Ok(history);
    }

    /// <summary>
    /// DELETE api/registration/{id}/drop — drop a course (add-drop period).
    /// </summary>
    [HttpDelete("{id}/drop")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> DropCourse(int id)
    {
        var studentId = await GetStudentIdAsync();
        if (studentId == null)
            return Unauthorized(new { error = "Ogrenci bilgisi bulunamadi." });

        var result = await _registrationService.DropCourseAsync(studentId.Value, id);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// POST api/registration/{id}/withdraw — withdraw from a course.
    /// </summary>
    [HttpPost("{id}/withdraw")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> WithdrawCourse(int id)
    {
        var studentId = await GetStudentIdAsync();
        if (studentId == null)
            return Unauthorized(new { error = "Ogrenci bilgisi bulunamadi." });

        var result = await _registrationService.WithdrawCourseAsync(studentId.Value, id);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Gets the Student.Id from the JWT claims (ClaimTypes.NameIdentifier -> UserId -> Student).
    /// </summary>
    private async Task<int?> GetStudentIdAsync()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return null;

        var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
        return student?.Id;
    }
}
