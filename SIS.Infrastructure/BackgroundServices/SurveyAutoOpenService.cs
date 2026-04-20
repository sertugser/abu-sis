using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseIntention.DTOs;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Infrastructure.BackgroundServices;

public class SurveyAutoOpenService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SurveyAutoOpenService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    public SurveyAutoOpenService(IServiceScopeFactory scopeFactory, ILogger<SurveyAutoOpenService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SurveyAutoOpenService baslatildi.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessSurveysAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Niyet anketi durumlari guncellenirken hata olustu.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    public async Task<int> ProcessSurveysAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        var surveys = await context.CourseIntentionSurveys
            .Where(s => !s.IsManualOverride)
            .ToListAsync(cancellationToken);

        var changedCount = 0;
        foreach (var survey in surveys)
        {
            var expectedStatus = CourseIntentionService.ResolveSurveyStatus(survey.OpenAtUtc, survey.CloseAtUtc, now);
            if (survey.Status == expectedStatus)
                continue;

            survey.Status = expectedStatus;
            survey.LastStatusChangedAtUtc = now;
            survey.UpdatedAt = now;
            changedCount++;
        }

        if (changedCount > 0)
            await context.SaveChangesAsync(cancellationToken);

        return changedCount;
    }
}
