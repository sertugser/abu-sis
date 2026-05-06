using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Common.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

public record WebModuleSettingDto(
    int Id,
    string ModuleKey,
    string Key,
    string Value,
    string? Description,
    string Category,
    string DataType,
    DateTime? EffectiveFrom,
    DateTime? EffectiveTo);

public record WebModuleControlOptionDto(string Code, string Label, string Description);
public record WebModuleControlSummaryDto(
    string Code,
    string Title,
    string Category,
    string Description,
    int DisplayOrder,
    int? ModeSettingId,
    int? AnnouncementSettingId,
    string SelectedMode,
    string SelectedModeLabel,
    bool IsCurrentlyOpen,
    string EffectiveStateLabel,
    string? EffectiveMessage,
    string? Announcement,
    IReadOnlyList<WebModuleControlOptionDto> Options);

public record UpdateWebModuleSettingDto(string Value, DateTime? EffectiveFrom, DateTime? EffectiveTo);
public record WebModuleSettingImportItemDto(string ModuleKey, string Key, string Value, DateTime? EffectiveFrom, DateTime? EffectiveTo);
public record UpdateWebModuleControlDto(string Mode, string? Announcement);

[ApiController]
[Route("api/admin/web-module-settings")]
[Authorize]
public class WebModuleSettingsController(
    AppDbContext context,
    IWebModuleSettingsProvider settingsProvider,
    IWebModuleControlService controlService) : ControllerBase
{
    private readonly AppDbContext _context = context;
    private readonly IWebModuleSettingsProvider _settingsProvider = settingsProvider;
    private readonly IWebModuleControlService _controlService = controlService;

    private static readonly Dictionary<string, (decimal? Min, decimal? Max)> NumericLimits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Announcements.StudentTopCount"] = (1, 20),
    };

    private static readonly JsonSerializerOptions ExportJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [HttpGet]
    [RequirePermission("menu.web-modul-ayarlari.view")]
    public async Task<IActionResult> GetAll([FromQuery] string? moduleKey, CancellationToken ct)
    {
        var query = _context.WebModuleSettings.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(moduleKey))
            query = query.Where(setting => setting.ModuleKey == moduleKey);

        var settings = await query
            .OrderBy(setting => setting.ModuleKey)
            .ThenBy(setting => setting.Category)
            .ThenBy(setting => setting.Key)
            .Select(setting => new WebModuleSettingDto(
                setting.Id,
                setting.ModuleKey,
                setting.Key,
                setting.Value,
                setting.Description,
                setting.Category,
                setting.DataType,
                setting.EffectiveFrom,
                setting.EffectiveTo))
            .ToListAsync(ct);

        return Ok(settings);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("menu.web-modul-ayarlari.view")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var setting = await _context.WebModuleSettings
            .AsNoTracking()
            .Where(setting => setting.Id == id)
            .Select(setting => new WebModuleSettingDto(
                setting.Id,
                setting.ModuleKey,
                setting.Key,
                setting.Value,
                setting.Description,
                setting.Category,
                setting.DataType,
                setting.EffectiveFrom,
                setting.EffectiveTo))
            .FirstOrDefaultAsync(ct);

        if (setting is null)
            return NotFound(new { error = "Web modül ayarı bulunamadı." });

        return Ok(setting);
    }

    [HttpGet("limits")]
    [RequirePermission("menu.web-modul-ayarlari.view")]
    public IActionResult GetLimits()
    {
        var limits = NumericLimits.ToDictionary(
            kvp => kvp.Key,
            kvp => new { min = kvp.Value.Min, max = kvp.Value.Max },
            StringComparer.OrdinalIgnoreCase);

        return Ok(limits);
    }

    [HttpGet("modules")]
    [RequirePermission("menu.web-modul-ayarlari.view")]
    public async Task<IActionResult> GetModuleControls(CancellationToken ct)
    {
        var summaries = await _controlService.GetSummariesAsync(ct);
        return Ok(summaries.Select(MapModuleSummary).ToList());
    }

    [HttpPut("modules/{code}")]
    [RequirePermission("menu.web-modul-ayarlari.edit")]
    public async Task<IActionResult> UpdateModuleControl(string code, [FromBody] UpdateWebModuleControlDto dto, CancellationToken ct)
    {
        var definition = _controlService.GetDefinition(code);
        if (definition is null)
            return NotFound(new { error = "Web modülü bulunamadı." });

        if (string.IsNullOrWhiteSpace(dto.Mode))
            return BadRequest(new { error = "Modül durumu boş olamaz." });

        var selectedOption = definition.Options.FirstOrDefault(option =>
            string.Equals(option.Code, dto.Mode.Trim(), StringComparison.OrdinalIgnoreCase));
        if (selectedOption is null)
        {
            return BadRequest(new
            {
                error = "Geçersiz modül durumu seçildi.",
                allowedModes = definition.Options.Select(option => option.Code).ToList(),
            });
        }

        var settings = await _context.WebModuleSettings
            .Where(setting => setting.ModuleKey == definition.SettingModuleKey
                && (setting.Key == definition.ModeKey || setting.Key == definition.AnnouncementKey))
            .ToListAsync(ct);

        var modeSetting = settings.FirstOrDefault(setting => setting.Key == definition.ModeKey);
        var announcementSetting = settings.FirstOrDefault(setting => setting.Key == definition.AnnouncementKey);

        if (modeSetting is null || announcementSetting is null)
            return NotFound(new { error = "Web modülüne ait ayar kayıtları eksik." });

        modeSetting.Value = selectedOption.Code;
        announcementSetting.Value = dto.Announcement?.Trim() ?? string.Empty;

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "Web modülü başka bir kullanıcı tarafından değiştirildi. Sayfayı yenileyip tekrar deneyin." });
        }

        _settingsProvider.InvalidateCache(modeSetting.ModuleKey, modeSetting.Key);
        _settingsProvider.InvalidateCache(announcementSetting.ModuleKey, announcementSetting.Key);

        var updatedSummary = (await _controlService.GetSummariesAsync(ct))
            .First(summary => string.Equals(summary.Code, code, StringComparison.OrdinalIgnoreCase));

        return Ok(MapModuleSummary(updatedSummary));
    }

    [HttpPut("{id:int}")]
    [RequirePermission("menu.web-modul-ayarlari.edit")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateWebModuleSettingDto dto, CancellationToken ct)
    {
        var setting = await _context.WebModuleSettings.FirstOrDefaultAsync(setting => setting.Id == id, ct);

        if (setting is null)
            return NotFound(new { error = "Web modül ayarı bulunamadı." });

        if (!TryNormalizeAndValidateValue(setting.ModuleKey, setting.Key, setting.DataType, dto.Value, out var normalizedValue, out var error))
            return BadRequest(new { error });

        if (!TryValidateEffectiveRange(dto.EffectiveFrom, dto.EffectiveTo, out error))
            return BadRequest(new { error });

        setting.Value = normalizedValue;
        setting.EffectiveFrom = dto.EffectiveFrom;
        setting.EffectiveTo = dto.EffectiveTo;

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "Bu ayar başka bir kullanıcı tarafından değiştirildi. Sayfayı yenileyip tekrar deneyin." });
        }

        _settingsProvider.InvalidateCache(setting.ModuleKey, setting.Key);

        return Ok(ToDto(setting));
    }

    [HttpGet("export")]
    [RequirePermission("menu.web-modul-ayarlari.export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var settings = await _context.WebModuleSettings
            .AsNoTracking()
            .OrderBy(setting => setting.ModuleKey)
            .ThenBy(setting => setting.Category)
            .ThenBy(setting => setting.Key)
            .Select(setting => new WebModuleSettingDto(
                setting.Id,
                setting.ModuleKey,
                setting.Key,
                setting.Value,
                setting.Description,
                setting.Category,
                setting.DataType,
                setting.EffectiveFrom,
                setting.EffectiveTo))
            .ToListAsync(ct);

        var json = JsonSerializer.Serialize(settings, ExportJsonOptions);
        var fileName = $"web-module-settings-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
        return File(Encoding.UTF8.GetBytes(json), "application/json", fileName);
    }

    [HttpPost("import")]
    [RequirePermission("menu.web-modul-ayarlari.import")]
    public async Task<IActionResult> Import([FromBody] List<WebModuleSettingImportItemDto>? items, CancellationToken ct)
    {
        if (items is null || items.Count == 0)
            return BadRequest(new { error = "İçe aktarım için en az bir ayar gönderilmelidir." });

        if (items.Any(item => string.IsNullOrWhiteSpace(item.ModuleKey) || string.IsNullOrWhiteSpace(item.Key)))
            return BadRequest(new { error = "Tüm ayar kayıtlarında geçerli modül ve anahtar bulunmalıdır." });

        var duplicateKeys = items
            .GroupBy(item => $"{item.ModuleKey.Trim()}:{item.Key.Trim()}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(key => key)
            .ToList();

        if (duplicateKeys.Count > 0)
            return BadRequest(new { error = $"İçe aktarılan JSON içinde tekrar eden ayarlar var: {string.Join(", ", duplicateKeys)}" });

        var moduleKeys = items
            .Select(item => item.ModuleKey.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var keys = items
            .Select(item => item.Key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var settings = await _context.WebModuleSettings
            .Where(setting => moduleKeys.Contains(setting.ModuleKey) && keys.Contains(setting.Key))
            .ToListAsync(ct);

        var settingsByCompositeKey = settings.ToDictionary(
            setting => $"{setting.ModuleKey}:{setting.Key}",
            StringComparer.OrdinalIgnoreCase);

        var missingKeys = items
            .Select(item => $"{item.ModuleKey.Trim()}:{item.Key.Trim()}")
            .Where(key => !settingsByCompositeKey.ContainsKey(key))
            .OrderBy(key => key)
            .ToList();

        if (missingKeys.Count > 0)
            return BadRequest(new { error = $"Bilinmeyen web modül ayarları bulundu: {string.Join(", ", missingKeys)}" });

        var updates = new List<(SIS.Domain.Entities.WebModuleSetting Setting, string NormalizedValue, DateTime? EffectiveFrom, DateTime? EffectiveTo)>();
        foreach (var item in items)
        {
            var compositeKey = $"{item.ModuleKey.Trim()}:{item.Key.Trim()}";
            var setting = settingsByCompositeKey[compositeKey];

            if (!TryNormalizeAndValidateValue(setting.ModuleKey, setting.Key, setting.DataType, item.Value, out var normalizedValue, out var error))
                return BadRequest(new { error = $"{setting.ModuleKey}.{setting.Key}: {error}" });

            if (!TryValidateEffectiveRange(item.EffectiveFrom, item.EffectiveTo, out error))
                return BadRequest(new { error = $"{setting.ModuleKey}.{setting.Key}: {error}" });

            updates.Add((setting, normalizedValue, item.EffectiveFrom, item.EffectiveTo));
        }

        var updatedCount = 0;
        foreach (var update in updates)
        {
            var changed = !string.Equals(update.Setting.Value, update.NormalizedValue, StringComparison.Ordinal)
                || update.Setting.EffectiveFrom != update.EffectiveFrom
                || update.Setting.EffectiveTo != update.EffectiveTo;

            if (!changed)
                continue;

            update.Setting.Value = update.NormalizedValue;
            update.Setting.EffectiveFrom = update.EffectiveFrom;
            update.Setting.EffectiveTo = update.EffectiveTo;
            updatedCount++;
        }

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "Bir veya daha fazla ayar başka bir kullanıcı tarafından değiştirildi. Sayfayı yenileyip tekrar deneyin." });
        }

        foreach (var update in updates)
            _settingsProvider.InvalidateCache(update.Setting.ModuleKey, update.Setting.Key);

        return Ok(new
        {
            updatedCount,
            processedCount = items.Count,
        });
    }

    private static WebModuleSettingDto ToDto(SIS.Domain.Entities.WebModuleSetting setting)
    {
        return new WebModuleSettingDto(
            setting.Id,
            setting.ModuleKey,
            setting.Key,
            setting.Value,
            setting.Description,
            setting.Category,
            setting.DataType,
            setting.EffectiveFrom,
            setting.EffectiveTo);
    }

    private static WebModuleControlSummaryDto MapModuleSummary(WebModuleControlSummary summary)
    {
        return new WebModuleControlSummaryDto(
            summary.Code,
            summary.Title,
            summary.Category,
            summary.Description,
            summary.DisplayOrder,
            summary.ModeSettingId,
            summary.AnnouncementSettingId,
            summary.SelectedMode,
            summary.SelectedModeLabel,
            summary.IsCurrentlyOpen,
            summary.EffectiveStateLabel,
            summary.EffectiveMessage,
            summary.Announcement,
            summary.Options.Select(option => new WebModuleControlOptionDto(
                option.Code,
                option.Label,
                option.Description)).ToList());
    }

    private static bool TryNormalizeAndValidateValue(
        string moduleKey,
        string key,
        string dataType,
        string? rawValue,
        out string normalizedValue,
        out string error)
    {
        normalizedValue = string.Empty;
        error = string.Empty;

        if (rawValue is null)
        {
            error = "Değer boş olamaz.";
            return false;
        }

        switch (dataType.Trim().ToLowerInvariant())
        {
            case "int":
                if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                {
                    error = "Geçerli bir tamsayı girilmelidir.";
                    return false;
                }

                if (!TryValidateNumericLimit(moduleKey, key, intValue, out error))
                    return false;

                normalizedValue = intValue.ToString(CultureInfo.InvariantCulture);
                return true;

            case "decimal":
                if (!decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
                {
                    error = "Ondalık değerler nokta (.) ile ve geçerli sayısal formatta girilmelidir.";
                    return false;
                }

                if (!TryValidateNumericLimit(moduleKey, key, decimalValue, out error))
                    return false;

                normalizedValue = decimalValue.ToString(CultureInfo.InvariantCulture);
                return true;

            case "bool":
                if (!TryParseBoolean(rawValue, out var boolValue))
                {
                    error = "Mantıksal değer olarak true/false, 1/0, yes/no veya on/off girilmelidir.";
                    return false;
                }

                normalizedValue = boolValue ? "true" : "false";
                return true;

            case "time":
                if (!TimeOnly.TryParseExact(rawValue.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    error = "Saat değeri HH:mm formatında girilmelidir (ör. 09:00, 13:30).";
                    return false;
                }

                normalizedValue = rawValue.Trim();
                return true;

            case "string":
                if (rawValue.Length > 500)
                {
                    error = "Metin değerleri en fazla 500 karakter olabilir.";
                    return false;
                }

                normalizedValue = rawValue;
                return true;

            case "json":
                try
                {
                    JsonDocument.Parse(rawValue);
                    normalizedValue = rawValue;
                    return true;
                }
                catch (JsonException)
                {
                    error = "Geçerli bir JSON değeri girilmelidir.";
                    return false;
                }

            default:
                error = $"Desteklenmeyen veri tipi: {dataType}";
                return false;
        }
    }

    private static bool TryValidateNumericLimit(string moduleKey, string key, decimal value, out string error)
    {
        error = string.Empty;
        var compositeKey = $"{moduleKey}.{key}";

        if (!NumericLimits.TryGetValue(compositeKey, out var limit))
            return true;

        if (limit.Min.HasValue && value < limit.Min.Value)
        {
            error = $"Değer en az {limit.Min.Value.ToString(CultureInfo.InvariantCulture)} olmalıdır.";
            return false;
        }

        if (limit.Max.HasValue && value > limit.Max.Value)
        {
            error = $"Değer en fazla {limit.Max.Value.ToString(CultureInfo.InvariantCulture)} olabilir.";
            return false;
        }

        return true;
    }

    private static bool TryValidateEffectiveRange(DateTime? effectiveFrom, DateTime? effectiveTo, out string error)
    {
        error = string.Empty;

        if (effectiveFrom.HasValue && effectiveTo.HasValue && effectiveFrom.Value >= effectiveTo.Value)
        {
            error = "Geçerlilik bitişi başlangıçtan sonra olmalıdır.";
            return false;
        }

        return true;
    }

    private static bool TryParseBoolean(string? value, out bool result)
    {
        result = false;

        var normalized = value?.Trim().ToLowerInvariant();
        if (normalized is null)
            return false;

        if (normalized is "true" or "1" or "yes" or "on")
        {
            result = true;
            return true;
        }

        if (normalized is "false" or "0" or "no" or "off")
        {
            result = false;
            return true;
        }

        return false;
    }
}
