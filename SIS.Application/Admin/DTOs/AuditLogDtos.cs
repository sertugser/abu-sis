namespace SIS.Application.Admin.DTOs;

public record AuditLogSummaryDto(
    int Id,
    int? UserId,
    string? UserName,
    string Action,
    string EntityType,
    string? EntityId,
    DateTime Timestamp,
    string? IpAddress);

public record AuditLogDetailDto(
    int Id,
    int? UserId,
    string? UserName,
    string Action,
    string EntityType,
    string? EntityId,
    string? OldValue,
    string? NewValue,
    DateTime Timestamp,
    string? IpAddress);

public record AuditLogPagedResponse(
    List<AuditLogSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record AuditLogStatsDto(
    int TodayTotal,
    int TodayFailedLogins,
    int TodayErrors,
    int TodayUnauthorized,
    int Last7DaysTotal);

public record AuditLogAnomalyDto(
    string Type,
    string Message,
    string Severity,
    int Count,
    DateTime DetectedAt);
