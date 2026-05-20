using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.BackgroundServices;

// FR-44: Clears unconfirmed Draft registrations every night at midnight.
// Draft registrations are soft holds on section capacity. Any plan item not submitted
// by midnight is automatically dropped, releasing the held capacity.
public class DraftRegistrationCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DraftRegistrationCleanupService> _logger;

    public DraftRegistrationCleanupService(IServiceScopeFactory scopeFactory, ILogger<DraftRegistrationCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                var nextMidnight = now.Date.AddDays(1);
                var delay = nextMidnight - now;

                _logger.LogInformation("FR-44: Draft cleanup scheduled in {Hours}h {Minutes}m", (int)delay.TotalHours, delay.Minutes);

                await Task.Delay(delay, stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                    await CleanupExpiredDraftRegistrationsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown — Task.Delay(..., stoppingToken) cancel'a TaskCanceledException fırlatır.
            // BackgroundServiceExceptionBehavior=StopHost (default .NET 8+) uygulamayı çakmasın.
        }
    }

    private async Task CleanupExpiredDraftRegistrationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            var now = DateTime.UtcNow;
            var activeTerm = await context.AcademicTerms
                .FirstOrDefaultAsync(t => !t.IsCancelled && t.IsActivated && t.TermStart <= now && t.TermEnd >= now, cancellationToken);

            if (activeTerm == null)
                return;

            var drafts = await context.CourseRegistrations
                .Include(cr => cr.CourseSection)
                .Where(cr => cr.Status == RegistrationStatus.Draft
                    && cr.CourseSection.TermId == activeTerm.Id)
                .ToListAsync(cancellationToken);

            if (!drafts.Any())
                return;

            foreach (var reg in drafts)
            {
                reg.Status = RegistrationStatus.Dropped;
                reg.UpdatedAt = DateTime.UtcNow;
                if (reg.CourseSection.EnrolledCount > 0)
                    reg.CourseSection.EnrolledCount--;
            }

            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "FR-44: Cleared {Count} expired draft registrations for term '{Term}'",
                drafts.Count,
                activeTerm.Name);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "FR-44: Error during draft registration cleanup");
        }
    }
}
