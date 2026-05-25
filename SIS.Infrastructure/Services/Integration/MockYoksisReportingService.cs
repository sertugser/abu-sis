using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.Integration;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Integration;

/// <summary>
/// Mock YÖKSİS student reporting (status, submission, ÖSYM placement lookup).
/// Returns deterministic fake data seeded by the student id and real program data.
/// The configured mode ("Integration.YoksisMode", default "Mock") selects behaviour:
/// Mock → fabricated result, Disabled → throws, Live → not yet implemented.
/// <see cref="IHttpClientFactory"/> is injected for the future Live path.
/// </summary>
public class MockYoksisReportingService : IExternalStudentReportingService
{
    private const string ConfigKey = "Integration.YoksisMode";

    private readonly AppDbContext _context;
    private readonly ISystemConfigProvider _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public MockYoksisReportingService(
        AppDbContext context,
        ISystemConfigProvider config,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<YoksisStatusResult> GetStatusAsync(int studentId, CancellationToken ct = default)
    {
        await EnsureModeAndStudentAsync(studentId, ct);
        var now = DateTime.UtcNow;

        return new YoksisStatusResult(
            IsMock: true,
            Status: "Bildirildi",
            YoksisRecordNo: BuildRecordNo(studentId, now),
            QueriedAt: now,
            Mode: "Mock",
            Message: "Test/mock verisi.");
    }

    public async Task<YoksisStatusResult> SubmitAsync(int studentId, CancellationToken ct = default)
    {
        await EnsureModeAndStudentAsync(studentId, ct);
        var now = DateTime.UtcNow;

        return new YoksisStatusResult(
            IsMock: true,
            Status: "Gönderildi",
            YoksisRecordNo: BuildRecordNo(studentId, now),
            QueriedAt: now,
            Mode: "Mock",
            Message: "Mock gönderim — gerçek YÖKSİS bildirimi yapılmadı.");
    }

    public async Task<OsymPlacementResult> QueryPlacementAsync(int studentId, CancellationToken ct = default)
    {
        await EnsureModeAndStudentAsync(studentId, ct);

        var program = await _context.StudentPrograms
            .AsNoTracking()
            .Include(sp => sp.Program)
            .Where(sp => sp.StudentId == studentId && sp.IsActive && sp.Type == StudentProgramType.Main)
            .OrderByDescending(sp => sp.EnrollmentDate)
            .FirstOrDefaultAsync(ct);

        // Deterministic, plausible fake values seeded by the student id.
        var score = 380m + ((long)studentId * 37 % 80) + 0.567m;
        var rankOrder = 10000 + (int)((long)studentId * 131 % 90000);
        var placementYear = program?.EnrollmentDate.Year ?? DateTime.UtcNow.Year;

        return new OsymPlacementResult(
            IsMock: true,
            EntranceType: "Osym",
            ScoreType: "SAY",
            Score: score,
            RankOrder: rankOrder,
            QuotaType: "Genel",
            PlacementYear: placementYear,
            PlacedProgramName: program?.Program?.Name,
            QueriedAt: DateTime.UtcNow,
            Mode: "Mock",
            Message: "Test/mock verisi — gerçek ÖSYM/YÖKSİS sorgusu yapılmadı.");
    }

    /// <summary>
    /// Validates the configured mode and the student's existence. Throws for a
    /// missing student, a disabled integration, or a not-yet-configured live path.
    /// </summary>
    private async Task EnsureModeAndStudentAsync(int studentId, CancellationToken ct)
    {
        // Evaluate the mode before any DB query: a disabled/live integration
        // must close immediately without leaking whether the student exists
        // (mirrors MockNviIdentityService ordering).
        var mode = await _config.GetStringAsync(ConfigKey, "Mock", ct);

        if (string.Equals(mode, "Disabled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("YÖKSİS entegrasyonu kapalı.");

        if (string.Equals(mode, "Live", StringComparison.OrdinalIgnoreCase))
            throw new NotImplementedException("YÖKSİS canlı entegrasyonu henüz yapılandırılmadı.");

        var studentExists = await _context.Students
            .AsNoTracking()
            .AnyAsync(s => s.Id == studentId, ct);
        if (!studentExists)
            throw new KeyNotFoundException($"Öğrenci bulunamadı: {studentId}");
    }

    private static string BuildRecordNo(int studentId, DateTime now) =>
        $"YOK-{now:yyyy}-{studentId:D6}";
}
