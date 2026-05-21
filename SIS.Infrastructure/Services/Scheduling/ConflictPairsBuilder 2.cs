using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Rules;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling;

/// <summary>
/// Tek otorite müfredat çakışma çift üretici.
///
/// Mode'lar (Schedule.CurriculumConflictMode config):
///   - "curriculum" : eski agresif (Cohort/Enrollment veri yok → H5 ders-bazlı kontrol yapar)
///   - "cohort"     : (Dept, Year) cohort + adjacent year, bottleneck-aware modulo
///   - "enrollment" : gerçek öğrenci kayıt verisinden çakışma matrisi
///
/// AutoScheduleEngine, manuel atama (SchedulingService.Snapshot) ve Excel import gibi tüm
/// akışlar bu builder'ı çağırır → mode değiştiğinde tek yer güncellenir.
/// </summary>
public static class ConflictPairsBuilder
{
    /// <summary>
    /// Verilen mode'a göre çakışma ön-yükleme verisini üretir. RuntimeSnapshot inşaası
    /// sırasında <see cref="ConflictPairsResult"/> RuntimeSnapshot init alanlarına geçirilir
    /// (immutable record — mutation yok).
    /// </summary>
    public static async Task<ConflictPairsResult> BuildAsync(
        string mode,
        IReadOnlyList<CohortSectionRef> sections,
        Dictionary<int, Dictionary<int, int>> courseSemestersByDept,
        Dictionary<int, HashSet<int>> courseAccessibleDepts,
        int enrollmentThreshold,
        AppDbContext context,
        int termId,
        CancellationToken ct = default)
    {
        switch (mode)
        {
            case "cohort":
            {
                var cohortBuilder = new CohortBuilder();
                var assignment = cohortBuilder.Build(sections, courseSemestersByDept, courseAccessibleDepts);
                return new ConflictPairsResult(
                    CohortConflictPairs: assignment.ConflictPairs,
                    AdjacentYearSoftPairs: assignment.AdjacentYearSoftPairs,
                    EnrollmentConflicts: null);
            }
            case "enrollment":
            {
                var sectionIds = sections.Select(s => s.SectionId);
                var matrix = await BuildEnrollmentMatrixAsync(context, termId, sectionIds, enrollmentThreshold, ct);
                return new ConflictPairsResult(
                    CohortConflictPairs: null,
                    AdjacentYearSoftPairs: null,
                    EnrollmentConflicts: matrix);
            }
            default:
                // "curriculum" veya bilinmeyen → evaluator default davranışına düşer.
                return ConflictPairsResult.Empty;
        }
    }

    /// <summary>
    /// "enrollment" mode için gerçek öğrenci kayıt verisinden hard çakışma matrisi üretir.
    /// Hard threshold: bir section çiftini en az bu kadar ortak öğrenci alıyorsa pair hard.
    /// </summary>
    private static async Task<EnrollmentConflictMatrix> BuildEnrollmentMatrixAsync(
        AppDbContext context,
        int termId,
        IEnumerable<int> sectionIds,
        int hardThreshold,
        CancellationToken ct)
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
        foreach (var (pair, count) in pairCounts)
        {
            if (count >= hardThreshold)
                hard.Add(pair);
        }

        return new EnrollmentConflictMatrix
        {
            HardConflictPairs = hard,
        };
    }
}

/// <summary>
/// ConflictPairsBuilder.BuildAsync sonucu. RuntimeSnapshot init alanlarına doğrudan geçirilir.
/// </summary>
public sealed record ConflictPairsResult(
    HashSet<(int, int)>? CohortConflictPairs,
    HashSet<(int, int)>? AdjacentYearSoftPairs,
    EnrollmentConflictMatrix? EnrollmentConflicts)
{
    public static readonly ConflictPairsResult Empty = new(null, null, null);
}
