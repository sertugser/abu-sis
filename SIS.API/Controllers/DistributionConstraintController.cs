using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/constraints/distributions")]
[Authorize]
public class DistributionConstraintController : ControllerBase
{
    private readonly AppDbContext _context;

    public DistributionConstraintController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET api/constraints/distributions?termId=X — list distribution constraints for a term.
    /// </summary>
    [HttpGet]
    [RequirePermission("menu.ders-programi-kisitlamalari.view")]
    public async Task<IActionResult> List([FromQuery] int termId)
    {
        if (termId <= 0)
            return BadRequest(new { error = "termId gereklidir." });

        var constraints = await _context.DistributionConstraints
            .AsNoTracking()
            .Include(d => d.Department)
            .Include(d => d.Courses).ThenInclude(dc => dc.Course)
            .Include(d => d.Courses).ThenInclude(dc => dc.CourseSection)
            .Where(d => d.TermId == termId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        var dtos = constraints.Select(MapToDto).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// GET api/constraints/distributions/{id} — get a single distribution constraint.
    /// </summary>
    [HttpGet("{id:int}")]
    [RequirePermission("menu.ders-programi-kisitlamalari.view")]
    public async Task<IActionResult> Get(int id)
    {
        var constraint = await _context.DistributionConstraints
            .AsNoTracking()
            .Include(d => d.Department)
            .Include(d => d.Courses).ThenInclude(dc => dc.Course)
            .Include(d => d.Courses).ThenInclude(dc => dc.CourseSection)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (constraint == null)
            return NotFound(new { error = "Kısıtlama bulunamadı." });

        return Ok(MapToDto(constraint));
    }

    /// <summary>
    /// POST api/constraints/distributions — create a new distribution constraint.
    /// </summary>
    [HttpPost]
    [RequirePermission("menu.ders-programi-kisitlamalari.create")]
    public async Task<IActionResult> Create([FromBody] CreateDistributionConstraintRequest request)
    {
        if (request.Courses == null || request.Courses.Count < 2)
            return BadRequest(new { error = "En az 2 ders seçilmelidir." });

        if (!Enum.TryParse<DistributionType>(request.DistributionType, out var distType))
            return BadRequest(new { error = $"Geçersiz kısıtlama tipi: {request.DistributionType}" });

        if (!Enum.TryParse<PreferenceLevel>(request.PreferenceLevel, out var prefLevel))
            return BadRequest(new { error = $"Geçersiz tercih seviyesi: {request.PreferenceLevel}" });

        // For Precedence, SortOrder must be unique
        if (distType == DistributionType.Precedence)
        {
            var sortOrders = request.Courses.Select(c => c.SortOrder).ToList();
            if (sortOrders.Distinct().Count() != sortOrders.Count)
                return BadRequest(new { error = "Sıralama (Precedence) kısıtlamasında her dersin sıra numarası farklı olmalıdır." });
        }

        var entity = new DistributionConstraint
        {
            DistributionType = distType,
            PreferenceLevel = prefLevel,
            TermId = request.TermId,
            DepartmentId = request.DepartmentId,
            Reason = request.Reason,
            CreatedAt = DateTime.UtcNow,
            Courses = request.Courses.Select(c => new DistributionConstraintCourse
            {
                CourseId = c.CourseId,
                CourseSectionId = c.CourseSectionId,
                SortOrder = c.SortOrder,
            }).ToList(),
        };

        _context.DistributionConstraints.Add(entity);
        await _context.SaveChangesAsync();

        // Reload with navigation props
        await _context.Entry(entity).Reference(e => e.Department).LoadAsync();
        foreach (var course in entity.Courses)
        {
            await _context.Entry(course).Reference(c => c.Course).LoadAsync();
            if (course.CourseSectionId.HasValue)
                await _context.Entry(course).Reference(c => c.CourseSection).LoadAsync();
        }

        return Created($"api/constraints/distributions/{entity.Id}", MapToDto(entity));
    }

    /// <summary>
    /// PUT api/constraints/distributions/{id} — update a distribution constraint.
    /// </summary>
    [HttpPut("{id:int}")]
    [RequirePermission("menu.ders-programi-kisitlamalari.edit")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateDistributionConstraintRequest request)
    {
        var entity = await _context.DistributionConstraints
            .Include(d => d.Courses)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (entity == null)
            return NotFound(new { error = "Kısıtlama bulunamadı." });

        if (request.Courses == null || request.Courses.Count < 2)
            return BadRequest(new { error = "En az 2 ders seçilmelidir." });

        if (!Enum.TryParse<DistributionType>(request.DistributionType, out var distType))
            return BadRequest(new { error = $"Geçersiz kısıtlama tipi: {request.DistributionType}" });

        if (!Enum.TryParse<PreferenceLevel>(request.PreferenceLevel, out var prefLevel))
            return BadRequest(new { error = $"Geçersiz tercih seviyesi: {request.PreferenceLevel}" });

        if (distType == DistributionType.Precedence)
        {
            var sortOrders = request.Courses.Select(c => c.SortOrder).ToList();
            if (sortOrders.Distinct().Count() != sortOrders.Count)
                return BadRequest(new { error = "Sıralama (Precedence) kısıtlamasında her dersin sıra numarası farklı olmalıdır." });
        }

        entity.DistributionType = distType;
        entity.PreferenceLevel = prefLevel;
        entity.DepartmentId = request.DepartmentId;
        entity.Reason = request.Reason;
        entity.UpdatedAt = DateTime.UtcNow;

        // Replace courses
        _context.DistributionConstraintCourses.RemoveRange(entity.Courses);
        entity.Courses = request.Courses.Select(c => new DistributionConstraintCourse
        {
            DistributionConstraintId = entity.Id,
            CourseId = c.CourseId,
            CourseSectionId = c.CourseSectionId,
            SortOrder = c.SortOrder,
        }).ToList();

        await _context.SaveChangesAsync();

        // Reload
        await _context.Entry(entity).Reference(e => e.Department).LoadAsync();
        foreach (var course in entity.Courses)
        {
            await _context.Entry(course).Reference(c => c.Course).LoadAsync();
            if (course.CourseSectionId.HasValue)
                await _context.Entry(course).Reference(c => c.CourseSection).LoadAsync();
        }

        return Ok(MapToDto(entity));
    }

    /// <summary>
    /// DELETE api/constraints/distributions/{id} — delete a distribution constraint.
    /// </summary>
    [HttpDelete("{id:int}")]
    [RequirePermission("menu.ders-programi-kisitlamalari.delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.DistributionConstraints.FindAsync(id);
        if (entity == null)
            return NotFound(new { error = "Kısıtlama bulunamadı." });

        _context.DistributionConstraints.Remove(entity);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ── Mapping ────────────────────────────────────────────────────

    private static DistributionConstraintDto MapToDto(DistributionConstraint d)
    {
        return new DistributionConstraintDto(
            d.Id,
            d.DistributionType.ToString(),
            d.PreferenceLevel.ToString(),
            d.TermId,
            d.DepartmentId,
            d.Department?.Name,
            d.Reason,
            d.CreatedAt,
            d.Courses
                .OrderBy(c => c.SortOrder)
                .Select(c => new DistributionConstraintCourseDto(
                    c.Id,
                    c.CourseId,
                    c.Course?.Code ?? string.Empty,
                    c.Course?.Name ?? string.Empty,
                    c.CourseSectionId,
                    c.CourseSection != null ? $"{c.Course?.Code}-{c.CourseSection.SectionNumber:D2}" : null,
                    c.SortOrder))
                .ToList());
    }
}
