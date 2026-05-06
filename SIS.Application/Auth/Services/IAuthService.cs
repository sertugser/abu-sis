using SIS.Application.Auth.DTOs;

namespace SIS.Application.Auth.Services;

public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginDto loginDto);
    Task<AuthResponseDto> RefreshTokenAsync(string token);
    Task<AuthResponseDto> ChangePasswordAsync(int userId, ChangePasswordDto dto, CancellationToken ct = default);
}
