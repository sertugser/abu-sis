using SIS.Domain.Entities;

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

    /// <summary>
    /// Resolves the letter-grade thresholds for a course by gathering all DegreeLevel-matched,
    /// term-matched active regulations for the programs whose curricula include the course.
    /// Returns null when no matching regulations exist.
    /// </summary>
    Task<LetterPolicy?> ResolveCourseLetterPolicyAsync(int courseId, int academicTermId, CancellationToken ct);
}

public sealed record WeightBounds(
    decimal MinMidterm, decimal MaxMidterm,
    decimal MinFinal, decimal MaxFinal,
    bool IsEmpty);

public sealed record LetterPolicy(
    int AAMin, int BAMin, int BBMin, int CBMin, int CCMin, int DCMin, int DDMin,
    bool Editable, bool HasUniqueThresholds);
