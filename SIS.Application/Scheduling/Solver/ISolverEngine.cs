using System.Threading;
using System.Threading.Tasks;

namespace SIS.Application.Scheduling.Solver;

public interface ISolverEngine
{
    Task<SolverResult> SolveAsync(
        SolverRequest request,
        CancellationToken cancellationToken);
}
