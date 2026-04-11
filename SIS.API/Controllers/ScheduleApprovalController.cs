using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/schedule-approval")]
[Authorize(Roles = "Dean,SystemAdministrator")]
public class ScheduleApprovalController : ControllerBase
{
    private readonly AppDbContext _context;

    public ScheduleApprovalController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("faculty/{facultyId}/approve")]
    public async Task<IActionResult> ApproveFacultySchedule(int facultyId, [FromBody] ApproveFacultyScheduleRequestDto request, CancellationToken cancellationToken)
    {
        if (facultyId <= 0)
            return BadRequest(new { error = "Gecerli bir facultyId gonderilmelidir." });

        if (request.TermId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out var roles))
            return Unauthorized();

        // Guard: Faculty must exist
        var faculty = await _context.Faculties
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == facultyId, cancellationToken);

        if (faculty == null)
            return NotFound(new { error = "Fakulte bulunamadi." });

        // Guard: Dean must belong to this faculty (SystemAdministrator bypasses)
        if (!roles.Contains("SystemAdministrator"))
        {
            var deanFacultyAccess = await CheckDeanFacultyAccessAsync(userId, facultyId, cancellationToken);
            if (!deanFacultyAccess)
                return Forbid();
        }

        // Guard: Term must exist and be in Scheduling status
        var term = await _context.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TermId, cancellationToken);

        if (term == null)
            return NotFound(new { error = "Akademik donem bulunamadi." });

        if (term.Status != TermStatus.Scheduling)
            return BadRequest(new { error = "Program onaylama islemi sadece Scheduling durumundaki donemler icin yapilabilir." });

        // Check for existing approval
        var existingApproval = await _context.FacultyScheduleApprovals
            .FirstOrDefaultAsync(a => a.FacultyId == facultyId && a.TermId == request.TermId, cancellationToken);

        if (existingApproval != null)
        {
            // Allow re-approval: update existing record
            existingApproval.ApprovedByUserId = userId;
            existingApproval.ApprovedAt = DateTime.UtcNow;
            existingApproval.Note = request.Note;
        }
        else
        {
            _context.FacultyScheduleApprovals.Add(new FacultyScheduleApproval
            {
                FacultyId = facultyId,
                TermId = request.TermId,
                ApprovedByUserId = userId,
                ApprovedAt = DateTime.UtcNow,
                Note = request.Note
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Fakulte ders programi basariyla onaylandi.",
            facultyId,
            termId = request.TermId,
            approvedByUserId = userId,
            approvedAt = DateTime.UtcNow,
            note = request.Note
        });
    }

    [HttpGet("faculty/{facultyId}/status")]
    public async Task<IActionResult> GetApprovalStatus(int facultyId, [FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (facultyId <= 0)
            return BadRequest(new { error = "Gecerli bir facultyId gonderilmelidir." });

        var resolvedTermId = termId;
        if (!resolvedTermId.HasValue)
        {
            var activeTerm = await _context.AcademicTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.IsActive, cancellationToken);

            if (activeTerm == null)
                return NotFound(new { error = "Aktif akademik donem bulunamadi." });

            resolvedTermId = activeTerm.Id;
        }

        var approval = await _context.FacultyScheduleApprovals
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.FacultyId == facultyId && a.TermId == resolvedTermId.Value, cancellationToken);

        if (approval == null)
        {
            return Ok(new FacultyScheduleApprovalStatusDto
            {
                IsApproved = false,
                FacultyId = facultyId,
                TermId = resolvedTermId.Value
            });
        }

        // Resolve approver name
        string? approverName = null;
        var approver = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == approval.ApprovedByUserId, cancellationToken);

        if (approver != null)
            approverName = $"{approver.FirstName} {approver.LastName}".Trim();

        return Ok(new FacultyScheduleApprovalStatusDto
        {
            IsApproved = true,
            ApprovedByUserId = approval.ApprovedByUserId,
            ApprovedByName = approverName,
            ApprovedAt = approval.ApprovedAt,
            Note = approval.Note,
            FacultyId = facultyId,
            TermId = resolvedTermId.Value
        });
    }

    [HttpDelete("faculty/{facultyId}/revoke")]
    public async Task<IActionResult> RevokeApproval(int facultyId, [FromQuery] int? termId, CancellationToken cancellationToken)
    {
        if (facultyId <= 0)
            return BadRequest(new { error = "Gecerli bir facultyId gonderilmelidir." });

        if (!TryGetAuthenticatedUser(out var userId, out var roles))
            return Unauthorized();

        // Guard: Dean must belong to this faculty
        if (!roles.Contains("SystemAdministrator"))
        {
            var deanFacultyAccess = await CheckDeanFacultyAccessAsync(userId, facultyId, cancellationToken);
            if (!deanFacultyAccess)
                return Forbid();
        }

        var resolvedTermId = termId;
        if (!resolvedTermId.HasValue)
        {
            var activeTerm = await _context.AcademicTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.IsActive, cancellationToken);

            if (activeTerm == null)
                return NotFound(new { error = "Aktif akademik donem bulunamadi." });

            resolvedTermId = activeTerm.Id;
        }

        // Guard: Term must not be Published or later
        var term = await _context.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == resolvedTermId.Value, cancellationToken);

        if (term == null)
            return NotFound(new { error = "Akademik donem bulunamadi." });

        if (term.Status >= TermStatus.Published)
            return BadRequest(new { error = "Yayimlanmis veya ileri durumdaki donemlerin onaylari geri alinamaz." });

        var approval = await _context.FacultyScheduleApprovals
            .FirstOrDefaultAsync(a => a.FacultyId == facultyId && a.TermId == resolvedTermId.Value, cancellationToken);

        if (approval == null)
            return NotFound(new { error = "Bu fakulte ve donem icin onay kaydı bulunamadi." });

        _context.FacultyScheduleApprovals.Remove(approval);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Fakulte ders programi onayi basariyla geri alindi.",
            facultyId,
            termId = resolvedTermId.Value
        });
    }

    // ── Private Helpers ──────────────────────────────────────────────────

    private async Task<bool> CheckDeanFacultyAccessAsync(int userId, int facultyId, CancellationToken ct)
    {
        // Dean's faculty is determined by their instructor record's department's faculty
        var instructor = await _context.Instructors
            .AsNoTracking()
            .Include(i => i.Department)
            .FirstOrDefaultAsync(i => i.UserId == userId, ct);

        if (instructor == null)
            return false;

        return instructor.Department.FacultyId == facultyId;
    }

    private bool TryGetAuthenticatedUser(out int userId, out IReadOnlyCollection<string> roles)
    {
        userId = 0;
        roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToList();

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userIdClaim != null && int.TryParse(userIdClaim, out userId);
    }
}
