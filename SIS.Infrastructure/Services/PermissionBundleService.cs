using Microsoft.EntityFrameworkCore;
using SIS.Application.Identity.DTOs;
using SIS.Application.Identity.Services;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class PermissionBundleService : IPermissionBundleService
{
    private readonly AppDbContext _context;
    private readonly IPermissionService _permissionService;

    public PermissionBundleService(AppDbContext context, IPermissionService permissionService)
    {
        _context = context;
        _permissionService = permissionService;
    }

    public async Task<List<PermissionBundleDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.PermissionBundles
            .AsNoTracking()
            .OrderByDescending(b => b.IsSystemDefault)
            .ThenBy(b => b.Category)
            .ThenBy(b => b.Name)
            .Select(b => new PermissionBundleDto
            {
                Id = b.Id,
                Name = b.Name,
                Description = b.Description,
                Category = b.Category,
                IsSystemDefault = b.IsSystemDefault,
                PermissionCount = b.Items.Count,
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt,
            })
            .ToListAsync(ct);
    }

    public async Task<PermissionBundleDetailDto?> GetByIdAsync(int bundleId, CancellationToken ct = default)
    {
        var bundle = await _context.PermissionBundles
            .AsNoTracking()
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == bundleId, ct);
        if (bundle is null) return null;
        return ToDetailDto(bundle);
    }

    public async Task<PermissionBundleDetailDto> CreateAsync(CreatePermissionBundleDto dto, CancellationToken ct = default)
    {
        ValidateName(dto.Name);
        var name = dto.Name.Trim();

        var nameTaken = await _context.PermissionBundles.AnyAsync(b => b.Name == name, ct);
        if (nameTaken)
        {
            throw new InvalidOperationException($"Bu isimde bir paket zaten var: '{name}'.");
        }

        var permissionIds = await ValidatePermissionIdsAsync(dto.PermissionIds, ct);

        var now = DateTime.UtcNow;
        var bundle = new PermissionBundle
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            Category = string.IsNullOrWhiteSpace(dto.Category) ? null : dto.Category.Trim(),
            IsSystemDefault = false,
            CreatedAt = now,
            UpdatedAt = now,
            Items = permissionIds.Select(pid => new PermissionBundleItem { PermissionId = pid }).ToList(),
        };

        _context.PermissionBundles.Add(bundle);
        await _context.SaveChangesAsync(ct);

        return ToDetailDto(bundle);
    }

    public async Task<PermissionBundleDetailDto> UpdateAsync(int bundleId, UpdatePermissionBundleDto dto, CancellationToken ct = default)
    {
        ValidateName(dto.Name);
        var name = dto.Name.Trim();

        var bundle = await _context.PermissionBundles
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == bundleId, ct);
        if (bundle is null)
        {
            throw new KeyNotFoundException($"Paket bulunamadı: {bundleId}");
        }

        var nameTaken = await _context.PermissionBundles
            .AnyAsync(b => b.Id != bundleId && b.Name == name, ct);
        if (nameTaken)
        {
            throw new InvalidOperationException($"Bu isimde bir paket zaten var: '{name}'.");
        }

        var permissionIds = await ValidatePermissionIdsAsync(dto.PermissionIds, ct);

        bundle.Name = name;
        bundle.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        bundle.Category = string.IsNullOrWhiteSpace(dto.Category) ? null : dto.Category.Trim();
        bundle.UpdatedAt = DateTime.UtcNow;

        _context.PermissionBundleItems.RemoveRange(bundle.Items);
        bundle.Items = permissionIds.Select(pid => new PermissionBundleItem
        {
            BundleId = bundle.Id,
            PermissionId = pid,
        }).ToList();

        await _context.SaveChangesAsync(ct);

        // Karar 1 — Snapshot semantics: paket değişikliği bağlı role'lere otomatik
        // yansımaz. Ancak bu pakete bağlı RolePermission satırları cache'lerini
        // taşıyor olabilir; şu an bunları invalidate etmiyoruz çünkü içerik
        // değişmedi (rol-permission satırları aynen kaldı). Karar 1 doğrulanmış olur.

        return ToDetailDto(bundle);
    }

    public async Task DeleteAsync(int bundleId, CancellationToken ct = default)
    {
        var bundle = await _context.PermissionBundles
            .FirstOrDefaultAsync(b => b.Id == bundleId, ct);
        if (bundle is null)
        {
            throw new KeyNotFoundException($"Paket bulunamadı: {bundleId}");
        }
        if (bundle.IsSystemDefault)
        {
            throw new InvalidOperationException("Sistem paketi silinemez. İçeriğini düzenleyebilirsiniz.");
        }

        // Bu pakete bağlı RolePermission satırlarını taşıyan role'lerin cache'i invalidate
        var affectedRoleIds = await _context.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.SourceBundleId == bundleId)
            .Select(rp => rp.RoleId)
            .Distinct()
            .ToListAsync(ct);

        _context.PermissionBundles.Remove(bundle);
        // FK cascade siler hem BundleItems hem RolePermission satırlarını
        await _context.SaveChangesAsync(ct);

        foreach (var roleId in affectedRoleIds)
        {
            await _permissionService.InvalidateRoleCacheAsync(roleId, ct);
        }
    }

    public async Task ApplyBundleToRoleAsync(int roleId, int bundleId, int actorUserId, CancellationToken ct = default)
    {
        var bundle = await _context.PermissionBundles
            .AsNoTracking()
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == bundleId, ct);
        if (bundle is null)
        {
            throw new KeyNotFoundException($"Paket bulunamadı: {bundleId}");
        }

        var roleExists = await _context.Roles.AnyAsync(r => r.Id == roleId, ct);
        if (!roleExists)
        {
            throw new KeyNotFoundException($"Rol bulunamadı: {roleId}");
        }

        // Bu bundle'dan gelen eski satırları sil (snapshot yenilemesi)
        var oldFromBundle = await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId && rp.SourceBundleId == bundleId)
            .ToListAsync(ct);
        _context.RolePermissions.RemoveRange(oldFromBundle);

        var now = DateTime.UtcNow;
        var newRows = bundle.Items.Select(item => new RolePermission
        {
            RoleId = roleId,
            PermissionId = item.PermissionId,
            SourceBundleId = bundleId,
            GrantedAt = now,
            GrantedByUserId = actorUserId,
        }).ToList();
        await _context.RolePermissions.AddRangeAsync(newRows, ct);

        await _context.SaveChangesAsync(ct);
        await _permissionService.InvalidateRoleCacheAsync(roleId, ct);
    }

    public async Task RemoveBundleFromRoleAsync(int roleId, int bundleId, CancellationToken ct = default)
    {
        var rows = await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId && rp.SourceBundleId == bundleId)
            .ToListAsync(ct);
        if (rows.Count == 0) return;

        _context.RolePermissions.RemoveRange(rows);
        await _context.SaveChangesAsync(ct);
        await _permissionService.InvalidateRoleCacheAsync(roleId, ct);
    }

    public async Task<List<int>> GetAppliedBundleIdsAsync(int roleId, CancellationToken ct = default)
    {
        return await _context.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId && rp.SourceBundleId != null)
            .Select(rp => rp.SourceBundleId!.Value)
            .Distinct()
            .ToListAsync(ct);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Paket adı boş olamaz.", nameof(name));
        }
        if (name.Trim().Length > 200)
        {
            throw new ArgumentException("Paket adı 200 karakteri aşamaz.", nameof(name));
        }
    }

    private async Task<List<int>> ValidatePermissionIdsAsync(List<int> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0)
        {
            return distinct;
        }

        var existing = await _context.Permissions
            .AsNoTracking()
            .Where(p => distinct.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (existing.Count != distinct.Count)
        {
            var missing = distinct.Except(existing).ToList();
            throw new ArgumentException($"Geçersiz permission ID(ler): {string.Join(", ", missing)}");
        }
        return distinct;
    }

    private static PermissionBundleDetailDto ToDetailDto(PermissionBundle bundle) => new()
    {
        Id = bundle.Id,
        Name = bundle.Name,
        Description = bundle.Description,
        Category = bundle.Category,
        IsSystemDefault = bundle.IsSystemDefault,
        CreatedAt = bundle.CreatedAt,
        UpdatedAt = bundle.UpdatedAt,
        PermissionIds = bundle.Items.Select(i => i.PermissionId).OrderBy(id => id).ToList(),
    };
}
