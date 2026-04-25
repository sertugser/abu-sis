using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/constraints")]
[Authorize]
public class ConstraintController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ISchedulingService _schedulingService;

    public ConstraintController(AppDbContext context, ISchedulingService schedulingService)
    {
        _context = context;
        _schedulingService = schedulingService;
    }

    // ── GET /api/constraints ─────────────────────────────────────────────
    [HttpGet]
    [RequirePermission("menu.ders-programi-kisitlamalari.view")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var definitions = await _context.ConstraintDefinitions
            .AsNoTracking()
            .OrderBy(d => d.SortOrder)
            .Select(d => new ConstraintDefinitionDto(
                d.Id, d.Code, d.Name, d.Description,
                d.Category, d.Scope, d.IsEnabled,
                d.Parameters, d.SortOrder, d.Weight, d.Priority,
                d.Exceptions.Count))
            .ToListAsync(ct);

        return Ok(definitions);
    }

    // ── GET /api/constraints/{id} ────────────────────────────────────────
    [HttpGet("{id:int}")]
    [RequirePermission("menu.ders-programi-kisitlamalari.view")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var definition = await _context.ConstraintDefinitions
            .AsNoTracking()
            .Include(d => d.Exceptions).ThenInclude(e => e.Instructor)
            .Include(d => d.Exceptions).ThenInclude(e => e.Student)
            .Include(d => d.Exceptions).ThenInclude(e => e.Department)
            .Include(d => d.Exceptions).ThenInclude(e => e.CourseSection!).ThenInclude(cs => cs.Course)
            .Include(d => d.Exceptions).ThenInclude(e => e.Term)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (definition == null)
            return NotFound(new { error = "Constraint tanimi bulunamadi." });

        // Resolve display names from ApplicationUser for instructors and students
        var instructorUserIds = definition.Exceptions
            .Where(e => e.Instructor != null)
            .Select(e => e.Instructor!.UserId)
            .Distinct().ToList();
        var studentUserIds = definition.Exceptions
            .Where(e => e.Student != null)
            .Select(e => e.Student!.UserId)
            .Distinct().ToList();
        var allUserIds = instructorUserIds.Union(studentUserIds).ToList();

        var userNames = allUserIds.Count > 0
            ? await _context.Users
                .Where(u => allUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}", ct)
            : new Dictionary<int, string>();

        var dto = new ConstraintDefinitionDetailDto(
            definition.Id, definition.Code, definition.Name, definition.Description,
            definition.Category, definition.Scope, definition.IsEnabled,
            definition.Parameters, definition.SortOrder,
            definition.Exceptions
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new ConstraintExceptionDto(
                    e.Id, e.ConstraintDefinitionId, definition.Code,
                    e.InstructorId,
                    e.Instructor != null && userNames.TryGetValue(e.Instructor.UserId, out var iName) ? iName : null,
                    e.StudentId,
                    e.Student != null && userNames.TryGetValue(e.Student.UserId, out var sName) ? sName : null,
                    e.DepartmentId, e.Department?.Name,
                    e.CourseSectionId,
                    e.CourseSection != null ? $"{e.CourseSection.Course.Code} - {e.CourseSection.SectionNumber}" : null,
                    e.TermId, e.Term?.Name,
                    e.IsExempt, e.ParameterOverride,
                    e.Reason, e.CreatedAt
                )).ToList());

        return Ok(dto);
    }

    // ── PUT /api/constraints/{id}/toggle ─────────────────────────────────
    [HttpPut("{id:int}/toggle")]
    [RequirePermission("menu.ders-programi-kisitlamalari.toggle")]
    public async Task<IActionResult> Toggle(int id, CancellationToken ct)
    {
        var definition = await _context.ConstraintDefinitions.FindAsync([id], ct);
        if (definition == null)
            return NotFound(new { error = "Constraint tanimi bulunamadi." });

        definition.IsEnabled = !definition.IsEnabled;
        definition.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Ok(new { definition.Id, definition.IsEnabled });
    }

    // ── PUT /api/constraints/{id}/category ───────────────────────────────
    [HttpPut("{id:int}/category")]
    [RequirePermission("menu.ders-programi-kisitlamalari.edit")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
    {
        if (request.Category is not ("Hard" or "Soft"))
            return BadRequest(new { error = "Category 'Hard' veya 'Soft' olmalidir." });

        var definition = await _context.ConstraintDefinitions.FindAsync([id], ct);
        if (definition == null)
            return NotFound(new { error = "Constraint tanimi bulunamadi." });

        definition.Category = request.Category;
        definition.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Ok(new { definition.Id, definition.Category });
    }

    // ── PUT /api/constraints/{id}/parameters ─────────────────────────────
    [HttpPut("{id:int}/parameters")]
    [RequirePermission("menu.ders-programi-kisitlamalari.edit")]
    public async Task<IActionResult> UpdateParameters(int id, [FromBody] UpdateParametersRequest request, CancellationToken ct)
    {
        // Validate JSON
        if (string.IsNullOrWhiteSpace(request.Parameters))
            return BadRequest(new { error = "Parameters alani bos olamaz." });

        try
        {
            JsonDocument.Parse(request.Parameters);
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "Parameters gecerli bir JSON olmalidir." });
        }

        var definition = await _context.ConstraintDefinitions.FindAsync([id], ct);
        if (definition == null)
            return NotFound(new { error = "Constraint tanimi bulunamadi." });

        definition.Parameters = request.Parameters;
        definition.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Ok(new { definition.Id, definition.Parameters });
    }

    // ── POST /api/constraints/{id}/exceptions ────────────────────────────
    [HttpPost("{id:int}/exceptions")]
    [RequirePermission("menu.ders-programi-kisitlamalari.exception")]
    public async Task<IActionResult> AddException(int id, [FromBody] AddExceptionRequest request, CancellationToken ct)
    {
        // Validate at least one entity field is provided
        if (request.InstructorId == null && request.StudentId == null &&
            request.DepartmentId == null && request.CourseSectionId == null && request.TermId == null)
        {
            return BadRequest(new { error = "En az bir entity alani (InstructorId, StudentId, DepartmentId, CourseSectionId, TermId) belirtilmelidir." });
        }

        // Validate ParameterOverride is valid JSON if provided
        if (request.ParameterOverride != null)
        {
            try
            {
                JsonDocument.Parse(request.ParameterOverride);
            }
            catch (JsonException)
            {
                return BadRequest(new { error = "ParameterOverride gecerli bir JSON olmalidir." });
            }
        }

        var definition = await _context.ConstraintDefinitions.FindAsync([id], ct);
        if (definition == null)
            return NotFound(new { error = "Constraint tanimi bulunamadi." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var exception = new ConstraintException
        {
            ConstraintDefinitionId = id,
            InstructorId = request.InstructorId,
            StudentId = request.StudentId,
            DepartmentId = request.DepartmentId,
            CourseSectionId = request.CourseSectionId,
            TermId = request.TermId,
            IsExempt = request.IsExempt,
            ParameterOverride = request.ParameterOverride,
            Reason = request.Reason,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
        };

        _context.ConstraintExceptions.Add(exception);
        await _context.SaveChangesAsync(ct);

        return Created($"/api/constraints/{id}/exceptions", new { exception.Id });
    }

    // ── DELETE /api/constraints/exceptions/{exceptionId} ─────────────────
    [HttpDelete("exceptions/{exceptionId:int}")]
    [RequirePermission("menu.ders-programi-kisitlamalari.exception")]
    public async Task<IActionResult> RemoveException(int exceptionId, CancellationToken ct)
    {
        var exception = await _context.ConstraintExceptions.FindAsync([exceptionId], ct);
        if (exception == null)
            return NotFound(new { error = "Exception bulunamadi." });

        _context.ConstraintExceptions.Remove(exception);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    // ── GET /api/constraints/preview ─────────────────────────────────────
    [HttpGet("preview")]
    [RequirePermission("menu.ders-programi-kisitlamalari.view")]
    public async Task<IActionResult> Preview(
        [FromQuery] int sectionId,
        [FromQuery] int? classroomId,
        [FromQuery] int? instructorId,
        CancellationToken ct)
    {
        if (sectionId <= 0)
            return BadRequest(new { error = "Gecerli bir sectionId gonderilmelidir." });

        var request = new ScheduleValidationRequestDto
        {
            SectionId = sectionId,
            ClassroomId = classroomId,
            InstructorId = instructorId,
        };

        var result = await _schedulingService.ValidateAsync(request, ct);
        return Ok(result);
    }

    // ── GET /api/constraints/score-breakdown ─────────────────────────────
    [HttpGet("score-breakdown")]
    [RequirePermission("menu.ders-programi-kisitlamalari.view")]
    public async Task<IActionResult> GetScoreBreakdown([FromQuery] int termId, CancellationToken ct)
    {
        if (termId <= 0)
            return BadRequest(new { error = "Gecerli bir termId gonderilmelidir." });

        var breakdown = await _schedulingService.GetScoreBreakdownAsync(termId, ct);
        return Ok(breakdown);
    }

    // ── PUT /api/constraints/{id}/weight ─────────────────────────────────
    [HttpPut("{id:int}/weight")]
    [RequirePermission("menu.ders-programi-kisitlamalari.edit")]
    public async Task<IActionResult> UpdateWeight(int id, [FromBody] UpdateWeightRequest request, CancellationToken ct)
    {
        if (request.Weight < 1 || request.Weight > 10)
            return BadRequest(new { error = "Weight 1-10 arasinda olmalidir." });

        var def = await _context.ConstraintDefinitions.FindAsync(new object[] { id }, ct);
        if (def == null)
            return NotFound(new { error = "Constraint tanimi bulunamadi." });

        def.Weight = request.Weight;
        def.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Ok(new { def.Id, def.Weight });
    }
}
