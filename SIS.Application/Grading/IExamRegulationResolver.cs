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
}

public sealed record WeightBounds(
    decimal MinMidterm, decimal MaxMidterm,
    decimal MinFinal, decimal MaxFinal,
    bool IsEmpty);
