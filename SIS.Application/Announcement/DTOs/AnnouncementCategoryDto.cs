namespace SIS.Application.Announcement.DTOs;

public record AnnouncementCategoryDto(
    int Id,
    string Name,
    string ColorHex,
    string IconName,
    int SortOrder,
    bool IsSystem,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreateAnnouncementCategoryDto(
    string Name,
    string ColorHex,
    string IconName,
    int SortOrder);

public record UpdateAnnouncementCategoryDto(
    string Name,
    string ColorHex,
    string IconName,
    int SortOrder);

/// <summary>Listede özet kategori bilgisi (duyuru DTO'larında gömülü kullanım için).</summary>
public record AnnouncementCategorySummaryDto(
    int Id,
    string Name,
    string ColorHex,
    string IconName);
