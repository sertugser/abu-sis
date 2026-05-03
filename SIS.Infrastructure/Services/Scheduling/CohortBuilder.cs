namespace SIS.Infrastructure.Services.Scheduling;

public sealed class CohortAssignment
{
    public Dictionary<int, int> SectionCohortMap { get; init; } = new();
    public HashSet<(int SectionId1, int SectionId2)> ConflictPairs { get; init; } = new();
}

public sealed class CohortBuilder
{
    public CohortAssignment Build(
        IReadOnlyList<SectionSnapshot> sections,
        Dictionary<int, Dictionary<int, int>> courseSemestersByDept,
        Dictionary<int, HashSet<int>> courseAccessibleDepts)
    {
        var sectionCohortMap = new Dictionary<int, int>();
        var conflictPairs = new HashSet<(int, int)>();

        // 1. Group sections by (DeptId, Year)
        var deptYearGroups = new Dictionary<(int DeptId, int Year), List<SectionSnapshot>>();

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
                    deptYearGroups[key] = new List<SectionSnapshot>();
                deptYearGroups[key].Add(section);
            }
        }

        // 2. Conflict pair üretimi — pairwise + bottleneck propagation
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

            // Her DERS ÇİFTİ için section-level conflict pair üret:
            //   • Bottleneck case (en az birinde 1 sec): TÜM (a, b) çiftleri çakışmamalı
            //     (öğrenci az olan ders için alternatifsiz, çoklu olanın HER section'ıyla
            //      uyumlu zaman gerekli)
            //   • Çoklu-çoklu case: index'e göre eşleştir (sec_i ↔ sec_i çakışmasın)
            //     Bu pairwise minimum — öğrenci için en az bir uyumlu kombinasyon kalır
            for (var ai = 0; ai < courseIds.Count; ai++)
            {
                for (var bi = ai + 1; bi < courseIds.Count; bi++)
                {
                    var sectionsA = byCourse[courseIds[ai]];
                    var sectionsB = byCourse[courseIds[bi]];

                    if (sectionsA.Count == 1 || sectionsB.Count == 1)
                    {
                        // Bottleneck: tek section'lı ders varsa TÜM çiftler çakışmasın
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
                        // Çoklu-çoklu: index'e göre eşleştir (cohort 0=sec1 ↔ cohort 0=sec1)
                        // Sec sayıları farklı olabilir — küçük olan kadar pair üret
                        var pairCount = Math.Min(sectionsA.Count, sectionsB.Count);
                        for (var i = 0; i < pairCount; i++)
                        {
                            AddPair(conflictPairs, sectionsA[i].SectionId, sectionsB[i].SectionId);
                        }
                    }
                }
            }
        }

        return new CohortAssignment
        {
            SectionCohortMap = sectionCohortMap,
            ConflictPairs = conflictPairs
        };
    }

    private static void AddPair(HashSet<(int, int)> set, int idA, int idB)
    {
        if (idA == idB) return;
        var pair = idA < idB ? (idA, idB) : (idB, idA);
        set.Add(pair);
    }
}
