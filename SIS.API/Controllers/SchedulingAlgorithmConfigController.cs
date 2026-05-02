using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Common.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

public record SchedulingAlgorithmConfigDto(int Id, string Key, string Value, string? Description, string Category, string DataType);
public record UpdateSchedulingAlgorithmConfigDto(string Value);

/// <summary>
/// Otomatik programlama algoritmasını etkileyen ayarlar (SA, MRV, LNS, kısıt eşikleri).
/// /ders-programi-kisitlamalari sayfasındaki "Algoritma Parametreleri" sekmesinden yönetilir.
/// SystemConfig'den ayrı bir tablo (SchedulingAlgorithmConfigs) — domain ayrımı için.
/// </summary>
[ApiController]
[Route("api/admin/scheduling-algorithm-config")]
[Authorize]
public class SchedulingAlgorithmConfigController(AppDbContext context, ISystemConfigProvider configProvider) : ControllerBase
{
    private readonly AppDbContext _context = context;
    private readonly ISystemConfigProvider _configProvider = configProvider;

    // HH:mm formatında saat değeri bekleyen string key'ler.
    private static readonly HashSet<string> TimeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Schedule.LunchBreakStart",
        "Schedule.LunchBreakEnd",
    };

    private static readonly Dictionary<string, (decimal? Min, decimal? Max)> NumericLimits = new(StringComparer.OrdinalIgnoreCase)
    {
        // Hard / Soft thresholds
        ["Classroom.AmphiThreshold"] = (0, 500),
        ["Classroom.OccupancyTarget.Min"] = (0, 100),
        ["Classroom.OccupancyTarget.Max"] = (0, 100),
        ["SoftConstraint.S2.MaxDailyHours"] = (0, 24),
        ["SoftConstraint.S3.MaxSectionsPerSlot"] = (0, 100),
        ["SoftConstraint.S4.MinCoPairThreshold"] = (0, 1000),
        ["SoftConstraint.S7.MaxGapSlots"] = (0, 20),
        ["Schedule.MaxConsecutiveHours"] = (1, 12),

        // Other
        ["Scheduling.MaxBacktrackingDepth"] = (0, 20),
        ["Schedule.TheoryLabSplitThreshold"] = (1, 20),

        // Simulated Annealing
        ["Schedule.SA.MaxIterations"] = (1, 1_000_000),
        ["Schedule.SA.InitialTemperature"] = (1, 10_000),
        ["Schedule.SA.TimeoutSeconds"] = (5, 3600),
        ["Schedule.SA.StagnationLimit"] = (1, 100_000),
        ["Schedule.SA.BacktrackLimit"] = (1, 10_000),
        ["Schedule.SA.RelocateWeight"] = (0, 100),
        ["Schedule.SA.SwapWeight"] = (0, 100),
        ["Schedule.SA.ChainWeight"] = (0, 100),

        // LNS
        ["Schedule.LNS.MaxIterations"] = (1, 100_000),
        ["Schedule.LNS.TimeoutSeconds"] = (5, 600),
        ["Schedule.LNS.DestroySize"] = (1, 50),

        // MRV
        ["Schedule.MRV.ClassroomWeight"] = (0, 100),
        ["Schedule.MRV.SlotWeight"] = (0, 100),
        ["Schedule.MRV.LabWeight"] = (0, 100),
        ["Schedule.MRV.CapacityWeight"] = (0, 100),
    };

    [HttpGet]
    [RequirePermission("menu.ders-programi-kisitlamalari.view")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var configs = await _context.SchedulingAlgorithmConfigs.AsNoTracking()
            .OrderBy(c => c.Category).ThenBy(c => c.Key)
            .Select(c => new SchedulingAlgorithmConfigDto(c.Id, c.Key, c.Value, c.Description, c.Category, c.DataType))
            .ToListAsync(ct);

        return Ok(configs);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("menu.ders-programi-kisitlamalari.view")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var config = await _context.SchedulingAlgorithmConfigs.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new SchedulingAlgorithmConfigDto(c.Id, c.Key, c.Value, c.Description, c.Category, c.DataType))
            .FirstOrDefaultAsync(ct);

        if (config == null)
            return NotFound(new { error = "Algoritma ayarı bulunamadı." });

        return Ok(config);
    }

    [HttpGet("limits")]
    [RequirePermission("menu.ders-programi-kisitlamalari.view")]
    public IActionResult GetLimits()
    {
        var limits = NumericLimits.ToDictionary(
            kvp => kvp.Key,
            kvp => new { min = kvp.Value.Min, max = kvp.Value.Max },
            StringComparer.OrdinalIgnoreCase);

        return Ok(limits);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("menu.ders-programi-kisitlamalari.edit")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSchedulingAlgorithmConfigDto dto, CancellationToken ct)
    {
        var config = await _context.SchedulingAlgorithmConfigs.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (config == null)
            return NotFound(new { error = "Algoritma ayarı bulunamadı." });

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

        // SystemConfig provider'ı invalidate edelim — bu key'ler farklı tabloda olsa da
        // cache'i paylaşıyor olabilir.
        _configProvider.InvalidateCache(config.Key);

        return Ok(new SchedulingAlgorithmConfigDto(config.Id, config.Key, config.Value, config.Description, config.Category, config.DataType));
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
                if (NumericLimits.TryGetValue(key, out var intLimit))
                {
                    if (intLimit.Min.HasValue && intValue < intLimit.Min.Value)
                    {
                        error = $"Bu ayar en az {intLimit.Min} olmalıdır.";
                        return false;
                    }
                    if (intLimit.Max.HasValue && intValue > intLimit.Max.Value)
                    {
                        error = $"Bu ayar en fazla {intLimit.Max} olabilir.";
                        return false;
                    }
                }
                normalizedValue = intValue.ToString(CultureInfo.InvariantCulture);
                return true;

            case "bool":
                var trimmed = rawValue.Trim().ToLowerInvariant();
                var boolValue = trimmed switch
                {
                    "true" or "1" or "yes" or "on" => (bool?)true,
                    "false" or "0" or "no" or "off" => (bool?)false,
                    _ => null,
                };
                if (boolValue is null)
                {
                    error = "Mantıksal değer olarak true/false, 1/0, yes/no veya on/off girilmelidir.";
                    return false;
                }
                normalizedValue = boolValue.Value ? "true" : "false";
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
                        error = "Saat değeri HH:mm formatında girilmelidir (ör. 11:40, 12:20).";
                        return false;
                    }
                }
                normalizedValue = rawValue.Trim();
                return true;

            default:
                error = $"Bilinmeyen veri tipi: {dataType}";
                return false;
        }
    }
}
