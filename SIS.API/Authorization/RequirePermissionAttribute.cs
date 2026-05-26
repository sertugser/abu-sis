using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SIS.Application.Identity.Services;

namespace SIS.API.Authorization;

/// <summary>
/// Permission-based authorization attribute. Karar 6 (v2): JWT'de permission claim YOK,
/// her çağrıda IPermissionService (cache'li) sorgulanır.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _permissionKeys;

    private const string SystemAdminRole = "SystemAdministrator";
    private const string LookupPrefix = "lookup.";

    /// <summary>
    /// Bir veya daha fazla permission anahtarı. Birden fazla verildiğinde "any-of"
    /// (herhangi biri yeterli) semantiği uygulanır — paylaşılan uçlar için. Aynı method'a
    /// iki ayrı attribute koymak ise "all-of" (AND) demektir; OR isteniyorsa tek attribute
    /// içinde birden çok anahtar kullanılmalıdır.
    /// </summary>
    public RequirePermissionAttribute(params string[] permissionKeys)
    {
        _permissionKeys = permissionKeys;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (!(user.Identity?.IsAuthenticated ?? false))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // 1) SystemAdmin shortcut — Karar 4: locked, hep yetkili (cache miss yok, en hızlı yol)
        if (user.IsInRole(SystemAdminRole))
        {
            return;
        }

        // 2) lookup.* shortcut — Karar 7: hardcoded, her authenticated user'a açık.
        // DB'de seed edilmediği için PermissionService'e gitmek anlamsız.
        // Any-of: anahtarlardan biri lookup.* ise erişim açıktır.
        if (_permissionKeys.Any(key => key.StartsWith(LookupPrefix, StringComparison.Ordinal)))
        {
            return;
        }

        // 3) PermissionService ile DB/cache kontrol
        var permissionService = context.HttpContext.RequestServices
            .GetRequiredService<IPermissionService>();

        var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Any-of: anahtarlardan herhangi birine sahipse yetkilidir.
        foreach (var key in _permissionKeys)
        {
            if (await permissionService.HasPermissionAsync(userId, key))
                return;
        }

        context.Result = new ForbidResult();
    }
}
