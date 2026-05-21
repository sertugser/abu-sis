namespace SIS.Application.Common.Services;

public sealed class NullWebModuleControlService : IWebModuleControlService
{
    public WebModuleControlDefinition? GetDefinition(string code) => null;

    public Task<IReadOnlyList<WebModuleControlSummary>> GetSummariesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WebModuleControlSummary>>(Array.Empty<WebModuleControlSummary>());

    public Task<WebModuleAccessResult> EvaluateAsync(string code, int? termId = null, CancellationToken ct = default)
        => Task.FromResult(new WebModuleAccessResult(
            IsEnabled: true,
            IsOpen: true,
            SelectedMode: WebModuleControlModes.Open,
            SelectedModeLabel: "Açık",
            EffectiveStateLabel: "Açık",
            Message: null));
}
