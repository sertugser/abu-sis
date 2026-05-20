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
    private int? _facultyId;

    public CurrentUserScope(
        IHttpContextAccessor httpContextAccessor,
        DbContextOptions<AppDbContext> dbOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbOptions = dbOptions;
    }

    public bool IsActive => _loaded && _isActive;

    public int? FacultyId => _loaded ? _facultyId : null;

    public async Task<bool> IsActiveAsync()
    {
        await EnsureLoadedAsync();
        return _isActive;
    }

    public async Task<int?> GetFacultyIdAsync()
    {
        await EnsureLoadedAsync();
        return _facultyId;
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

        // 1) Kendi rolleri scope-bound ise kendi scope'unu yükle
        if (RoleScopeRegistry.AnyScopeBound(roles))
        {
            _facultyId = await db.UserScopes
                .AsNoTracking()
                .Where(scope => scope.UserId == userId && scope.Kind == ScopeKind.Faculty)
                .Select(scope => scope.FacultyId)
                .FirstOrDefaultAsync();

            _isActive = true;
            _loaded = true;
            return;
        }

        // 2) Kendi scope-bound rolü yok ama aktif vekâlet altında scope-bound bir kullanıcının
        //    yetkilerini taşıyor olabilir → o delegator'ın scope'unu devral.
        var now = DateTime.UtcNow;
        var delegatorIds = await db.PermissionDelegations
            .AsNoTracking()
            .Where(d => d.DelegateeUserId == userId
                        && d.RevokedAtUtc == null
                        && d.StartsAtUtc <= now
                        && now <= d.EndsAtUtc)
            .Select(d => d.DelegatorUserId)
            .Distinct()
            .ToListAsync();

        if (delegatorIds.Count == 0)
        {
            MarkInactive();
            return;
        }

        var inheritedFacultyIds = await db.UserScopes
            .AsNoTracking()
            .Where(scope => delegatorIds.Contains(scope.UserId) && scope.Kind == ScopeKind.Faculty)
            .Select(scope => scope.FacultyId)
            .Distinct()
            .ToListAsync();

        if (inheritedFacultyIds.Count == 0)
        {
            // Delegator'lar scope-bound değil → vekil de scope-bound değil
            MarkInactive();
            return;
        }

        if (inheritedFacultyIds.Count == 1)
        {
            _facultyId = inheritedFacultyIds[0];
            _isActive = true;
            _loaded = true;
            return;
        }

        // Çakışan farklı fakülte scope'ları → fail-safe, hiçbir fakülte verisi görünmez
        _facultyId = -1;
        _isActive = true;
        _loaded = true;
    }

    private void MarkInactive()
    {
        _isActive = false;
        _facultyId = null;
        _loaded = true;
    }
}
