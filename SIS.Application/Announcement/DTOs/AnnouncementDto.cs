using SIS.Application.Announcement.Configuration;
using SIS.Domain.Enums;

namespace SIS.Application.Announcement.DTOs;

public record AnnouncementListDto(
    int Id,
    string Title,
    string ContentPreview,
    string ContentFormat,
    AnnouncementAudience TargetAudience,
    int? TargetDepartmentId,
    string? TargetDepartmentName,
    int? TargetFacultyId,
    string? TargetFacultyName,
    int? TargetSectionId,
    string? TargetSectionInfo,
    int? TargetStudentYear,
    AnnouncementCategorySummaryDto? Category,
    bool IsPinned,
    bool IsPopup,
    bool RequiresAcknowledgement,
    DateTime StartsAt,
    DateTime? ExpiresAt,
    string CreatedByName,
    DateTime CreatedAt,
    bool IsActive,
    bool IsReadByMe,
    int AttachmentCount);

public record AnnouncementDetailDto(
    int Id,
    string Title,
    string Content,
    string ContentFormat,
    AnnouncementAudience TargetAudience,
    int? TargetDepartmentId,
    string? TargetDepartmentName,
    int? TargetFacultyId,
    string? TargetFacultyName,
    int? TargetSectionId,
    string? TargetSectionInfo,
    int? TargetStudentYear,
    AnnouncementCategorySummaryDto? Category,
    bool IsPinned,
    bool IsPopup,
    bool RequiresAcknowledgement,
    DateTime StartsAt,
    DateTime? ExpiresAt,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsActive,
    bool IsReadByMe,
    IReadOnlyList<AnnouncementAttachmentSummaryDto> Attachments);

public record CreateAnnouncementDto(
    string Title,
    string Content,
    string ContentFormat,
    AnnouncementAudience TargetAudience,
    int? TargetDepartmentId,
    int? TargetFacultyId,
    int? TargetSectionId,
    int? TargetStudentYear,
    int? CategoryId,
    bool IsPinned,
    bool IsPopup,
    bool RequiresAcknowledgement,
    DateTime? StartsAt,
    DateTime? ExpiresAt);

public record UpdateAnnouncementDto(
    string Title,
    string Content,
    string ContentFormat,
    AnnouncementAudience TargetAudience,
    int? TargetDepartmentId,
    int? TargetFacultyId,
    int? TargetSectionId,
    int? TargetStudentYear,
    int? CategoryId,
    bool IsPinned,
    bool IsPopup,
    bool RequiresAcknowledgement,
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
    IReadOnlyList<AnnouncementSectionOptionDto> Sections,
    AnnouncementSettings Settings);
