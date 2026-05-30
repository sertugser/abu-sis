using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Attendance.DTOs;
using SIS.Application.Attendance.Validators;
using SIS.Application.Common.Time;
using SIS.Application.Grading;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

/// <summary>
/// Dönem (+ program) bazlı devamsızlık politikası yönetimi.
/// Sistem Ayarları sayfasındaki "Dönem/Program Bazlı Devamsızlık" panelini besler.
/// Yetki: Sistem Ayarları ile aynı (menu.system-config.*).
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/attendance-policy")]
public sealed class AttendancePolicyController : ControllerBase
{
    private static readonly string[] AllowedMethods = ["Separate", "TotalHours", "Passive", "MarkOnly"];

    private readonly AppDbContext _context;
    private readonly IAttendancePolicyResolver _resolver;
    private readonly IDateTimeProvider _clock;

    public AttendancePolicyController(
        AppDbContext context,
        IAttendancePolicyResolver resolver,
        IDateTimeProvider clock)
    {
        _context = context;
        _resolver = resolver;
        _clock = clock;
    }

    /// <summary>Bir dönemin tüm devamsızlık politikaları (dönem geneli + program override'ları) + global varsayılan.</summary>
    [HttpGet("{termId:int}")]
    [RequirePermission("menu.system-config.view")]
    public async Task<IActionResult> GetForTerm(int termId, CancellationToken ct)
    {
        var term = await _context.AcademicTerms.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == termId, ct);
        if (term == null)
            return NotFound(new { error = "Dönem bulunamadı." });

        var rows = await _context.TermAttendancePolicies.AsNoTracking()
            .Where(p => p.TermId == termId)
            .ToListAsync(ct);

        var programIds = rows.Where(r => r.ProgramId.HasValue).Select(r => r.ProgramId!.Value).Distinct().ToList();
        var programNames = programIds.Count == 0
            ? new Dictionary<int, string>()
            : await _context.AcademicPrograms.AsNoTracking()
                .Where(p => programIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var global = await _resolver.GetGlobalDefaultAsync(ct);

        var dto = new TermAttendancePolicyListDto
        {
            TermId = term.Id,
            TermName = term.Name,
            GlobalDefault = new TermAttendancePolicyDto
            {
                ProgramId = null,
                ProgramName = null,
                TermWeekCount = global.TermWeekCount,
                TheoryAbsenceLimitPercent = global.TheoryAbsenceLimitPercent,
                LabAbsenceLimitPercent = global.LabAbsenceLimitPercent,
                Method = global.Method.ToString()
            },
            Policies = rows
                .OrderBy(r => r.ProgramId.HasValue) // dönem geneli (null) önce
                .ThenBy(r => r.ProgramId)
                .Select(r => new TermAttendancePolicyDto
                {
                    ProgramId = r.ProgramId,
                    ProgramName = r.ProgramId.HasValue ? programNames.GetValueOrDefault(r.ProgramId.Value) : null,
                    TermWeekCount = r.TermWeekCount,
                    TheoryAbsenceLimitPercent = r.TheoryAbsenceLimitPercent,
                    LabAbsenceLimitPercent = r.LabAbsenceLimitPercent,
                    Method = r.Method
                })
                .ToList()
        };

        return Ok(dto);
    }

    /// <summary>Bir (dönem, program?) politikasını ekler veya günceller.</summary>
    [HttpPut("{termId:int}")]
    [RequirePermission("menu.system-config.edit")]
    public async Task<IActionResult> Upsert(int termId, [FromBody] UpsertTermAttendancePolicyRequest req, CancellationToken ct)
    {
        var term = await _context.AcademicTerms.FirstOrDefaultAsync(t => t.Id == termId, ct);
        if (term == null)
            return NotFound(new { error = "Dönem bulunamadı." });

        var validation = new UpsertTermAttendancePolicyRequestValidator().Validate(req);
        if (!validation.IsValid)
            return BadRequest(new { error = validation.Errors.First().ErrorMessage });

        var method = AllowedMethods.First(m => string.Equals(m, req.Method.Trim(), StringComparison.OrdinalIgnoreCase));

        if (req.ProgramId.HasValue)
        {
            var programExists = await _context.AcademicPrograms.AnyAsync(p => p.Id == req.ProgramId.Value, ct);
            if (!programExists)
                return BadRequest(new { error = "Program bulunamadı." });
        }

        var existing = await _context.TermAttendancePolicies
            .FirstOrDefaultAsync(p => p.TermId == termId && p.ProgramId == req.ProgramId, ct);

        if (existing == null)
        {
            _context.TermAttendancePolicies.Add(new TermAttendancePolicy
            {
                TermId = termId,
                ProgramId = req.ProgramId,
                TermWeekCount = req.TermWeekCount,
                TheoryAbsenceLimitPercent = req.TheoryAbsenceLimitPercent,
                LabAbsenceLimitPercent = req.LabAbsenceLimitPercent,
                Method = method,
                CreatedAt = _clock.UtcNow
            });
        }
        else
        {
            existing.TermWeekCount = req.TermWeekCount;
            existing.TheoryAbsenceLimitPercent = req.TheoryAbsenceLimitPercent;
            existing.LabAbsenceLimitPercent = req.LabAbsenceLimitPercent;
            existing.Method = method;
            existing.UpdatedAt = _clock.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        return await GetForTerm(termId, ct);
    }

    /// <summary>Bir program override'ını siler (program o dönem için dönem geneline/globale düşer).</summary>
    [HttpDelete("{termId:int}/program/{programId:int}")]
    [RequirePermission("menu.system-config.edit")]
    public async Task<IActionResult> DeleteProgramOverride(int termId, int programId, CancellationToken ct)
    {
        var row = await _context.TermAttendancePolicies
            .FirstOrDefaultAsync(p => p.TermId == termId && p.ProgramId == programId, ct);
        if (row == null)
            return NotFound(new { error = "Program override'ı bulunamadı." });

        _context.TermAttendancePolicies.Remove(row);
        await _context.SaveChangesAsync(ct);
        return await GetForTerm(termId, ct);
    }
}
