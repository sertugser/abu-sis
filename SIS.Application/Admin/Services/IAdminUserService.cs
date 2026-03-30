using SIS.Application.Admin.DTOs;

namespace SIS.Application.Admin.Services;

public interface IAdminUserService
{
    Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(AdminUserFilterDto filter);
    Task<AdminUserDetailDto> GetUserByIdAsync(int id);
    Task UpdateUserRolesAsync(int id, UpdateUserRolesDto dto);
    Task ResetPasswordAsync(int id, AdminResetPasswordDto dto);
    Task SetUserStatusAsync(int id, bool isActive);
}
