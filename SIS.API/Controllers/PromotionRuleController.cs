using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

// CurriculumId opsiyonel — null ise program-bazlı default, set ise müfredat-spesifik override (γ hibrit)
public record PromotionRuleDto(
    int Id, int ProgramId, string ProgramName,
    int? CurriculumId, string? CurriculumVersion,
    int FromYear, int ToYear,
    decimal? MinCgpa, int? MinEcts, int? MinCourseCount,
    int? MaxFailedCourses, int? MinCompulsoryCoursePassed);

public record CreatePromotionRuleDto(
    int ProgramId, int? CurriculumId,
    int FromYear, int ToYear,
    decimal? MinCgpa, int? MinEcts, int? MinCourseCount,
    int? MaxFailedCourses, int? MinCompulsoryCoursePassed);

public record UpdatePromotionRuleDto(
    int? CurriculumId,
    int FromYear, int ToYear,
    decimal? MinCgpa, int? MinEcts, int? MinCourseCount,
    int? MaxFailedCourses, int? MinCompulsoryCoursePassed);

[ApiController]
[Route("api/admin/promotion-rules")]
[Authorize(Roles = "SystemAdministrator")]
public class PromotionRuleController(AppDbContext context, ISystemConfigProvider config) : ControllerBase
{
    private readonly AppDbContext _context = context;
    private readonly ISystemConfigProvider _config = config;

    /// <summary>
    /// MinCgpa değerini SystemConfig.GPA.MaxScale'e göre doğrular.
    /// Geçersiz ise hata mesajı, geçerliyse null döner.
    /// </summary>
    private async Task<string?> ValidateMinCgpaAsync(decimal? minCgpa, CancellationToken ct)
    {
        if (!minCgpa.HasValue) return null;
        if (minCgpa.Value < 0) return "En Az Ortalama negatif olamaz.";
        var maxScale = await _config.GetDecimalAsync("GPA.MaxScale", 4.00m, ct);
        if (minCgpa.Value > maxScale)
            return $"En Az Ortalama {maxScale:F2} üzerinde olamaz (sistem GPA üst sınırı).";
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int? programId,
        [FromQuery] int? curriculumId,
        CancellationToken ct)
    {
        var query = _context.PromotionRules
            .AsNoTracking()
            .Include(r => r.Program)
            .Include(r => r.Curriculum)
            .AsQueryable();

        // curriculumId verilirse: o müfredata özel kurallar + program-bazlı default'lar (CurriculumId null)
        if (curriculumId.HasValue)
        {
            var programIdOfCurriculum = await _context.Curricula
                .AsNoTracking()
                .Where(c => c.Id == curriculumId.Value)
                .Select(c => (int?)c.ProgramId)
                .FirstOrDefaultAsync(ct);

            if (programIdOfCurriculum == null)
                return NotFound(new { error = "Müfredat bulunamadı." });

            query = query.Where(r => r.ProgramId == programIdOfCurriculum.Value
                && (r.CurriculumId == null || r.CurriculumId == curriculumId.Value));
        }
        else if (programId.HasValue)
        {
            query = query.Where(r => r.ProgramId == programId.Value);
        }

        var rules = await query
            .OrderBy(r => r.ProgramId).ThenBy(r => r.CurriculumId).ThenBy(r => r.FromYear)
            .Select(r => new PromotionRuleDto(
                r.Id, r.ProgramId, r.Program.Name,
                r.CurriculumId, r.Curriculum != null ? r.Curriculum.Version : null,
                r.FromYear, r.ToYear,
                r.MinCgpa, r.MinEcts, r.MinCourseCount,
                r.MaxFailedCourses, r.MinCompulsoryCoursePassed))
            .ToListAsync(ct);

        return Ok(rules);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePromotionRuleDto dto, CancellationToken ct)
    {
        var program = await _context.AcademicPrograms.AsNoTracking().FirstOrDefaultAsync(p => p.Id == dto.ProgramId, ct);
        if (program == null)
            return NotFound(new { error = "Program bulunamadı." });

        // CurriculumId verilirse program ile uyumlu olmalı
        if (dto.CurriculumId.HasValue)
        {
            var curr = await _context.Curricula.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == dto.CurriculumId.Value, ct);
            if (curr == null)
                return NotFound(new { error = "Müfredat bulunamadı." });
            if (curr.ProgramId != dto.ProgramId)
                return BadRequest(new { error = "Müfredat seçilen programa ait değil." });
        }

        if (dto.FromYear >= dto.ToYear)
            return BadRequest(new { error = "Hedef sınıf, kaynak sınıftan büyük olmalıdır." });

        if (dto.MinCgpa == null && dto.MinEcts == null && dto.MinCourseCount == null
            && dto.MaxFailedCourses == null && dto.MinCompulsoryCoursePassed == null)
            return BadRequest(new { error = "En az bir kural kriteri belirtilmelidir." });

