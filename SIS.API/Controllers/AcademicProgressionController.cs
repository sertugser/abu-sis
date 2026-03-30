using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.AcademicProgression.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "DepartmentManager,Dean,SystemAdministrator")]
public class AcademicProgressionController : ControllerBase
{
    private readonly IAcademicProgressionService _progressionService;

    public AcademicProgressionController(IAcademicProgressionService progressionService)
    {
        _progressionService = progressionService;
    }

    /// <summary>
    /// Bölüm bazlı toplu sınıf atlatma (AKTS bazlı).
    /// dryRun=true (varsayılan): Sadece simülasyon yapar, veritabanını değiştirmez.
    /// dryRun=false: Gerçek güncelleme uygular.
    /// </summary>
    [HttpPost("promote/{departmentId}")]
    public async Task<IActionResult> PromoteDepartmentStudents(
        int departmentId,
        [FromQuery] bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _progressionService.PromoteDepartmentStudentsAsync(
            departmentId, dryRun, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Bölüm bazlı toplu GPA yeniden hesaplama.
    /// </summary>
    [HttpPost("recalculate-gpa/{departmentId}")]
    public async Task<IActionResult> RecalculateGpa(
        int departmentId,
        CancellationToken cancellationToken = default)
    {
        var result = await _progressionService.RecalculateDepartmentGpaAsync(
            departmentId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Dönem sonu not girişi eksiklik kontrolü.
    /// </summary>
    [HttpGet("grade-lock-readiness/{departmentId}")]
    public async Task<IActionResult> CheckGradeLockReadiness(
        int departmentId,
        [FromQuery] int? termId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _progressionService.CheckGradeLockReadinessAsync(
            departmentId, termId, cancellationToken);
        return Ok(result);
    }
}
