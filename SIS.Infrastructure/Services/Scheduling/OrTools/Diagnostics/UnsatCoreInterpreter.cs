using Google.OrTools.Sat;
using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;

/// <summary>
/// CP-SAT UNSAT core'undan Türkçe sebep listesi üretir.
/// solver.SufficientAssumptionsForInfeasibility() literal indekslerini AssumptionRegistry üzerinden
/// metadata'ya, sonra constraint code'a göre human-readable mesaja çevirir.
///
/// Run 303'ün jenerik "Zamanlama kısıtlamaları nedeniyle atanamadı." mesajının çözümü.
/// </summary>
public sealed class UnsatCoreInterpreter
{
    private static readonly IReadOnlyDictionary<string, string> ConstraintNames = new Dictionary<string, string>
    {
        ["H1"] = "Derslik çakışması",
        ["H3"] = "Kapasite",
        ["H4"] = "Derslik tipi uyuşmazlığı",
        ["H4_L"] = "Lab derslik tipi uyuşmazlığı",
        ["H4_T"] = "Teori derslik tipi uyuşmazlığı (kapasite veya tip kuralı)",
        ["H5"] = "Müfredat çakışması",
        ["H6"] = "Aynı section'ın teori-lab çakışması",
        ["H8"] = "Aynı dersin farklı şubeleri çakışıyor",
        ["H10"] = "Fakülte derslik kısıtlaması",
        ["H11"] = "Eşkoşul",
        ["H13"] = "Öğle arası",
        ["H15"] = "Maksimum ardışık saat",
        ["H16"] = "Teori-lab gün ayrımı",
        ["H17"] = "Yüz-yüze ders online slot'a düşüyor"
    };

    /// <summary>
    /// solver.SufficientAssumptionsForInfeasibility() çıktısını Türkçe UnsatCoreEntry listesine çevirir.
    /// </summary>
    public IReadOnlyList<UnsatCoreEntry> Interpret(
        CpSolver solver,
        AssumptionRegistry registry,
        SolverRequest request)
    {
        var coreIndices = solver.SufficientAssumptionsForInfeasibility();
        var allLiterals = registry.AllLiterals;
        var sectionLookup = request.Sections.ToDictionary(s => s.Id, s => s);
        var entries = new List<UnsatCoreEntry>();

        foreach (var idx in coreIndices)
        {
            if (idx < 0 || idx >= allLiterals.Count) continue;
            var meta = registry.LookupByLiteral(allLiterals[idx]);
            if (meta == null) continue;

            var reason = BuildReason(meta, sectionLookup);
            entries.Add(new UnsatCoreEntry(
                meta.ConstraintCode,
                reason,
                meta.PrimarySectionId,
                meta.SecondarySectionId));
        }

        return entries;
    }

    /// <summary>
    /// Tek bir AssumptionMetadata için Türkçe sebep metni üretir.
    /// Public to enable unit testing without invoking CP-SAT.
    /// </summary>
    public string BuildReason(AssumptionMetadata meta, IReadOnlyDictionary<int, SectionInput> sectionLookup)
    {
        var name = ConstraintNames.GetValueOrDefault(meta.ConstraintCode, meta.ConstraintCode);

        var primaryDesc = DescribeId(meta.PrimarySectionId, sectionLookup);
        var secondaryDesc = DescribeId(meta.SecondarySectionId, sectionLookup);

        // Compose based on which IDs are present
        if (primaryDesc == null && secondaryDesc == null)
            return name;
        if (primaryDesc != null && secondaryDesc != null)
            return $"{name}: {primaryDesc} ile {secondaryDesc}";
        return $"{name}: {primaryDesc ?? secondaryDesc}";
    }

    /// <summary>
    /// Section ID'ye göre "§N (CourseCode)" formatı; section lookup'ta yoksa düz numerik fallback.
    /// </summary>
    private static string? DescribeId(int? id, IReadOnlyDictionary<int, SectionInput> sectionLookup)
    {
        if (!id.HasValue) return null;

        if (sectionLookup.TryGetValue(id.Value, out var section))
            return $"§{section.SectionNumber} ({section.CourseCode})";

        // Not a section ID (could be classroom, instructor, day, etc. depending on encoder)
        return id.Value.ToString();
    }
}
