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

public record SystemConfigDto(int Id, string Key, string Value, string? Description, string Category, string DataType);
public record UpdateSystemConfigDto(string Value);
public record SystemConfigImportItemDto(string Key, string Value);

[ApiController]
[Route("api/admin/system-config")]
[Authorize]
public class SystemConfigController(AppDbContext context, ISystemConfigProvider configProvider) : ControllerBase
{
    private readonly AppDbContext _context = context;
    private readonly ISystemConfigProvider _configProvider = configProvider;

    private static readonly Dictionary<string, (decimal? Min, decimal? Max)> NumericLimits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Registration.EctsLimit.CgpaBelow200"] = (0, 60),
        ["Registration.EctsLimit.Cgpa200To249"] = (0, 60),
        ["Registration.EctsLimit.CgpaAbove250"] = (0, 60),
        ["Audit.RetentionDays"] = (30, 3650),
        ["Security.PasswordRotationMonths"] = (1, 24),
        ["GPA.RetentionThreshold"] = (0, 4),
        ["Withdrawal.MaxCount.Undergraduate"] = (0, 20),
        ["Withdrawal.MaxCount.Associate"] = (0, 20),
        ["Instructor.MaxWeeklyHours.Professor"] = (0, 60),
        ["Instructor.MaxWeeklyHours.AssociateProfessor"] = (0, 60),
        ["Instructor.MaxWeeklyHours.AssistantProfessor"] = (0, 60),
        ["Instructor.MaxWeeklyHours.Lecturer"] = (0, 60),
        ["Classroom.AmphiThreshold"] = (0, 500),
        ["Classroom.OccupancyTarget.Min"] = (0, 100),
        ["Classroom.OccupancyTarget.Max"] = (0, 100),
        ["SoftConstraint.S2.MaxDailyHours"] = (0, 24),
        ["SoftConstraint.S3.MaxSectionsPerSlot"] = (0, 100),
        ["SoftConstraint.S4.MinCoPairThreshold"] = (0, 1000),
        ["SoftConstraint.S7.MaxGapSlots"] = (0, 20),
        ["SoftConstraint.Weight.S1"] = (0, 10),
        ["SoftConstraint.Weight.S2"] = (0, 10),
        ["SoftConstraint.Weight.S3"] = (0, 10),
        ["SoftConstraint.Weight.S4"] = (0, 10),
        ["SoftConstraint.Weight.S5"] = (0, 10),
        ["SoftConstraint.Weight.S7"] = (0, 10),
        ["SoftConstraint.Weight.S8"] = (0, 10),
        ["SoftConstraint.Weight.S9"] = (0, 10),
        ["SoftConstraint.Weight.S12"] = (0, 10),
        ["Scheduling.MaxBacktrackingDepth"] = (0, 20),
        ["Schedule.MaxConsecutiveHours"] = (1, 12),
        ["Schedule.SlotDurationMinutes"] = (30, 180),
        ["Schedule.BreakBetweenSlotsMinutes"] = (0, 60),
        ["Schedule.EveningSlotCount"] = (0, 10),
        ["Schedule.TheoryLabSplitThreshold"] = (1, 20),
        ["Schedule.SA.MaxIterations"] = (1, 1_000_000),
        ["Schedule.SA.InitialTemperature"] = (1, 10_000),
        ["Schedule.SA.TimeoutSeconds"] = (5, 3600),
        ["Schedule.SA.StagnationLimit"] = (1, 100_000),
        ["Schedule.SA.BacktrackLimit"] = (1, 10_000),
        ["Schedule.SA.RelocateWeight"] = (0, 100),
        ["Schedule.SA.SwapWeight"] = (0, 100),
        ["Schedule.SA.ChainWeight"] = (0, 100),
        ["Schedule.MRV.ClassroomWeight"] = (0, 100),
        ["Schedule.MRV.SlotWeight"] = (0, 100),
        ["Schedule.MRV.LabWeight"] = (0, 100),
        ["Schedule.MRV.CapacityWeight"] = (0, 100),
        ["Attendance.TermWeekCount"] = (1, 52),
        ["Attendance.TheoryAbsenceLimitPercent"] = (0, 100),
        ["Attendance.LabAbsenceLimitPercent"] = (0, 100),
    };

    private static readonly HashSet<string> TimeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Schedule.MorningStartTime",
        "Schedule.LunchBreakStart",
        "Schedule.LunchBreakEnd",
        "Schedule.EveningStartTime",
    };

    private static readonly HashSet<string> DayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday",
    };

    private static readonly JsonSerializerOptions ExportJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private const NumberStyles DecimalStyle = NumberStyles.AllowLeadingWhite
        | NumberStyles.AllowTrailingWhite
        | NumberStyles.AllowLeadingSign
        | NumberStyles.AllowDecimalPoint;

    [HttpGet]
    [RequirePermission("menu.system-config.view")]
    public async Task<IActionResult> GetAll([FromQuery] string? category, CancellationToken ct)
    {
        var query = _context.SystemConfigs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(c => c.Category == category);

        var configs = await query
            .OrderBy(c => c.Category).ThenBy(c => c.Key)
            .Select(c => new SystemConfigDto(c.Id, c.Key, c.Value, c.Description, c.Category, c.DataType))
            .ToListAsync(ct);

        return Ok(configs);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("menu.system-config.view")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var config = await _context.SystemConfigs.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new SystemConfigDto(c.Id, c.Key, c.Value, c.Description, c.Category, c.DataType))
            .FirstOrDefaultAsync(ct);

        if (config == null)
            return NotFound(new { error = "Konfigürasyon bulunamadı." });

        return Ok(config);
    }

    [HttpGet("limits")]
    [RequirePermission("menu.system-config.view")]
    public IActionResult GetLimits()
    {
        var limits = NumericLimits.ToDictionary(
            kvp => kvp.Key,
            kvp => new { min = kvp.Value.Min, max = kvp.Value.Max },
            StringComparer.OrdinalIgnoreCase);

        return Ok(limits);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("menu.system-config.edit")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSystemConfigDto dto, CancellationToken ct)
    {
        var config = await _context.SystemConfigs.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (config == null)
            return NotFound(new { error = "Konfigürasyon bulunamadı." });

        if (!TryNormalizeAndValidateValue(config.Key, config.DataType, dto.Value, out var normalizedValue, out var error))
            return BadRequest(new { error });

        config.Value = normalizedValue;

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "Bu ayar başka bir kullanıcı tarafından değiştirildi. Sayfayı yenileyip tekrar deneyin." });
        }

        _configProvider.InvalidateCache(config.Key);

        return Ok(new SystemConfigDto(config.Id, config.Key, config.Value, config.Description, config.Category, config.DataType));
    }

    [HttpGet("export")]
    [RequirePermission("menu.system-config.export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var configs = await _context.SystemConfigs
            .AsNoTracking()
            .OrderBy(c => c.Category)
            .ThenBy(c => c.Key)
            .Select(c => new SystemConfigDto(c.Id, c.Key, c.Value, c.Description, c.Category, c.DataType))
            .ToListAsync(ct);

        var json = JsonSerializer.Serialize(configs, ExportJsonOptions);
        var fileName = $"system-config-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
        return File(Encoding.UTF8.GetBytes(json), "application/json", fileName);
    }

    [HttpPost("import")]
    [RequirePermission("menu.system-config.import")]
    public async Task<IActionResult> Import([FromBody] List<SystemConfigImportItemDto>? items, CancellationToken ct)
    {
        if (items is null || items.Count == 0)
            return BadRequest(new { error = "İçe aktarım için en az bir ayar gönderilmelidir." });

        if (items.Any(item => string.IsNullOrWhiteSpace(item.Key)))
            return BadRequest(new { error = "Tüm ayar kayıtlarında geçerli bir anahtar bulunmalıdır." });

        var duplicateKeys = items
            .GroupBy(item => item.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(key => key)
            .ToList();

        if (duplicateKeys.Count > 0)
            return BadRequest(new { error = $"İçe aktarılan JSON içinde tekrar eden anahtarlar var: {string.Join(", ", duplicateKeys)}" });

        var requestedKeys = items
            .Select(item => item.Key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var configs = await _context.SystemConfigs
            .Where(config => requestedKeys.Contains(config.Key))
            .ToListAsync(ct);

        var configByKey = configs.ToDictionary(config => config.Key, StringComparer.OrdinalIgnoreCase);
        var missingKeys = requestedKeys
            .Where(key => !configByKey.ContainsKey(key))
            .OrderBy(key => key)
            .ToList();

        if (missingKeys.Count > 0)
            return BadRequest(new { error = $"Bilinmeyen konfigürasyon anahtarları bulundu: {string.Join(", ", missingKeys)}" });

        var updates = new List<(SIS.Domain.Entities.SystemConfig Config, string NormalizedValue)>();
        foreach (var item in items)
        {
            var key = item.Key.Trim();
            var config = configByKey[key];

            if (!TryNormalizeAndValidateValue(config.Key, config.DataType, item.Value, out var normalizedValue, out var error))
                return BadRequest(new { error = $"{config.Key}: {error}" });

            updates.Add((config, normalizedValue));
        }

        var updatedKeys = new List<string>();
        foreach (var update in updates)
        {
            if (string.Equals(update.Config.Value, update.NormalizedValue, StringComparison.Ordinal))
                continue;

            update.Config.Value = update.NormalizedValue;
            updatedKeys.Add(update.Config.Key);
        }

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "Bir veya daha fazla ayar başka bir kullanıcı tarafından değiştirildi. Sayfayı yenileyip tekrar deneyin." });
        }

        foreach (var key in updatedKeys)
            _configProvider.InvalidateCache(key);

        return Ok(new
        {
            updatedCount = updatedKeys.Count,
            processedCount = items.Count,
        });
    }

    private static bool TryNormalizeAndValidateValue(
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

                if (!TryValidateNumericLimit(key, intValue, out error))
                    return false;

                normalizedValue = intValue.ToString(CultureInfo.InvariantCulture);
                return true;

            case "decimal":
                if (!decimal.TryParse(rawValue, DecimalStyle, CultureInfo.InvariantCulture, out var decimalValue))
                {
                    error = "Ondalık değerler nokta (.) ile ve geçerli sayısal formatta girilmelidir.";
                    return false;
                }

                if (!TryValidateNumericLimit(key, decimalValue, out error))
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

            case "string":
                if (rawValue.Length > 500)
                {
                    error = "Metin değerleri en fazla 500 karakter olabilir.";
                    return false;
                }

                if (TimeKeys.Contains(key))
                {
                    if (!TimeOnly.TryParseExact(rawValue.Trim(), "HH:mm", CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                    {
                        error = "Saat değeri HH:mm formatında girilmelidir (ör. 09:00, 13:30).";
                        return false;
                    }

                    normalizedValue = rawValue.Trim();
                    return true;
                }

                if (string.Equals(key, "Schedule.WorkDays", StringComparison.OrdinalIgnoreCase))
                {
                    var days = rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (days.Length == 0)
                    {
                        error = "En az bir çalışma günü belirtilmelidir.";
                        return false;
                    }

                    var invalidDays = days.Where(d => !DayNames.Contains(d)).ToList();
                    if (invalidDays.Count > 0)
                    {
                        error = $"Geçersiz gün adları: {string.Join(", ", invalidDays)}. Geçerli değerler: Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday";
                        return false;
                    }

                    normalizedValue = string.Join(",", days);
                    return true;
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

    private static bool TryValidateNumericLimit(string key, decimal value, out string error)
    {
        error = string.Empty;

        if (!NumericLimits.TryGetValue(key, out var limit))
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
