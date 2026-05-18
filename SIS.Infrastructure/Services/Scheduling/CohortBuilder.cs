using SIS.Application.Scheduling.Rules;

namespace SIS.Infrastructure.Services.Scheduling;

public sealed class CohortAssignment
{
    public Dictionary<int, int> SectionCohortMap { get; init; } = new();

    /// <summary>
    /// HARD çakışma çiftleri — aynı bölüm + aynı yıl ders çiftleri için bottleneck-aware
    /// modulo. Bu çakışmalar her zaman engellenir (öğrenci her ikisini de ZORUNLU alır).
    /// </summary>
    public HashSet<(int SectionId1, int SectionId2)> ConflictPairs { get; init; } = new();

    /// <summary>
    /// SOFT çakışma çiftleri — aynı bölüm + komşu yıl (|year_a - year_b| == 1) için
    /// SADECE modulo (bottleneck yok). Az sayıda öğrenci (kalan/önden alan) iki dersi
    /// birden alır; section seçim esnekliği var, bu yüzden TÜM pair'leri engellemek
    /// over-constrain olur. SOFT penalty olarak değerlendirilir.
    /// </summary>
    public HashSet<(int SectionId1, int SectionId2)> AdjacentYearSoftPairs { get; init; } = new();
}

/// <summary>
/// Cohort (DeptId, Year) gruplama + bottleneck-aware modulo pair üretici.
/// Build SADECE (SectionId, SectionNumber, CourseId, IsCompulsory) tuple'ına bağımlıdır — bu nedenle
/// hafif <see cref="CohortSectionRef"/> projeksiyonu kabul eder. Hem RuntimeCandidate
/// hem de CP-SAT SectionInput'tan üretilebilir.
///
/// <para>
/// <b>IsCompulsory</b> (2026-05-17): komşu yıl çakışma kuralının kapsamını belirler.
/// Yalnızca her iki tarafı da zorunlu olan ders çiftleri komşu yıl hard pair'ine girer
/// (seçmeli/önerilen dersler bu kuralın dışında). CurriculumCourse.IsCompulsory'den gelir
/// — bir ders birden çok müfredatta yer alıyorsa "en az birinde zorunlu" yeterli (heuristic).
/// </para>
/// </summary>
public sealed record CohortSectionRef(int SectionId, int SectionNumber, int CourseId, bool IsCompulsory);

public sealed class CohortBuilder
{
    public CohortAssignment Build(
        IReadOnlyList<CohortSectionRef> sections,
        Dictionary<int, Dictionary<int, int>> courseSemestersByDept,
        Dictionary<int, HashSet<int>> courseAccessibleDepts)
    {
        var sectionCohortMap = new Dictionary<int, int>();
        var conflictPairs = new HashSet<(int, int)>();
        var adjacentYearSoftPairs = new HashSet<(int, int)>();

        // 1. Group sections by (DeptId, Year)
        var deptYearGroups = new Dictionary<(int DeptId, int Year), List<CohortSectionRef>>();

        foreach (var section in sections)
        {
            if (!courseAccessibleDepts.TryGetValue(section.CourseId, out var depts))
                continue;

            foreach (var deptId in depts)
            {
                if (!courseSemestersByDept.TryGetValue(section.CourseId, out var deptSems))
                    continue;
                if (!deptSems.TryGetValue(deptId, out var semester) || semester <= 0)
                    continue;

                var year = (semester - 1) / 2;
                var key = (deptId, year);
                if (!deptYearGroups.ContainsKey(key))
                    deptYearGroups[key] = new List<CohortSectionRef>();
                deptYearGroups[key].Add(section);
            }
        }

        // 2. Conflict pair üretimi — pairwise + bottleneck propagation
        // Aynı (deptId, year) cohort içindeki ders çiftleri için.
        foreach (var (key, groupSections) in deptYearGroups)
        {
            var byCourse = groupSections
                .GroupBy(s => s.CourseId)
                .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SectionNumber).ToList());

            var courseIds = byCourse.Keys.ToList();

            // Her section için cohort index (gösterim amaçlı, gerçek conflict altta)
            foreach (var (courseId, courseSections) in byCourse)
            {
                for (var i = 0; i < courseSections.Count; i++)
                {
                    if (!sectionCohortMap.ContainsKey(courseSections[i].SectionId))
                        sectionCohortMap[courseSections[i].SectionId] = i;
                }
            }

