namespace SIS.Application.Identity.Services;

/// <summary>
/// Request bazlı scope context'i. Scope aktifse query filter ve write guard tetiklenir.
/// </summary>
public interface ICurrentUserScope
{
    bool IsActive { get; }

    /// <summary>
    /// Geriye uyumluluk: tekil fakülte (set'in ilki). Çoklu fakülte için <see cref="FacultyIds"/> kullanın.
    /// </summary>
    int? FacultyId { get; }

    /// <summary>
    /// Kullanıcının erişebildiği TÜM fakülteler. Kendi scope-bound rolü + aktif vekâletle devraldığı
    /// fakültelerin birleşimi. Boş set = aktif ama hiçbir fakülte verisi görünmez (fail-safe).
    /// Varsayılan implementasyon eski tekil <see cref="FacultyId"/>'den türetir (test stub'ları için).
    /// </summary>
    IReadOnlyCollection<int> FacultyIds =>
        FacultyId is { } id ? new[] { id } : Array.Empty<int>();

    Task<bool> IsActiveAsync();
    Task<int?> GetFacultyIdAsync();
}
