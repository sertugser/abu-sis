using SIS.Application.Announcement.DTOs;

namespace SIS.Application.Announcement.Services;

public interface IInternalAnnouncementService
{
    /// <summary>
    /// Kullanıcının görebileceği aktif duyuruları döner (hiyerarşik hedefleme dahil).
    /// </summary>
    Task<List<AnnouncementListDto>> GetActiveForUserAsync(
        string userId,
        IEnumerable<string> roles,
        int? userDepartmentId,
        int? userFacultyId,
        IEnumerable<int> userEnrolledSectionIds,
        string? advisorUserId,
        int? maxResults,
        CancellationToken ct = default);

    Task<AnnouncementDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Admin: tüm duyurular (SysAdmin + AdministrativeStaff için).</summary>
    Task<List<AnnouncementListDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Kendi oluşturduğu duyurular (Instructor / DeptManager / Dean için).</summary>
    Task<List<AnnouncementListDto>> GetMyCreatedAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Hiyerarşik kural doğrulamasıyla duyuru oluşturur.
    /// </summary>
    Task<AnnouncementDetailDto> CreateAsync(
        CreateAnnouncementDto dto,
        string createdBy,
        string createdByName,
        IEnumerable<string> senderRoles,
        int? senderDepartmentId,
        int? senderFacultyId,
        CancellationToken ct = default);

    Task<AnnouncementDetailDto?> UpdateAsync(
        int id,
        UpdateAnnouncementDto dto,
        string requestingUserId,
        bool isAdminOverride,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(
        int id,
        string requestingUserId,
        bool isAdminOverride,
        CancellationToken ct = default);

    /// <summary>Duyuruyu veritabanından kalıcı olarak siler.</summary>
    Task<bool> PermanentDeleteAsync(
        int id,
        string requestingUserId,
        bool isAdminOverride,
        CancellationToken ct = default);

    /// <summary>Geri çekilmiş duyuruyu tekrar yayına alır (IsActive = true).</summary>
    Task<bool> ReactivateAsync(
        int id,
        string requestingUserId,
        bool isAdminOverride,
        CancellationToken ct = default);

    /// <summary>
    /// Form için: gönderenin rolüne göre seçilebilecek hedef kitle + bölüm/fakülte/section bilgisi.
    /// </summary>
    Task<AnnouncementCreateContextDto> GetCreateContextAsync(
        string userId,
        IEnumerable<string> roles,
        CancellationToken ct = default);
}
