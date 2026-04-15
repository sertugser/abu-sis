using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Services;

public interface IAdminTermService
{
    Task<List<AdminTermDto>> GetAllTermsAsync();
    Task<AdminTermDto> GetTermByIdAsync(int id);
    Task<AdminTermDto> CreateTermAsync(CreateTermDto dto);
    Task<AdminTermDto> UpdateTermAsync(int id, UpdateTermDto dto);
    Task ActivateTermAsync(int id);
    Task TransitionStatusAsync(int id, string targetStatus);
}
