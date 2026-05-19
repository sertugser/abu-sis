using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SIS.Application.Scheduling.Services;

namespace SIS.API.Hubs;

/// <summary>
/// Auto-schedule (ve diğer uzun süreli scheduling işlemleri) için real-time progress hub'ı.
///
/// Akış:
///   1. Frontend bağlanır (JWT token query param ile auth).
///   2. AutoScheduleEngine her aşamada IHubContext üzerinden bağlı kullanıcıya
///      "ScheduleProgress" event'i yayınlar.
///   3. Frontend "Cancel" çağrısı yaparak çalışan job'u durdurabilir.
///
/// Kullanıcı izolasyonu: SignalR'ın varsayılan IUserIdProvider, ClaimsPrincipal'in
/// NameIdentifier'ını kullanır. JWT'mizde NameIdentifier = userId olduğu için
/// Hub.Clients.User(userId).SendAsync(...) o kullanıcının tüm açık sekmelerine push eder.
/// </summary>
[Authorize]
public sealed class ScheduleProgressHub : Hub
{
    private readonly IScheduleCancelRegistry _cancelRegistry;

    public ScheduleProgressHub(IScheduleCancelRegistry cancelRegistry)
    {
        _cancelRegistry = cancelRegistry;
    }

    public override Task OnConnectedAsync() => base.OnConnectedAsync();

    public override Task OnDisconnectedAsync(Exception? exception) => base.OnDisconnectedAsync(exception);

    /// <summary>
    /// Frontend "İptal Et" butonuna basınca çağırır. Bağlı kullanıcının aktif
    /// auto-schedule run'ının CancellationTokenSource'unu tetikler. AutoScheduleEngine
    /// her aşamada CancellationToken'ı kontrol ettiği için kısa sürede durur.
    /// </summary>
    public Task<bool> Cancel()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Task.FromResult(false);
        return Task.FromResult(_cancelRegistry.Cancel(userId));
    }
}
