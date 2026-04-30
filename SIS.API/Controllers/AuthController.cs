using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Auth.DTOs;
using SIS.Application.Auth.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PasswordRotationService _passwordRotationService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public AuthController(
        IAuthService authService,
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        PasswordRotationService passwordRotationService)
    {
        _authService = authService;
        _context = context;
        _userManager = userManager;
        _passwordRotationService = passwordRotationService;
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
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "Istek govdesi gecerli degil." });

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Kullanici kimligi bulunamadi." });

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Unauthorized(new { error = "Kullanici bulunamadi." });

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                error = string.Join(", ", result.Errors.Select(error => error.Description))
            });
        }

        _passwordRotationService.MarkPasswordChanged(user);
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return BadRequest(new
            {
                error = string.Join(", ", updateResult.Errors.Select(error => error.Description))
            });
        }

        return NoContent();
    }
}
