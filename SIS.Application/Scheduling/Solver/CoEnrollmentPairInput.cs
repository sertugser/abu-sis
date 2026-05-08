namespace SIS.Application.Scheduling.Solver;

/// <summary>
/// Plan 8 — S4 CoEnrollment encoder input. v1 AutoScheduleEngine.LoadCoEnrollmentPairsAsync portu.
/// <see cref="StudentCount"/> last 3 term'de aynı student tarafından birlikte alınmış pair sayısı; threshold
/// ≥ <see cref="SoftThresholds.S4MinCoPairThreshold"/> filter SolverRequestBuilder'da uygulanır.
/// </summary>
/// <remarks>
/// <b>Canonical form contract:</b> SolverRequestBuilder her pair'i her zaman
/// <c>CourseId1 &lt; CourseId2</c> olacak şekilde normalize eder. Encoder'lar bu invariant'a
/// güvenir; aynı pair'in (A,B) ve (B,A) varyantları double-count riski yaratmaz.
/// </remarks>
public sealed record CoEnrollmentPairInput(int CourseId1, int CourseId2, int StudentCount);
