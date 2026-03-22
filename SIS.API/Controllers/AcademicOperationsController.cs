using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.AcademicOperations.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "AdministrativeStaff,DepartmentManager,SystemAdministrator")]
public class AcademicOperationsController : ControllerBase
{
    private readonly IAcademicOperationsService _academicOperationsService;

    public AcademicOperationsController(IAcademicOperationsService academicOperationsService)
    {
        _academicOperationsService = academicOperationsService;
    }

    [HttpGet("context")]
    public async Task<IActionResult> GetContext(CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUser(out var userId, out var roles))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        var context = await _academicOperationsService.GetContextAsync(userId, roles, cancellationToken);
        return Ok(context);
    }

    [HttpGet("planning-summary")]
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
    public async Task<IActionResult> GetDepartmentClassLists([FromQuery] int departmentId, [FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (departmentId <= 0)
            return BadRequest(new { error = "Gecerli bir departmentId gonderilmelidir." });

        if (termId.HasValue && termId.Value <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out var roles))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        if (!await HasDepartmentAccessAsync(userId, roles, departmentId, cancellationToken))
            return Forbid();

        var summary = await _academicOperationsService.GetDepartmentClassListAsync(departmentId, termId, userId, roles, cancellationToken);
        if (summary == null)
            return NotFound(new { error = "Bolum sinif listesi bulunamadi." });

        return Ok(summary);
    }

    [HttpGet("curriculum-overview")]
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
        var context = await _academicOperationsService.GetContextAsync(userId, roles, cancellationToken);
        return context.AccessibleDepartments.Any(department => department.DepartmentId == departmentId);
    }

    private bool TryGetAuthenticatedUser(out int userId, out IReadOnlyCollection<string> roles)
    {
        userId = 0;
        roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToList();

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userIdClaim != null && int.TryParse(userIdClaim, out userId);
    }
}
