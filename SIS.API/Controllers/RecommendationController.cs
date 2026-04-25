using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Recommendation.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/recommendation")]
[Authorize]
public class RecommendationController : ControllerBase
{
    private readonly IRecommendationService _recommendationService;
    private readonly ICourseDifficultyService _courseDifficultyService;
    private readonly ICriticalPathService _criticalPathService;
    private readonly AppDbContext _context;

    public RecommendationController(
        IRecommendationService recommendationService,
        ICourseDifficultyService courseDifficultyService,
        ICriticalPathService criticalPathService,
        AppDbContext context)
    {
        _recommendationService = recommendationService;
        _courseDifficultyService = courseDifficultyService;
        _criticalPathService = criticalPathService;
        _context = context;
    }

    [HttpGet("my")]
    [RequirePermission("menu.akilli-planlama.view")]
    public async Task<IActionResult> GetMyRecommendations([FromQuery] int pacePreference = 50, CancellationToken cancellationToken = default)
    {
        var studentId = await GetStudentIdAsync(cancellationToken);
        if (studentId == null)
            return Unauthorized(new { error = "Ogrenci bilgisi bulunamadi." });

        var result = await _recommendationService.GetStudentRecommendationsAsync(studentId.Value, pacePreference, cancellationToken);
        return Ok(result);
    }

    [HttpGet("difficulty/{courseId:int}")]
    [RequirePermission("menu.akilli-planlama.view")]
    public async Task<IActionResult> GetCourseDifficulty(int courseId, CancellationToken cancellationToken = default)
    {
        if (courseId <= 0)
            return BadRequest(new { error = "Gecerli bir courseId gonderilmelidir." });

        var exists = await _context.Courses
            .AsNoTracking()
            .AnyAsync(c => c.Id == courseId, cancellationToken);
        if (!exists)
            return NotFound(new { error = "Ders bulunamadi." });

        var result = await _courseDifficultyService.GetDifficultyAsync(courseId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("critical-path/{courseId:int}")]
    [RequirePermission("menu.akilli-planlama.view")]
    public async Task<IActionResult> GetCriticalPath(int courseId, CancellationToken cancellationToken = default)
    {
        if (courseId <= 0)
            return BadRequest(new { error = "Gecerli bir courseId gonderilmelidir." });

        var studentId = await GetStudentIdAsync(cancellationToken);
        if (studentId == null)
            return Unauthorized(new { error = "Ogrenci bilgisi bulunamadi." });

        var exists = await _context.Courses
            .AsNoTracking()
            .AnyAsync(c => c.Id == courseId, cancellationToken);
        if (!exists)
            return NotFound(new { error = "Ders bulunamadi." });

        var result = await _criticalPathService.AnalyzeCourseAsync(studentId.Value, courseId, cancellationToken);
        return Ok(result);
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
