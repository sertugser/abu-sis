using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.AcademicOperations.DTOs;
using SIS.Application.AcademicOperations.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AcademicOperationsController : ControllerBase
{
    private readonly IAcademicOperationsService _academicOperationsService;
    private AcademicOperationsContextDto? _cachedContext;

    public AcademicOperationsController(IAcademicOperationsService academicOperationsService)
    {
        _academicOperationsService = academicOperationsService;
    }

    [HttpGet("context")]
    [RequirePermission("menu.derslerim.view")]
    public async Task<IActionResult> GetContext(CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUser(out var userId, out var roles))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        var context = await _academicOperationsService.GetContextAsync(userId, roles, cancellationToken);
        return Ok(context);
    }

    [HttpGet("planning-summary")]
    [RequirePermission("menu.akilli-planlama.view")]
    public async Task<IActionResult> GetPlanningSummary([FromQuery] int departmentId, [FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (departmentId <= 0)
            return BadRequest(new { error = "Gecerli bir departmentId gonderilmelidir." });

        if (termId.HasValue && termId.Value <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out var roles))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        if (!await HasDepartmentAccessAsync(userId, roles, departmentId, cancellationToken))
            return Forbid();

        var summary = await _academicOperationsService.GetPlanningSummaryAsync(departmentId, termId, userId, roles, cancellationToken);
        if (summary == null)
            return NotFound(new { error = "Bolum planlama ozeti bulunamadi." });

        return Ok(summary);
    }

    [HttpGet("class-lists")]
    [RequirePermission("menu.sinif-listeleri.view")]
    public async Task<IActionResult> GetDepartmentClassLists([FromQuery] int departmentId, [FromQuery] int? termId, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        if (departmentId <= 0)
            return BadRequest(new { error = "Gecerli bir departmentId gonderilmelidir." });

        if (termId.HasValue && termId.Value <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        if (!string.IsNullOrEmpty(search) && search.Length > 200)
            return BadRequest(new { error = "Arama metni en fazla 200 karakter olabilir." });

        if (!TryGetAuthenticatedUser(out var userId, out var roles))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        if (!await HasDepartmentAccessAsync(userId, roles, departmentId, cancellationToken))
            return Forbid();

        var summary = await _academicOperationsService.GetDepartmentClassListAsync(departmentId, termId, userId, roles, search, cancellationToken);
        if (summary == null)
            return NotFound(new { error = "Bolum sinif listesi bulunamadi." });

        return Ok(summary);
    }

    [HttpGet("curriculum-overview")]
    [RequirePermission("menu.mufredat-islemleri.view")]
    public async Task<IActionResult> GetCurriculumOverview([FromQuery] int departmentId, CancellationToken cancellationToken)
    {
        if (departmentId <= 0)
            return BadRequest(new { error = "Gecerli bir departmentId gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out var roles))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        if (!await HasDepartmentAccessAsync(userId, roles, departmentId, cancellationToken))
            return Forbid();

        var summary = await _academicOperationsService.GetCurriculumOverviewAsync(departmentId, userId, roles, cancellationToken);
        if (summary == null)
            return NotFound(new { error = "Bolum mufredat ozeti bulunamadi." });

        return Ok(summary);
    }

    private async Task<bool> HasDepartmentAccessAsync(int userId, IReadOnlyCollection<string> roles, int departmentId, CancellationToken cancellationToken)
    {
        _cachedContext ??= await _academicOperationsService.GetContextAsync(userId, roles, cancellationToken);
        return _cachedContext.AccessibleDepartments.Any(department => department.DepartmentId == departmentId);
    }

    private bool TryGetAuthenticatedUser(out int userId, out IReadOnlyCollection<string> roles)
    {
        userId = 0;
        roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToList();

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userIdClaim != null && int.TryParse(userIdClaim, out userId);
    }
}
