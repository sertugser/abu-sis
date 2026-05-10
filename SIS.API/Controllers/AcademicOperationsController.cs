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
    [RequireAnyPermission(
        "menu.ana-sayfa.view",
        "menu.derslerim.view",
        "menu.akilli-planlama.view",
        "menu.sinif-listeleri.view",
        "menu.ders-programi-olusturma.view",
        "menu.ders-acma-islemleri.view",
        "menu.ogretim-elemani-atama.view",
        "menu.subelendirme-islemleri.view",
        "menu.ders-veren-ogretim-uyeleri.view",
        "menu.toplu-danisman-atama.view",
        "menu.toplu-mufredat-atama.view",
        "menu.donem-programi-aktarim.view")]
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

    [HttpGet("students")]
    [RequireAnyPermission("menu.toplu-danisman-atama.view", "menu.toplu-mufredat-atama.view")]
    public async Task<IActionResult> GetDepartmentStudents(
        [FromQuery] int departmentId,
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int? programId,
        [FromQuery] int? curriculumId,
        [FromQuery] int? advisorId,
        CancellationToken cancellationToken)
    {
        if (departmentId <= 0)
            return BadRequest(new { error = "Gecerli bir departmentId gonderilmelidir." });

        if (!string.IsNullOrWhiteSpace(search) && search.Length > 200)
            return BadRequest(new { error = "Arama metni en fazla 200 karakter olabilir." });

        if (!TryGetAuthenticatedUser(out var userId, out var roles))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        if (!await HasDepartmentAccessAsync(userId, roles, departmentId, cancellationToken))
            return Forbid();

        var result = await _academicOperationsService.GetDepartmentStudentsAsync(
            departmentId, userId, roles, search, status, programId, curriculumId, advisorId, cancellationToken);

        if (result == null)
            return NotFound(new { error = "Bolum ogrenci listesi bulunamadi." });

        return Ok(result);
    }

    [HttpGet("curriculum-options")]
    [RequireAnyPermission("menu.toplu-mufredat-atama.view", "menu.toplu-mufredat-atama.assign")]
    public async Task<IActionResult> GetDepartmentCurriculumOptions([FromQuery] int departmentId, CancellationToken cancellationToken)
    {
        if (departmentId <= 0)
            return BadRequest(new { error = "Gecerli bir departmentId gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out var roles))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        if (!await HasDepartmentAccessAsync(userId, roles, departmentId, cancellationToken))
            return Forbid();

        var result = await _academicOperationsService.GetDepartmentCurriculumOptionsAsync(
            departmentId, userId, roles, cancellationToken);

        if (result == null)
            return NotFound(new { error = "Bolum mufredat secenekleri bulunamadi." });

        return Ok(result);
    }

    [HttpPost("bulk-advisor-assignment")]
    [RequirePermission("menu.toplu-danisman-atama.assign")]
    public async Task<IActionResult> BulkAssignAdvisors([FromBody] BulkAdvisorAssignmentRequestDto request, CancellationToken cancellationToken)
    {
        if (request.DepartmentId <= 0)
            return BadRequest(new { error = "Gecerli bir departmentId gonderilmelidir." });

        if (request.AdvisorId <= 0)
            return BadRequest(new { error = "Gecerli bir advisorId gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out var roles))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        if (!await HasDepartmentAccessAsync(userId, roles, request.DepartmentId, cancellationToken))
            return Forbid();

        var result = await _academicOperationsService.BulkAssignAdvisorsAsync(request, userId, roles, cancellationToken);
        if (result == null)
            return Forbid();

        if (result.Errors.Count > 0 && result.Results.Count == 0)
            return BadRequest(new { error = result.Errors[0], result });

        return Ok(result);
    }

    [HttpPost("bulk-curriculum-assignment")]
    [RequirePermission("menu.toplu-mufredat-atama.assign")]
    public async Task<IActionResult> BulkAssignCurriculum([FromBody] BulkCurriculumAssignmentRequestDto request, CancellationToken cancellationToken)
    {
        if (request.DepartmentId <= 0)
            return BadRequest(new { error = "Gecerli bir departmentId gonderilmelidir." });

        if (request.CurriculumId <= 0)
            return BadRequest(new { error = "Gecerli bir curriculumId gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out var roles))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        if (!await HasDepartmentAccessAsync(userId, roles, request.DepartmentId, cancellationToken))
            return Forbid();

        var result = await _academicOperationsService.BulkAssignCurriculumAsync(request, userId, roles, cancellationToken);
        if (result == null)
            return Forbid();

        if (result.Errors.Count > 0 && result.Results.Count == 0)
            return BadRequest(new { error = result.Errors[0], result });

        return Ok(result);
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
