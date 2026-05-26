namespace SIS.Application.Reports;

/// <summary>Frontend'e dönen sayfalı sonuç (kolon meta + satırlar + toplam).</summary>
public sealed record ReportRunResultDto(
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    int TotalCount,
    int Page,
    int PageSize);

public interface IReportExecutionService
{
    /// <summary>Rapor kodunun parametre şemasını döner. Yetki yoksa null.</summary>
    Task<ReportParameterSchema?> GetSchemaAsync(int userId, string reportCode, CancellationToken ct = default);

    /// <summary>Raporu çalıştırır (sayfalı). Yetki yoksa UnauthorizedAccessException; kod yoksa KeyNotFoundException.</summary>
    Task<ReportRunResultDto> RunAsync(int userId, string reportCode, ReportRunRequest request, CancellationToken ct = default);

    /// <summary>Raporun TÜM satırlarını (sayfalama olmadan) export için çalıştırır.</summary>
    Task<ReportResult> RunForExportAsync(int userId, string reportCode, ReportRunRequest request, CancellationToken ct = default);
}
