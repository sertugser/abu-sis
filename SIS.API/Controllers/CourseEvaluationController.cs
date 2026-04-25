using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.CourseEvaluation.DTOs;
using SIS.Application.CourseEvaluation.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/course-evaluation")]
[Authorize]
public class CourseEvaluationController : ControllerBase
{
    private readonly ICourseEvaluationService _service;

    public CourseEvaluationController(ICourseEvaluationService service)
    {
        _service = service;
    }

    [HttpGet("period/active")]
    [RequirePermission("lookup.evaluation-period")]
    public async Task<IActionResult> GetActivePeriod(CancellationToken ct)
    {
        var period = await _service.GetActivePeriodAsync(ct);
        if (period == null)
            return Ok(new { isOpen = false });
        return Ok(new { isOpen = period.IsCurrentlyOpen, period });
    }

    [HttpGet("my-courses")]
    [RequirePermission("menu.ders-degerlendirme.view")]
    public async Task<IActionResult> GetMyCourses(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var courses = await _service.GetMyCoursesAsync(userId.Value, ct);
        return Ok(courses);
    }

    [HttpGet("questions")]
    [RequirePermission("menu.ders-degerlendirme.view")]
    public async Task<IActionResult> GetActiveQuestions(CancellationToken ct)
    {
        var questions = await _service.GetActiveQuestionsAsync(ct);
        return Ok(questions);
    }

    [HttpPost("submit")]
    [RequirePermission("menu.ders-degerlendirme.submit")]
    public async Task<IActionResult> Submit([FromBody] SubmitEvaluationDto dto, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var (success, error) = await _service.SubmitEvaluationAsync(userId.Value, dto, ct);
        if (!success)
            return BadRequest(new { error });

        return Ok(new { success = true });
    }

    [HttpGet("results/section/{sectionId:int}")]
    [RequirePermission("menu.degerlendirme-sonuclari.view")]
    public async Task<IActionResult> GetSectionResults(int sectionId, CancellationToken ct)
    {
        var result = await _service.GetSectionResultsAsync(sectionId, ct);
        if (result == null)
            return NotFound(new { error = "Ders bulunamadi." });

        return Ok(result);
    }

    [HttpGet("results/department/{departmentId:int}")]
    [RequirePermission("menu.degerlendirme-sonuclari.view")]
    public async Task<IActionResult> GetDepartmentSummary(
        int departmentId,
        [FromQuery] int? termId,
        CancellationToken ct)
    {
        var summary = await _service.GetDepartmentSummaryAsync(departmentId, termId, ct);
        if (summary == null)
            return NotFound(new { error = "Bolum veya donem bulunamadi." });

        return Ok(summary);
    }

    [HttpPost("period/open")]
    [RequirePermission("menu.degerlendirme-donemi-yonetimi.open")]
    public async Task<IActionResult> OpenPeriod([FromBody] OpenPeriodRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        try
        {
            var period = await _service.OpenPeriodAsync(request.TermId, request.ClosesAt, userId.Value.ToString(), ct);
            return Ok(period);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("period/{periodId:int}/close")]
    [RequirePermission("menu.degerlendirme-donemi-yonetimi.close")]
    public async Task<IActionResult> ClosePeriod(int periodId, CancellationToken ct)
    {
        try
        {
            var period = await _service.ClosePeriodAsync(periodId, ct);
            return Ok(period);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("questions/all")]
    [RequirePermission("menu.degerlendirme-donemi-yonetimi.view")]
    public async Task<IActionResult> GetAllQuestions(CancellationToken ct)
    {
        var questions = await _service.GetAllQuestionsAsync(ct);
        return Ok(questions);
    }

    [HttpPost("questions")]
    [RequirePermission("menu.degerlendirme-donemi-yonetimi.create")]
    public async Task<IActionResult> CreateQuestion([FromBody] CreateEvaluationQuestionDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Text))
            return BadRequest(new { error = "Soru metni bos olamaz." });

        var question = await _service.CreateQuestionAsync(dto, ct);
        return Ok(question);
    }

    [HttpPut("questions/{id:int}")]
    [RequirePermission("menu.degerlendirme-donemi-yonetimi.edit")]
    public async Task<IActionResult> UpdateQuestion(int id, [FromBody] UpdateEvaluationQuestionDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Text))
            return BadRequest(new { error = "Soru metni bos olamaz." });

        var question = await _service.UpdateQuestionAsync(id, dto, ct);
        if (question == null)
            return NotFound(new { error = "Soru bulunamadi." });

        return Ok(question);
    }

    [HttpDelete("questions/{id:int}")]
    [RequirePermission("menu.degerlendirme-donemi-yonetimi.delete")]
    public async Task<IActionResult> DeleteQuestion(int id, CancellationToken ct)
    {
        var success = await _service.DeleteQuestionAsync(id, ct);
        if (!success)
            return NotFound(new { error = "Soru bulunamadi." });

        return NoContent();
    }

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim == null || !int.TryParse(claim, out var userId))
            return null;
        return userId;
    }

    public record OpenPeriodRequest(int TermId, DateTime ClosesAt);
}
