using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SIS.API.Security;
using SIS.Application.Auth.DTOs;
using SIS.Application.Auth.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly AppDbContext _context;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public AuthController(IAuthService authService, AppDbContext context)
    {
        _authService = authService;
        _context = context;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthLogin")]
    [PublicEndpointJustification("Login must be reachable before authentication; IP rate limiting and audit logging are enforced.")]
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
}
