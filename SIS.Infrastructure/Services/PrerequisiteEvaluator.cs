using SIS.Domain.Entities;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Önkoşul/eş-koşul (prerequisite/corequisite) karşılanma kuralının TEK kaynağı.
/// Daha önce aynı mantık RegistrationValidationService (Submit/validate yolu) ile
/// CourseRegistrationService.PlanAdd (plana ekle yolu) içinde AYRI AYRI ve TUTARSIZ
/// (intibak yalnız PlanAdd'de, corequisite hiçbirinde) uygulanıyordu. İki yol da artık
/// buradan geçer.
/// </summary>
internal static class PrerequisiteEvaluator
{
    /// <summary>
    /// <paramref name="requiredCourseId"/> gereksinimi karşılanmış mı?
    /// - Normal önkoşul: ders (veya intibaklı eski karşılığı / eşdeğeri) GEÇİLMİŞ olmalı.
    /// - Eş-koşul (corequisite): geçilmiş olması VEYA aynı dönemde birlikte alınıyor olması yeterli.
    /// </summary>
    public static bool IsSatisfied(
        int requiredCourseId,
        bool isCorequisite,
        IReadOnlySet<int> passedCourseIds,
        IReadOnlyDictionary<int, int> previousMap,
        IReadOnlyDictionary<int, HashSet<int>> equivalencyMap,
        IReadOnlySet<int> concurrentCourseIds)
    {
        // Doğrudan geçilmiş veya intibaklı eski dersi (PreviousCourse) geçilmiş.
        if (PreviousCourseResolver.IsCovered(requiredCourseId, passedCourseIds, previousMap))
            return true;

        // Eşdeğer (CourseEquivalency, simetrik) ders geçilmiş.
        if (equivalencyMap.TryGetValue(requiredCourseId, out var equivalents)
            && equivalents.Any(passedCourseIds.Contains))
            return true;

        // Eş-koşul: aynı dönemde birlikte alınıyorsa (sepet/aktif kayıt) yeterli.
        if (isCorequisite && concurrentCourseIds.Contains(requiredCourseId))
            return true;

        return false;
    }

    /// <summary>CourseEquivalency satırlarından simetrik (çift yönlü) eşdeğerlik haritası kurar.</summary>
    public static Dictionary<int, HashSet<int>> BuildEquivalencyMap(IEnumerable<CourseEquivalency> equivalencies)
    {
        var map = new Dictionary<int, HashSet<int>>();

        void Link(int key, int value)
        {
            if (!map.TryGetValue(key, out var set))
            {
                set = new HashSet<int>();
                map[key] = set;
            }
            set.Add(value);
        }

        foreach (var equivalency in equivalencies)
        {
            Link(equivalency.CourseId, equivalency.EquivalentCourseId);
            Link(equivalency.EquivalentCourseId, equivalency.CourseId);
        }

        return map;
    }
}
