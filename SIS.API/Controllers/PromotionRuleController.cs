using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

public record PromotionRuleDto(int Id, int ProgramId, string ProgramName, int FromYear, int ToYear, decimal? MinCgpa, int? MinEcts, int? MinCourseCount);
public record CreatePromotionRuleDto(int ProgramId, int FromYear, int ToYear, decimal? MinCgpa, int? MinEcts, int? MinCourseCount);
public record UpdatePromotionRuleDto(int FromYear, int ToYear, decimal? MinCgpa, int? MinEcts, int? MinCourseCount);

[ApiController]
[Route("api/admin/promotion-rules")]
[Authorize(Roles = "SystemAdministrator")]
public class PromotionRuleController(AppDbContext context) : ControllerBase
{
    private readonly AppDbContext _context = context;

    [HttpGet]
    public async Task<IActionResult> GetByProgram([FromQuery] int? programId, CancellationToken ct)
    {
        var query = _context.PromotionRules
            .AsNoTracking()
            .Include(r => r.Program)
            .AsQueryable();

        if (programId.HasValue)
            query = query.Where(r => r.ProgramId == programId.Value);

        var rules = await query
            .OrderBy(r => r.ProgramId).ThenBy(r => r.FromYear)
            .Select(r => new PromotionRuleDto(
                r.Id, r.ProgramId, r.Program.Name,
                r.FromYear, r.ToYear, r.MinCgpa, r.MinEcts, r.MinCourseCount))
            .ToListAsync(ct);

        return Ok(rules);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePromotionRuleDto dto, CancellationToken ct)
    {
        var program = await _context.AcademicPrograms.AsNoTracking().FirstOrDefaultAsync(p => p.Id == dto.ProgramId, ct);
        if (program == null)
            return NotFound(new { error = "Program bulunamadı." });

        if (dto.FromYear >= dto.ToYear)
            return BadRequest(new { error = "FromYear, ToYear'dan küçük olmalıdır." });

        if (dto.MinCgpa == null && dto.MinEcts == null && dto.MinCourseCount == null)
            return BadRequest(new { error = "En az bir kural kriteri belirtilmelidir (MinCgpa, MinEcts veya MinCourseCount)." });

        // Aynı program ve sınıf geçişi için kural var mı?
        var exists = await _context.PromotionRules.AnyAsync(r =>
            r.ProgramId == dto.ProgramId && r.FromYear == dto.FromYear && r.ToYear == dto.ToYear, ct);

        if (exists)
            return BadRequest(new { error = "Bu program ve sınıf geçişi için zaten bir kural tanımlı." });

        var rule = new PromotionRule
        {
            ProgramId = dto.ProgramId,
            FromYear = dto.FromYear,
            ToYear = dto.ToYear,
            MinCgpa = dto.MinCgpa,
            MinEcts = dto.MinEcts,
            MinCourseCount = dto.MinCourseCount
        };

        _context.PromotionRules.Add(rule);
        await _context.SaveChangesAsync(ct);

        return CreatedAtAction(null, null,
            new PromotionRuleDto(rule.Id, rule.ProgramId, program.Name,
                rule.FromYear, rule.ToYear, rule.MinCgpa, rule.MinEcts, rule.MinCourseCount));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePromotionRuleDto dto, CancellationToken ct)
    {
        var rule = await _context.PromotionRules
            .Include(r => r.Program)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (rule == null)
            return NotFound(new { error = "Sınıf atlatma kuralı bulunamadı." });

        if (dto.FromYear >= dto.ToYear)
            return BadRequest(new { error = "FromYear, ToYear'dan küçük olmalıdır." });

        if (dto.MinCgpa == null && dto.MinEcts == null && dto.MinCourseCount == null)
            return BadRequest(new { error = "En az bir kural kriteri belirtilmelidir (MinCgpa, MinEcts veya MinCourseCount)." });

        // Aynı program ve sınıf geçişi için başka kural var mı? (kendi hariç)
        var duplicate = await _context.PromotionRules.AnyAsync(r =>
            r.ProgramId == rule.ProgramId && r.FromYear == dto.FromYear && r.ToYear == dto.ToYear && r.Id != id, ct);

        if (duplicate)
            return BadRequest(new { error = "Bu program ve sınıf geçişi için zaten bir kural tanımlı." });

        rule.FromYear = dto.FromYear;
        rule.ToYear = dto.ToYear;
        rule.MinCgpa = dto.MinCgpa;
        rule.MinEcts = dto.MinEcts;
        rule.MinCourseCount = dto.MinCourseCount;

        await _context.SaveChangesAsync(ct);

        return Ok(new PromotionRuleDto(rule.Id, rule.ProgramId, rule.Program.Name,
            rule.FromYear, rule.ToYear, rule.MinCgpa, rule.MinEcts, rule.MinCourseCount));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var rule = await _context.PromotionRules.FirstOrDefaultAsync(r => r.Id == id, ct);

        if (rule == null)
            return NotFound(new { error = "Sınıf atlatma kuralı bulunamadı." });

        _context.PromotionRules.Remove(rule);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }
}
