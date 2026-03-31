using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.BackgroundServices;

/// <summary>
/// Akademik takvimdeki tarihlere göre otomatik dönem durumu geçişi yapar.
/// Published → RegistrationOpen, RegistrationOpen → AddDrop, AddDrop → WithdrawalPeriod,
/// WithdrawalPeriod → GradeEntry geçişleri otomatik tetiklenir.
/// Manuel geçişler (Draft→Planning, Planning→Scheduling, Scheduling→Published, GradeEntry→Closed)
/// bu servisin kapsamı dışındadır.
/// </summary>
public class TermTransitionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TermTransitionBackgroundService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    public TermTransitionBackgroundService(IServiceScopeFactory scopeFactory, ILogger<TermTransitionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TermTransitionBackgroundService başlatıldı.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndTransitionAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Dönem geçiş kontrolünde hata.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CheckAndTransitionAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        // Aktif dönemleri kontrol et
        var activeTerms = await context.AcademicTerms
            .Where(t => t.IsActive || t.Status == TermStatus.Published)
            .ToListAsync(ct);

        foreach (var term in activeTerms)
        {
            var transitioned = false;

            // Published → RegistrationOpen (RegistrationStart tarihi geldiğinde)
            if (term.Status == TermStatus.Published && now >= term.RegistrationStart)
            {
                term.Status = TermStatus.RegistrationOpen;
                term.IsActive = true;

                // Diğer dönemlerin IsActive'ini kapat
                var otherActive = await context.AcademicTerms
                    .Where(t => t.IsActive && t.Id != term.Id)
                    .ToListAsync(ct);
                foreach (var t in otherActive)
                    t.IsActive = false;

                transitioned = true;
                _logger.LogInformation("Dönem '{Term}' otomatik olarak RegistrationOpen'a geçirildi.", term.Name);
            }

            // RegistrationOpen → AddDrop (AddDropStart tarihi geldiğinde)
            if (term.Status == TermStatus.RegistrationOpen && now >= term.AddDropStart)
            {
                // Onaylanmamış kayıtları iptal et
                var pendingRegistrations = await context.CourseRegistrations
                    .Include(cr => cr.CourseSection)
                    .Where(cr => cr.CourseSection.TermId == term.Id
                        && (cr.Status == RegistrationStatus.Draft || cr.Status == RegistrationStatus.PendingAdvisorApproval))
                    .ToListAsync(ct);

                foreach (var reg in pendingRegistrations)
                {
                    reg.Status = RegistrationStatus.Dropped;
                    reg.UpdatedAt = now;
                    if (reg.CourseSection.EnrolledCount > 0)
                        reg.CourseSection.EnrolledCount--;
                }

                term.Status = TermStatus.AddDrop;
                transitioned = true;
                _logger.LogInformation("Dönem '{Term}' otomatik olarak AddDrop'a geçirildi. {Count} bekleyen kayıt iptal edildi.", term.Name, pendingRegistrations.Count);
            }

            // AddDrop → WithdrawalPeriod (AddDropEnd tarihi geldiğinde)
            if (term.Status == TermStatus.AddDrop && now >= term.AddDropEnd)
            {
                term.Status = TermStatus.WithdrawalPeriod;
                transitioned = true;
                _logger.LogInformation("Dönem '{Term}' otomatik olarak WithdrawalPeriod'a geçirildi.", term.Name);
            }

            // WithdrawalPeriod → GradeEntry (WithdrawalDeadline geçtiğinde)
            if (term.Status == TermStatus.WithdrawalPeriod && now >= term.WithdrawalDeadline)
            {
                term.Status = TermStatus.GradeEntry;
                transitioned = true;
                _logger.LogInformation("Dönem '{Term}' otomatik olarak GradeEntry'e geçirildi.", term.Name);
            }

            if (transitioned)
            {
                term.UpdatedAt = now;
                await context.SaveChangesAsync(ct);
            }
        }
    }
}
