using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIS.Application.Common.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.BackgroundServices;

/// <summary>
/// Applies the configurable audit retention policy once per day.
/// </summary>
public class AuditRetentionService : BackgroundService
{
    private const string RetentionDaysKey = "Audit.RetentionDays";
    private const int DefaultRetentionDays = 365;
    private const int MinRetentionDays = 30;
    private const int MaxRetentionDays = 3650;
    private const int BatchSize = 1000;
    private const int RunHour = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditRetentionService> _logger;

    public AuditRetentionService(IServiceScopeFactory scopeFactory, ILogger<AuditRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit retention service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRun(DateTimeOffset.Now);
            _logger.LogInformation(
                "Audit retention scheduled in {Hours}h {Minutes}m.",
                (int)delay.TotalHours,
                delay.Minutes);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await ApplyRetentionAsync(stoppingToken);
        }
    }

    private async Task ApplyRetentionAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var configProvider = scope.ServiceProvider.GetRequiredService<ISystemConfigProvider>();

            var configuredDays = await configProvider.GetIntAsync(RetentionDaysKey, DefaultRetentionDays, ct);
            var retentionDays = Math.Clamp(configuredDays, MinRetentionDays, MaxRetentionDays);
            if (configuredDays != retentionDays)
            {
                _logger.LogWarning(
                    "Audit retention config {Key}={ConfiguredDays} is outside allowed range {Min}-{Max}; using {RetentionDays}.",
                    RetentionDaysKey,
                    configuredDays,
                    MinRetentionDays,
                    MaxRetentionDays,
                    retentionDays);
            }

            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            var auditCandidateCount = await context.AuditLogs
                .AsNoTracking()
                .CountAsync(log => log.Timestamp < cutoff, ct);
            var schedulingCandidateCount = await context.SchedulingAuditLogs
                .AsNoTracking()
                .CountAsync(log => log.CreatedAt < cutoff, ct);

            _logger.LogInformation(
                "Audit retention cutoff {Cutoff:o}; candidates: AuditLogs={AuditCount}, SchedulingAuditLogs={SchedulingCount}.",
                cutoff,
                auditCandidateCount,
                schedulingCandidateCount);

            var deletedAuditLogs = auditCandidateCount > 0
                ? await DeleteAuditLogsAsync(context, cutoff, ct)
                : 0;
            var deletedSchedulingLogs = schedulingCandidateCount > 0
                ? await DeleteSchedulingAuditLogsAsync(context, cutoff, ct)
                : 0;

            _logger.LogInformation(
                "Audit retention completed. Deleted AuditLogs={AuditCount}, SchedulingAuditLogs={SchedulingCount}.",
                deletedAuditLogs,
                deletedSchedulingLogs);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Audit retention failed.");
        }
    }

    private async Task<int> DeleteAuditLogsAsync(AppDbContext context, DateTime cutoff, CancellationToken ct)
    {
        var totalDeleted = 0;

        await SetAuditLogDeleteTriggerAsync(context, enable: false, ct);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var deleted = await context.Database.ExecuteSqlInterpolatedAsync($"""
                    DELETE FROM "AuditLogs"
                    WHERE "Id" IN (
                        SELECT "Id"
                        FROM "AuditLogs"
                        WHERE "Timestamp" < {cutoff}
                        ORDER BY "Timestamp"
                        LIMIT {BatchSize}
                    )
                    """, ct);

                totalDeleted += deleted;

                if (deleted < BatchSize)
                    break;
            }
        }
        finally
        {
            try
            {
                await SetAuditLogDeleteTriggerAsync(context, enable: true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit retention could not re-enable the AuditLogs delete trigger.");
            }
        }

        return totalDeleted;
    }

    private static async Task<int> DeleteSchedulingAuditLogsAsync(AppDbContext context, DateTime cutoff, CancellationToken ct)
    {
        var totalDeleted = 0;

        while (!ct.IsCancellationRequested)
        {
            var deleted = await context.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM "SchedulingAuditLogs"
                WHERE "Id" IN (
                    SELECT "Id"
                    FROM "SchedulingAuditLogs"
                    WHERE "CreatedAt" < {cutoff}
                    ORDER BY "CreatedAt"
                    LIMIT {BatchSize}
                )
                """, ct);

            totalDeleted += deleted;

            if (deleted < BatchSize)
                break;
        }

        return totalDeleted;
    }

    private static async Task SetAuditLogDeleteTriggerAsync(AppDbContext context, bool enable, CancellationToken ct)
    {
        var action = enable ? "ENABLE" : "DISABLE";
        var command = $"""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM pg_trigger t
                    JOIN pg_class c ON c.oid = t.tgrelid
                    WHERE t.tgname = 'trg_audit_log_no_delete'
                      AND c.relname = 'AuditLogs'
                ) THEN
                    EXECUTE 'ALTER TABLE "AuditLogs" {action} TRIGGER trg_audit_log_no_delete';
                END IF;
            END $$;
            """;

        await context.Database.ExecuteSqlRawAsync(command, ct);
    }

    private static TimeSpan GetDelayUntilNextRun(DateTimeOffset now)
    {
        var nextRun = new DateTimeOffset(now.Year, now.Month, now.Day, RunHour, 0, 0, now.Offset);
        if (now >= nextRun)
            nextRun = nextRun.AddDays(1);

        return nextRun - now;
    }
}
