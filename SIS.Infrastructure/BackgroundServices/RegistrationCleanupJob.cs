using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIS.Application.Common.Time;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.BackgroundServices;

/// <summary>
/// AddDropStart tarihi geldiğinde Ready durumundaki dönemler için bekleyen
/// (Draft / PendingAdvisorApproval) kayıtları Dropped'a çeker. Idempotent.
/// </summary>
public class RegistrationCleanupJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<RegistrationCleanupJob> _logger;

    public RegistrationCleanupJob(
        IServiceScopeFactory scopeFactory,
        IDateTimeProvider clock,
        ILogger<RegistrationCleanupJob> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "RegistrationCleanupJob hatası.");
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = _clock.UtcNow;

        var terms = await ctx.AcademicTerms
            .Where(t => t.Status == TermStatus.Ready && t.AddDropStart <= now)
            .Select(t => t.Id)
            .ToListAsync(ct);

        foreach (var termId in terms)
        {
            var pending = await ctx.CourseRegistrations
                .Include(cr => cr.CourseSection)
                .Where(cr => cr.CourseSection.TermId == termId
                    && (cr.Status == RegistrationStatus.Draft
                        || cr.Status == RegistrationStatus.PendingAdvisorApproval))
                .ToListAsync(ct);

            if (pending.Count == 0) continue;

            foreach (var reg in pending)
            {
                reg.Status = RegistrationStatus.Dropped;
                reg.UpdatedAt = now;
                if (reg.CourseSection.EnrolledCount > 0)
                    reg.CourseSection.EnrolledCount--;
            }

            await ctx.SaveChangesAsync(ct);
            _logger.LogInformation(
                "RegistrationCleanupJob: dönem {TermId}, {Count} bekleyen kayıt Dropped'a çekildi.",
                termId, pending.Count);
        }
    }
}
