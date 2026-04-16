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
[Route("api/instructors")]
[Authorize]
public class InstructorPreferenceController : ControllerBase
{
    private readonly AppDbContext _context;

    public InstructorPreferenceController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET api/instructors/{instructorId}/preferences — list preferences for an instructor.
    /// </summary>
    [HttpGet("{instructorId:int}/preferences")]
    public async Task<IActionResult> GetPreferences(int instructorId, [FromQuery] int? termId)
    {
        if (!await CanAccessInstructorAsync(instructorId))
            return Forbid();

        var query = _context.InstructorPreferences
            .AsNoTracking()
            .Include(p => p.Instructor)
            .Include(p => p.Classroom)
            .Where(p => p.InstructorId == instructorId);

        if (termId.HasValue)
            query = query.Where(p => p.TermId == null || p.TermId == termId.Value);

        var preferences = await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var userIds = preferences.Select(p => p.Instructor.UserId).Distinct().ToList();
        var userNameMap = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        var dtos = preferences.Select(p => MapToDto(p, userNameMap)).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// POST api/instructors/{instructorId}/preferences — create a new preference.
    /// </summary>
    [HttpPost("{instructorId:int}/preferences")]
    public async Task<IActionResult> CreatePreference(int instructorId, [FromBody] CreateInstructorPreferenceRequest request)
    {
        if (!await CanAccessInstructorAsync(instructorId))
            return Forbid();

        if (!Enum.TryParse<PreferenceType>(request.PreferenceType, out var prefType))
            return BadRequest(new { error = $"Gecersiz tercih tipi: {request.PreferenceType}" });

        if (!Enum.TryParse<PreferenceLevel>(request.PreferenceLevel, out var prefLevel))
            return BadRequest(new { error = $"Gecersiz tercih seviyesi: {request.PreferenceLevel}" });

        var isManager = IsInRole("SystemAdministrator") || IsInRole("Dean") || IsInRole("DepartmentManager");

        var entity = new InstructorPreference
        {
            InstructorId = instructorId,
            PreferenceType = prefType,
            PreferenceLevel = prefLevel,
            DayOfWeek = request.DayOfWeek,
            TimeSlotStart = ParseTimeOnly(request.TimeSlotStart),
            TimeSlotEnd = ParseTimeOnly(request.TimeSlotEnd),
            ClassroomId = request.ClassroomId,
            BuildingName = request.BuildingName,
            MaxHours = request.MaxHours,
            Reason = request.Reason,
            TermId = request.TermId,
            IsApproved = isManager,
            CreatedAt = DateTime.UtcNow,
        };

        _context.InstructorPreferences.Add(entity);
        await _context.SaveChangesAsync();

        // Reload with navigation props
        await _context.Entry(entity).Reference(e => e.Instructor).LoadAsync();
        if (entity.ClassroomId.HasValue)
            await _context.Entry(entity).Reference(e => e.Classroom).LoadAsync();

        var user = await _context.Users.FindAsync(entity.Instructor.UserId);
        var nameMap = new Dictionary<int, string>();
        if (user != null) nameMap[user.Id] = $"{user.FirstName} {user.LastName}";

        return Created($"api/instructors/preferences/{entity.Id}", MapToDto(entity, nameMap));
    }

    /// <summary>
    /// PUT api/instructors/preferences/{prefId} — update a preference.
    /// </summary>
    [HttpPut("preferences/{prefId:int}")]
    public async Task<IActionResult> UpdatePreference(int prefId, [FromBody] CreateInstructorPreferenceRequest request)
    {
        var entity = await _context.InstructorPreferences
            .Include(p => p.Instructor)
            .FirstOrDefaultAsync(p => p.Id == prefId);

        if (entity == null)
            return NotFound(new { error = "Tercih bulunamadi." });

        if (!await CanAccessInstructorAsync(entity.InstructorId))
            return Forbid();

        if (!Enum.TryParse<PreferenceType>(request.PreferenceType, out var prefType))
            return BadRequest(new { error = $"Gecersiz tercih tipi: {request.PreferenceType}" });

        if (!Enum.TryParse<PreferenceLevel>(request.PreferenceLevel, out var prefLevel))
            return BadRequest(new { error = $"Gecersiz tercih seviyesi: {request.PreferenceLevel}" });

        var isManager = IsInRole("SystemAdministrator") || IsInRole("Dean") || IsInRole("DepartmentManager");

        entity.PreferenceType = prefType;
        entity.PreferenceLevel = prefLevel;
        entity.DayOfWeek = request.DayOfWeek;
        entity.TimeSlotStart = ParseTimeOnly(request.TimeSlotStart);
        entity.TimeSlotEnd = ParseTimeOnly(request.TimeSlotEnd);
        entity.ClassroomId = request.ClassroomId;
        entity.BuildingName = request.BuildingName;
        entity.MaxHours = request.MaxHours;
        entity.Reason = request.Reason;
        entity.TermId = request.TermId;

        // If instructor edits, reset approval
        if (!isManager)
            entity.IsApproved = false;

        await _context.SaveChangesAsync();

        if (entity.ClassroomId.HasValue)
            await _context.Entry(entity).Reference(e => e.Classroom).LoadAsync();

        var user = await _context.Users.FindAsync(entity.Instructor.UserId);
        var nameMap = new Dictionary<int, string>();
        if (user != null) nameMap[user.Id] = $"{user.FirstName} {user.LastName}";

        return Ok(MapToDto(entity, nameMap));
    }

    /// <summary>
    /// DELETE api/instructors/preferences/{prefId} — delete a preference.
    /// </summary>
    [HttpDelete("preferences/{prefId:int}")]
    public async Task<IActionResult> DeletePreference(int prefId)
    {
        var entity = await _context.InstructorPreferences.FindAsync(prefId);
        if (entity == null)
            return NotFound(new { error = "Tercih bulunamadi." });

        if (!await CanAccessInstructorAsync(entity.InstructorId))
            return Forbid();

        _context.InstructorPreferences.Remove(entity);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// PUT api/instructors/preferences/{prefId}/approve — approve a preference (DeptManager+).
    /// </summary>
    [HttpPut("preferences/{prefId:int}/approve")]
    [Authorize(Roles = "SystemAdministrator,Dean,DepartmentManager")]
    public async Task<IActionResult> ApprovePreference(int prefId, [FromQuery] bool approved = true)
    {
        var entity = await _context.InstructorPreferences
            .Include(p => p.Instructor)
            .FirstOrDefaultAsync(p => p.Id == prefId);

        if (entity == null)
            return NotFound(new { error = "Tercih bulunamadi." });

        entity.IsApproved = approved;
        await _context.SaveChangesAsync();

        if (entity.ClassroomId.HasValue)
            await _context.Entry(entity).Reference(e => e.Classroom).LoadAsync();

        var user = await _context.Users.FindAsync(entity.Instructor.UserId);
        var nameMap = new Dictionary<int, string>();
        if (user != null) nameMap[user.Id] = $"{user.FirstName} {user.LastName}";

        return Ok(MapToDto(entity, nameMap));
    }

    /// <summary>
    /// GET api/instructors/preferences/summary — all instructors' preferences summary (admin).
    /// </summary>
    [HttpGet("preferences/summary")]
    [Authorize(Roles = "SystemAdministrator,Dean,DepartmentManager")]
    public async Task<IActionResult> GetSummary([FromQuery] int? termId, [FromQuery] int? departmentId)
    {
        var query = _context.InstructorPreferences
            .AsNoTracking()
            .Include(p => p.Instructor)
            .Include(p => p.Classroom)
            .AsQueryable();

        if (termId.HasValue)
            query = query.Where(p => p.TermId == null || p.TermId == termId.Value);

        if (departmentId.HasValue)
            query = query.Where(p => p.Instructor.DepartmentId == departmentId.Value);

        var preferences = await query
            .OrderBy(p => p.InstructorId)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync();

        var userIds = preferences.Select(p => p.Instructor.UserId).Distinct().ToList();
        var userNameMap = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        var dtos = preferences.Select(p => MapToDto(p, userNameMap)).ToList();
        return Ok(dtos);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static InstructorPreferenceDto MapToDto(InstructorPreference p, IReadOnlyDictionary<int, string> userNameMap)
    {
        var instructorName = userNameMap.TryGetValue(p.Instructor.UserId, out var name)
            ? name
            : $"Instructor #{p.InstructorId}";

        return new InstructorPreferenceDto(
            p.Id,
            p.InstructorId,
            instructorName,
            p.PreferenceType.ToString(),
            p.PreferenceLevel.ToString(),
            p.DayOfWeek,
            p.TimeSlotStart,
            p.TimeSlotEnd,
            p.ClassroomId,
            p.Classroom?.Name,
            p.BuildingName,
            p.MaxHours,
            p.Reason,
            p.TermId,
            p.IsApproved,
            p.CreatedAt);
    }

    private static TimeOnly? ParseTimeOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return TimeOnly.TryParse(value, out var result) ? result : null;
    }

    private async Task<bool> CanAccessInstructorAsync(int instructorId)
    {
        if (IsInRole("SystemAdministrator") || IsInRole("Dean"))
            return true;

        if (IsInRole("DepartmentManager"))
        {
            var managerInstructor = await GetCurrentInstructorAsync();
            if (managerInstructor == null) return false;

            var targetInstructor = await _context.Instructors.FindAsync(instructorId);
            if (targetInstructor == null) return false;

            return managerInstructor.DepartmentId == targetInstructor.DepartmentId;
        }

        if (IsInRole("Instructor") || IsInRole("AcademicAdvisor"))
        {
            var currentInstructor = await GetCurrentInstructorAsync();
            return currentInstructor?.Id == instructorId;
        }

        return false;
    }

    private async Task<Instructor?> GetCurrentInstructorAsync()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return null;

        return await _context.Instructors.FirstOrDefaultAsync(i => i.UserId == userId);
    }

    private bool IsInRole(string role) => User.IsInRole(role);
}
