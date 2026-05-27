using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;

namespace SIS.Infrastructure.Services.Gpa;

/// <summary>
/// Tek GPA çekirdeği — ANO/AGNO, kredi/AKTS, eşdeğer ders zinciri ve "son not geçerli"
/// mantığının TEK kaynağı. Hem <see cref="GpaCalculationService"/> (Dönem Ortalamaları)
/// hem de StudentTranscriptService (Transkript) bu sınıftan okur; iki ayrı kopya hesap
/// (ikibaşlılık) bu sınıfla ortadan kaldırıldı.
///
/// Saf/stateless: yalnızca yüklenmiş entity'lerle çalışır (DB erişmez). Çağıran taraf
/// CourseSection.Course ve OpeningReasonSnapshot navigation'larını include etmelidir.
/// </summary>
public static class GpaMath
{
    /// <summary>
    /// Kayıt anında donan AKTS önceliklidir (grandfathering); yoksa dersin katalog AKTS'si.
    /// </summary>
    public static int GetEcts(CourseRegistration registration) =>
        registration.EctsSnapshot ?? registration.CourseSection.Course.Ects;

    /// <summary>
    /// Kayıt anında donan ulusal kredi önceliklidir; yoksa dersin katalog NationalCredit'i.
    /// (Eski "teori+lab" hesabı yerine sistemle uyumlu gerçek ulusal kredi.)
    /// </summary>
    public static int GetCredit(CourseRegistration registration) =>
        registration.CreditSnapshot ?? registration.CourseSection.Course.NationalCredit;

    /// <summary>
    /// Dersin GPA'ya katılıp katılmadığı: harf notunun bir katsayısı olmalı VE Açma Nedeni
    /// snapshot'ında IsExcludedFromGpa=true olmamalı (Tez/Staj/Seminer vb. akademik standart
    /// gereği ortalamaya girmez). Eski kayıtlarda snapshot null → mevcut davranış sürer.
    /// </summary>
    public static bool CountsTowardsGpa(CourseRegistration registration)
    {
        if (registration.Grade is not { } grade)
            return false;
        if (GradePointMap.GetPoint(grade) is null)
            return false;
        if (registration.OpeningReasonSnapshot?.IsExcludedFromGpa == true)
            return false;
        return true;
    }

    /// <summary>Geçer not mu (kredi kazanımı için).</summary>
    public static bool IsPassedGrade(GradeCode grade) =>
        grade != GradeCode.FF
        && grade != GradeCode.NA
        && grade != GradeCode.U
        && grade != GradeCode.W
        && grade != GradeCode.I;

    /// <summary>Bu kaydın GPA'ya giren AKTS'si (girmiyorsa 0).</summary>
    public static int GetGpaEcts(CourseRegistration registration) =>
        CountsTowardsGpa(registration) ? GetEcts(registration) : 0;

