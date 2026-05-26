namespace SIS.Application.Reports;

public enum ReportColumnAlign { Left, Right, Center }

public sealed record ReportColumn(string Key, string Label, ReportColumnAlign Align = ReportColumnAlign.Left);

/// <summary>Bir rapor çalıştırma isteği. Parametreler key→string ham değer (handler parse eder).</summary>
public sealed record ReportRunRequest(
    IReadOnlyDictionary<string, string?> Parameters,
    int Page = 1,
    int PageSize = 50,
    string? SortKey = null,
    bool SortDescending = false);

/// <summary>Handler'a verilen çalıştırma bağlamı: kullanıcı kimliği + scope.</summary>
public sealed record ReportExecutionContext(
    int UserId,
    IReadOnlyCollection<string> Roles,
    bool IsSystemAdmin,
    bool ScopeActive,
    IReadOnlyCollection<int> ScopeFacultyIds);

public sealed record ReportResult(
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    int TotalCount);
