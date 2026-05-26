using System.Globalization;

namespace SIS.Application.Reports;

/// <summary>
/// Rapor parametrelerini (key→string ham değer) güvenli parse eden ortak yardımcılar.
/// Tüm IReportHandler'lar bunu kullanır (tekrar yok).
/// </summary>
public static class ReportParameterReader
{
    public static bool TryGetEnum<TEnum>(this IReadOnlyDictionary<string, string?> p, string key, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        return p.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw) && Enum.TryParse(raw, out value);
    }

    public static bool TryGetInt(this IReadOnlyDictionary<string, string?> p, string key, out int value)
    {
        value = 0;
        return p.TryGetValue(key, out var raw) && int.TryParse(raw, out value);
    }

    public static bool TryGetDecimal(this IReadOnlyDictionary<string, string?> p, string key, out decimal value)
    {
        value = 0m;
        return p.TryGetValue(key, out var raw)
            && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
