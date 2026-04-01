using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public AuthController(IAuthService authService, AppDbContext context)
    {
        _authService = authService;
        _context = context;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
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
                NewValue = JsonSerializer.Serialize(new { reason }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Timestamp = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            });
            await _context.SaveChangesAsync();

            return Unauthorized(result);
        }
        return Ok(result);
    }
}
