using SIS.Application.Announcement.DTOs;

namespace SIS.Application.Announcement.Services;

public interface IInternalAnnouncementService
{
    Task<List<AnnouncementListDto>> GetActiveForUserAsync(
        string userId,
        IEnumerable<string> roles,
        int? userDepartmentId,
        int? maxResults,
        CancellationToken ct = default);

    Task<AnnouncementDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<List<AnnouncementListDto>> GetAllAsync(CancellationToken ct = default);

    Task<AnnouncementDetailDto> CreateAsync(
        CreateAnnouncementDto dto,
        string createdBy,
        string createdByName,
        CancellationToken ct = default);

    Task<AnnouncementDetailDto?> UpdateAsync(int id, UpdateAnnouncementDto dto, CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
