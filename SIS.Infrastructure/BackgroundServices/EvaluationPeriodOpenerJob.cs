using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIS.Application.Common.Time;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.BackgroundServices;

/// <summary>
/// GradeEntryStart tarihi geldiğinde Ready durumundaki dönemler için
/// açık EvaluationPeriod yoksa oluşturur. Idempotent.
/// </summary>
public class EvaluationPeriodOpenerJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<EvaluationPeriodOpenerJob> _logger;

    public EvaluationPeriodOpenerJob(
        IServiceScopeFactory scopeFactory,
        IDateTimeProvider clock,
        ILogger<EvaluationPeriodOpenerJob> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await RunOnceAsync(stoppingToken); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "EvaluationPeriodOpenerJob hatası.");
                }
                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown — Task.Delay(..., stoppingToken) cancel'a TaskCanceledException fırlatır.
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = _clock.UtcNow;

        var terms = await ctx.AcademicTerms
            .Include(t => t.AcademicCalendar)
            .ActiveTerm()
            .ToListAsync(ct);
        terms = terms.Where(t => t.AcademicCalendar != null && t.AcademicCalendar.GradeEntryStart <= now).ToList();

        foreach (var term in terms)
        {
            var alreadyOpen = await ctx.EvaluationPeriods
                .AnyAsync(p => p.TermId == term.Id && p.Status == EvaluationPeriodStatus.Open, ct);
            if (alreadyOpen) continue;

            ctx.EvaluationPeriods.Add(new EvaluationPeriod
            {
                TermId = term.Id,
                OpenedAt = now,
                ClosesAt = term.TermEnd,
                Status = EvaluationPeriodStatus.Open,
                OpenedBy = null
            });
            await ctx.SaveChangesAsync(ct);
            _logger.LogInformation("EvaluationPeriodOpenerJob: dönem {TermId} için anket açıldı.", term.Id);
        }
    }
}
