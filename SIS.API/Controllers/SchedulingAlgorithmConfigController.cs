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
public record SchedulingAlgorithmConfigGroupDto(string Category, string DisplayName, string? Description, string? BadgeClass, int SortOrder);

/// <summary>
/// Dropdown'larda gösterilecek enum seçeneği. Disabled=true seçenekler UI'da görünür
/// ama seçilemez (örn. "Enrollment — henüz desteklenmiyor").
/// </summary>
public record EnumOptionDto(string Value, string Label, bool Disabled, string? HelpText);

/// <summary>
/// Otomatik programlama algoritmasını etkileyen ayarlar (kısıt eşikleri, soft constraint
/// katsayıları, eski V1 algoritma parametreleri). /ders-programi-kisitlamalari sayfasındaki
/// "Algoritma Parametreleri" sekmesinden yönetilir.
///
/// Tek tablo: SystemConfigs (2026-05-09 dual-table merge). Filter: <see cref="SchedulingCategories"/>
/// içindeki kategoriler. Solver da SystemConfigs'i okuduğu için admin değişikliği anında etkili olur.
/// </summary>
[ApiController]
[Route("api/admin/scheduling-algorithm-config")]
[Authorize]
public class SchedulingAlgorithmConfigController(AppDbContext context, ISystemConfigProvider configProvider) : ControllerBase
{
    private readonly AppDbContext _context = context;
    private readonly ISystemConfigProvider _configProvider = configProvider;

