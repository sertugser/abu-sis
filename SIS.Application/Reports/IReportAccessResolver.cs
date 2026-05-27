namespace SIS.Application.Reports;

/// <summary>Bir kullanıcının hangi raporlara erişebildiğini hesaplayan tek kaynak.</summary>
public interface IReportAccessResolver
{
    /// <summary>Belirli bir rapora erişim. SystemAdmin → her zaman true.</summary>
    Task<bool> CanAccessAsync(int userId, string reportCode, CancellationToken ct = default);

    /// <summary>Kullanıcının erişebildiği raporların Id kümesi (tüm raporlar arasından).</summary>
    Task<IReadOnlySet<int>> AccessibleReportIdsAsync(int userId, CancellationToken ct = default);
}
