namespace SIS.Application.Scheduling.Solver;

/// <summary>
/// İki kurs arasındaki ilişki (eşkoşul / corequisite).
/// Plan 2 H11 encoder'ı ve SolverRequestBuilder kullanır.
/// </summary>
public sealed record CourseRelationship(int PrimaryCourseId, int CorequisiteCourseId);
