using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SIS.API.Authorization;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize]
public class UserManagementController : ControllerBase
{
    private readonly IAdminUserService _service;

    public UserManagementController(IAdminUserService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission("menu.kullanici-tanimlama.view")]
    public async Task<IActionResult> GetUsers([FromQuery] AdminUserFilterDto filter)
    {
        var result = await _service.GetUsersAsync(filter);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("menu.kullanici-tanimlama.view")]
    public async Task<IActionResult> GetUser(int id)
    {
        try
        {
            var result = await _service.GetUserByIdAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}/roles")]
    [RequirePermission("menu.kullanici-tanimlama.update-roles")]
    public async Task<IActionResult> UpdateRoles(int id, [FromBody] UpdateUserRolesDto dto)
    {
        try
        {
            await _service.UpdateUserRolesAsync(id, dto);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/reset-password")]
    [RequirePermission("menu.kullanici-tanimlama.reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] AdminResetPasswordDto dto)
    {
        try
        {
            await _service.ResetPasswordAsync(id, dto);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}/status")]
    [RequirePermission("menu.kullanici-tanimlama.set-status")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] SetUserStatusDto dto)
    {
        try
        {
            if (!dto.IsActive && IsCurrentUser(id))
                return BadRequest(new { message = "Kendi hesabınızı pasifleştiremezsiniz." });

            await _service.SetUserStatusAsync(id, dto.IsActive);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private bool IsCurrentUser(int id)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim, out var userId) && userId == id;
    }
}

public record SetUserStatusDto(bool IsActive);
