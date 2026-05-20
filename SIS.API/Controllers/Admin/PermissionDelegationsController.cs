using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.Identity.DTOs;
using SIS.Application.Identity.Services;

namespace SIS.API.Controllers.Admin;

[ApiController]
[Route("api/admin/delegations")]
public class PermissionDelegationsController : ControllerBase
{
    private readonly IPermissionDelegationService _service;

    public PermissionDelegationsController(IPermissionDelegationService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission("menu.kullanici-vekalet-islemleri.view")]
    public async Task<IActionResult> List(
        [FromQuery] bool includeInactive = false,
        [FromQuery] int? delegatorUserId = null,
        [FromQuery] int? delegateeUserId = null,
        CancellationToken ct = default)
    {
        var rows = await _service.ListAsync(includeInactive, delegatorUserId, delegateeUserId, ct);
        return Ok(rows);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("menu.kullanici-vekalet-islemleri.view")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.GetAsync(id, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    [RequirePermission("menu.kullanici-vekalet-islemleri.manage")]
    public async Task<IActionResult> Create([FromBody] CreateDelegationDto dto, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _service.CreateAsync(dto, currentUserId, ct);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
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

    [HttpPost("{id:int}/revoke")]
    [RequirePermission("menu.kullanici-vekalet-islemleri.manage")]
    public async Task<IActionResult> Revoke(int id, [FromBody] RevokeDelegationDto dto, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _service.RevokeAsync(id, dto, currentUserId, ct);
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

    private bool TryGetCurrentUserId(out int userId)
    {
        userId = 0;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out userId);
    }
}
