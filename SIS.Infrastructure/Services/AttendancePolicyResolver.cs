using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.Grading;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

/// <summary>
/// <see cref="IAttendancePolicyResolver"/> uygulaması.
/// Dönem/program override'ları <c>TermAttendancePolicies</c> tablosundan, global varsayılan
/// <see cref="ISystemConfigProvider"/>'dan (Attendance.* anahtarları) okunur.
/// </summary>
public sealed class AttendancePolicyResolver : IAttendancePolicyResolver
{
    private readonly AppDbContext _context;
    private readonly ISystemConfigProvider _config;

    public AttendancePolicyResolver(AppDbContext context, ISystemConfigProvider config)
    {
        _context = context;
        _config = config;
    }

    public async Task<ResolvedAttendancePolicy> ResolveAsync(int termId, int? programId, CancellationToken ct = default)
    {
        var global = await GetGlobalDefaultAsync(ct);

        var termPolicies = await _context.TermAttendancePolicies
            .AsNoTracking()
            .Where(p => p.TermId == termId && (p.ProgramId == null || p.ProgramId == programId))
            .ToListAsync(ct);

        return Pick(termPolicies, programId, global);
    }

    public async Task<IReadOnlyDictionary<int, ResolvedAttendancePolicy>> ResolveForStudentsAsync(
        IReadOnlyCollection<int> studentIds, int termId, CancellationToken ct = default)
    {
        var result = new Dictionary<int, ResolvedAttendancePolicy>();
        if (studentIds.Count == 0)
            return result;

        var global = await GetGlobalDefaultAsync(ct);

        // O döneme ait tüm politika satırları (program-özel + dönem geneli) tek sorguda.
        var termPolicies = await _context.TermAttendancePolicies
            .AsNoTracking()
            .Where(p => p.TermId == termId)
            .ToListAsync(ct);

        // Öğrenci → ana (Main+aktif) program tek sorguda.
        var programByStudent = await _context.StudentPrograms
            .AsNoTracking()
            .Where(sp => studentIds.Contains(sp.StudentId)
                      && sp.Type == StudentProgramType.Main
                      && sp.IsActive)
            .GroupBy(sp => sp.StudentId)
            .Select(g => new { StudentId = g.Key, ProgramId = g.Max(sp => sp.ProgramId) })
            .ToDictionaryAsync(x => x.StudentId, x => (int?)x.ProgramId, ct);

        foreach (var studentId in studentIds)
        {
            var programId = programByStudent.GetValueOrDefault(studentId);
            result[studentId] = Pick(termPolicies, programId, global);
        }

        return result;
    }

    // ── private helpers ───────────────────────────────────────────────────────

    private static ResolvedAttendancePolicy Pick(
        IReadOnlyCollection<TermAttendancePolicy> termPolicies,
        int? programId,
        ResolvedAttendancePolicy global)
    {
        // Program-özel override önce.
        if (programId.HasValue)
        {
            var programPolicy = termPolicies.FirstOrDefault(p => p.ProgramId == programId.Value);
            if (programPolicy != null)
                return ToResolved(programPolicy);
        }

        // Dönem geneli (ProgramId == null).
        var termGeneral = termPolicies.FirstOrDefault(p => p.ProgramId == null);
        if (termGeneral != null)
            return ToResolved(termGeneral);

        // Hiç dönem politikası yoksa global varsayılan.
        return global;
    }

    private static ResolvedAttendancePolicy ToResolved(TermAttendancePolicy policy) =>
        new(
            Math.Clamp(policy.TermWeekCount, 1, 52),
            ClampPercent(policy.TheoryAbsenceLimitPercent),
            ClampPercent(policy.LabAbsenceLimitPercent),
            AttendanceMethodExtensions.ParseAttendanceMethod(policy.Method));

    public async Task<ResolvedAttendancePolicy> GetGlobalDefaultAsync(CancellationToken ct = default)
    {
        var weekCount = Math.Clamp(await _config.GetIntAsync("Attendance.TermWeekCount", 14, ct), 1, 52);
        var theory = ClampPercent(await _config.GetDecimalAsync("Attendance.TheoryAbsenceLimitPercent", 30m, ct));
        var lab = ClampPercent(await _config.GetDecimalAsync("Attendance.LabAbsenceLimitPercent", 20m, ct));
        var method = AttendanceMethodExtensions.ParseAttendanceMethod(
            await _config.GetStringAsync("Attendance.Method", "Separate", ct));

        return new ResolvedAttendancePolicy(weekCount, theory, lab, method);
    }

    private static decimal ClampPercent(decimal value)
    {
        if (value < 0m) return 0m;
        if (value > 100m) return 100m;
        return value;
    }
}
