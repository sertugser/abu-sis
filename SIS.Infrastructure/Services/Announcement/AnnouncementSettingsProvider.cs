using SIS.Application.Announcement.Configuration;
using SIS.Application.Common.Services;

namespace SIS.Infrastructure.Services.Announcement;

/// <summary>
/// <see cref="AnnouncementSettings"/>'i <see cref="IWebModuleSettingsProvider"/> üzerinden
/// WebModuleSettings tablosundan üretir. Her çağrı 6 anahtarı paralel okur — her anahtar
/// kendi 5 dakikalık cache'inde olduğundan çağrı maliyeti düşüktür.
/// </summary>
public sealed class AnnouncementSettingsProvider : IAnnouncementSettingsProvider
{
    private const string ModuleKey = "Announcements";

    // Fallback değerleri DB seed eksikse devreye girer ve sistemin kapanmamasını sağlar.
    // Seed (SeedData.WebModuleSettings) bu değerlerle aynıdır.
    private const int    FallbackMaxTitleLength      = 200;
    private const int    FallbackMaxContentLength    = 50_000;
    private const int    FallbackStudentTopCount     = 5;
    private const bool   FallbackPinnedFirst         = true;
    private const bool   FallbackAllowImagesInContent = true;
    private const bool   FallbackAllowTablesInContent = true;
    private const int    FallbackStudentYearFilterMin = 1;
    private const int    FallbackStudentYearFilterMax = 7;

    private readonly IWebModuleSettingsProvider _webSettings;

    public AnnouncementSettingsProvider(IWebModuleSettingsProvider webSettings)
    {
        _webSettings = webSettings;
    }

    public async Task<AnnouncementSettings> GetAsync(CancellationToken ct = default)
    {
        var maxTitleTask    = _webSettings.GetIntAsync(ModuleKey, "MaxTitleLength",    FallbackMaxTitleLength,    ct);
        var maxContentTask  = _webSettings.GetIntAsync(ModuleKey, "MaxContentLength",  FallbackMaxContentLength,  ct);
        var topCountTask    = _webSettings.GetIntAsync(ModuleKey, "StudentTopCount",   FallbackStudentTopCount,   ct);
        var pinnedFirstTask = _webSettings.GetBoolAsync(ModuleKey, "PinnedAnnouncementsFirst", FallbackPinnedFirst, ct);
        var allowImagesTask = _webSettings.GetBoolAsync(ModuleKey, "AllowImagesInContent",     FallbackAllowImagesInContent, ct);
        var allowTablesTask = _webSettings.GetBoolAsync(ModuleKey, "AllowTablesInContent",     FallbackAllowTablesInContent, ct);
        var yearMinTask     = _webSettings.GetIntAsync(ModuleKey, "StudentYearFilterMin",      FallbackStudentYearFilterMin, ct);
        var yearMaxTask     = _webSettings.GetIntAsync(ModuleKey, "StudentYearFilterMax",      FallbackStudentYearFilterMax, ct);

        await Task.WhenAll(
            maxTitleTask, maxContentTask, topCountTask, pinnedFirstTask,
            allowImagesTask, allowTablesTask, yearMinTask, yearMaxTask);

        return new AnnouncementSettings(
            MaxTitleLength:        maxTitleTask.Result,
            MaxContentLength:      maxContentTask.Result,
            StudentTopCount:       topCountTask.Result,
            PinnedFirst:           pinnedFirstTask.Result,
            AllowImagesInContent:  allowImagesTask.Result,
            AllowTablesInContent:  allowTablesTask.Result,
            StudentYearFilterMin:  yearMinTask.Result,
            StudentYearFilterMax:  yearMaxTask.Result);
    }
}
