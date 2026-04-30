using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SIS.Application.Auth.DTOs;
using SIS.Application.Auth.Services;
using SIS.Application.Common.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ISystemConfigProvider _configProvider;
    private readonly PasswordRotationService _passwordRotationService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ISystemConfigProvider configProvider,
        PasswordRotationService passwordRotationService)
    {
        _userManager = userManager;
        _configuration = configuration;
        _configProvider = configProvider;
        _passwordRotationService = passwordRotationService;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto loginDto)
    {
        // 1. Find user by email
        var user = await _userManager.FindByEmailAsync(loginDto.Email);
        if (user == null)
        {
            return new AuthResponseDto
            {
                Success = false,
                Error = "Invalid email or password."
            };
        }

        // 2. Check if account is locked (FR-07: use Identity's built-in lockout)
        if (await _userManager.IsLockedOutAsync(user))
        {
            return new AuthResponseDto
            {
                Success = false,
                Error = "Account is locked. Please try again later."
            };
        }

        // 3. Check password with userManager.CheckPasswordAsync
        var passwordValid = await _userManager.CheckPasswordAsync(user, loginDto.Password);

        if (!passwordValid)
        {
            // 4. If failed: increment AccessFailedCount via userManager.AccessFailedAsync
            await _userManager.AccessFailedAsync(user);

            // If lockout triggered (5 attempts, FR-07), return locked error
            if (await _userManager.IsLockedOutAsync(user))
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Error = "Account is locked due to multiple failed login attempts. Please try again later."
                };
            }

            return new AuthResponseDto
            {
                Success = false,
                Error = "Invalid email or password."
            };
        }

        // 5. If success: reset failed count via userManager.ResetAccessFailedCountAsync
        await _userManager.ResetAccessFailedCountAsync(user);

        // 6. Get user roles
        var roles = await _userManager.GetRolesAsync(user);

        // 7. Generate JWT token with claims
        var token = GenerateJwtToken(user, roles);

        var jwtSettings = _configuration.GetSection("Jwt");
        var expiresInMinutes = double.Parse(jwtSettings["ExpiresInMinutes"]!);
        var passwordRotation = await _passwordRotationService.GetStatusAsync(user);
        var defaultLanguage = await _configProvider.GetStringAsync("Localization.DefaultLanguage", "tr-TR");

        // 8. Return AuthResponseDto
        return new AuthResponseDto
        {
            Success = true,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiresInMinutes),
            Roles = roles.ToArray(),
            RequiresPasswordChange = passwordRotation.RequiresPasswordChange,
            PasswordExpiresAt = passwordRotation.PasswordExpiresAtUtc,
            DefaultLanguage = defaultLanguage,
        };
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(string token)
    {
        // Basic refresh token implementation
        // Validate the existing token and issue a new one
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false, // Lifetime kontrolü aşağıda manuel yapılır
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = key
            }, out var validatedToken);

            // Güvenlik: Süresi çok eski token'larla sonsuz yenilemeyi engelle
            // Sadece son 7 gün içinde expire olmuş token'ları kabul et
            if (validatedToken is JwtSecurityToken jwt)
            {
                var maxRefreshWindow = TimeSpan.FromDays(7);
                if (jwt.ValidTo < DateTime.UtcNow - maxRefreshWindow)
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Error = "Token süresi çok eski, lütfen tekrar giriş yapın."
                    };
                }
            }

            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Error = "Invalid token."
                };
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Error = "User not found."
                };
            }

            var roles = await _userManager.GetRolesAsync(user);
            var newToken = GenerateJwtToken(user, roles);
            var expiresInMinutes = double.Parse(jwtSettings["ExpiresInMinutes"]!);
            var passwordRotation = await _passwordRotationService.GetStatusAsync(user);
            var defaultLanguage = await _configProvider.GetStringAsync("Localization.DefaultLanguage", "tr-TR");

            return new AuthResponseDto
            {
                Success = true,
                Token = newToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expiresInMinutes),
                Roles = roles.ToArray(),
                RequiresPasswordChange = passwordRotation.RequiresPasswordChange,
                PasswordExpiresAt = passwordRotation.PasswordExpiresAtUtc,
                DefaultLanguage = defaultLanguage,
            };
        }
        catch
        {
            return new AuthResponseDto
            {
                Success = false,
                Error = "Invalid token."
            };
        }
    }

    private string GenerateJwtToken(ApplicationUser user, IList<string> roles)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["ExpiresInMinutes"]!)),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
