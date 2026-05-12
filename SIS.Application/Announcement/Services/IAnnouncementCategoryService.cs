using SIS.Application.Announcement.DTOs;

namespace SIS.Application.Announcement.Services;

public interface IAnnouncementCategoryService
{
    Task<List<AnnouncementCategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<AnnouncementCategoryDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<AnnouncementCategoryDto> CreateAsync(CreateAnnouncementCategoryDto dto, CancellationToken ct = default);
    Task<AnnouncementCategoryDto?> UpdateAsync(int id, UpdateAnnouncementCategoryDto dto, CancellationToken ct = default);

    /// <summary>
    /// Kategoriyi siler. Sistem kategorileri silinemez (<see cref="InvalidOperationException"/>).
    /// Silinen kategoriye bağlı duyuruların CategoryId değeri null'a düşer (cascade SET NULL).
    /// </summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
