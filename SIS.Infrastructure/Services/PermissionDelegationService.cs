using Microsoft.EntityFrameworkCore;
using SIS.Application.Identity.DTOs;
using SIS.Application.Identity.Services;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Persistence.Extensions;

namespace SIS.Infrastructure.Services;

public class PermissionDelegationService : IPermissionDelegationService
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissionService;

    public PermissionDelegationService(AppDbContext db, IPermissionService permissionService)
    {
        _db = db;
        _permissionService = permissionService;
    }

    public async Task<IReadOnlyList<PermissionDelegationDto>> ListAsync(
        bool includeInactive,
        int? delegatorUserId,
        int? delegateeUserId,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var query = _db.PermissionDelegations
            .WithoutScope()
            .AsNoTracking()
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(d => d.RevokedAtUtc == null && d.EndsAtUtc >= now);
        }

        if (delegatorUserId.HasValue)
        {
            query = query.Where(d => d.DelegatorUserId == delegatorUserId.Value);
        }

        if (delegateeUserId.HasValue)
        {
            query = query.Where(d => d.DelegateeUserId == delegateeUserId.Value);
        }

        var rows = await (
            from d in query
            join delegator in _db.Set<ApplicationUser>().AsNoTracking().IgnoreQueryFilters()
                on d.DelegatorUserId equals delegator.Id
            join delegatee in _db.Set<ApplicationUser>().AsNoTracking().IgnoreQueryFilters()
                on d.DelegateeUserId equals delegatee.Id
            orderby d.CreatedAtUtc descending
            select new
            {
                d.Id,
                d.DelegatorUserId,
                DelegatorUserName = delegator.UserName ?? string.Empty,
                DelegatorFullName = (delegator.FirstName + " " + delegator.LastName).Trim(),
                d.DelegateeUserId,
                DelegateeUserName = delegatee.UserName ?? string.Empty,
                DelegateeFullName = (delegatee.FirstName + " " + delegatee.LastName).Trim(),
                d.StartsAtUtc,
                d.EndsAtUtc,
                d.Reason,
                d.RevokedAtUtc,
                d.RevokedByUserId,
                d.RevokeReason,
                d.CreatedAtUtc,
                d.CreatedByUserId
            }
        ).ToListAsync(ct);

        return rows
            .Select(r => new PermissionDelegationDto(
                r.Id,
                r.DelegatorUserId,
                r.DelegatorUserName,
                r.DelegatorFullName,
                r.DelegateeUserId,
                r.DelegateeUserName,
                r.DelegateeFullName,
                r.StartsAtUtc,
                r.EndsAtUtc,
                r.Reason,
                r.RevokedAtUtc,
                r.RevokedByUserId,
                r.RevokeReason,
                r.CreatedAtUtc,
                r.CreatedByUserId,
                ComputeStatus(r.RevokedAtUtc, r.StartsAtUtc, r.EndsAtUtc, now)))
            .ToList();
    }

    public async Task<PermissionDelegationDto> GetAsync(int id, CancellationToken ct = default)
    {
        var list = await ListAsync(includeInactive: true, delegatorUserId: null, delegateeUserId: null, ct);
        var match = list.FirstOrDefault(d => d.Id == id);
        if (match is null)
        {
            throw new KeyNotFoundException($"Vekâlet kaydı bulunamadı: {id}");
        }
        return match;
    }

    public async Task<PermissionDelegationDto> CreateAsync(
        CreateDelegationDto dto,
        int createdByUserId,
        CancellationToken ct = default)
    {
        if (dto.DelegatorUserId == dto.DelegateeUserId)
        {
            throw new InvalidOperationException("Bir kullanıcı kendisine vekâlet veremez.");
        }

        if (dto.EndsAtUtc <= dto.StartsAtUtc)
        {
            throw new InvalidOperationException("Bitiş zamanı başlangıçtan sonra olmalı.");
        }

        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            throw new InvalidOperationException("Vekâlet sebebi (Reason) zorunludur.");
        }

        var delegatorExists = await _db.Set<ApplicationUser>()
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Id == dto.DelegatorUserId, ct);
        if (!delegatorExists)
        {
            throw new KeyNotFoundException($"Yetki devreden kullanıcı bulunamadı: {dto.DelegatorUserId}");
        }

        var delegateeExists = await _db.Set<ApplicationUser>()
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Id == dto.DelegateeUserId, ct);
        if (!delegateeExists)
        {
            throw new KeyNotFoundException($"Vekil kullanıcı bulunamadı: {dto.DelegateeUserId}");
        }

        // Çakışan aktif vekâlet kontrolü (aynı delegator → aynı delegatee, üst üste pencere)
        var overlap = await _db.PermissionDelegations
            .WithoutScope()
            .AsNoTracking()
            .Where(d => d.DelegatorUserId == dto.DelegatorUserId
                        && d.DelegateeUserId == dto.DelegateeUserId
                        && d.RevokedAtUtc == null
                        && d.StartsAtUtc <= dto.EndsAtUtc
                        && dto.StartsAtUtc <= d.EndsAtUtc)
            .AnyAsync(ct);
        if (overlap)
        {
            throw new InvalidOperationException(
                "Bu kullanıcılar arasında, çakışan tarih aralığında aktif bir vekâlet zaten var.");
        }

        var entity = new PermissionDelegation
        {
            DelegatorUserId = dto.DelegatorUserId,
            DelegateeUserId = dto.DelegateeUserId,
            StartsAtUtc = dto.StartsAtUtc,
            EndsAtUtc = dto.EndsAtUtc,
            Reason = dto.Reason.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
        };

        _db.PermissionDelegations.Add(entity);
        await _db.SaveChangesAsync(ct);

        // Vekilin permission cache'ini hemen tazele (5dk gecikme yerine)
        _permissionService.InvalidateUserCache(dto.DelegateeUserId);

        return await GetAsync(entity.Id, ct);
    }

    public async Task<PermissionDelegationDto> RevokeAsync(
        int id,
        RevokeDelegationDto dto,
        int revokedByUserId,
        CancellationToken ct = default)
    {
        var entity = await _db.PermissionDelegations
            .WithoutScope()
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null)
        {
            throw new KeyNotFoundException($"Vekâlet kaydı bulunamadı: {id}");
        }

        if (entity.RevokedAtUtc is not null)
        {
            throw new InvalidOperationException("Bu vekâlet zaten iptal edilmiş.");
        }

        entity.RevokedAtUtc = DateTime.UtcNow;
        entity.RevokedByUserId = revokedByUserId;
        entity.RevokeReason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();

        await _db.SaveChangesAsync(ct);

        _permissionService.InvalidateUserCache(entity.DelegateeUserId);

        return await GetAsync(entity.Id, ct);
    }

    public async Task<IReadOnlyList<string>> GetActiveDelegatedRoleNamesAsync(
        int userId,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var delegatorIds = await _db.PermissionDelegations
            .WithoutScope()
            .AsNoTracking()
            .Where(d => d.DelegateeUserId == userId
                        && d.RevokedAtUtc == null
                        && d.StartsAtUtc <= now
                        && now <= d.EndsAtUtc)
            .Select(d => d.DelegatorUserId)
            .Distinct()
            .ToListAsync(ct);

        if (delegatorIds.Count == 0)
        {
            return Array.Empty<string>();
        }

        var roleNames = await (
            from ur in _db.Set<Microsoft.AspNetCore.Identity.IdentityUserRole<int>>().AsNoTracking()
            join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where delegatorIds.Contains(ur.UserId) && r.Name != "SystemAdministrator"
            select r.Name!
        ).Distinct().ToListAsync(ct);

        return roleNames;
    }

    private static string ComputeStatus(DateTime? revokedAt, DateTime startsAt, DateTime endsAt, DateTime now)
    {
        if (revokedAt is not null) return "Revoked";
        if (now < startsAt) return "Pending";
        if (now > endsAt) return "Expired";
        return "Active";
    }
}
