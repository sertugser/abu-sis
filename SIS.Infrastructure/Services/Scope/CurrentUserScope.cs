using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Identity.Services;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scope;

public class CurrentUserScope : ICurrentUserScope
{
    private const string SystemAdminRole = "SystemAdministrator";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    private bool _loaded;
    private bool _isActive;
    private int[] _facultyIds = Array.Empty<int>();

    public CurrentUserScope(
        IHttpContextAccessor httpContextAccessor,
        DbContextOptions<AppDbContext> dbOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbOptions = dbOptions;
    }

    public bool IsActive => _loaded && _isActive;

    public int? FacultyId => _loaded && _facultyIds.Length > 0 ? _facultyIds[0] : null;

    public IReadOnlyCollection<int> FacultyIds => _loaded ? _facultyIds : Array.Empty<int>();

    public async Task<bool> IsActiveAsync()
    {
        await EnsureLoadedAsync();
        return _isActive;
    }

    public async Task<int?> GetFacultyIdAsync()
    {
        await EnsureLoadedAsync();
        return FacultyId;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded)
        {
            return;
        }

        var user = _httpContextAccessor.HttpContext?.User;
        var userIdRaw = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (user?.Identity?.IsAuthenticated != true || !int.TryParse(userIdRaw, out var userId))
        {
            MarkInactive();
            return;
        }

        var roles = user.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToList();

        if (roles.Contains(SystemAdminRole, StringComparer.Ordinal))
        {
            MarkInactive();
            return;
        }

        await using var db = new AppDbContext(_dbOptions);

        var facultyIds = new HashSet<int>();
        var scoped = false;

        // 1) Kendi rolleri scope-bound ise kendi fakülte scope'unu ekle.
        //    Scope-bound rol var ama UserScope satırı yoksa: aktif kalır ama set boş (hiçbir şey görmez).
        if (RoleScopeRegistry.AnyScopeBound(roles))
        {
            scoped = true;
            var ownFacultyId = await db.UserScopes
                .AsNoTracking()
                .Where(scope => scope.UserId == userId && scope.Kind == ScopeKind.Faculty)
                .Select(scope => (int?)scope.FacultyId)
                .FirstOrDefaultAsync();

            if (ownFacultyId is { } own)
            {
                facultyIds.Add(own);
            }
        }

        // 2) Aktif vekâletle scope-bound bir kullanıcının yetkilerini taşıyor olabilir →
        //    o delegator'ların fakülte scope'larını da devral (birleşim — çakışma artık "ikisini de gör").
        //    Yalnızca AKTİF (kilitli olmayan) delegator'lar sayılır (#10).
        var now = DateTime.UtcNow;
        var nowOffset = DateTimeOffset.UtcNow;
        var delegatorIds = await db.PermissionDelegations
            .AsNoTracking()
            .Where(d => d.DelegateeUserId == userId
                        && d.RevokedAtUtc == null
                        && d.StartsAtUtc <= now
                        && now <= d.EndsAtUtc)
            .Select(d => d.DelegatorUserId)
            .Distinct()
            .ToListAsync();

        if (delegatorIds.Count > 0)
        {
            var activeDelegatorIds = await db.Users
                .AsNoTracking()
                .Where(u => delegatorIds.Contains(u.Id)
                            && (!u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= nowOffset))
                .Select(u => u.Id)
                .ToListAsync();

            if (activeDelegatorIds.Count > 0)
            {
                var inheritedFacultyIds = await db.UserScopes
                    .AsNoTracking()
                    .Where(scope => activeDelegatorIds.Contains(scope.UserId)
                                    && scope.Kind == ScopeKind.Faculty
                                    && scope.FacultyId != null)
                    .Select(scope => scope.FacultyId!.Value)
                    .Distinct()
                    .ToListAsync();

                if (inheritedFacultyIds.Count > 0)
                {
                    scoped = true;
                    foreach (var fid in inheritedFacultyIds)
                    {
                        facultyIds.Add(fid);
                    }
                }
            }
        }

        if (!scoped)
        {
            // Ne kendi scope-bound rolü ne de scope devreden aktif vekâleti var → scope kapalı (her şeyi görür).
            MarkInactive();
            return;
        }

        _facultyIds = facultyIds.ToArray();
        _isActive = true;
        _loaded = true;
    }

    private void MarkInactive()
    {
        _isActive = false;
        _facultyIds = Array.Empty<int>();
        _loaded = true;
    }
}
