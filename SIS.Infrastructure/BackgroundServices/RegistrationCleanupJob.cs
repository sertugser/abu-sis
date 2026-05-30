using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIS.Application.Common.Time;
using SIS.Application.Notifications.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Infrastructure.BackgroundServices;

/// <summary>
/// Ders kayıt koltuk-rezervasyonu (soft-hold) temizliğinin TEK job'u. Daha önce iki ayrı job
/// (DraftRegistrationCleanupService + RegistrationCleanupJob) Draft kayıtları çakışık hedefliyor,
/// aynı koltuğu iki kez düşürme + onay bekleyenleri sessiz iptal riski taşıyordu. İki sorumluluk
/// burada tek serviste net ayrıldı:
///   1) FR-44 — her gece (YEREL gece yarısı) aktif dönemdeki GÖNDERİLMEMİŞ Draft holdlarını bırakır.
///   2) Add/Drop başlayınca — kalan Draft + onay bekleyen (Pending) kayıtları düşürür ve Pending
///      öğrencilerine BİLDİRİM gönderir (sessiz iptal yok).
/// Koltuk (EnrolledCount) değişimi tek kaynaktan: <see cref="SeatLedger"/>.
/// </summary>
public class RegistrationCleanupJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private static readonly TimeZoneInfo TurkeyTimeZone = ResolveTurkeyTimeZone();

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
        // Gece-yarısı Draft temizliği günde bir kez çalışmalı. İlk tick baseline kurar; kısa interval
        // restart sonrası kaçan günü doğal olarak telafi eder (catch-up) — sabit "gece yarısı bekle"
        // yaklaşımının "00:00'dan sonra açılınca o günü atlama" hatası ortadan kalkar.
        DateOnly? lastDraftSweepLocalDay = null;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken); // add/drop temizliği (idempotent)

                    var localToday = DateOnly.FromDateTime(
                        TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, TurkeyTimeZone));
                    if (lastDraftSweepLocalDay is null)
                    {
                        lastDraftSweepLocalDay = localToday; // baseline — ilk turda süpürme yok
                    }
                    else if (localToday > lastDraftSweepLocalDay.Value)
                    {
                        await RunDraftSoftHoldCleanupAsync(stoppingToken); // FR-44 yerel gece yarısı
                        lastDraftSweepLocalDay = localToday;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "RegistrationCleanupJob hatası.");
                }
                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown — Task.Delay cancel'a TaskCanceledException fırlatır.
        }
    }

    /// <summary>
    /// Add/Drop başlamış aktif dönem(ler)de bekleyen (Draft + PendingAdvisorApproval) kayıtları
    /// düşürür, koltukları iade eder, onay bekleyenlerin öğrencilerine bildirim gönderir. İdempotent.
    /// </summary>
    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetService<INotificationService>();
        var now = _clock.UtcNow;

        var terms = await ctx.AcademicTerms
            .Include(t => t.AcademicCalendar)
            .ActiveTerm()
            .Select(t => new { t.Id, t.AcademicCalendar })
            .ToListAsync(ct);

        var termIds = terms
            .Where(t => t.AcademicCalendar != null && t.AcademicCalendar.AddDropStart <= now)
            .Select(t => t.Id)
            .ToList();

        foreach (var termId in termIds)
        {
            var pending = await ctx.CourseRegistrations
                .Include(cr => cr.CourseSection)
                .Where(cr => cr.CourseSection.TermId == termId
                    && (cr.Status == RegistrationStatus.Draft
                        || cr.Status == RegistrationStatus.PendingAdvisorApproval))
                .ToListAsync(ct);

            if (pending.Count == 0) continue;

            var droppedPendingStudentIds = pending
                .Where(cr => cr.Status == RegistrationStatus.PendingAdvisorApproval)
                .Select(cr => cr.StudentId)
                .Distinct()
                .ToList();

            foreach (var reg in pending)
            {
                reg.Status = RegistrationStatus.Dropped;
                reg.UpdatedAt = now;
                SeatLedger.Release(reg.CourseSection); // tek kaynak, floor-0 korumalı
            }

            await ctx.SaveChangesAsync(ct);
            _logger.LogInformation(
                "RegistrationCleanupJob: dönem {TermId}, {Count} bekleyen kayıt Dropped'a çekildi.",
                termId, pending.Count);

            // Onay bekleyen kaydı düşürülen öğrencilere bildirim (sessiz iptal yok).
            if (notifications is not null && droppedPendingStudentIds.Count > 0)
            {
                var userIds = await ctx.Students
                    .Where(s => droppedPendingStudentIds.Contains(s.Id))
                    .Select(s => s.UserId)
                    .ToListAsync(ct);
                foreach (var userId in userIds)
                {
                    await notifications.NotifyAsync(
                        userId,
                        NotificationType.RegistrationRejected,
                        "Onay bekleyen ders kaydınız düştü",
                        "Ekle/bırak dönemi başladığı için danışman onayı tamamlanmamış ders kaydınız iptal edildi. Lütfen kaydınızı yenileyin.",
                        link: "/ders-kayit",
                        severity: NotificationSeverity.Warning,
                        entityRef: $"term:{termId}",
                        ct: ct);
                }
            }
        }
    }

    /// <summary>
    /// FR-44: aktif dönemde GÖNDERİLMEMİŞ (Draft) soft-hold kayıtların koltuğunu serbest bırakır.
    /// Gönderilmemiş plan kaydı olduğundan öğrenci bildirimi yapılmaz.
    /// </summary>
    public async Task RunDraftSoftHoldCleanupAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = _clock.UtcNow;

        var activeTerm = await ctx.AcademicTerms.ActiveTerm().FirstOrDefaultAsync(ct);
        if (activeTerm == null)
            return;

        var drafts = await ctx.CourseRegistrations
            .Include(cr => cr.CourseSection)
            .Where(cr => cr.Status == RegistrationStatus.Draft
                && cr.CourseSection.TermId == activeTerm.Id)
            .ToListAsync(ct);

        if (drafts.Count == 0)
            return;

        foreach (var reg in drafts)
        {
            reg.Status = RegistrationStatus.Dropped;
            reg.UpdatedAt = now;
            SeatLedger.Release(reg.CourseSection);
        }

        await ctx.SaveChangesAsync(ct);
        _logger.LogInformation(
            "RegistrationCleanupJob (FR-44): aktif dönem {TermId}, {Count} gönderilmemiş Draft hold serbest bırakıldı.",
            activeTerm.Id, drafts.Count);
    }

    private static TimeZoneInfo ResolveTurkeyTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
        }
    }
}
