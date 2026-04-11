using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "SystemAdministrator")]
public class UserManagementController : ControllerBase
{
    private readonly IAdminUserService _service;

    public UserManagementController(IAdminUserService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] AdminUserFilterDto filter)
    {
        var result = await _service.GetUsersAsync(filter);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
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
    }

    [HttpPut("{id:int}/roles")]
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
    }

    [HttpPost("{id:int}/reset-password")]
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
    public async Task<IActionResult> SetStatus(int id, [FromBody] SetUserStatusDto dto)
    {
        try
        {
            await _service.SetUserStatusAsync(id, dto.IsActive);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}

public record SetUserStatusDto(bool IsActive);
