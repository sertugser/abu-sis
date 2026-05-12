namespace SIS.Application.Announcement.Attachments;

/// <summary>
/// Eklerin fiziksel saklanacağı yere ilişkin deployment-spesifik konfigürasyon.
/// <c>appsettings.json</c> üzerinden bağlanır (admin UI'dan değil — bu deployment kararıdır).
/// </summary>
public sealed class AttachmentStorageOptions
{
    public const string SectionName = "AnnouncementAttachments";

    /// <summary>
    /// Eklerin saklanacağı kök dizin. Mutlak yol veya app'in çalıştığı yere göre
    /// göreceli. Default: <c>"attachments/announcements"</c> (ContentRoot altında).
    /// </summary>
    public string RootPath { get; init; } = "attachments/announcements";
}
