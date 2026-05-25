namespace SIS.Application.Integration;

/// <summary>
/// Boundary abstraction over the YÖKSİS student reporting system (status, submission,
/// and ÖSYM placement lookup). Implementations are selected by configuration mode
/// (Mock / Disabled / Live); the mock returns deterministic fake data flagged
/// <c>IsMock = true</c>.
/// </summary>
public interface IExternalStudentReportingService
{
    Task<YoksisStatusResult> GetStatusAsync(int studentId, CancellationToken ct = default);
    Task<YoksisStatusResult> SubmitAsync(int studentId, CancellationToken ct = default);
    Task<OsymPlacementResult> QueryPlacementAsync(int studentId, CancellationToken ct = default);
}
