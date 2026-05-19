using System;
using System.Threading;
using System.Threading.Tasks;

namespace SIS.Application.Scheduling.Solver;

public interface ISolverEngine
{
    /// <summary>
    /// Verilen request için CP-SAT modeli kurar ve çözer.
    /// </summary>
    /// <param name="onProgress">
    /// Opsiyonel — solver her improving solution bulduğunda tetiklenir.
    /// Solver thread'inden çağrılır; implementer non-blocking olmalı (fire-and-forget).
    /// Null geçilirse callback bağlanmaz, mevcut/eski Solve davranışı geçerli.
    /// </param>
    Task<SolverResult> SolveAsync(
        SolverRequest request,
        CancellationToken cancellationToken,
        Action<SolverProgressInfo>? onProgress = null);
}
