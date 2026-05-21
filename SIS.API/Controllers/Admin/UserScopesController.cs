using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.Identity.DTOs;
using SIS.Application.Identity.Services;

namespace SIS.API.Controllers.Admin;

[ApiController]
[Route("api/admin/users/{userId:int}/scope")]
public class UserScopesController : ControllerBase
{
    private readonly IUserScopeService _service;

    public UserScopesController(IUserScopeService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission("system.user-scope.view")]
    public async Task<IActionResult> Get(int userId, CancellationToken ct)
    {
        var dto = await _service.GetForUserAsync(userId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPut]
    [RequirePermission("system.user-scope.assign")]
    public async Task<IActionResult> Set(int userId, [FromBody] SetUserScopeDto dto, CancellationToken ct)
    {
        try
        {
            var result = await _service.SetForUserAsync(userId, dto, ct);
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

    [HttpDelete]
    [RequirePermission("system.user-scope.assign")]
    public async Task<IActionResult> Delete(int userId, CancellationToken ct)
    {
        await _service.ClearForUserAsync(userId, ct);
        return NoContent();
    }
}
