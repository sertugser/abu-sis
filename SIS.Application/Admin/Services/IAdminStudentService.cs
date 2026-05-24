using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Services;

public interface IAdminStudentService
{
    Task<PagedResult<AdminStudentListItemDto>> GetStudentsAsync(AdminStudentFilterDto filter);
    Task<AdminStudentDetailDto?> GetStudentByIdAsync(int id);
    Task<AdminStudentDetailDto> CreateStudentAsync(CreateStudentDto dto);
    Task AssignAdvisorAsync(int studentId, int advisorId);
    Task UpdateAcademicAsync(int studentId, UpdateStudentAcademicDto dto);
}
