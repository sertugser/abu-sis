using SIS.Application.Common.Services;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Harf notu görünürlüğünün dönem bazlı, web-modül kontrollü tek kaynağı.
/// Her dönem için letter-grade-publication açıksa görünür; değilse
/// makeup-letter-grade-publication açıksa görünür.
/// </summary>
public sealed class GradeVisibilityResolver : IGradeVisibilityResolver
{
    private readonly IWebModuleControlService _webModuleControls;

    public GradeVisibilityResolver(IWebModuleControlService webModuleControls)
    {
        _webModuleControls = webModuleControls;
    }

    public async Task<bool> IsVisibleAsync(int termId, CancellationToken ct = default)
    {
        var finalAccess = await _webModuleControls.EvaluateAsync("letter-grade-publication", termId, ct);
        if (finalAccess.IsOpen)
            return true;

        var makeupAccess = await _webModuleControls.EvaluateAsync("makeup-letter-grade-publication", termId, ct);
        return makeupAccess.IsOpen;
    }

    public async Task<IReadOnlyDictionary<int, bool>> BuildMapAsync(
        IReadOnlyCollection<int> termIds,
        CancellationToken ct = default)
    {
        var result = new Dictionary<int, bool>();
        foreach (var termId in termIds.Distinct())
        {
            result[termId] = await IsVisibleAsync(termId, ct);
        }

        return result;
    }
}
