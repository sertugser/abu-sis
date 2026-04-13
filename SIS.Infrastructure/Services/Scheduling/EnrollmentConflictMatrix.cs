using Microsoft.EntityFrameworkCore;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling;

public sealed class EnrollmentConflictMatrix
{
    public HashSet<(int SectionId1, int SectionId2)> HardConflictPairs { get; init; } = new();
    public Dictionary<(int SectionId1, int SectionId2), int> SoftConflictPairs { get; init; } = new();

    public static async Task<EnrollmentConflictMatrix> BuildAsync(
        AppDbContext context,
        int termId,
        IEnumerable<int> sectionIds,
        int hardThreshold,
        CancellationToken ct = default)
    {
        var sectionIdSet = sectionIds.ToHashSet();

        var registrations = await context.CourseRegistrations
            .AsNoTracking()
            .Where(cr => cr.Status == RegistrationStatus.Active
                && sectionIdSet.Contains(cr.CourseSectionId))
            .Select(cr => new { cr.StudentId, cr.CourseSectionId })
            .ToListAsync(ct);

        var studentSections = registrations
            .GroupBy(r => r.StudentId)
            .Where(g => g.Count() > 1)
            .ToDictionary(g => g.Key, g => g.Select(r => r.CourseSectionId).Distinct().OrderBy(x => x).ToList());

        var pairCounts = new Dictionary<(int, int), int>();

        foreach (var (_, sections) in studentSections)
        {
            for (var i = 0; i < sections.Count; i++)
            {
                for (var j = i + 1; j < sections.Count; j++)
                {
                    var pair = (sections[i], sections[j]);
                    pairCounts[pair] = pairCounts.GetValueOrDefault(pair) + 1;
                }
            }
        }

        var hard = new HashSet<(int, int)>();
        var soft = new Dictionary<(int, int), int>();

        foreach (var (pair, count) in pairCounts)
        {
            if (count >= hardThreshold)
                hard.Add(pair);
            else
                soft[pair] = count;
        }

        return new EnrollmentConflictMatrix
        {
            HardConflictPairs = hard,
            SoftConflictPairs = soft
        };
    }
}
