using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Scheduling.Constraints;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/constraints/exception-requests")]
[Authorize]
public class ExceptionRequestController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IApprovalWorkflowService _workflow;

    public ExceptionRequestController(AppDbContext context, IApprovalWorkflowService workflow)
    {
        _context = context;
        _workflow = workflow;
    }

    // ── POST / ── create request ────────────────────────────────────
    [HttpPost]
    [RequirePermission("menu.ders-programi-kisitlamalari.exception-request")]
    public async Task<IActionResult> Create([FromBody] CreateExceptionRequestDto dto, CancellationToken ct)
    {
        if (dto.InstructorId == null && dto.StudentId == null &&
            dto.DepartmentId == null && dto.CourseSectionId == null && dto.TermId == null)
        {
            return BadRequest(new { error = "En az bir entity alanı belirtilmelidir." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        var request = new ConstraintExceptionRequest
        {
            ConstraintDefinitionId = dto.ConstraintDefinitionId,
            RequestedByUserId = userId,
            InstructorId = dto.InstructorId,
            StudentId = dto.StudentId,
            DepartmentId = dto.DepartmentId,
            CourseSectionId = dto.CourseSectionId,
            TermId = dto.TermId,
            IsExempt = dto.IsExempt,
            ParameterOverride = dto.ParameterOverride,
            Reason = dto.Reason,
            ExpiresAt = dto.ExpiresAt,
        };

        try
        {
            var created = await _workflow.CreateRequestAsync(request, ct);
            return Created($"/api/constraints/exception-requests/{created.Id}", new { created.Id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── GET /pending ── requests needing current user's approval ─────
    [HttpGet("pending")]
    [RequirePermission("menu.ders-programi-kisitlamalari.exception-pending")]
    public async Task<IActionResult> GetPending(CancellationToken ct)
    {
        var userRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        var query = _context.ConstraintExceptionRequests
            .AsNoTracking()
            .Include(r => r.ApprovalSteps.OrderBy(s => s.StepOrder))
            .Include(r => r.ConstraintDefinition)
            .Where(r => r.Status != ExceptionRequestStatus.Rejected
                     && r.Status != ExceptionRequestStatus.ApprovedByAdmin
                     && r.Status != ExceptionRequestStatus.Expired)
            .Where(r => r.ApprovalSteps.Any(s =>
                s.Status == ApprovalStepStatus.Pending
                && userRoles.Contains(s.RequiredRole)
                && !r.ApprovalSteps.Any(prev =>
                    prev.StepOrder < s.StepOrder && prev.Status == ApprovalStepStatus.Pending)));

        var requests = await query.OrderByDescending(r => r.RequestedAt).ToListAsync(ct);
        var dtos = await MapToDtosAsync(requests, ct);
        return Ok(dtos);
    }

    // ── GET /my ── current user's own requests ──────────────────────
    [HttpGet("my")]
    [RequirePermission("menu.ders-programi-kisitlamalari.exception-my")]
    public async Task<IActionResult> GetMy(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        var requests = await _context.ConstraintExceptionRequests
            .AsNoTracking()
            .Include(r => r.ApprovalSteps.OrderBy(s => s.StepOrder))
            .Include(r => r.ConstraintDefinition)
            .Where(r => r.RequestedByUserId == userId)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(ct);

        var dtos = await MapToDtosAsync(requests, ct);
        return Ok(dtos);
    }

    // ── GET /{id} ── request detail ─────────────────────────────────
    [HttpGet("{id:int}")]
    [RequirePermission("menu.ders-programi-kisitlamalari.exception-view")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var request = await _context.ConstraintExceptionRequests
            .AsNoTracking()
            .Include(r => r.ApprovalSteps.OrderBy(s => s.StepOrder))
            .Include(r => r.ConstraintDefinition)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (request == null)
            return NotFound(new { error = "Talep bulunamadı." });

        var dtos = await MapToDtosAsync(new List<ConstraintExceptionRequest> { request }, ct);
        return Ok(dtos[0]);
    }

    // ── POST /{id}/approve ──────────────────────────────────────────
    [HttpPost("{id:int}/approve")]
    [RequirePermission("menu.ders-programi-kisitlamalari.exception-approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] ApprovalActionDto dto, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var (success, error) = await _workflow.ApproveStepAsync(id, userId, dto.Comment, ct);

        if (!success) return BadRequest(new { error });
        return Ok(new { message = "Adım onaylandı." });
    }

    // ── POST /{id}/reject ───────────────────────────────────────────
    [HttpPost("{id:int}/reject")]
    [RequirePermission("menu.ders-programi-kisitlamalari.exception-reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] ApprovalActionDto dto, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var (success, error) = await _workflow.RejectStepAsync(id, userId, dto.Comment, ct);

        if (!success) return BadRequest(new { error });
        return Ok(new { message = "Talep reddedildi." });
    }

    // ── Helper: map entities to DTOs with resolved names ────────────
    private async Task<List<ExceptionRequestDto>> MapToDtosAsync(
        List<ConstraintExceptionRequest> requests, CancellationToken ct)
    {
        if (requests.Count == 0) return new List<ExceptionRequestDto>();

        // Collect all user IDs for name resolution
        var allUserIds = requests.Select(r => r.RequestedByUserId)
            .Union(requests.SelectMany(r => r.ApprovalSteps)
                .Where(s => s.DecidedByUserId != null)
                .Select(s => s.DecidedByUserId!))
            .Distinct()
            .Where(id => int.TryParse(id, out _))
            .Select(int.Parse)
            .ToList();

        var userNames = allUserIds.Count > 0
            ? await _context.Users
                .Where(u => allUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id.ToString(), u => $"{u.FirstName} {u.LastName}", ct)
            : new Dictionary<string, string>();

        // Collect entity IDs for name resolution
        var instructorIds = requests.Where(r => r.InstructorId.HasValue).Select(r => r.InstructorId!.Value).Distinct().ToList();
        var studentIds = requests.Where(r => r.StudentId.HasValue).Select(r => r.StudentId!.Value).Distinct().ToList();
        var departmentIds = requests.Where(r => r.DepartmentId.HasValue).Select(r => r.DepartmentId!.Value).Distinct().ToList();
        var sectionIds = requests.Where(r => r.CourseSectionId.HasValue).Select(r => r.CourseSectionId!.Value).Distinct().ToList();
        var termIds = requests.Where(r => r.TermId.HasValue).Select(r => r.TermId!.Value).Distinct().ToList();

        var instructorUserIds = instructorIds.Count > 0
            ? await _context.Instructors.Where(i => instructorIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.UserId, ct)
            : new Dictionary<int, int>();
        var instructorResolvedUserIds = instructorUserIds.Values.Distinct().ToList();
        var instructorNames = instructorResolvedUserIds.Count > 0
            ? await _context.Users.Where(u => instructorResolvedUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}", ct)
            : new Dictionary<int, string>();

        var studentUserIds = studentIds.Count > 0
            ? await _context.Students.Where(s => studentIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.UserId, ct)
            : new Dictionary<int, int>();
        var studentResolvedUserIds = studentUserIds.Values.Distinct().ToList();
        var studentNames = studentResolvedUserIds.Count > 0
            ? await _context.Users.Where(u => studentResolvedUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}", ct)
            : new Dictionary<int, string>();

        var departmentNames = departmentIds.Count > 0
            ? await _context.Departments.Where(d => departmentIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, ct)
            : new Dictionary<int, string>();

        var sectionInfos = sectionIds.Count > 0
            ? await _context.CourseSections
                .Include(cs => cs.Course)
                .Where(cs => sectionIds.Contains(cs.Id))
                .ToDictionaryAsync(cs => cs.Id, cs => $"{cs.Course.Code} - {cs.SectionNumber}", ct)
            : new Dictionary<int, string>();

        var termNames = termIds.Count > 0
            ? await _context.AcademicTerms.Where(t => termIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct)
            : new Dictionary<int, string>();

        string? ResolveInstructorName(int? id) =>
            id.HasValue && instructorUserIds.TryGetValue(id.Value, out var uid) && instructorNames.TryGetValue(uid, out var name) ? name : null;
        string? ResolveStudentName(int? id) =>
            id.HasValue && studentUserIds.TryGetValue(id.Value, out var uid) && studentNames.TryGetValue(uid, out var name) ? name : null;

        return requests.Select(r => new ExceptionRequestDto(
            r.Id,
            r.ConstraintDefinitionId,
            r.ConstraintDefinition.Code,
            r.ConstraintDefinition.Name,
            r.RequestedByUserId,
            userNames.GetValueOrDefault(r.RequestedByUserId),
            r.RequestedAt,
            r.Status.ToString(),
            r.InstructorId, ResolveInstructorName(r.InstructorId),
            r.StudentId, ResolveStudentName(r.StudentId),
            r.DepartmentId, r.DepartmentId.HasValue ? departmentNames.GetValueOrDefault(r.DepartmentId.Value) : null,
            r.CourseSectionId, r.CourseSectionId.HasValue ? sectionInfos.GetValueOrDefault(r.CourseSectionId.Value) : null,
            r.TermId, r.TermId.HasValue ? termNames.GetValueOrDefault(r.TermId.Value) : null,
            r.IsExempt, r.ParameterOverride, r.Reason,
            r.ExpiresAt,
            r.ApprovalSteps.OrderBy(s => s.StepOrder).Select(s => new ApprovalStepDto(
                s.Id, s.StepOrder, s.RequiredRole, s.Status.ToString(),
                s.DecidedByUserId,
                s.DecidedByUserId != null ? userNames.GetValueOrDefault(s.DecidedByUserId) : null,
                s.DecidedAt, s.Comment
            )).ToList()
        )).ToList();
    }
}
