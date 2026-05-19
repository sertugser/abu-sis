using SIS.Application.Announcement.Configuration;
using SIS.Application.Common.Services;

namespace SIS.Infrastructure.Services.Announcement;

/// <summary>
/// <see cref="AnnouncementSettings"/>'i <see cref="IWebModuleSettingsProvider"/> üzerinden
/// WebModuleSettings tablosundan üretir. Her anahtar 5 dakikalık cache'de olduğundan
/// sıcak (warm) çağrılarda maliyet ~0; ilk (soğuk) çağrı 11 anahtarı sırayla okur.
///
/// DİKKAT: Sequential olmak ZORUNLU — request-scoped tek DbContext üzerinden bu provider
/// çağrıları yapılıyor; Task.WhenAll ile paralel çekmek "A second operation was started
/// on this context instance" InvalidOperationException'ı fırlatır.
/// </summary>
public sealed class AnnouncementSettingsProvider : IAnnouncementSettingsProvider
{
    private const string ModuleKey = "Announcements";

    // Fallback değerleri DB seed eksikse devreye girer ve sistemin kapanmamasını sağlar.
    // Seed (SeedData.WebModuleSettings) bu değerlerle aynıdır.
    private const int    FallbackMaxTitleLength       = 200;
    private const int    FallbackMaxContentLength     = 50_000;
    private const int    FallbackStudentTopCount      = 5;
    private const bool   FallbackPinnedFirst          = true;
    private const bool   FallbackAllowImagesInContent = true;
    private const bool   FallbackAllowTablesInContent = true;
    private const int    FallbackStudentYearFilterMin = 1;
    private const int    FallbackStudentYearFilterMax = 7;
    private const bool   FallbackAttachmentsEnabled   = true;
    private const int    FallbackAttachmentMaxSizeMB  = 20;
    private const int    FallbackAttachmentMaxCount   = 5;

    private readonly IWebModuleSettingsProvider _webSettings;

    public AnnouncementSettingsProvider(IWebModuleSettingsProvider webSettings)
    {
        _webSettings = webSettings;
    }

    public async Task<AnnouncementSettings> GetAsync(CancellationToken ct = default)
    {
        var maxTitle        = await _webSettings.GetIntAsync(ModuleKey, "MaxTitleLength",            FallbackMaxTitleLength,        ct);
        var maxContent      = await _webSettings.GetIntAsync(ModuleKey, "MaxContentLength",          FallbackMaxContentLength,      ct);
        var topCount        = await _webSettings.GetIntAsync(ModuleKey, "StudentTopCount",           FallbackStudentTopCount,       ct);
        var pinnedFirst     = await _webSettings.GetBoolAsync(ModuleKey, "PinnedAnnouncementsFirst", FallbackPinnedFirst,           ct);
        var allowImages     = await _webSettings.GetBoolAsync(ModuleKey, "AllowImagesInContent",     FallbackAllowImagesInContent,  ct);
        var allowTables     = await _webSettings.GetBoolAsync(ModuleKey, "AllowTablesInContent",     FallbackAllowTablesInContent,  ct);
        var yearMin         = await _webSettings.GetIntAsync(ModuleKey, "StudentYearFilterMin",      FallbackStudentYearFilterMin,  ct);
        var yearMax         = await _webSettings.GetIntAsync(ModuleKey, "StudentYearFilterMax",      FallbackStudentYearFilterMax,  ct);
        var attachEnabled   = await _webSettings.GetBoolAsync(ModuleKey, "AttachmentsEnabled",       FallbackAttachmentsEnabled,    ct);
        var attachMaxSize   = await _webSettings.GetIntAsync(ModuleKey, "AttachmentMaxSizeMB",       FallbackAttachmentMaxSizeMB,   ct);
        var attachMaxCount  = await _webSettings.GetIntAsync(ModuleKey, "AttachmentMaxCount",        FallbackAttachmentMaxCount,    ct);

        return new AnnouncementSettings(
            MaxTitleLength:        maxTitle,
            MaxContentLength:      maxContent,
            StudentTopCount:       topCount,
            PinnedFirst:           pinnedFirst,
            AllowImagesInContent:  allowImages,
            AllowTablesInContent:  allowTables,
            StudentYearFilterMin:  yearMin,
            StudentYearFilterMax:  yearMax,
            AttachmentsEnabled:    attachEnabled,
            AttachmentMaxSizeMB:   attachMaxSize,
            AttachmentMaxCount:    attachMaxCount);
    }
}
