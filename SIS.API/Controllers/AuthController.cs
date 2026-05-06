using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Auth.DTOs;
using SIS.Application.Auth.Services;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly AppDbContext _context;
    private readonly ISystemConfigProvider _systemConfig;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public AuthController(IAuthService authService, AppDbContext context, ISystemConfigProvider systemConfig)
    {
        _authService = authService;
        _context = context;
        _systemConfig = systemConfig;
    }

    [HttpGet("login-settings")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLoginSettings([FromQuery] string? email, CancellationToken ct)
    {
        var configuredLanguage = await _systemConfig.GetStringAsync("Localization.DefaultLanguage", "tr-TR", ct);
        var effectiveLanguage = configuredLanguage;

        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();
            var isInstructor = await (
                from user in _context.Users.AsNoTracking()
                join instructor in _context.Instructors.AsNoTracking() on user.Id equals instructor.UserId
                where user.NormalizedEmail == normalizedEmail
                select instructor.Id)
                .AnyAsync(ct);

            if (isInstructor)
                effectiveLanguage = await _systemConfig.GetStringAsync("Instructor.DefaultLoginLanguage", configuredLanguage, ct);
        }

        var normalizedLanguage = string.Equals(configuredLanguage, "en-US", StringComparison.OrdinalIgnoreCase)
            || string.Equals(effectiveLanguage, "en-US", StringComparison.OrdinalIgnoreCase)
            ? "en-US"
            : "tr-TR";

        return Ok(new
        {
            defaultLanguage = normalizedLanguage,
        });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(loginDto);
        if (!result.Success)
        {
            var reason = result.Error switch
            {
                var e when e != null && e.Contains("locked", StringComparison.OrdinalIgnoreCase) => "AccountLocked",
                var e when e != null && e.Contains("Invalid", StringComparison.OrdinalIgnoreCase) => "InvalidPassword",
                _ => "Unknown"
            };

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = null,
                Action = "LoginFailed",
                EntityType = "Authentication",
                EntityId = loginDto.Email,
                OldValue = null,
                NewValue = JsonSerializer.Serialize(new { reason }, JsonOptions),
                Timestamp = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            });
            await _context.SaveChangesAsync(ct);

            return Unauthorized(result);
        }

        var normalizedEmail = loginDto.Email.Trim().ToUpperInvariant();
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.NormalizedEmail == normalizedEmail)
            .Select(u => new { u.Id, u.Email })
            .FirstOrDefaultAsync(ct);

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = user?.Id,
            Action = "LoginSuccess",
            EntityType = "Authentication",
            EntityId = user?.Email ?? loginDto.Email,
            NewValue = JsonSerializer.Serialize(new { roles = result.Roles }, JsonOptions),
            Timestamp = DateTime.UtcNow,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
        });
        await _context.SaveChangesAsync(ct);

        return Ok(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        var result = await _authService.ChangePasswordAsync(userId, dto, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
