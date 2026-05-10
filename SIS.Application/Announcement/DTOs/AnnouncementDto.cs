using SIS.Domain.Enums;

namespace SIS.Application.Announcement.DTOs;

public record AnnouncementListDto(
    int Id,
    string Title,
    string ContentPreview,
    AnnouncementAudience TargetAudience,
    int? TargetDepartmentId,
    string? TargetDepartmentName,
    int? TargetFacultyId,
    string? TargetFacultyName,
    int? TargetSectionId,
    string? TargetSectionInfo,
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
    int? TargetFacultyId,
    string? TargetFacultyName,
    int? TargetSectionId,
    string? TargetSectionInfo,
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
    int? TargetFacultyId,
    int? TargetSectionId,
    bool IsPinned,
    DateTime? StartsAt,
    DateTime? ExpiresAt);

public record UpdateAnnouncementDto(
    string Title,
    string Content,
    AnnouncementAudience TargetAudience,
    int? TargetDepartmentId,
    int? TargetFacultyId,
    int? TargetSectionId,
    bool IsPinned,
    DateTime StartsAt,
    DateTime? ExpiresAt);

// ── Create context: form'a hangi seçeneklerin sunulacağını döner ─────────────

public record AnnouncementSectionOptionDto(
    int Id,
    string CourseName,
    string CourseCode,
    int SectionNumber,
    string TermName);

public record AnnouncementCreateContextDto(
    string SenderRole,
    IReadOnlyList<AnnouncementAudience> AllowedAudiences,
    int? DepartmentId,
    string? DepartmentName,
    int? FacultyId,
    string? FacultyName,
    IReadOnlyList<AnnouncementSectionOptionDto> Sections);
