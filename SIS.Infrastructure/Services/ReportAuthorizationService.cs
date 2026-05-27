using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Identity.DTOs;
using SIS.Application.Identity.Services;
using SIS.Application.Reports;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class ReportAuthorizationService : IReportAuthorizationService
{
    private const string SystemAdminRole = "SystemAdministrator";

    // DB kolon limitleriyle birebir (AppDbContext + migration). Aşımı temiz 400 olarak yakala,
    // aksi halde EF/Postgres insert'te yakalanmamış 500 üretir.
    private const int CategoryCodeMaxLength = 64;
    private const int ReportCodeMaxLength = 100;
    private const int NameMaxLength = 200;
    private const int DescriptionMaxLength = 500;
    private const int ReasonMaxLength = 500;

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<int>> _roleManager;
    private readonly IReportAccessResolver _resolver;

    public ReportAuthorizationService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<int>> roleManager,
        IReportAccessResolver resolver)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _resolver = resolver;
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
        ValidateCode(dto.Code, CategoryCodeMaxLength);
        ValidateName(dto.Name);

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

        ValidateName(dto.Name);

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
        ValidateCode(dto.Code, ReportCodeMaxLength);
        ValidateName(dto.Name);
        ValidateOptionalText(dto.Description, DescriptionMaxLength, "Açıklama");

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

        ValidateName(dto.Name);
        ValidateOptionalText(dto.Description, DescriptionMaxLength, "Açıklama");

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

    // ── Kullanıcı grupları ───────────────────────────────────

    public async Task<IReadOnlyList<ReportUserGroupDto>> ListGroupsAsync(CancellationToken ct = default) =>
        await _db.ReportUserGroups.AsNoTracking().OrderBy(g => g.Name)
            .Select(g => new ReportUserGroupDto(g.Id, g.Name, g.Description, g.Members.Count))
            .ToListAsync(ct);

    public async Task<ReportUserGroupDto> CreateGroupAsync(CreateReportUserGroupDto dto, CancellationToken ct = default)
    {
        ValidateName(dto.Name);
        ValidateOptionalText(dto.Description, DescriptionMaxLength, "Açıklama");
        if (await _db.ReportUserGroups.AnyAsync(g => g.Name == dto.Name.Trim(), ct))
            throw new InvalidOperationException($"Grup adı zaten kullanımda: {dto.Name.Trim()}");
        var entity = new ReportUserGroup { Name = dto.Name.Trim(), Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim() };
        _db.ReportUserGroups.Add(entity);
        await _db.SaveChangesAsync(ct);
        return new ReportUserGroupDto(entity.Id, entity.Name, entity.Description, 0);
    }

    public async Task<ReportUserGroupDto> UpdateGroupAsync(int id, UpdateReportUserGroupDto dto, CancellationToken ct = default)
    {
        var entity = await _db.ReportUserGroups.FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new KeyNotFoundException($"Grup bulunamadı: {id}");
        ValidateName(dto.Name);
        ValidateOptionalText(dto.Description, DescriptionMaxLength, "Açıklama");
        entity.Name = dto.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        var count = await _db.ReportUserGroupMembers.CountAsync(m => m.GroupId == id, ct);
        return new ReportUserGroupDto(entity.Id, entity.Name, entity.Description, count);
    }

    public async Task DeleteGroupAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.ReportUserGroups.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (entity is null) return;
        // Bu gruba verilmiş grant'ları da temizle (FK yok — manuel)
        var grants = await _db.ReportAccessGrants
            .Where(g => g.SubjectKind == ReportGrantSubjectKind.Group && g.SubjectId == id).ToListAsync(ct);
        _db.ReportAccessGrants.RemoveRange(grants);
        _db.ReportUserGroups.Remove(entity); // üyeler cascade
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ReportUserGroupMemberDto>> ListGroupMembersAsync(int groupId, CancellationToken ct = default)
    {
        var members = await _db.ReportUserGroupMembers.AsNoTracking()
            .Where(m => m.GroupId == groupId).ToListAsync(ct);
        if (members.Count == 0) return Array.Empty<ReportUserGroupMemberDto>();
        var userIds = members.Select(m => m.UserId).ToList();
        var users = await _userManager.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToListAsync(ct);
        return members
            .Select(m =>
            {
                var u = users.FirstOrDefault(x => x.Id == m.UserId);
                var fullName = u is null ? $"#{m.UserId}" : $"{u.FirstName} {u.LastName}".Trim();
                return new ReportUserGroupMemberDto(m.UserId, fullName, u?.Email ?? string.Empty, m.AddedAt);
            })
            .OrderBy(m => m.FullName)
            .ToList();
    }

    public async Task AddGroupMemberAsync(int groupId, int userId, CancellationToken ct = default)
    {
        if (!await _db.ReportUserGroups.AnyAsync(g => g.Id == groupId, ct))
            throw new KeyNotFoundException($"Grup bulunamadı: {groupId}");
        if (await _db.ReportUserGroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId, ct))
            return;
        _db.ReportUserGroupMembers.Add(new ReportUserGroupMember { GroupId = groupId, UserId = userId });
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveGroupMemberAsync(int groupId, int userId, CancellationToken ct = default)
    {
        var member = await _db.ReportUserGroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId, ct);
        if (member is null) return;
        _db.ReportUserGroupMembers.Remove(member);
        await _db.SaveChangesAsync(ct);
    }

    // ── Birleşik yetki (grant) ───────────────────────────────

    public async Task<IReadOnlyList<ReportAccessGrantDto>> ListGrantsAsync(CancellationToken ct = default)
    {
        var grants = await _db.ReportAccessGrants.AsNoTracking().OrderByDescending(g => g.GrantedAt).ToListAsync(ct);

        var categoryIds = grants.Where(g => g.ScopeKind == ReportGrantScopeKind.Category).Select(g => g.TargetId).Distinct().ToList();
        var reportIds = grants.Where(g => g.ScopeKind == ReportGrantScopeKind.Report).Select(g => g.TargetId).Distinct().ToList();
        var roleIds = grants.Where(g => g.SubjectKind == ReportGrantSubjectKind.Role).Select(g => g.SubjectId).Distinct().ToList();
        var userIds = grants.Where(g => g.SubjectKind == ReportGrantSubjectKind.User).Select(g => g.SubjectId).Distinct().ToList();
        var groupIds = grants.Where(g => g.SubjectKind == ReportGrantSubjectKind.Group).Select(g => g.SubjectId).Distinct().ToList();

        var categories = await _db.ReportCategories.AsNoTracking().Where(c => categoryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var reports = await _db.ReportDefinitions.AsNoTracking().Where(r => reportIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var roles = await _db.Roles.AsNoTracking().Where(r => roleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name ?? string.Empty, ct);
        var groups = await _db.ReportUserGroups.AsNoTracking().Where(g => groupIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Name, ct);
        var userLabels = new Dictionary<int, string>();
        if (userIds.Count > 0)
        {
            var users = await _userManager.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName }).ToListAsync(ct);
            foreach (var u in users)
                userLabels[u.Id] = $"{u.FirstName} {u.LastName}".Trim();
        }

        string TargetLabel(ReportAccessGrant g) => g.ScopeKind == ReportGrantScopeKind.Category
            ? categories.GetValueOrDefault(g.TargetId, $"Kategori #{g.TargetId}")
            : reports.GetValueOrDefault(g.TargetId, $"Rapor #{g.TargetId}");

        string SubjectLabel(ReportAccessGrant g) => g.SubjectKind switch
        {
            ReportGrantSubjectKind.Role => roles.GetValueOrDefault(g.SubjectId, $"Rol #{g.SubjectId}"),
            ReportGrantSubjectKind.Group => groups.GetValueOrDefault(g.SubjectId, $"Grup #{g.SubjectId}"),
            _ => userLabels.GetValueOrDefault(g.SubjectId, $"Kullanıcı #{g.SubjectId}"),
        };

        return grants.Select(g => new ReportAccessGrantDto(
            g.Id, (int)g.ScopeKind, g.TargetId, TargetLabel(g),
            (int)g.SubjectKind, g.SubjectId, SubjectLabel(g),
            (int)g.Effect, g.Reason, g.GrantedAt)).ToList();
    }

    public async Task<ReportAccessGrantDto> SetGrantAsync(SetReportAccessGrantDto dto, int grantedByUserId, CancellationToken ct = default)
    {
        var scope = (ReportGrantScopeKind)dto.ScopeKind;
        var subject = (ReportGrantSubjectKind)dto.SubjectKind;
        var effect = (ReportGrantEffect)dto.Effect;
        ValidateOptionalText(dto.Reason, ReasonMaxLength, "Sebep");
        await EnsureTargetExistsAsync(scope, dto.TargetId, ct);
        await EnsureSubjectExistsAsync(subject, dto.SubjectId, ct);

        var existing = await _db.ReportAccessGrants.FirstOrDefaultAsync(g =>
            g.ScopeKind == scope && g.TargetId == dto.TargetId &&
            g.SubjectKind == subject && g.SubjectId == dto.SubjectId, ct);

        if (existing is null)
        {
            existing = new ReportAccessGrant
            {
                ScopeKind = scope, TargetId = dto.TargetId, SubjectKind = subject, SubjectId = dto.SubjectId,
                Effect = effect, Reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim(),
                GrantedAt = DateTime.UtcNow, GrantedByUserId = grantedByUserId,
            };
            _db.ReportAccessGrants.Add(existing);
        }
        else
        {
            existing.Effect = effect;
            existing.Reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
            existing.GrantedAt = DateTime.UtcNow;
            existing.GrantedByUserId = grantedByUserId;
        }
        await _db.SaveChangesAsync(ct);

        var list = await ListGrantsAsync(ct);
        return list.First(g => g.Id == existing.Id);
    }

    public async Task DeleteGrantAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.ReportAccessGrants.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (entity is null) return;
        _db.ReportAccessGrants.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureTargetExistsAsync(ReportGrantScopeKind scope, int targetId, CancellationToken ct)
    {
        var exists = scope == ReportGrantScopeKind.Category
            ? await _db.ReportCategories.AnyAsync(c => c.Id == targetId, ct)
            : await _db.ReportDefinitions.AnyAsync(r => r.Id == targetId, ct);
        if (!exists) throw new KeyNotFoundException($"Hedef bulunamadı (scope={scope}, id={targetId}).");
    }

    private async Task EnsureSubjectExistsAsync(ReportGrantSubjectKind subject, int subjectId, CancellationToken ct)
    {
        var exists = subject switch
        {
            ReportGrantSubjectKind.Role => await _db.Roles.AnyAsync(r => r.Id == subjectId, ct),
            ReportGrantSubjectKind.Group => await _db.ReportUserGroups.AnyAsync(g => g.Id == subjectId, ct),
            _ => await _userManager.Users.AnyAsync(u => u.Id == subjectId, ct),
        };
        if (!exists) throw new KeyNotFoundException($"Özne bulunamadı (kind={subject}, id={subjectId}).");
    }

    // ── Yetki çözümleme (tek resolver'a delege) ──────────────

    public Task<bool> CanAccessReportAsync(int userId, string reportCode, CancellationToken ct = default)
        => _resolver.CanAccessAsync(userId, reportCode, ct);

    public async Task<IReadOnlyList<AccessibleReportDto>> ListAccessibleReportsAsync(int userId, CancellationToken ct = default)
    {
        var allowedIds = await _resolver.AccessibleReportIdsAsync(userId, ct);
        return await _db.ReportDefinitions.AsNoTracking()
            .Include(r => r.Category)
            .Where(r => allowedIds.Contains(r.Id))
            .OrderBy(r => r.Category!.Name).ThenBy(r => r.Name)
            .Select(r => new AccessibleReportDto(r.Id, r.Code, r.Name, r.Description, r.CategoryId, r.Category!.Name))
            .ToListAsync(ct);
    }

    private static void ValidateCode(string code, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Kod zorunlu.");
        var trimmed = code.Trim();
        if (trimmed.Length < 2 || trimmed.Length > maxLength)
            throw new InvalidOperationException($"Kod 2-{maxLength} karakter arası olmalı.");
        foreach (var ch in trimmed)
        {
            var ok = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '-' || ch == '_';
            if (!ok)
                throw new InvalidOperationException("Kod sadece küçük harf, rakam, tire ve alt-tire içerebilir.");
        }
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Ad zorunlu.");
        if (name.Trim().Length > NameMaxLength)
            throw new InvalidOperationException($"Ad en fazla {NameMaxLength} karakter olabilir.");
    }

    private static void ValidateOptionalText(string? text, int maxLength, string fieldLabel)
    {
        if (!string.IsNullOrWhiteSpace(text) && text.Trim().Length > maxLength)
            throw new InvalidOperationException($"{fieldLabel} en fazla {maxLength} karakter olabilir.");
    }
}
