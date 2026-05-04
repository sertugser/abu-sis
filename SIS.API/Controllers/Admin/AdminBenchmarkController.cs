using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.FacultyPlanning.Services;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers.Admin;

/// <summary>
/// Auto-schedule algoritma performansını ölçer. Test verisi (Güz 2024-25 + Bahar 2025-26)
/// üzerinde çalışır, sonuçları metrik raporu olarak döner.
///
/// Faz 1+2+3+4 değişikliklerinden önce ve sonra çağrılır → karşılaştırma.
/// </summary>
[ApiController]
[Authorize(Roles = "SystemAdministrator")]
[Route("api/admin/benchmark")]
public class AdminBenchmarkController : ControllerBase
{
    private readonly IFacultyPlanningService _service;
    private readonly AppDbContext _context;

    public AdminBenchmarkController(IFacultyPlanningService service, AppDbContext context)
    {
        _service = service;
        _context = context;
    }

    /// <summary>
    /// Verilen dönem için auto-schedule çalıştırır + validation metrikleri hesaplar.
    /// Term parametresi: "fall2024" (Güz 2024-25) veya "spring2025" (Bahar 2025-26).
    /// </summary>
    [HttpPost("auto-schedule/{term}")]
    public async Task<ActionResult<BenchmarkReport>> Run(string term, [FromQuery] int? seed, CancellationToken ct)
    {
        var (year, type) = term.ToLowerInvariant() switch
        {
            "fall2024" => (2024, TermType.Fall),
            "spring2025" => (2025, TermType.Spring),
            "spring2026" => (2026, TermType.Spring),
            "fall2026" => (2026, TermType.Fall),
            _ => (0, TermType.Fall),
        };
        if (year == 0)
            return BadRequest(new { error = "Term must be one of: fall2024, spring2025, spring2026, fall2026." });

        var faculty = await _context.Faculties.FirstOrDefaultAsync(f => f.Code == "MUH", ct);
        if (faculty == null)
            return NotFound(new { error = "Mühendislik fakültesi (MUH) bulunamadı." });

        var academicTerm = await _context.AcademicTerms
            .FirstOrDefaultAsync(t => t.Year == year && t.TermType == type, ct);
        if (academicTerm == null)
            return NotFound(new { error = $"Term {year} {type} bulunamadı." });

        var request = new AutoScheduleRequest
        {
            FacultyId = faculty.Id,
            TermId = academicTerm.Id,
            Seed = seed,
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var preview = await _service.AutoSchedulePreviewAsync(request, "benchmark", ct);
        sw.Stop();

        var report = BuildReport(preview, term, sw.Elapsed.TotalSeconds);
        return Ok(report);
    }

    private static BenchmarkReport BuildReport(AutoSchedulePreviewDto preview, string term, double elapsedSec)
    {
        var report = new BenchmarkReport
        {
            Term = term,
            ElapsedSeconds = Math.Round(elapsedSec, 2),
            TotalSections = preview.TotalProcessed,
            SuccessCount = preview.SuccessCount,
            FailedCount = preview.FailedCount,
            SuccessRate = preview.TotalProcessed > 0
                ? Math.Round(100.0 * preview.SuccessCount / preview.TotalProcessed, 1)
                : 0,
            TotalPenalty = Math.Round(preview.TotalPenalty, 1),
            Stats = preview.Stats,
            FailureReasons = preview.Failures
                .GroupBy(f => f.Reason)
                .Select(g => new FailureReasonGroup { Reason = g.Key, Count = g.Count(), Examples = g.Take(3).Select(f => $"{f.CourseCode}-{f.SectionNumber}").ToList() })
                .OrderByDescending(g => g.Count)
                .ToList(),
        };

        // ── Derslik dağılımı ──
        var classroomUsage = preview.Assignments
            .Where(a => a.ClassroomId.HasValue)
            .SelectMany(a => a.ScheduleSlots.Where(s => s.ClassroomId.HasValue)
                .Select(s => new { s.ClassroomId, s.ClassroomName }))
            .GroupBy(x => new { x.ClassroomId, x.ClassroomName })
            .Select(g => new ClassroomUsageRow
            {
                ClassroomName = g.Key.ClassroomName ?? "?",
                SlotCount = g.Count(),
            })
            .OrderByDescending(c => c.SlotCount)
            .ToList();
        report.ClassroomDistribution = classroomUsage;

        if (classroomUsage.Count > 0)
        {
            var counts = classroomUsage.Select(c => (double)c.SlotCount).ToList();
            var avg = counts.Average();
            var variance = counts.Sum(c => (c - avg) * (c - avg)) / counts.Count;
            report.ClassroomLoadStdDev = Math.Round(Math.Sqrt(variance), 2);
            report.ClassroomMaxUsage = (int)counts.Max();
            report.ClassroomMinUsage = (int)counts.Min();
        }

        // ── Gün dağılımı ──
        var dayUsage = preview.Assignments
            .SelectMany(a => a.ScheduleSlots)
            .GroupBy(s => s.Day)
            .OrderBy(g => g.Key)
            .Select(g => new DayUsageRow
            {
                Day = g.Key.ToString(),
                SlotCount = g.Count(),
            })
            .ToList();
        report.DayDistribution = dayUsage;

        // ── Hafta sonu yüz-yüze sayısı ──
        report.SaturdayFaceToFaceCount = dayUsage.FirstOrDefault(d => d.Day == "Saturday")?.SlotCount ?? 0;
        report.SundayFaceToFaceCount = dayUsage.FirstOrDefault(d => d.Day == "Sunday")?.SlotCount ?? 0;

        // ── Aynı kursun section'ları aynı (Day, StartTime) yığılması ──
        var sameCoursePileups = preview.Assignments
            .GroupBy(a => a.CourseCode)
            .Where(g => g.Count() > 1)
            .Select(g => new SameCoursePileupRow
            {
                CourseCode = g.Key,
                SectionCount = g.Count(),
                UniqueStartTimes = g.SelectMany(a => a.ScheduleSlots)
                    .Select(s => s.StartTime.ToString("HH:mm"))
                    .Distinct()
                    .Count(),
                UniqueDays = g.SelectMany(a => a.ScheduleSlots)
                    .Select(s => s.Day.ToString())
                    .Distinct()
                    .Count(),
            })
            .OrderByDescending(r => r.SectionCount)
            .ToList();
        report.SameCoursePileups = sameCoursePileups;

        return report;
    }
}

public sealed class BenchmarkReport
{
    public string Term { get; set; } = "";
    public double ElapsedSeconds { get; set; }
    public int TotalSections { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public double SuccessRate { get; set; }
    public double TotalPenalty { get; set; }
    public AutoScheduleStats Stats { get; set; } = new();

    public List<ClassroomUsageRow> ClassroomDistribution { get; set; } = new();
    public double ClassroomLoadStdDev { get; set; }
    public int ClassroomMaxUsage { get; set; }
    public int ClassroomMinUsage { get; set; }

    public List<DayUsageRow> DayDistribution { get; set; } = new();
    public int SaturdayFaceToFaceCount { get; set; }
    public int SundayFaceToFaceCount { get; set; }

    public List<SameCoursePileupRow> SameCoursePileups { get; set; } = new();
    public List<FailureReasonGroup> FailureReasons { get; set; } = new();
}

public sealed class ClassroomUsageRow
{
    public string ClassroomName { get; set; } = "";
    public int SlotCount { get; set; }
}

public sealed class DayUsageRow
{
    public string Day { get; set; } = "";
    public int SlotCount { get; set; }
}

public sealed class SameCoursePileupRow
{
    public string CourseCode { get; set; } = "";
    public int SectionCount { get; set; }
    public int UniqueStartTimes { get; set; }
    public int UniqueDays { get; set; }
}

public sealed class FailureReasonGroup
{
    public string Reason { get; set; } = "";
    public int Count { get; set; }
    public List<string> Examples { get; set; } = new();
}
