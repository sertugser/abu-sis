using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.Auth.DTOs;
using SIS.Application.Common.Services;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using System.Security.Claims;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/runtime-settings")]
public class RuntimeSettingsController : ControllerBase
{
    private readonly ISystemConfigProvider _configProvider;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PasswordRotationService _passwordRotationService;

    public RuntimeSettingsController(
        ISystemConfigProvider configProvider,
        UserManager<ApplicationUser> userManager,
        PasswordRotationService passwordRotationService)
    {
        _configProvider = configProvider;
        _userManager = userManager;
        _passwordRotationService = passwordRotationService;
    }

    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicRuntimeSettingsDto>> GetPublicSettings(CancellationToken ct)
    {
        var defaultLanguage = await _configProvider.GetStringAsync("Localization.DefaultLanguage", "tr-TR", ct);
        return Ok(new PublicRuntimeSettingsDto(defaultLanguage));
    }

    [HttpGet("session")]
    [Authorize]
    public async Task<IActionResult> GetSessionSettings(CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Kullanici kimligi bulunamadi." });

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Unauthorized(new { error = "Kullanici bulunamadi." });

        var defaultLanguage = await _configProvider.GetStringAsync("Localization.DefaultLanguage", "tr-TR", ct);
        var passwordRotationStatus = await _passwordRotationService.GetStatusAsync(user, ct);

        return Ok(new
        {
            defaultLanguage,
            requiresPasswordChange = passwordRotationStatus.RequiresPasswordChange,
            passwordExpiresAt = passwordRotationStatus.PasswordExpiresAtUtc,
        });
    }
}
