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
/// Bu hafta yaşanan bug: AutoScheduleEngine cohort mode kullandı ama
/// SchedulingService.BuildTermSnapshotAsync mode'u hiç okumadı, hep eski "curriculum"
/// branch'ine düşüyordu → preview ile apply tutarsız → 39 atama rollback oldu.
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
                var matrix = await EnrollmentConflictMatrix.BuildAsync(
                    context, termId, sectionIds, enrollmentThreshold, ct);
                snapshot.EnrollmentConflicts = matrix;
                break;
            }
            // "curriculum" veya bilinmeyen → evaluator 3. branch'i (ders-bazlı agresif kontrol).
            // CohortConflictPairs ve EnrollmentConflicts null kalır, H5 default davranışına düşer.
        }
    }
}
