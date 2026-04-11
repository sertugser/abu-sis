using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Services;

public interface IAdminInstructorService
{
    Task<List<AdminInstructorListItemDto>> GetInstructorsAsync(AdminInstructorFilterDto filter);
    Task<AdminInstructorDetailDto?> GetInstructorByIdAsync(int id);
    Task<AdminInstructorDetailDto> CreateInstructorAsync(CreateInstructorDto dto);
}
