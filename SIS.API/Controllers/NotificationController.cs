using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.Notifications.DTOs;
using SIS.Application.Notifications.Services;

namespace SIS.API.Controllers;

/// <summary>
/// Oturum açan kullanıcının kişisel bildirimleri. Menü izni gerektirmez; "lookup.*"
/// shortcut'ı her authenticated kullanıcıya açar (Karar 7) — kullanıcı yalnızca kendi
/// bildirimlerini görür (servis tarafında userId scope).
/// </summary>
[ApiController]
[Route("api/me/notifications")]
[Authorize]
[RequirePermission("lookup.notifications")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _svc;

    public NotificationController(INotificationService svc) => _svc = svc;

    private int? CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? type,
        [FromQuery] bool onlyUnread = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (CurrentUserId() is not int userId) return Unauthorized();
        return Ok(await _svc.GetForUserAsync(userId, new NotificationFilter(type, onlyUnread, page, pageSize), ct));
    }

    [HttpGet("recent")]
    public async Task<IActionResult> Recent([FromQuery] int max = 10, CancellationToken ct = default)
    {
        if (CurrentUserId() is not int userId) return Unauthorized();
        return Ok(await _svc.GetRecentAsync(userId, max, ct));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct = default)
    {
        if (CurrentUserId() is not int userId) return Unauthorized();
        return Ok(new { count = await _svc.GetUnreadCountAsync(userId, ct) });
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct = default)
    {
        if (CurrentUserId() is not int userId) return Unauthorized();
        await _svc.MarkReadAsync(userId, id, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct = default)
    {
        if (CurrentUserId() is not int userId) return Unauthorized();
        await _svc.MarkAllReadAsync(userId, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        if (CurrentUserId() is not int userId) return Unauthorized();
        await _svc.DeleteAsync(userId, id, ct);
        return NoContent();
    }
}
