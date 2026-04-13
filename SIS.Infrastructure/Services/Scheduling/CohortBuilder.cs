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

        // 2. Assign cohorts per group
        foreach (var (key, groupSections) in deptYearGroups)
        {
            var byCourse = groupSections
                .GroupBy(s => s.CourseId)
                .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SectionNumber).ToList());

            var maxSections = byCourse.Values.Max(list => list.Count);

            foreach (var (courseId, courseSections) in byCourse)
            {
                for (var i = 0; i < courseSections.Count; i++)
                {
                    var cohortIndex = i % maxSections;
                    if (!sectionCohortMap.ContainsKey(courseSections[i].SectionId))
                        sectionCohortMap[courseSections[i].SectionId] = cohortIndex;
                }
            }

            // 3. Build conflict pairs from same-cohort sections
            var cohortSections = groupSections
                .Where(s => sectionCohortMap.ContainsKey(s.SectionId))
                .GroupBy(s => sectionCohortMap[s.SectionId])
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var (cohort, cohortMembers) in cohortSections)
            {
                for (var i = 0; i < cohortMembers.Count; i++)
                {
                    for (var j = i + 1; j < cohortMembers.Count; j++)
                    {
                        if (cohortMembers[i].CourseId == cohortMembers[j].CourseId)
                            continue;

                        var pair = cohortMembers[i].SectionId < cohortMembers[j].SectionId
                            ? (cohortMembers[i].SectionId, cohortMembers[j].SectionId)
                            : (cohortMembers[j].SectionId, cohortMembers[i].SectionId);
                        conflictPairs.Add(pair);
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
}
