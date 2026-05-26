using SIS.Application.Identity.Services;
using SIS.Application.Reports;

namespace SIS.Infrastructure.Services.Reports;

public sealed class ReportExecutionService : IReportExecutionService
{
    private const string SystemAdminRole = "SystemAdministrator";

    private readonly IReadOnlyDictionary<string, IReportHandler> _handlers;
    private readonly IReportAuthorizationService _authz;
    private readonly ICurrentUserScope _scope;
    private readonly IUserRoleReader _roleReader;

    public ReportExecutionService(
        IEnumerable<IReportHandler> handlers,
        IReportAuthorizationService authz,
        ICurrentUserScope scope,
        IUserRoleReader roleReader)
    {
        _handlers = handlers.ToDictionary(h => h.Code, StringComparer.Ordinal);
        _authz = authz;
        _scope = scope;
        _roleReader = roleReader;
    }

    public async Task<ReportParameterSchema?> GetSchemaAsync(int userId, string reportCode, CancellationToken ct = default)
    {
        if (!_handlers.TryGetValue(reportCode, out var handler)) return null;
        if (!await _authz.CanAccessReportAsync(userId, reportCode, ct)) return null;
        return handler.GetParameterSchema();
    }

    public async Task<ReportRunResultDto> RunAsync(int userId, string reportCode, ReportRunRequest request, CancellationToken ct = default)
    {
        var (handler, exec) = await ResolveAsync(userId, reportCode, ct);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 500);
        var normalized = request with { Page = page, PageSize = pageSize };
        var result = await handler.RunAsync(normalized, exec, ct);
        return new ReportRunResultDto(result.Columns, result.Rows, result.TotalCount, page, pageSize);
    }

    public async Task<ReportResult> RunForExportAsync(int userId, string reportCode, ReportRunRequest request, CancellationToken ct = default)
    {
        var (handler, exec) = await ResolveAsync(userId, reportCode, ct);
        // Export: tüm satırlar (büyük sayfa). PageSize üst sınır export için yükseltilir.
        var exportReq = request with { Page = 1, PageSize = 100_000 };
        return await handler.RunAsync(exportReq, exec, ct);
    }

    private async Task<(IReportHandler handler, ReportExecutionContext ctx)> ResolveAsync(
        int userId, string reportCode, CancellationToken ct)
    {
        if (!_handlers.TryGetValue(reportCode, out var handler))
            throw new KeyNotFoundException($"Rapor handler bulunamadı: {reportCode}");
        if (!await _authz.CanAccessReportAsync(userId, reportCode, ct))
            throw new UnauthorizedAccessException($"Bu rapora erişim yetkiniz yok: {reportCode}");

        var roles = await _roleReader.GetRolesAsync(userId, ct);
        var isAdmin = roles.Contains(SystemAdminRole);
        var exec = new ReportExecutionContext(
            UserId: userId,
            Roles: roles,
            IsSystemAdmin: isAdmin,
            ScopeActive: _scope.IsActive,
            ScopeFacultyIds: _scope.FacultyIds);
        return (handler, exec);
    }
}