    // Bu panelde görünecek SystemConfigs.Category değerleri. Genel "Schedule" kategorisi
    // (örn. SlotDurationMinutes, MorningStartTime) sistem ayarları sayfasında ayrıca yönetilir.
    private static readonly HashSet<string> SchedulingCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "HardThreshold", "SA", "MRV", "LNS", "Other", "Soft"
    };

    private static readonly HashSet<string> TimeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Schedule.LunchBreakStart",
        "Schedule.LunchBreakEnd",
    };

    private static readonly Dictionary<string, (decimal? Min, decimal? Max)> NumericLimits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Classroom.AmphiThreshold"] = (0, 500),
        ["Schedule.MaxConsecutiveHours"] = (1, 12),

        ["Schedule.TheoryLabSplitThreshold"] = (1, 20),
        // Lab parçalama: min 2 (1 saçma; her saat farklı güne yayılır), max 6 (uzun bloklar
        // pratikte slot bulamaz; default 2).
        ["Schedule.MaxLabBlockHours"] = (2, 6),
        // V1 algoritma limit'leri (Schedule.SA.*, LNS.*, MRV.*, Construction.*, Candidate.*)
        // Plan 9 V1 silinmesi sonrası atıl olduğu için 2026-05-10'da migration ile kaldırıldı.
    };

    /// <summary>
    /// Sabit değer kümeli string parametreler için dropdown seçenekleri. Bu sözlükteki
    /// key'ler için PUT validasyonu sadece listedeki Value'lara izin verir (Disabled olanlar
    /// hariç). Frontend GET /enum-options ile bu listeyi çekip dropdown render eder.
    /// </summary>
    private static readonly Dictionary<string, EnumOptionDto[]> EnumOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Schedule.CurriculumConflictMode"] = new[]
        {
            new EnumOptionDto("cohort", "Cohort (önerilen)", false,
                "Section'ların alternatif olduğunu bilir, gerçek üniversite gibi çakışma kontrolü yapar."),
            new EnumOptionDto("curriculum", "Curriculum", false,
                "Section sayısını umursamaz, daha agresif. Çoğu durumda program oluşmaz (UNSAT)."),
            new EnumOptionDto("enrollment", "Enrollment (desteklenmiyor)", Disabled: true,
                "Gerçek kayıt verisinden çakışma matrisi. Encoder henüz tamamlanmadı, seçilirse program çöker."),
        },
    };

    // Eski SchedulingAlgorithmConfigGroups tablosu kaldırıldı (2026-05-09); UI metadatası burada hardcoded.
    private static readonly SchedulingAlgorithmConfigGroupDto[] Groups =
    [
        new("HardThreshold", "Paylaşılan Kısıt Eşikleri",
            "Birden fazla kısıdı etkileyen ortak eşikler (H4, H13, H15). Kısıda özel eşikler ilgili kısıt kartından düzenleniyor.",
            "bg-orange-100 text-orange-800 border-orange-200", 1),
        new("SA", "Ana Optimizasyon Motoru",
            "Programı iyileştiren ana motor — kaç deneme yapsın, ne kadar süre çalışsın, hangi hamlelere ağırlık versin.",
            "bg-blue-100 text-blue-800 border-blue-200", 2),
        new("MRV", "Çözüm Önceliği (Başlangıç)",
            "İlk programı kurarken hangi tür kısıtların öncelikli olduğunu belirler — uygun derslik az olan dersler önce, vb.",
            "bg-indigo-100 text-indigo-800 border-indigo-200", 3),
        new("LNS", "Sıkışma Çözümü (Yedek Motor)",
            "Ana motor takıldığında devreye giren yan motor — bir kerede kaç dersi yıkıp yeniden çözer.",
            "bg-emerald-100 text-emerald-800 border-emerald-200", 4),
        new("Other", "Genel Sistem Ayarları",
            "Tek bir kurala bağlı olmayan, sistem-geneli scheduling stratejisi parametreleri.",
            "bg-violet-100 text-violet-800 border-violet-200", 5),
        new("Soft", "Soft Constraint Katsayıları",
            "Soft kısıtların ağırlık ve eşik değerleri (örn. S20 komşu yıl çakışma cezası).",
            "bg-amber-100 text-amber-800 border-amber-200", 6),
    ];

    [HttpGet]
    [RequirePermission("menu.ders-programi-kisitlamalari.view")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var configs = await _context.SystemConfigs.AsNoTracking()
            .Where(c => SchedulingCategories.Contains(c.Category))
            .OrderBy(c => c.Category).ThenBy(c => c.Key)
            .Select(c => new SchedulingAlgorithmConfigDto(c.Id, c.Key, c.Value, c.Description, c.Category, c.DataType))
            .ToListAsync(ct);

        return Ok(configs);
    }

    [HttpGet("groups")]
    [RequirePermission("menu.ders-programi-kisitlamalari.view")]
    public IActionResult GetGroups() => Ok(Groups);

    [HttpGet("{id:int}")]
    [RequirePermission("menu.ders-programi-kisitlamalari.view")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var config = await _context.SystemConfigs.AsNoTracking()
            .Where(c => c.Id == id && SchedulingCategories.Contains(c.Category))
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

    /// <summary>
    /// Dropdown render edilecek string parametreler için izin verilen değer listesi.
    /// Frontend bu sözlükte key'i bulduğu config için Edit dialog'unu Select olarak açar.
    /// </summary>
    [HttpGet("enum-options")]
    [RequirePermission("menu.ders-programi-kisitlamalari.view")]
    public IActionResult GetEnumOptions() => Ok(EnumOptions);

    [HttpPut("{id:int}")]
    [RequirePermission("menu.ders-programi-kisitlamalari.edit")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSchedulingAlgorithmConfigDto dto, CancellationToken ct)
    {
        var config = await _context.SystemConfigs.FirstOrDefaultAsync(
            c => c.Id == id && SchedulingCategories.Contains(c.Category), ct);

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

            case "decimal":
            case "double":
                if (!decimal.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var decValue))
                {
                    error = "Geçerli bir ondalık sayı girilmelidir.";
                    return false;
                }
                if (NumericLimits.TryGetValue(key, out var decLimit))
                {
                    if (decLimit.Min.HasValue && decValue < decLimit.Min.Value)
                    {
                        error = $"Bu ayar en az {decLimit.Min} olmalıdır.";
                        return false;
                    }
                    if (decLimit.Max.HasValue && decValue > decLimit.Max.Value)
                    {
                        error = $"Bu ayar en fazla {decLimit.Max} olabilir.";
                        return false;
                    }
                }
                normalizedValue = decValue.ToString(CultureInfo.InvariantCulture);
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
                if (EnumOptions.TryGetValue(key, out var allowedOptions))
                {
                    var enumCandidate = rawValue.Trim();
                    var match = allowedOptions.FirstOrDefault(o =>
                        string.Equals(o.Value, enumCandidate, StringComparison.OrdinalIgnoreCase));
                    if (match is null)
                    {
                        var valid = string.Join(", ", allowedOptions.Where(o => !o.Disabled).Select(o => $"'{o.Value}'"));
                        error = $"Geçersiz değer. İzin verilen: {valid}.";
                        return false;
                    }
                    if (match.Disabled)
                    {
                        error = $"'{match.Value}' seçeneği şu an desteklenmiyor: {match.HelpText}";
                        return false;
                    }
                    normalizedValue = match.Value;
                    return true;
                }
                normalizedValue = rawValue.Trim();
                return true;

            default:
                error = $"Bilinmeyen veri tipi: {dataType}";
                return false;
        }
    }
}
