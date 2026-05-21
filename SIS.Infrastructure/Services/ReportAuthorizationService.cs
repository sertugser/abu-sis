using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Identity.DTOs;
using SIS.Application.Identity.Services;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class ReportAuthorizationService : IReportAuthorizationService
{
    private const string SystemAdminRole = "SystemAdministrator";

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<int>> _roleManager;

    public ReportAuthorizationService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<int>> roleManager)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    // ── Categories ───────────────────────────────────────────

    public async Task<IReadOnlyList<ReportCategoryDto>> ListCategoriesAsync(CancellationToken ct = default)
    {
        var rows = await _db.ReportCategories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new ReportCategoryDto(c.Id, c.Code, c.Name, c.Reports.Count))
            .ToListAsync(ct);
        return rows;
    }

    public async Task<ReportCategoryDto> CreateCategoryAsync(CreateReportCategoryDto dto, CancellationToken ct = default)
    {
        ValidateCode(dto.Code);
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidOperationException("Kategori adı zorunlu.");

        if (await _db.ReportCategories.AnyAsync(c => c.Code == dto.Code, ct))
            throw new InvalidOperationException($"Kod zaten kullanımda: {dto.Code}");

        var entity = new ReportCategory
        {
            Code = dto.Code.Trim(),
            Name = dto.Name.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.ReportCategories.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new ReportCategoryDto(entity.Id, entity.Code, entity.Name, 0);
    }

    public async Task<ReportCategoryDto> UpdateCategoryAsync(int id, UpdateReportCategoryDto dto, CancellationToken ct = default)
    {
        var entity = await _db.ReportCategories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException($"Kategori bulunamadı: {id}");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidOperationException("Kategori adı zorunlu.");

        entity.Name = dto.Name.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var reportCount = await _db.ReportDefinitions.CountAsync(r => r.CategoryId == id, ct);
        return new ReportCategoryDto(entity.Id, entity.Code, entity.Name, reportCount);
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken ct = default)
    {
        var hasReports = await _db.ReportDefinitions.AnyAsync(r => r.CategoryId == id, ct);
        if (hasReports)
            throw new InvalidOperationException("Bu kategoride raporlar var, önce raporları taşı/sil.");

        var entity = await _db.ReportCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return;

        _db.ReportCategories.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ── Reports ──────────────────────────────────────────────

    public async Task<IReadOnlyList<ReportDefinitionDto>> ListReportsAsync(int? categoryId, CancellationToken ct = default)
    {
        var query = _db.ReportDefinitions.AsNoTracking().Include(r => r.Category).AsQueryable();
        if (categoryId.HasValue)
            query = query.Where(r => r.CategoryId == categoryId.Value);

        var rows = await query
            .OrderBy(r => r.Category!.Name)
            .ThenBy(r => r.Name)
            .Select(r => new ReportDefinitionDto(
                r.Id, r.Code, r.Name, r.Description,
                r.CategoryId, r.Category!.Code, r.Category.Name))
            .ToListAsync(ct);
        return rows;
    }

    public async Task<ReportDefinitionDto> CreateReportAsync(CreateReportDefinitionDto dto, CancellationToken ct = default)
    {
        ValidateCode(dto.Code);
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidOperationException("Rapor adı zorunlu.");

        var category = await _db.ReportCategories.FirstOrDefaultAsync(c => c.Id == dto.CategoryId, ct)
            ?? throw new KeyNotFoundException($"Kategori bulunamadı: {dto.CategoryId}");

        if (await _db.ReportDefinitions.AnyAsync(r => r.Code == dto.Code, ct))
            throw new InvalidOperationException($"Rapor kodu zaten kullanımda: {dto.Code}");

        var entity = new ReportDefinition
        {
            Code = dto.Code.Trim(),
            Name = dto.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            CategoryId = dto.CategoryId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.ReportDefinitions.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new ReportDefinitionDto(
            entity.Id, entity.Code, entity.Name, entity.Description,
            entity.CategoryId, category.Code, category.Name);
    }

    public async Task<ReportDefinitionDto> UpdateReportAsync(int id, UpdateReportDefinitionDto dto, CancellationToken ct = default)
    {
        var entity = await _db.ReportDefinitions.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException($"Rapor bulunamadı: {id}");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidOperationException("Rapor adı zorunlu.");

        var category = await _db.ReportCategories.FirstOrDefaultAsync(c => c.Id == dto.CategoryId, ct)
            ?? throw new KeyNotFoundException($"Kategori bulunamadı: {dto.CategoryId}");

        entity.Name = dto.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        entity.CategoryId = dto.CategoryId;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new ReportDefinitionDto(
            entity.Id, entity.Code, entity.Name, entity.Description,
            entity.CategoryId, category.Code, category.Name);
    }

    public async Task DeleteReportAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.ReportDefinitions.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return;

        _db.ReportDefinitions.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ── Category × Role matrix ───────────────────────────────

    public async Task<IReadOnlyList<CategoryRoleMatrixRowDto>> GetCategoryRoleMatrixAsync(CancellationToken ct = default)
    {
        var categories = await _db.ReportCategories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        var allMappings = await _db.ReportCategoryRolePermissions
            .AsNoTracking()
            .ToListAsync(ct);

        return categories
            .Select(c => new CategoryRoleMatrixRowDto(
                c.Id,
                c.Code,
                c.Name,
                allMappings.Where(m => m.CategoryId == c.Id).Select(m => m.RoleId).ToList()))
            .ToList();
    }

    public async Task SetCategoryRolesAsync(SetCategoryRolesDto dto, int grantedByUserId, CancellationToken ct = default)
    {
        var category = await _db.ReportCategories.FirstOrDefaultAsync(c => c.Id == dto.CategoryId, ct)
            ?? throw new KeyNotFoundException($"Kategori bulunamadı: {dto.CategoryId}");

        // Replace-all: önce mevcut satırları sil, sonra yenileri ekle
        var existing = await _db.ReportCategoryRolePermissions
            .Where(rp => rp.CategoryId == dto.CategoryId)
            .ToListAsync(ct);
        _db.ReportCategoryRolePermissions.RemoveRange(existing);

        var roleIds = dto.RoleIds.Distinct().ToList();
        var validRoleIds = await _db.Roles
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => r.Id)
            .ToListAsync(ct);

        foreach (var rid in validRoleIds)
        {
            _db.ReportCategoryRolePermissions.Add(new ReportCategoryRolePermission
            {
                CategoryId = dto.CategoryId,
                RoleId = rid,
                GrantedAt = DateTime.UtcNow,
                GrantedByUserId = grantedByUserId,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    // ── Per-report override ──────────────────────────────────

    public async Task<IReadOnlyList<ReportPermissionOverrideDto>> ListOverridesAsync(int? reportId, CancellationToken ct = default)
    {
        var query = _db.ReportPermissionOverrides
            .AsNoTracking()
            .Include(o => o.Report)
            .AsQueryable();
        if (reportId.HasValue)
            query = query.Where(o => o.ReportId == reportId.Value);

        var rows = await query.ToListAsync(ct);

        var roleIds = rows.Select(r => r.RoleId).Distinct().ToList();
        var roleLookup = await _db.Roles
            .Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name ?? string.Empty, ct);

        return rows
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new ReportPermissionOverrideDto(
                o.Id,
                o.ReportId,
                o.Report?.Code ?? string.Empty,
                o.Report?.Name ?? string.Empty,
                o.RoleId,
                roleLookup.GetValueOrDefault(o.RoleId, string.Empty),
                o.IsAllowed,
                o.Reason,
                o.CreatedAt))
            .ToList();
    }

    public async Task<ReportPermissionOverrideDto> UpsertOverrideAsync(
        CreateReportOverrideDto dto,
        int createdByUserId,
        CancellationToken ct = default)
    {
        var report = await _db.ReportDefinitions.FirstOrDefaultAsync(r => r.Id == dto.ReportId, ct)
            ?? throw new KeyNotFoundException($"Rapor bulunamadı: {dto.ReportId}");

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == dto.RoleId, ct)
            ?? throw new KeyNotFoundException($"Rol bulunamadı: {dto.RoleId}");

        var existing = await _db.ReportPermissionOverrides
            .FirstOrDefaultAsync(o => o.ReportId == dto.ReportId && o.RoleId == dto.RoleId, ct);

        if (existing is null)
        {
            existing = new ReportPermissionOverride
            {
                ReportId = dto.ReportId,
                RoleId = dto.RoleId,
                IsAllowed = dto.IsAllowed,
                Reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = createdByUserId,
            };
            _db.ReportPermissionOverrides.Add(existing);
        }
        else
        {
            existing.IsAllowed = dto.IsAllowed;
            existing.Reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
        }

        await _db.SaveChangesAsync(ct);

        return new ReportPermissionOverrideDto(
            existing.Id, existing.ReportId,
            report.Code, report.Name,
            existing.RoleId, role.Name ?? string.Empty,
            existing.IsAllowed, existing.Reason, existing.CreatedAt);
    }

    public async Task DeleteOverrideAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.ReportPermissionOverrides.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (entity is null) return;

        _db.ReportPermissionOverrides.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    // ── Yetki çözümleme ──────────────────────────────────────

    public async Task<bool> CanAccessReportAsync(int userId, string reportCode, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return false;

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(SystemAdminRole)) return true;

        var report = await _db.ReportDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Code == reportCode, ct);
        if (report is null) return false;

        var roleIds = await _db.Roles
            .AsNoTracking()
            .Where(r => roles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync(ct);

        return await EvaluateAccessAsync(report.Id, report.CategoryId, roleIds, ct);
    }

    public async Task<IReadOnlyList<AccessibleReportDto>> ListAccessibleReportsAsync(int userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Array.Empty<AccessibleReportDto>();

        var roles = await _userManager.GetRolesAsync(user);
        var isSysAdmin = roles.Contains(SystemAdminRole);

        var allReports = await _db.ReportDefinitions
            .AsNoTracking()
            .Include(r => r.Category)
            .ToListAsync(ct);

        if (isSysAdmin)
        {
            return allReports
                .Select(r => new AccessibleReportDto(r.Id, r.Code, r.Name, r.CategoryId, r.Category!.Name))
                .OrderBy(r => r.CategoryName).ThenBy(r => r.Name)
                .ToList();
        }

        var roleIds = await _db.Roles
            .AsNoTracking()
            .Where(r => roles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync(ct);
        var roleIdSet = new HashSet<int>(roleIds);

        // Single round-trip: tüm category role + override eşleşmelerini çek
        var categoryRoles = await _db.ReportCategoryRolePermissions
            .AsNoTracking()
            .Where(rp => roleIdSet.Contains(rp.RoleId))
            .Select(rp => new { rp.CategoryId, rp.RoleId })
            .ToListAsync(ct);
        var allowedCategories = categoryRoles.Select(c => c.CategoryId).ToHashSet();

        var overrides = await _db.ReportPermissionOverrides
            .AsNoTracking()
            .Where(o => roleIdSet.Contains(o.RoleId))
            .Select(o => new { o.ReportId, o.IsAllowed })
            .ToListAsync(ct);

        var denySet = overrides.Where(o => !o.IsAllowed).Select(o => o.ReportId).ToHashSet();
        var allowSet = overrides.Where(o => o.IsAllowed).Select(o => o.ReportId).ToHashSet();

        return allReports
            .Where(r =>
            {
                if (denySet.Contains(r.Id)) return false;
                if (allowSet.Contains(r.Id)) return true;
                return allowedCategories.Contains(r.CategoryId);
            })
            .Select(r => new AccessibleReportDto(r.Id, r.Code, r.Name, r.CategoryId, r.Category!.Name))
            .OrderBy(r => r.CategoryName).ThenBy(r => r.Name)
            .ToList();
    }

    private async Task<bool> EvaluateAccessAsync(int reportId, int categoryId, List<int> roleIds, CancellationToken ct)
    {
        // 1) Deny override?
        var hasDeny = await _db.ReportPermissionOverrides
            .AsNoTracking()
            .AnyAsync(o => o.ReportId == reportId && roleIds.Contains(o.RoleId) && !o.IsAllowed, ct);
        if (hasDeny) return false;

        // 2) Allow override?
        var hasAllow = await _db.ReportPermissionOverrides
            .AsNoTracking()
            .AnyAsync(o => o.ReportId == reportId && roleIds.Contains(o.RoleId) && o.IsAllowed, ct);
        if (hasAllow) return true;

        // 3) Kategori-rol eşleşmesi
        return await _db.ReportCategoryRolePermissions
            .AsNoTracking()
            .AnyAsync(rp => rp.CategoryId == categoryId && roleIds.Contains(rp.RoleId), ct);
    }

    private static void ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Kod zorunlu.");
        var trimmed = code.Trim();
        if (trimmed.Length < 2 || trimmed.Length > 100)
            throw new InvalidOperationException("Kod 2-100 karakter arası olmalı.");
        foreach (var ch in trimmed)
        {
            var ok = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '-' || ch == '_';
            if (!ok)
                throw new InvalidOperationException("Kod sadece küçük harf, rakam, tire ve alt-tire içerebilir.");
        }
    }
}
