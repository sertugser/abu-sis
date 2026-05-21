using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Services;

public interface IAdminTermService
{
    Task<List<AdminTermDto>> GetAllTermsAsync();
    Task<AdminTermDto> GetTermByIdAsync(int id);
    Task<AdminTermDto> CreateTermAsync(CreateTermDto dto);
    Task<AdminTermDto> UpdateTermAsync(int id, UpdateTermDto dto);

    // Kullanima acma/pasife alma ayri; takvimsel Active/Inactive durum tarih araligindan hesaplanir.
    Task ActivateTermAsync(int id);
    Task DeactivateTermAsync(int id);
    Task CancelTermAsync(int id);
    Task UncancelTermAsync(int id);
}
