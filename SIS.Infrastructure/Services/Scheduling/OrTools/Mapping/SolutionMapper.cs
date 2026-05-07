using SIS.Application.Scheduling.Solver;

namespace SIS.Infrastructure.Services.Scheduling.OrTools.Mapping;

/// <summary>
/// CP-SAT solver response → SolverResult dönüştürücüsü.
/// Faz 2 PoC: Sadece MapInfeasible implementasyonu var.
/// MapSuccess (assignments mapping) ve MapTimeout (partial) Plan 2'de eklenecek.
/// </summary>
public sealed class SolutionMapper
{
    public SolverResult MapInfeasible(
        IReadOnlyList<UnsatCoreEntry> unsatCore,
        SolverRequest request,
        double elapsedSeconds)
    {
        var failures = request.Sections
            .Select(s => new Failure
            {
                SectionId = s.Id,
                Reason = unsatCore.Count > 0
                    ? unsatCore[0].HumanReason
                    : "Çözüm bulunamadı (kısıtlamalar matematiksel olarak çelişkili).",
                RelatedConstraints = unsatCore
            })
            .ToList();

        return new SolverResult
        {
            Status = SolverStatus.Infeasible,
            Assignments = new List<Assignment>(),
            Failures = failures,
            Diagnostics = new SolverDiagnostics
            {
                EngineVersion = "v2-ortools",
                ElapsedSeconds = elapsedSeconds,
                Branches = 0,
                Conflicts = 0,
                ObjectiveValue = 0,
                UnsatCore = unsatCore
            }
        };
    }
}
