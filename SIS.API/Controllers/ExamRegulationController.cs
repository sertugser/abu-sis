using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/exam-regulations")]
[Authorize]
public class ExamRegulationController(AppDbContext context) : ControllerBase
{
    [HttpGet]
    [RequirePermission("menu.sinav-mufredati.view")]
    public async Task<IActionResult> List([FromQuery] int? programId, CancellationToken ct)
    {
        var query = context.ExamRegulations.AsNoTracking().Include(r => r.Program);
        var filtered = programId.HasValue
            ? query.Where(r => r.ProgramId == programId.Value)
            : query;

        var entities = await filtered
            .OrderByDescending(r => r.Year).ThenBy(r => r.Program.Name)
            .ToListAsync(ct);
        var items = entities.Select(r => ToDto(r)).ToList();

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("menu.sinav-mufredati.view")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var entity = await context.ExamRegulations.AsNoTracking()
            .Include(r => r.Program)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null) return NotFound(new { message = "Sınav yönergesi bulunamadı." });
        return Ok(ToDto(entity));
    }

    [HttpPost]
    [RequirePermission("menu.sinav-mufredati.create")]
    public async Task<IActionResult> Create([FromBody] SaveExamRegulationRequest req, CancellationToken ct)
    {
        var validation = Validate(req);
        if (validation != null) return BadRequest(new { message = validation });

        if (await ScopeExistsAsync(req.ProgramId, req.DegreeLevel, req.TermType, req.Year, null, ct))
            return BadRequest(new { message = "Bu program/öğrenci tipi/dönem/yıl için zaten bir yönerge var." });

        var entity = new ExamRegulation { CreatedAt = DateTime.UtcNow };
        Apply(entity, req);
        context.ExamRegulations.Add(entity);
        await context.SaveChangesAsync(ct);

        await context.Entry(entity).Reference(e => e.Program).LoadAsync(ct);
        return Ok(ToDto(entity));
    }

    [HttpPut("{id:int}")]
    [RequirePermission("menu.sinav-mufredati.edit")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveExamRegulationRequest req, CancellationToken ct)
    {
        var entity = await context.ExamRegulations.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null) return NotFound(new { message = "Sınav yönergesi bulunamadı." });

        var validation = Validate(req);
        if (validation != null) return BadRequest(new { message = validation });

        if (await ScopeExistsAsync(req.ProgramId, req.DegreeLevel, req.TermType, req.Year, id, ct))
            return BadRequest(new { message = "Bu program/öğrenci tipi/dönem/yıl için zaten başka bir yönerge var." });

        Apply(entity, req);
        entity.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        await context.Entry(entity).Reference(e => e.Program).LoadAsync(ct);
        return Ok(ToDto(entity));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("menu.sinav-mufredati.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await context.ExamRegulations.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null) return NotFound(new { message = "Sınav yönergesi bulunamadı." });
        context.ExamRegulations.Remove(entity);
        await context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:int}/clone")]
    [RequirePermission("menu.sinav-mufredati.create")]
    public async Task<IActionResult> Clone(int id, [FromBody] CloneExamRegulationRequest req, CancellationToken ct)
    {
        var source = await context.ExamRegulations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (source == null) return NotFound(new { message = "Kaynak yönerge bulunamadı." });

        if (req.Year < 2000 || req.Year > 2100)
            return BadRequest(new { message = "Yıl geçerli değil." });

        var targetProgramId = req.ProgramId ?? source.ProgramId;
        if (await ScopeExistsAsync(targetProgramId, source.DegreeLevel, source.TermType, req.Year, null, ct))
            return BadRequest(new { message = "Hedef program/dönem/yıl için zaten bir yönerge var." });

        var clone = new ExamRegulation
        {
            ProgramId = targetProgramId,
            DegreeLevel = source.DegreeLevel,
            TermType = source.TermType,
            Year = req.Year,
            IsActive = source.IsActive,
            Name = source.Name,
            MidtermCount = source.MidtermCount,
            FinalCount = source.FinalCount,
            MinMidtermWeightPercent = source.MinMidtermWeightPercent,
            MaxMidtermWeightPercent = source.MaxMidtermWeightPercent,
            MinFinalWeightPercent = source.MinFinalWeightPercent,
            MaxFinalWeightPercent = source.MaxFinalWeightPercent,
            PassingGrade = source.PassingGrade,
            RawScoreFloor1 = source.RawScoreFloor1,
            RawScoreFloor2 = source.RawScoreFloor2,
            FinalExamFloor = source.FinalExamFloor,
            CreatedAt = DateTime.UtcNow
        };
        context.ExamRegulations.Add(clone);
        await context.SaveChangesAsync(ct);
        await context.Entry(clone).Reference(e => e.Program).LoadAsync(ct);
        return Ok(ToDto(clone));
    }

    private async Task<bool> ScopeExistsAsync(int programId, DegreeLevel level, TermType termType, int year, int? excludeId, CancellationToken ct) =>
        await context.ExamRegulations.AsNoTracking().AnyAsync(r =>
            r.ProgramId == programId && r.DegreeLevel == level &&
            r.TermType == termType && r.Year == year &&
            (excludeId == null || r.Id != excludeId.Value), ct);

    private static string? Validate(SaveExamRegulationRequest req)
    {
        if (req.ProgramId <= 0) return "Program seçilmelidir.";
        if (string.IsNullOrWhiteSpace(req.Name)) return "Yönerge adı boş olamaz.";
        if (req.Year < 2000 || req.Year > 2100) return "Yıl geçerli değil.";
        if (req.MidtermCount < 0 || req.FinalCount < 0) return "Sınav sayısı negatif olamaz.";
        foreach (var (min, max, label) in new[]
                 {
                     (req.MinMidtermWeightPercent, req.MaxMidtermWeightPercent, "Ara sınav etki oranı"),
                     (req.MinFinalWeightPercent, req.MaxFinalWeightPercent, "YYS etki oranı")
                 })
        {
            if (min < 0 || max > 100) return $"{label} 0-100 arasında olmalıdır.";
            if (min > max) return $"{label}: en az değer en çok değeri aşamaz.";
        }
        if (req.PassingGrade < 0 || req.PassingGrade > 100) return "Geçme notu 0-100 arasında olmalıdır.";
        foreach (var (value, label) in new[]
                 {
                     (req.RawScoreFloor1, "Ham başarı notu alt limiti"),
                     (req.RawScoreFloor2, "Ham başarı notu alt limiti 2"),
                     (req.FinalExamFloor, "Yarıyıl sonu sınav limiti")
                 })
        {
            if (value < 0 || value > 100) return $"{label} 0-100 arasında olmalıdır.";
        }
        return null;
    }

    private static void Apply(ExamRegulation e, SaveExamRegulationRequest req)
    {
        e.ProgramId = req.ProgramId;
        e.DegreeLevel = req.DegreeLevel;
        e.TermType = req.TermType;
        e.Year = req.Year;
        e.IsActive = req.IsActive;
        e.Name = req.Name.Trim();
        e.MidtermCount = req.MidtermCount;
        e.FinalCount = req.FinalCount;
        e.MinMidtermWeightPercent = req.MinMidtermWeightPercent;
        e.MaxMidtermWeightPercent = req.MaxMidtermWeightPercent;
        e.MinFinalWeightPercent = req.MinFinalWeightPercent;
        e.MaxFinalWeightPercent = req.MaxFinalWeightPercent;
        e.PassingGrade = req.PassingGrade;
        e.RawScoreFloor1 = req.RawScoreFloor1;
        e.RawScoreFloor2 = req.RawScoreFloor2;
        e.FinalExamFloor = req.FinalExamFloor;
    }

    private static ExamRegulationDto ToDto(ExamRegulation r) => new(
        r.Id, r.ProgramId, r.Program?.Name ?? "", r.DegreeLevel.ToString(), r.TermType.ToString(),
        r.Year, r.IsActive, r.Name, r.MidtermCount, r.FinalCount,
        r.MinMidtermWeightPercent, r.MaxMidtermWeightPercent,
        r.MinFinalWeightPercent, r.MaxFinalWeightPercent,
        r.PassingGrade, r.RawScoreFloor1, r.RawScoreFloor2, r.FinalExamFloor);
}

public record ExamRegulationDto(
    int Id, int ProgramId, string ProgramName, string DegreeLevel, string TermType,
    int Year, bool IsActive, string Name, int MidtermCount, int FinalCount,
    decimal MinMidtermWeightPercent, decimal MaxMidtermWeightPercent,
    decimal MinFinalWeightPercent, decimal MaxFinalWeightPercent,
    decimal PassingGrade, decimal RawScoreFloor1, decimal RawScoreFloor2, decimal FinalExamFloor);

public record SaveExamRegulationRequest(
    int ProgramId, DegreeLevel DegreeLevel, TermType TermType, int Year,
    bool IsActive, string Name, int MidtermCount, int FinalCount,
    decimal MinMidtermWeightPercent, decimal MaxMidtermWeightPercent,
    decimal MinFinalWeightPercent, decimal MaxFinalWeightPercent,
    decimal PassingGrade, decimal RawScoreFloor1, decimal RawScoreFloor2, decimal FinalExamFloor);

public record CloneExamRegulationRequest(int? ProgramId, int Year);
