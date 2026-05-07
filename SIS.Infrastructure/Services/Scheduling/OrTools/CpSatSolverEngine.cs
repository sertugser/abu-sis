using Google.OrTools.Sat;
using Microsoft.Extensions.Logging;
using SIS.Application.Scheduling.Solver;
using SIS.Infrastructure.Services.Scheduling.OrTools.Diagnostics;
using SIS.Infrastructure.Services.Scheduling.OrTools.Mapping;
using SIS.Infrastructure.Services.Scheduling.OrTools.Model;
using System.Diagnostics;

namespace SIS.Infrastructure.Services.Scheduling.OrTools;

/// <summary>
/// OR-Tools CP-SAT tabanlı ISolverEngine implementasyonu.
/// Faz 2 PoC: Sadece H1 (ClassroomConflict) encoder'ı ile çalışır.
/// Diğer 11 hard + N soft Plan 2 ve Plan 3'te eklenecek.
/// </summary>
public sealed class CpSatSolverEngine : ISolverEngine
{
    private readonly ScheduleModelBuilder _modelBuilder;
    private readonly SolutionMapper _mapper;
    private readonly ILogger<CpSatSolverEngine>? _logger;

    public CpSatSolverEngine(
        ScheduleModelBuilder modelBuilder,
        SolutionMapper mapper,
        ILogger<CpSatSolverEngine>? logger = null)
    {
        _modelBuilder = modelBuilder;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<SolverResult> SolveAsync(
        SolverRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();
        var build = _modelBuilder.Build(request);

        var solver = new CpSolver
        {
            StringParameters = "num_workers:8 max_time_in_seconds:30 random_seed:42"
        };

        // Cancellation propagation: ct trip → solver.StopSearch()
        using var registration = cancellationToken.Register(() => solver.StopSearch());

        CpSolverStatus status;
        try
        {
            status = await Task.Run(() => solver.Solve(build.Model), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        sw.Stop();
        var elapsedSeconds = sw.Elapsed.TotalSeconds;

        return status switch
        {
            CpSolverStatus.Infeasible => _mapper.MapInfeasible(
                unsatCore: ExtractUnsatCore(solver, build.Assumptions),
                request,
                elapsedSeconds),
            CpSolverStatus.Optimal or CpSolverStatus.Feasible => BuildSuccessResult(
                solver, build, request, elapsedSeconds, status),
            _ => BuildTimeoutResult(elapsedSeconds, request)
        };
    }

    private static IReadOnlyList<UnsatCoreEntry> ExtractUnsatCore(
        CpSolver solver,
        AssumptionRegistry registry)
    {
        // Faz 2 PoC: SufficientAssumptionsForInfeasibility'den sebep listesi.
        // Tam Türkçe sebep mapping Plan 2'de UnsatCoreInterpreter'a taşınacak.
        var coreIndices = solver.SufficientAssumptionsForInfeasibility();
        var allLiterals = registry.AllLiterals;
        var entries = new List<UnsatCoreEntry>();

        foreach (var idx in coreIndices)
        {
            if (idx < 0 || idx >= allLiterals.Count) continue;
            var meta = registry.LookupByLiteral(allLiterals[idx]);
            if (meta == null) continue;

            entries.Add(new UnsatCoreEntry(
                meta.ConstraintCode,
                $"{meta.ConstraintCode} kuralı: §{meta.PrimarySectionId} ile §{meta.SecondarySectionId} çakışıyor.",
                meta.PrimarySectionId,
                meta.SecondarySectionId));
        }

        return entries;
    }

    private SolverResult BuildSuccessResult(
        CpSolver solver,
        ScheduleModelBuildResult build,
        SolverRequest request,
        double elapsedSeconds,
        CpSolverStatus status)
    {
        // PoC: Detaylı assignment mapping Plan 2'de SolutionMapper.MapSuccess olarak gelecek.
        return new SolverResult
        {
            Status = status == CpSolverStatus.Optimal ? SolverStatus.Optimal : SolverStatus.Feasible,
            Assignments = new List<Assignment>(),
            Failures = new List<Failure>(),
            Diagnostics = new SolverDiagnostics
            {
                EngineVersion = "v2-ortools",
                ElapsedSeconds = elapsedSeconds,
                Branches = solver.NumBranches(),
                Conflicts = solver.NumConflicts(),
                ObjectiveValue = solver.ObjectiveValue,
                UnsatCore = new List<UnsatCoreEntry>()
            }
        };
    }

    private SolverResult BuildTimeoutResult(double elapsedSeconds, SolverRequest request)
    {
        return new SolverResult
        {
            Status = SolverStatus.Timeout,
            Assignments = new List<Assignment>(),
            Failures = new List<Failure>(),
            Diagnostics = new SolverDiagnostics
            {
                EngineVersion = "v2-ortools",
                ElapsedSeconds = elapsedSeconds,
                Branches = 0,
                Conflicts = 0,
                ObjectiveValue = 0,
                UnsatCore = new List<UnsatCoreEntry>()
            }
        };
    }
}
