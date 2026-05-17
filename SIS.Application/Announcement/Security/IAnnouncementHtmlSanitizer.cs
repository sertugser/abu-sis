namespace SIS.Application.Announcement.Security;

/// <summary>
/// Duyuru HTML içeriklerini güvenli hale getirir. XSS, script enjeksiyonu, iframe gibi
/// risklere karşı whitelist tabanlı temizleme uygular.
/// </summary>
/// <remarks>
/// İzin verilen tag/attr listesi <see cref="AnnouncementSanitizationPolicy"/>'de tanımlıdır
/// ve güvenlik sınırı olduğu için kasıtlı olarak DB'den gelmez — admin UI'dan
/// gevşetilemeyecek bir politikadır.
/// </remarks>
public interface IAnnouncementHtmlSanitizer
{
    /// <summary>
    /// Verilen HTML'yi politikaya göre temizler. Politika dışı tüm öğeler kaldırılır.
    /// </summary>
    /// <param name="html">Temizlenecek ham HTML</param>
    /// <param name="allowImages">Politikaca izinli olsa da bu çağrı için resimlerin gösterilmesi</param>
    /// <param name="allowTables">Politikaca izinli olsa da bu çağrı için tabloların gösterilmesi</param>
    /// <returns>Temizlenmiş HTML</returns>
    string Sanitize(string html, bool allowImages = true, bool allowTables = true);
}
