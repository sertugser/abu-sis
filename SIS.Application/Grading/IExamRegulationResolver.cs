using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Application.Grading;

public interface IExamRegulationResolver
{
    Task<ExamRegulation?> ResolveForStudentAsync(int studentId, int academicTermId, CancellationToken ct);

    /// <summary>
    /// Bulk variant of <see cref="ResolveForStudentAsync"/> — resolves exam regulations for
    /// all supplied students in three queries (term, student-programs, regulations) instead of
    /// two queries per student. Returns a dictionary keyed by studentId; value is null when
    /// no matching regulation exists for that student.
    /// </summary>
    Task<IReadOnlyDictionary<int, ExamRegulation?>> ResolveForStudentsAsync(
        IReadOnlyCollection<int> studentIds, int academicTermId, CancellationToken ct);

    Task<WeightBounds?> ResolveCourseWeightBoundsAsync(int courseId, int academicTermId, CancellationToken ct);

    /// <summary>Dersin programlarındaki regülasyondan ara/final sınav sayısı (en katı = max). Regülasyon yoksa (0,0,false).</summary>
    Task<ExamCounts> ResolveCourseExamCountsAsync(int courseId, int academicTermId, CancellationToken ct);

    /// <summary>
    /// Resolves the letter-grade thresholds for a course by gathering all DegreeLevel-matched,
    /// term-matched active regulations for the programs whose curricula include the course.
    /// Returns null when no matching regulations exist.
    /// </summary>
    Task<LetterPolicy?> ResolveCourseLetterPolicyAsync(int courseId, int academicTermId, CancellationToken ct);

    /// <summary>
    /// Resolves the relative-grading policy for a course by gathering all DegreeLevel-matched,
    /// term-matched active regulations for the programs whose curricula include the course.
    /// Returns null when no matching regulations exist.
    /// When programs disagree on EvaluationType, <see cref="RelativePolicy.IsConflicting"/> is
    /// true and <see cref="RelativePolicy.EvaluationType"/> falls back to Absolute (safe default).
    /// All numeric parameters are taken from the first matched regulation.
    /// </summary>
    Task<RelativePolicy?> ResolveCourseRelativePolicyAsync(int courseId, int academicTermId, CancellationToken ct);
}

public sealed record WeightBounds(
    decimal MinMidterm, decimal MaxMidterm,
    decimal MinFinal, decimal MaxFinal,
    bool IsEmpty);

public sealed record LetterPolicy(
    int AAMin, int BAMin, int BBMin, int CBMin, int CCMin, int DCMin, int DDMin,
    bool Editable, bool HasUniqueThresholds);

public sealed record ExamCounts(int MidtermCount, int FinalCount, bool HasRegulation);

public sealed record RelativePolicy(
    GradeEvaluationType EvaluationType,
    int MinStudentCount,
    decimal InclusionLimit,
    decimal AbsoluteFloor,
    int TAAMin, int TBAMin, int TBBMin, int TCBMin, int TCCMin, int TDCMin, int TDDMin,
    GradeCode BelowFloorGrade,
    bool IsConflicting);
