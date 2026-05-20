using SIS.Application.Identity.DTOs;

namespace SIS.Application.Identity.Services;

public interface IUserScopeService
{
    Task<UserScopeDto?> GetForUserAsync(int userId, CancellationToken ct = default);
    Task<UserScopeDto> SetForUserAsync(int userId, SetUserScopeDto dto, CancellationToken ct = default);
    Task ClearForUserAsync(int userId, CancellationToken ct = default);
}
