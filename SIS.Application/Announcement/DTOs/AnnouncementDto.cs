using SIS.Domain.Enums;

namespace SIS.Application.Announcement.DTOs;

public record AnnouncementListDto(
    int Id,
    string Title,
    string ContentPreview,
    AnnouncementAudience TargetAudience,
    int? TargetDepartmentId,
    string? TargetDepartmentName,
    bool IsPinned,
    DateTime StartsAt,
    DateTime? ExpiresAt,
    string CreatedByName,
    DateTime CreatedAt,
    bool IsActive);

public record AnnouncementDetailDto(
    int Id,
    string Title,
    string Content,
    AnnouncementAudience TargetAudience,
    int? TargetDepartmentId,
    string? TargetDepartmentName,
    bool IsPinned,
    DateTime StartsAt,
    DateTime? ExpiresAt,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsActive);

public record CreateAnnouncementDto(
    string Title,
    string Content,
    AnnouncementAudience TargetAudience,
    int? TargetDepartmentId,
    bool IsPinned,
    DateTime? StartsAt,
    DateTime? ExpiresAt);

public record UpdateAnnouncementDto(
    string Title,
    string Content,
    AnnouncementAudience TargetAudience,
    int? TargetDepartmentId,
    bool IsPinned,
    DateTime StartsAt,
    DateTime? ExpiresAt);
