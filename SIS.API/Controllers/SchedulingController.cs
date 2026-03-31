using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.AcademicOperations.Services;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "AdministrativeStaff,DepartmentManager,Dean,SystemAdministrator")]
public class SchedulingController : ControllerBase
{
    private readonly ISchedulingService _schedulingService;
    private readonly IAcademicOperationsService _academicOperationsService;
    private readonly AppDbContext _context;

    public SchedulingController(
        ISchedulingService schedulingService,
        IAcademicOperationsService academicOperationsService,
        AppDbContext context)
    {
        _schedulingService = schedulingService;
        _academicOperationsService = academicOperationsService;
        _context = context;
    }

    [HttpGet("conflicts")]
    public async Task<IActionResult> GetConflicts([FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (termId.HasValue && termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        var conflicts = await _schedulingService.GetConflictsAsync(termId, cancellationToken);
        return Ok(conflicts);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (termId.HasValue && termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        var dashboard = await _schedulingService.GetClassroomDashboardAsync(termId, cancellationToken);
        return Ok(dashboard);
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ScheduleValidationRequestDto request, CancellationToken cancellationToken)
    {
        if (request.SectionId <= 0)
            return BadRequest(new { error = "Gecerli bir sectionId gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out _))
            return Unauthorized();
        var accessCheck = await CheckSectionAccessAsync(request.SectionId, userId, cancellationToken);
        if (accessCheck != null) return accessCheck;

        var result = await _schedulingService.ValidateAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("suggest")]
    public async Task<IActionResult> Suggest([FromBody] ScheduleSuggestionRequestDto request, CancellationToken cancellationToken)
    {
        if (request.SectionId <= 0)
            return BadRequest(new { error = "Gecerli bir sectionId gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out _))
            return Unauthorized();
        var accessCheck = await CheckSectionAccessAsync(request.SectionId, userId, cancellationToken);
        if (accessCheck != null) return accessCheck;

        var result = await _schedulingService.SuggestAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] ApplyScheduleRequestDto request, CancellationToken cancellationToken)
    {
        if (request.SectionId <= 0)
            return BadRequest(new { error = "Gecerli bir sectionId gonderilmelidir." });

        if (request.ScheduleSlots == null || request.ScheduleSlots.Count == 0)
            return BadRequest(new { error = "En az bir schedule slot gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out _))
            return Unauthorized();
        var accessCheck = await CheckSectionAccessAsync(request.SectionId, userId, cancellationToken);
        if (accessCheck != null) return accessCheck;

        var result = await _schedulingService.ApplyAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("validate-existing")]
    public async Task<IActionResult> ValidateExisting([FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (termId.HasValue && termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        var result = await _schedulingService.ValidateExistingAsync(termId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("batch-auto-schedule")]
    public async Task<IActionResult> BatchAutoSchedule([FromBody] BatchAutoScheduleRequestDto request, CancellationToken cancellationToken)
    {
        if (request.TermId.HasValue && request.TermId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        if (request.DepartmentId.HasValue && request.DepartmentId <= 0)
            return BadRequest(new { error = "Gecerli bir departmentId gonderilmelidir." });

        if (request.FacultyId.HasValue && request.FacultyId <= 0)
            return BadRequest(new { error = "Gecerli bir facultyId gonderilmelidir." });

        if (request.DepartmentId.HasValue && request.FacultyId.HasValue)
            return BadRequest(new { error = "departmentId ve facultyId ayni anda gonderilemez." });

        if (!TryGetAuthenticatedUser(out var userId, out var roles))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        var context = await _academicOperationsService.GetContextAsync(userId, roles, cancellationToken);

        if (request.DepartmentId.HasValue)
        {
            var hasDepartmentAccess = context.AccessibleDepartments
                .Any(department => department.DepartmentId == request.DepartmentId.Value);

            if (!hasDepartmentAccess)
                return Forbid();
        }
        else if (!context.CanViewAllDepartments)
        {
            if (!context.DefaultDepartmentId.HasValue)
                return BadRequest(new { error = "Bu kullanici icin erisilebilir bolum bulunamadi." });

            request.DepartmentId = context.DefaultDepartmentId.Value;
        }

        var result = await _schedulingService.BatchAutoScheduleAsync(request, cancellationToken);
        return Ok(result);
    }

    private async Task<IActionResult?> CheckSectionAccessAsync(int sectionId, int userId, CancellationToken ct)
    {
        var section = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == sectionId, ct);

        if (section == null)
            return NotFound(new { error = "Şube bulunamadı." });

        if (!section.Course.DepartmentId.HasValue)
            return null; // Ortak ders — herkes erişebilir

        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (roles.Contains("SystemAdministrator") || roles.Contains("AdministrativeStaff"))
            return null;

        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.UserId == userId, ct);

        if (instructor == null)
            return Forbid();

        if (roles.Contains("Dean"))
        {
            var department = await _context.Departments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == section.Course.DepartmentId.Value, ct);
            if (department == null || department.FacultyId != instructor.Department.FacultyId)
                return Forbid();
            return null;
        }

        if (roles.Contains("DepartmentManager"))
        {
            if (instructor.DepartmentId != section.Course.DepartmentId.Value)
                return Forbid();
            return null;
        }

        return Forbid();
    }

    private bool TryGetAuthenticatedUser(out int userId, out IReadOnlyCollection<string> roles)
    {
        userId = 0;
        roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToList();

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userIdClaim != null && int.TryParse(userIdClaim, out userId);
    }
}
