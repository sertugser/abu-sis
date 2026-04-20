using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.UserPreferences.DTOs;
using SIS.Application.UserPreferences.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/user/preferences")]
[Authorize]
public class UserPreferencesController : ControllerBase
{
    private readonly IUserPreferenceService _service;

    public UserPreferencesController(IUserPreferenceService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Kullanici kimligi bulunamadi." });

        var pref = await _service.GetAsync(userId, cancellationToken);
        return Ok(pref);
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateUserPreferenceRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Istek govdesi gecerli degil." });

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Kullanici kimligi bulunamadi." });

        try
        {
            var updated = await _service.UpdateAsync(userId, request, cancellationToken);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private bool TryGetUserId(out int userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(claim, out var parsed))
        {
            userId = parsed;
            return true;
        }
        userId = 0;
        return false;
    }
}
