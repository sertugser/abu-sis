using SIS.Application.Common.Services;

namespace SIS.Tests.TestDoubles;

/// <summary>
/// Test double: tüm dönemlerin notları görünür kabul edilir (ilan/web-modül kontrolü
/// test kapsamı dışında). GpaCalculationService'i kuran tüm testler bunu paylaşır.
/// </summary>
public sealed class AllVisibleGradeVisibility : IGradeVisibilityService
{
    public Task<Dictionary<int, bool>> BuildTermGradeVisibilityAsync(
        IEnumerable<int> termIds, CancellationToken ct = default) =>
        Task.FromResult(termIds.Distinct().ToDictionary(id => id, _ => true));
}
