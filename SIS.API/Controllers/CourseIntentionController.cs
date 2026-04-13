using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseIntention.DTOs;
using SIS.Application.CourseIntention.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/course-intention")]
[Authorize]
public class CourseIntentionController : ControllerBase
{
    private readonly ICourseIntentionService _courseIntentionService;
    private readonly AppDbContext _context;

    public CourseIntentionController(ICourseIntentionService courseIntentionService, AppDbContext context)
    {
        _courseIntentionService = courseIntentionService;
        _context = context;
    }

    [HttpGet("active")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetActiveSurvey(CancellationToken cancellationToken = default)
    {
        var studentId = await GetStudentIdAsync(cancellationToken);
        if (studentId == null)
            return Unauthorized(new { error = "Ogrenci bilgisi bulunamadi." });

        var result = await _courseIntentionService.GetActiveSurveyForStudentAsync(studentId.Value, cancellationToken);
        return result == null
            ? NotFound(new { error = "Acik niyet anketi bulunamadi." })
            : Ok(result);
    }

    [HttpGet("surveys/{surveyId:int}/me")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetSurveyForStudent(int surveyId, CancellationToken cancellationToken = default)
    {
        if (surveyId <= 0)
            return BadRequest(new { error = "Gecerli bir surveyId gonderilmelidir." });

        var studentId = await GetStudentIdAsync(cancellationToken);
        if (studentId == null)
            return Unauthorized(new { error = "Ogrenci bilgisi bulunamadi." });

        var result = await _courseIntentionService.GetSurveyForStudentAsync(studentId.Value, surveyId, cancellationToken);
        return result == null
            ? NotFound(new { error = "Anket bulunamadi." })
            : Ok(result);
    }

    [HttpPost("save")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> SaveResponses([FromBody] SaveCourseIntentionsRequest request, CancellationToken cancellationToken = default)
    {
        var studentId = await GetStudentIdAsync(cancellationToken);
        if (studentId == null)
            return Unauthorized(new { error = "Ogrenci bilgisi bulunamadi." });

        try
        {
            var result = await _courseIntentionService.SaveResponsesAsync(studentId.Value, request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("surveys")]
    [Authorize(Roles = "AdministrativeStaff,DepartmentManager,Dean,SystemAdministrator")]
    public async Task<IActionResult> GetSurveys([FromQuery] int? termId = null, CancellationToken cancellationToken = default)
    {
        var result = await _courseIntentionService.GetSurveysAsync(termId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("surveys")]
    [Authorize(Roles = "AdministrativeStaff,DepartmentManager,Dean,SystemAdministrator")]
    public async Task<IActionResult> UpsertSurvey([FromBody] UpsertCourseIntentionSurveyRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _courseIntentionService.UpsertSurveyAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("surveys/{surveyId:int}/state")]
    [Authorize(Roles = "AdministrativeStaff,DepartmentManager,Dean,SystemAdministrator")]
    public async Task<IActionResult> SetSurveyState(int surveyId, [FromBody] SetCourseIntentionSurveyStateRequest request, CancellationToken cancellationToken = default)
    {
        if (surveyId <= 0)
            return BadRequest(new { error = "Gecerli bir surveyId gonderilmelidir." });

        var result = await _courseIntentionService.SetSurveyStateAsync(surveyId, request, cancellationToken);
        return result == null
            ? NotFound(new { error = "Anket bulunamadi." })
            : Ok(result);
    }

    [HttpGet("surveys/{surveyId:int}/summary")]
    [Authorize(Roles = "AdministrativeStaff,DepartmentManager,Dean,SystemAdministrator")]
    public async Task<IActionResult> GetSurveySummary(int surveyId, [FromQuery] int? departmentId = null, CancellationToken cancellationToken = default)
    {
        if (surveyId <= 0)
            return BadRequest(new { error = "Gecerli bir surveyId gonderilmelidir." });

        var result = await _courseIntentionService.GetSurveySummaryAsync(surveyId, departmentId, cancellationToken);
        return result == null
            ? NotFound(new { error = "Anket bulunamadi." })
            : Ok(result);
    }

    private async Task<int?> GetStudentIdAsync(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return null;

        var student = await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        return student?.Id;
    }
}
