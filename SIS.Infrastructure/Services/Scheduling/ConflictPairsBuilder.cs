using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Rules;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling;

/// <summary>
/// Tek otorite müfredat çakışma çift üretici.
///
/// PROBLEM: 3 farklı yer aynı mode-branching mantığını yazıyordu:
///   1. AutoScheduleEngine.cs:230-249 (auto-schedule preview)
///   2. SchedulingService.BuildTermSnapshotAsync:1546-1562 (manuel atama validate)
///   3. (gelecekte) Excel import vb akışlar
///
/// Mode'lar (Schedule.CurriculumConflictMode config):
///   - "curriculum" : eski agresif (CohortConflictPairs ve EnrollmentConflicts boş kalır,
///                    H5 evaluator 3. branch'ine düşer ve aynı bölüm/yıl ders-bazlı kontrol yapar)
///   - "cohort"     : (Dept, Year) cohort + adjacent year, bottleneck-aware modulo
///   - "enrollment" : gerçek öğrenci kayıt verisinden çakışma matrisi
///
/// ÇÖZÜM: tüm mode-branching bu dosyada. AutoScheduleEngine ve SchedulingService
/// aynı çağrıyı yapar; mode değiştiğinde tek yer güncellenir.
/// </summary>
public static class ConflictPairsBuilder
{
    /// <summary>
    /// Verilen mode'a göre snapshot'a uygun konflikt verisini ekler.
    /// "cohort" → CohortConflictPairs + AdjacentYearSoftPairs.
    /// "enrollment" → EnrollmentConflicts (matrix).
    /// "curriculum" veya bilinmeyen → hiçbir şey set edilmez (evaluator 3. branch'ine düşer).
    /// </summary>
    public static async Task ApplyToSnapshotAsync(
        TermSnapshot snapshot,
        string mode,
        IReadOnlyList<SectionSnapshot> sections,
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
                var cohortRefs = sections
                    .Select(s => new CohortSectionRef(s.SectionId, s.SectionNumber, s.CourseId))
                    .ToList();
                var assignment = cohortBuilder.Build(cohortRefs, courseSemestersByDept, courseAccessibleDepts);
                snapshot.CohortConflictPairs = assignment.ConflictPairs;
                snapshot.AdjacentYearSoftPairs = assignment.AdjacentYearSoftPairs;
                break;
            }
            case "enrollment":
            {
                var sectionIds = sections.Select(s => s.SectionId);
                snapshot.EnrollmentConflicts = await BuildEnrollmentMatrixAsync(
                    context, termId, sectionIds, enrollmentThreshold, ct);
                break;
            }
            // "curriculum" veya bilinmeyen → evaluator 3. branch'i (ders-bazlı agresif kontrol).
            // CohortConflictPairs ve EnrollmentConflicts null kalır, H5 default davranışına düşer.
        }
    }

    /// <summary>
    /// "enrollment" mode için gerçek öğrenci kayıt verisinden hard çakışma matrisi üretir.
    /// Hard threshold: bir section çiftini en az bu kadar ortak öğrenci alıyorsa pair hard.
    /// Eski V1 <c>EnrollmentConflictMatrix.BuildAsync</c> portu — SoftConflictPairs alanı
    /// hiç tüketilmediği için elenmiştir.
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