    /// <summary>Bu kaydın ağırlıklı (AKTS × katsayı) puanı (GPA dışıysa 0).</summary>
    public static decimal GetWeightedEcts(CourseRegistration registration)
    {
        if (!CountsTowardsGpa(registration))
            return 0m;

        var point = GradePointMap.GetPoint(registration.Grade!.Value)!.Value;
        return Math.Round(GetEcts(registration) * point, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Ortalama bileşenleri: (GPA'ya giren toplam AKTS, toplam ağırlıklı puan).</summary>
    public static (int TotalEcts, decimal TotalPoints) GetAverageComponents(
        IEnumerable<CourseRegistration> registrations)
    {
        var totalEcts = 0;
        decimal totalPoints = 0;

        foreach (var registration in registrations)
        {
            if (!CountsTowardsGpa(registration))
                continue;

            var ects = GetEcts(registration);
            if (ects <= 0)
                continue;

            var point = GradePointMap.GetPoint(registration.Grade!.Value)!.Value;
            totalEcts += ects;
            totalPoints += ects * point;
        }

        return (totalEcts, totalPoints);
    }

    /// <summary>
    /// Ortalama; GPA'ya giren ders yoksa <c>null</c> döner (yalnızca S/U/W gibi notlardan
    /// oluşan dönem → "veri yok", gerçek 0 değil).
    /// </summary>
    public static decimal? CalculateAverageOrNull(IEnumerable<CourseRegistration> registrations)
    {
        var (totalEcts, totalPoints) = GetAverageComponents(registrations);
        if (totalEcts == 0)
            return null;

        return Math.Round(totalPoints / totalEcts, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Ortalama; GPA'ya giren ders yoksa 0 döner (saklanan Cgpa/Sgpa için).</summary>
    public static decimal CalculateAverage(IEnumerable<CourseRegistration> registrations) =>
        CalculateAverageOrNull(registrations) ?? 0m;

    /// <summary>
    /// Her ders/eşdeğer zincirinden yalnızca SON kaydı tutar (son alınan not geçerli).
    /// Sıralama: dönem başlangıcı → dönem id → kayıt id (en yeni).
    /// </summary>
    public static List<CourseRegistration> GetLatestRegistrationsByChain(
        IEnumerable<CourseRegistration> registrations,
        IReadOnlyDictionary<int, int> courseChainIdByCourseId) =>
        registrations
            .GroupBy(registration => courseChainIdByCourseId.GetValueOrDefault(
                registration.CourseSection.CourseId,
                registration.CourseSection.CourseId))
            .Select(group => group
                .OrderByDescending(registration => registration.CourseSection.Term.TermStart)
                .ThenByDescending(registration => registration.CourseSection.Term.Id)
                .ThenByDescending(registration => registration.Id)
                .First())
            .ToList();

    /// <summary>
    /// Eşdeğerlik kayıtlarından (çift yönlü) zincir haritası kurar. Yalnızca verilen
    /// courseId kümesi içindeki eşdeğerlikler dikkate alınır. Çağıran taraf eşdeğerlikleri
    /// DB'den yükleyip verir (GpaMath DB'ye erişmez). Tek yön/çift yön farkını ortadan
    /// kaldırır — her iki servis de aynı çift yönlü mantığı kullanır.
    /// </summary>
    public static Dictionary<int, int> BuildCourseChainIdMap(
        IEnumerable<int> courseIds,
        IEnumerable<CourseEquivalency> equivalencies)
    {
        var relevantCourseIds = courseIds.Distinct().ToHashSet();
        var adjacency = relevantCourseIds.ToDictionary(courseId => courseId, _ => new HashSet<int>());

        foreach (var equivalency in equivalencies)
        {
            if (!relevantCourseIds.Contains(equivalency.CourseId)
                || !relevantCourseIds.Contains(equivalency.EquivalentCourseId))
            {
                continue;
            }

            adjacency[equivalency.CourseId].Add(equivalency.EquivalentCourseId);
            adjacency[equivalency.EquivalentCourseId].Add(equivalency.CourseId);
        }

        return BuildCourseChainIdMap(relevantCourseIds, adjacency);
    }

    /// <summary>
    /// Eşdeğer ders gruplarını birleşik bileşenlere indirger; her dersi bileşenin en küçük
    /// id'sine (chainId) eşler. Böylece eşdeğer dersler tek bir "zincir" gibi davranır.
    /// </summary>
    public static Dictionary<int, int> BuildCourseChainIdMap(
        IEnumerable<int> courseIds,
        IReadOnlyDictionary<int, HashSet<int>> equivalentCourseIdsByCourseId)
    {
        var distinctCourseIds = courseIds.Distinct().ToList();
        var map = distinctCourseIds.ToDictionary(courseId => courseId, courseId => courseId);
        var relevantCourseIds = distinctCourseIds.ToHashSet();
        var visited = new HashSet<int>();

        foreach (var courseId in distinctCourseIds)
        {
            if (!visited.Add(courseId))
                continue;

            var queue = new Queue<int>();
            var component = new List<int>();
            queue.Enqueue(courseId);

            while (queue.Count > 0)
            {
                var currentCourseId = queue.Dequeue();
                component.Add(currentCourseId);

                if (!equivalentCourseIdsByCourseId.TryGetValue(currentCourseId, out var equivalentIds))
                    continue;

                foreach (var equivalentCourseId in equivalentIds)
                {
                    if (!relevantCourseIds.Contains(equivalentCourseId) || !visited.Add(equivalentCourseId))
                        continue;

                    queue.Enqueue(equivalentCourseId);
                }
            }

            var chainId = component.Min();
            foreach (var componentCourseId in component)
            {
                map[componentCourseId] = chainId;
            }
        }

        return map;
    }
}
