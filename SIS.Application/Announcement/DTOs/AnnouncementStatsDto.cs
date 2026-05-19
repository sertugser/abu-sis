namespace SIS.Application.Announcement.DTOs;

/// <summary>
/// Yönetim sayfasında detay drawer için bir duyurunun istatistik özeti (Madde 9).
/// Target count bu sürümde yer almaz — audience filtreleri ile kesin sayı pahalı; sonraki sprint.
/// </summary>
public sealed record AnnouncementStatsDto(
    int AnnouncementId,
    int ReadCount,
    int AcknowledgedCount,
    int AttachmentCount,
    DateTime? FirstReadAt,
    DateTime? LastReadAt,
    IReadOnlyList<AnnouncementReaderDto> RecentReaders,
    /// <summary>Son N günlük okuma sayıları (yeniden eskiye). Boş gün de 0 ile döner.</summary>
    IReadOnlyList<AnnouncementReadsByDayDto> DailyReads,
    /// <summary>Son 24 saatte yapılan okuma sayısı.</summary>
    int ReadsInLast24h);

public sealed record AnnouncementReaderDto(
    int UserId,
    string DisplayName,
    string? Email,
    DateTime ReadAt,
    DateTime? AcknowledgedAt);

public sealed record AnnouncementReadsByDayDto(
    DateOnly Day,
    int Count);
