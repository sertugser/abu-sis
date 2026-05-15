namespace SIS.Application.Identity.Services;

/// <summary>
/// Request bazlı scope context'i. Scope aktifse query filter ve write guard tetiklenir.
/// </summary>
public interface ICurrentUserScope
{
    bool IsActive { get; }
    int? FacultyId { get; }

    Task<bool> IsActiveAsync();
    Task<int?> GetFacultyIdAsync();
}
