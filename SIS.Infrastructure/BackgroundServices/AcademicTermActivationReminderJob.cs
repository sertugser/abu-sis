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
/// Creates a single management announcement when an inactive academic term is
/// close to its start date, so admins can activate it before term workflows open.
/// </summary>
public class AcademicTermActivationReminderJob : BackgroundService
{
    public const int ReminderWindowDays = 10;
    public const string SystemCreatedBy = "system:academic-term-activation-reminder";

    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<AcademicTermActivationReminderJob> _logger;

    public AcademicTermActivationReminderJob(
        IServiceScopeFactory scopeFactory,
        IDateTimeProvider clock,
        ILogger<AcademicTermActivationReminderJob> logger)
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
                    _logger.LogError(ex, "AcademicTermActivationReminderJob failed.");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = _clock.UtcNow;
        var today = now.Date;
        var windowEndExclusive = today.AddDays(ReminderWindowDays + 1);

        var terms = await ctx.AcademicTerms
            .AsNoTracking()
            .Where(t => !t.IsCancelled
                && !t.IsActivated
                && t.TermEnd >= today
                && t.TermStart < windowEndExclusive)
            .OrderBy(t => t.TermStart)
            .ToListAsync(ct);

        foreach (var term in terms)
        {
            var title = BuildTitle(term);
            var alreadyCreated = await ctx.InternalAnnouncements
                .AsNoTracking()
                .AnyAsync(a => a.CreatedBy == SystemCreatedBy && a.Title == title, ct);
            if (alreadyCreated)
                continue;

            var startsInDays = (term.TermStart.Date - today).Days;
            ctx.InternalAnnouncements.Add(new InternalAnnouncement
            {
                Title = title,
                Content = BuildContent(term, startsInDays),
                ContentFormat = "plain",
                TargetAudience = AnnouncementAudience.Management,
                IsPinned = true,
                IsPopup = false,
                RequiresAcknowledgement = false,
                StartsAt = now,
                ExpiresAt = term.TermEnd,
                CreatedBy = SystemCreatedBy,
                CreatedByName = "Sistem",
                CreatedAt = now,
                IsActive = true,
            });

            await ctx.SaveChangesAsync(ct);
            _logger.LogInformation(
                "AcademicTermActivationReminderJob: created activation reminder for term {TermId}.",
                term.Id);
        }
    }

    public static string BuildTitle(AcademicTerm term) =>
        $"Akademik dönem aktivasyonu gerekli: {term.Name}";

    private static string BuildContent(AcademicTerm term, int startsInDays)
    {
        var dayText = startsInDays < 0
            ? "başladı"
            : startsInDays == 0
                ? "bugün başlıyor"
                : $"başlamasına {startsInDays} gün kaldı";

        return $"{term.Name} dönemi {dayText}. Dönemin aktif dönem olarak kullanılabilmesi için Akademik Dönem Tanımları sayfasından kullanıma açın.";
    }
}
