namespace SIS.Application.Grading;

/// <summary>Bir sınavın ait olduğu program+sınıf kohortu.</summary>
public sealed record ExamCohort(int ProgramId, int ClassYear);

/// <summary>Çakışma değerlendirmesi için tek bir sınavın saf girdisi.</summary>
public sealed record ExamConflictInput(
    int ExamId,                                   // 0 = yeni/kaydedilmemiş; kendini hariç tutmak için
    DateTime Start,
    DateTime End,
    IReadOnlyCollection<int> ClassroomIds,
    IReadOnlyCollection<ExamCohort> Cohorts,
    string Label);

public enum ExamConflictKind { None, Classroom, Cohort }

public sealed record ExamConflictResult(ExamConflictKind Kind, string? Message);

/// <summary>
/// Saf, DB'siz çakışma motoru. İki sert kural:
/// (1) Aynı derslik + zaman örtüşmesi → Classroom.
/// (2) Aynı program + |yıl farkı| ≤ 1 + zaman örtüşmesi → Cohort (aynı/komşu sınıf öğrencisi).
/// Oda kontrolü kohorttan önce gelir (daha somut mesaj). İlk bulunan çakışma döner.
/// </summary>
public static class ExamConflictEvaluator
{
    private const int AdjacentYearTolerance = 1;

    public static ExamConflictResult Evaluate(
        ExamConflictInput candidate,
        IReadOnlyCollection<ExamConflictInput> existing)
    {
        foreach (var other in existing)
        {
            if (other.ExamId == candidate.ExamId) continue;
            if (!Overlaps(candidate, other)) continue;

            if (candidate.ClassroomIds.Intersect(other.ClassroomIds).Any())
                return new ExamConflictResult(
                    ExamConflictKind.Classroom,
                    $"Derslik çakışması: '{other.Label}' sınavı aynı derslikte ve aynı saatte.");

            foreach (var c in candidate.Cohorts)
                foreach (var o in other.Cohorts)
                    if (c.ProgramId == o.ProgramId &&
                        Math.Abs(c.ClassYear - o.ClassYear) <= AdjacentYearTolerance)
                        return new ExamConflictResult(
                            ExamConflictKind.Cohort,
                            $"Sınıf çakışması: aynı/komşu sınıfın '{other.Label}' sınavıyla aynı saatte.");
        }
        return new ExamConflictResult(ExamConflictKind.None, null);
    }

    private static bool Overlaps(ExamConflictInput a, ExamConflictInput b)
        => a.Start < b.End && b.Start < a.End;
}