        var cgpaError = await ValidateMinCgpaAsync(dto.MinCgpa, ct);
        if (cgpaError != null)
            return BadRequest(new { error = cgpaError });

        // Aynı program × müfredat × FromYear × ToYear kombinasyonu tek olabilir
        var exists = await _context.PromotionRules.AnyAsync(r =>
            r.ProgramId == dto.ProgramId
            && r.CurriculumId == dto.CurriculumId
            && r.FromYear == dto.FromYear
            && r.ToYear == dto.ToYear, ct);

        if (exists)
            return BadRequest(new { error = "Bu program/müfredat ve sınıf geçişi için zaten bir kural tanımlı." });

        var rule = new PromotionRule
        {
            ProgramId = dto.ProgramId,
            CurriculumId = dto.CurriculumId,
            FromYear = dto.FromYear,
            ToYear = dto.ToYear,
            MinCgpa = dto.MinCgpa,
            MinEcts = dto.MinEcts,
            MinCourseCount = dto.MinCourseCount,
            MaxFailedCourses = dto.MaxFailedCourses,
            MinCompulsoryCoursePassed = dto.MinCompulsoryCoursePassed
        };

        _context.PromotionRules.Add(rule);
        await _context.SaveChangesAsync(ct);

        // Curriculum bilgisini sürümüyle döndürmek için yeniden yükle
        var saved = await _context.PromotionRules
            .AsNoTracking()
            .Include(r => r.Program)
            .Include(r => r.Curriculum)
            .FirstAsync(r => r.Id == rule.Id, ct);

        return CreatedAtAction(null, null,
            new PromotionRuleDto(saved.Id, saved.ProgramId, saved.Program.Name,
                saved.CurriculumId, saved.Curriculum?.Version,
                saved.FromYear, saved.ToYear,
                saved.MinCgpa, saved.MinEcts, saved.MinCourseCount,
                saved.MaxFailedCourses, saved.MinCompulsoryCoursePassed));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePromotionRuleDto dto, CancellationToken ct)
    {
        var rule = await _context.PromotionRules
            .Include(r => r.Program)
            .Include(r => r.Curriculum)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (rule == null)
            return NotFound(new { error = "Sınıf atlatma kuralı bulunamadı." });

        if (dto.CurriculumId.HasValue)
        {
            var curr = await _context.Curricula.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == dto.CurriculumId.Value, ct);
            if (curr == null)
                return NotFound(new { error = "Müfredat bulunamadı." });
            if (curr.ProgramId != rule.ProgramId)
                return BadRequest(new { error = "Müfredat kuralın programıyla uyumlu değil." });
        }

        if (dto.FromYear >= dto.ToYear)
            return BadRequest(new { error = "Hedef sınıf, kaynak sınıftan büyük olmalıdır." });

        if (dto.MinCgpa == null && dto.MinEcts == null && dto.MinCourseCount == null
            && dto.MaxFailedCourses == null && dto.MinCompulsoryCoursePassed == null)
            return BadRequest(new { error = "En az bir kural kriteri belirtilmelidir." });

        var cgpaError = await ValidateMinCgpaAsync(dto.MinCgpa, ct);
        if (cgpaError != null)
            return BadRequest(new { error = cgpaError });

        var duplicate = await _context.PromotionRules.AnyAsync(r =>
            r.ProgramId == rule.ProgramId
            && r.CurriculumId == dto.CurriculumId
            && r.FromYear == dto.FromYear
            && r.ToYear == dto.ToYear
            && r.Id != id, ct);

        if (duplicate)
            return BadRequest(new { error = "Bu program/müfredat ve sınıf geçişi için zaten bir kural tanımlı." });

        rule.CurriculumId = dto.CurriculumId;
        rule.FromYear = dto.FromYear;
        rule.ToYear = dto.ToYear;
        rule.MinCgpa = dto.MinCgpa;
        rule.MinEcts = dto.MinEcts;
        rule.MinCourseCount = dto.MinCourseCount;
        rule.MaxFailedCourses = dto.MaxFailedCourses;
        rule.MinCompulsoryCoursePassed = dto.MinCompulsoryCoursePassed;

        await _context.SaveChangesAsync(ct);

        var updated = await _context.PromotionRules
            .AsNoTracking()
            .Include(r => r.Program)
            .Include(r => r.Curriculum)
            .FirstAsync(r => r.Id == id, ct);

        return Ok(new PromotionRuleDto(updated.Id, updated.ProgramId, updated.Program.Name,
            updated.CurriculumId, updated.Curriculum?.Version,
            updated.FromYear, updated.ToYear,
            updated.MinCgpa, updated.MinEcts, updated.MinCourseCount,
            updated.MaxFailedCourses, updated.MinCompulsoryCoursePassed));
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
