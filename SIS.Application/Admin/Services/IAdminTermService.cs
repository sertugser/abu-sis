using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Services;

public interface IAdminTermService
{
    Task<List<AdminTermDto>> GetAllTermsAsync();
    Task<AdminTermDto> GetTermByIdAsync(int id);
    Task<AdminTermDto> CreateTermAsync(CreateTermDto dto);
    Task<AdminTermDto> UpdateTermAsync(int id, UpdateTermDto dto);

    // Tek manuel aksiyon: Cancel ve tersini alma. Active/Inactive otomatik hesaplanır.
    Task CancelTermAsync(int id);
    Task UncancelTermAsync(int id);
}
