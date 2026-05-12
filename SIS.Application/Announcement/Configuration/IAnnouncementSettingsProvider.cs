namespace SIS.Application.Announcement.Configuration;

/// <summary>
/// Duyuru modülü için yönetilebilir ayarları sağlar.
/// Tüm değerler <c>WebModuleSettings</c> üzerinden gelir, fallback değerleri sabittir
/// (DB seed eksikse de sistem ayakta kalır).
/// </summary>
public interface IAnnouncementSettingsProvider
{
    Task<AnnouncementSettings> GetAsync(CancellationToken ct = default);
}
