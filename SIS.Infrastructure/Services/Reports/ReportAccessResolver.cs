using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Reports;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Reports;

public sealed class ReportAccessResolver : IReportAccessResolver
{
    private const string SystemAdminRole = "SystemAdministrator";

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportAccessResolver(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<bool> CanAccessAsync(int userId, string reportCode, CancellationToken ct = default)
    {
        var subjects = await ResolveSubjectsAsync(userId, ct);
        if (subjects is null) return true; // SystemAdmin

        var report = await _db.ReportDefinitions.AsNoTracking()
            .Where(r => r.Code == reportCode)
            .Select(r => new { r.Id, r.CategoryId })
            .FirstOrDefaultAsync(ct);
        if (report is null) return false;

        var grants = await LoadGrantsAsync(subjects, ct);
        return Evaluate(report.Id, report.CategoryId, grants);
    }

    public async Task<IReadOnlySet<int>> AccessibleReportIdsAsync(int userId, CancellationToken ct = default)
    {
        var reports = await _db.ReportDefinitions.AsNoTracking()
            .Select(r => new { r.Id, r.CategoryId })
            .ToListAsync(ct);

        var subjects = await ResolveSubjectsAsync(userId, ct);
        if (subjects is null) return reports.Select(r => r.Id).ToHashSet(); // SystemAdmin

        var grants = await LoadGrantsAsync(subjects, ct);
        return reports.Where(r => Evaluate(r.Id, r.CategoryId, grants)).Select(r => r.Id).ToHashSet();
    }

    /// <summary>null = SystemAdmin (sınırsız). Aksi halde (roleIds, groupIds, userId).</summary>
    private async Task<SubjectSet?> ResolveSubjectsAsync(int userId, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return new SubjectSet(new HashSet<int>(), new HashSet<int>(), userId);

        var roleNames = await _userManager.GetRolesAsync(user);
        if (roleNames.Contains(SystemAdminRole)) return null;

        var roleIds = await _db.Roles.AsNoTracking()
            .Where(r => roleNames.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync(ct);

        var groupIds = await _db.ReportUserGroupMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.GroupId)
            .ToListAsync(ct);

        return new SubjectSet(roleIds.ToHashSet(), groupIds.ToHashSet(), userId);
    }

    private async Task<List<ReportAccessGrant>> LoadGrantsAsync(SubjectSet s, CancellationToken ct)
    {
        // Yalnızca bu kullanıcının öznelerine ait grant'lar
        return await _db.ReportAccessGrants.AsNoTracking()
            .Where(g =>
                (g.SubjectKind == ReportGrantSubjectKind.Role && s.RoleIds.Contains(g.SubjectId)) ||
                (g.SubjectKind == ReportGrantSubjectKind.User && g.SubjectId == s.UserId) ||
                (g.SubjectKind == ReportGrantSubjectKind.Group && s.GroupIds.Contains(g.SubjectId)))
            .ToListAsync(ct);
    }

    /// <summary>Öncelik: rapor-Deny &gt; rapor-Allow &gt; kategori-Deny &gt; kategori-Allow &gt; yok.</summary>
    private static bool Evaluate(int reportId, int categoryId, List<ReportAccessGrant> grants)
    {
        var reportGrants = grants.Where(g => g.ScopeKind == ReportGrantScopeKind.Report && g.TargetId == reportId).ToList();
        if (reportGrants.Any(g => g.Effect == ReportGrantEffect.Deny)) return false;
        if (reportGrants.Any(g => g.Effect == ReportGrantEffect.Allow)) return true;

        var categoryGrants = grants.Where(g => g.ScopeKind == ReportGrantScopeKind.Category && g.TargetId == categoryId).ToList();
        if (categoryGrants.Any(g => g.Effect == ReportGrantEffect.Deny)) return false;
        if (categoryGrants.Any(g => g.Effect == ReportGrantEffect.Allow)) return true;

        return false;
    }

    private sealed record SubjectSet(HashSet<int> RoleIds, HashSet<int> GroupIds, int UserId);
}
