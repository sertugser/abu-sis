namespace SIS.Application.Announcement.Configuration;

/// <summary>
/// Yönetilebilir duyuru ayarları. Değerler <see cref="IAnnouncementSettingsProvider"/> üzerinden
/// <c>WebModuleSettings</c> tablosundan okunur ve admin UI'dan güncellenebilir.
/// </summary>
public sealed record AnnouncementSettings(
    int MaxTitleLength,
    int MaxContentLength,
    int StudentTopCount,
    bool PinnedFirst,
    bool AllowImagesInContent,
    bool AllowTablesInContent,
    int StudentYearFilterMin,
    int StudentYearFilterMax,
    bool AttachmentsEnabled,
    int AttachmentMaxSizeMB,
    int AttachmentMaxCount);