            for (var ai = 0; ai < courseIds.Count; ai++)
            {
                for (var bi = ai + 1; bi < courseIds.Count; bi++)
                {
                    var sectionsA = byCourse[courseIds[ai]];
                    var sectionsB = byCourse[courseIds[bi]];
                    AddCoursePairConflicts(conflictPairs, sectionsA, sectionsB);
                }
            }
        }

        // 3. KOMŞU YIL: HARD pair üretimi (2026-05-17 güncelleme).
        // Aynı bölüm içinde |year_a - year_b| == 1 olan ZORUNLU ders çiftleri için aynı
        // section-aware bottleneck mantığı (AddCoursePairConflicts) uygulanır:
        //   - Bir öğrenci alttan/üstten ders alıyorsa, alacağı dersin EN AZ BİR section'ı
        //     diğer sınıfın zorunlu dersleriyle uyumlu olmalı (öğrencinin alabileceği bir
        //     yol kalmalı).
        //   - Pair-wise hard kural section'ların solver tarafından farklı slotlara
        //     dağıtılmasını zorlar; uyumsuz konfigürasyonlar zaten kural ihlali olur,
        //     joint encoding gerektirmez.
        //   - Seçmeli dersler dışarıda kalır — yalnızca her iki tarafı da
        //     <see cref="CohortSectionRef.IsCompulsory"/> = true olan çiftler kapsanır.
        // Eski davranış (sadece modulo SOFT penalty) terkedildi; kuralın "her zaman
        // ihlal edilebilir" yapısı pratikte yetersizdi (kullanıcı alttan dersi alamıyordu).
        var deptYearKeys = deptYearGroups.Keys.ToList();
        for (var ki = 0; ki < deptYearKeys.Count; ki++)
        {
            var keyA = deptYearKeys[ki];
            for (var kj = ki + 1; kj < deptYearKeys.Count; kj++)
            {
                var keyB = deptYearKeys[kj];
                if (keyA.DeptId != keyB.DeptId) continue;
                if (Math.Abs(keyA.Year - keyB.Year) != 1) continue;

                var byCourseA = deptYearGroups[keyA]
                    .GroupBy(s => s.CourseId)
                    .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SectionNumber).ToList());
                var byCourseB = deptYearGroups[keyB]
                    .GroupBy(s => s.CourseId)
                    .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SectionNumber).ToList());

                foreach (var (courseIdA, sectionsA) in byCourseA)
                {
                    // Bu ders zorunlu mu? CourseId aynı grupta birden fazla section taşır,
                    // hepsi aynı CourseId → IsCompulsory bayrağı aynıdır; ilkini al.
                    if (!sectionsA[0].IsCompulsory) continue;

                    foreach (var (courseIdB, sectionsB) in byCourseB)
                    {
                        if (courseIdA == courseIdB) continue; // aynı kurs iki cohort'a düşmüş
                        if (!sectionsB[0].IsCompulsory) continue; // seçmeli dersler hariç

                        AddCoursePairConflicts(conflictPairs, sectionsA, sectionsB);
                    }
                }
            }
        }

        return new CohortAssignment
        {
            SectionCohortMap = sectionCohortMap,
            ConflictPairs = conflictPairs,
            AdjacentYearSoftPairs = adjacentYearSoftPairs,
        };
    }

    /// <summary>
    /// Modulo-only pair üretir: sec_i ↔ sec_i, küçük olan section sayısı kadar.
    /// Bottleneck (TÜM pair) yok — single-section ders olsa bile sadece sec_1 pair'i.
    /// Komşu yıl SOFT penalty için kullanılır (öğrenci alternatif sec seçebilir).
    /// </summary>
    private static void AddModuloPairs(
        HashSet<(int, int)> set,
        List<CohortSectionRef> sectionsA,
        List<CohortSectionRef> sectionsB)
    {
        var pairCount = Math.Min(sectionsA.Count, sectionsB.Count);
        for (var i = 0; i < pairCount; i++)
        {
            AddPair(set, sectionsA[i].SectionId, sectionsB[i].SectionId);
        }
    }

    /// <summary>
    /// İki dersin section listeleri arasında bottleneck-aware modulo pair üretir:
    ///   • En az biri tek-section: TÜM (a, b) çiftleri (öğrenci alternatifsiz)
    ///   • Her ikisi multi-section: index'e göre eşleştir (sec_i ↔ sec_i),
    ///     öğrenci için en az bir uyumlu kombinasyon kalsın diye.
    /// </summary>
    private static void AddCoursePairConflicts(
        HashSet<(int, int)> conflictPairs,
        List<CohortSectionRef> sectionsA,
        List<CohortSectionRef> sectionsB)
    {
        if (sectionsA.Count == 1 || sectionsB.Count == 1)
        {
            foreach (var a in sectionsA)
            {
                foreach (var b in sectionsB)
                {
                    AddPair(conflictPairs, a.SectionId, b.SectionId);
                }
            }
        }
        else
        {
            var pairCount = Math.Min(sectionsA.Count, sectionsB.Count);
            for (var i = 0; i < pairCount; i++)
            {
                AddPair(conflictPairs, sectionsA[i].SectionId, sectionsB[i].SectionId);
            }
        }
    }

    private static void AddPair(HashSet<(int, int)> set, int idA, int idB)
    {
        if (idA == idB) return;
        var pair = idA < idB ? (idA, idB) : (idB, idA);
        set.Add(pair);
    }
}
