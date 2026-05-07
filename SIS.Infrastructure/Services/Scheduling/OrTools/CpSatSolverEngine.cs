using Google.OrTools.Sat;
using Microsoft.Extensions.Logging;
using SIS.Application.Common.Services;
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
    // Fallback defaults — production reads from DB (SystemConfig seeded in Plan 1 Task 4).
    // These constants only apply if a SystemConfig row is missing for the corresponding key.
    private const int FALLBACK_TIME_LIMIT_SECONDS = 30;
    private const int FALLBACK_NUM_WORKERS = 8;
    private const int FALLBACK_RANDOM_SEED = 42;
    private const bool FALLBACK_LOG_TO_CONSOLE = false;

    private readonly ScheduleModelBuilder _modelBuilder;
    private readonly SolutionMapper _mapper;
    private readonly ISystemConfigProvider _config;
    private readonly UnsatCoreInterpreter _unsatInterpreter;
    private readonly PreflightFeasibilityChecker _preflightChecker;
    private readonly ILogger<CpSatSolverEngine>? _logger;

    public CpSatSolverEngine(
        ScheduleModelBuilder modelBuilder,
        SolutionMapper mapper,
        ISystemConfigProvider config,
        UnsatCoreInterpreter unsatInterpreter,
        PreflightFeasibilityChecker preflightChecker,
        ILogger<CpSatSolverEngine>? logger = null)
    {
        _modelBuilder = modelBuilder;
        _mapper = mapper;
        _config = config;
        _unsatInterpreter = unsatInterpreter;
        _preflightChecker = preflightChecker;
        _logger = logger;
    }

    public async Task<SolverResult> SolveAsync(
        SolverRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // ── Pre-flight feasibility check (Plan 3 Task 2) ──────────────────
        // Filter out sections that have NO valid classroom; mark them as failures.
        // Prevents CP-SAT 'all-or-nothing' infeasibility on real-world data.
        var preflight = _preflightChecker.Check(request.Sections, request.Classrooms);

        if (preflight.FeasibleSections.Count == 0)
        {
            // All sections infeasible — return early with all as preflight failures
            return BuildAllInfeasibleResult(preflight, elapsedSeconds: 0);
        }

        // Reduce request.Sections to only feasible ones for solver
        var reducedRequest = request with { Sections = preflight.FeasibleSections };

        // Read solver parameters from SystemConfig (5-min cached). Fallbacks only fire on missing keys.
        var timeLimit = await _config.GetIntAsync(
            "Schedule.OrTools.TimeLimitSeconds", FALLBACK_TIME_LIMIT_SECONDS, cancellationToken);
        var numWorkers = await _config.GetIntAsync(
            "Schedule.OrTools.NumWorkers", FALLBACK_NUM_WORKERS, cancellationToken);
        // SolverRequest.Seed (if provided) overrides the configured seed for caller-driven determinism.
        var configuredSeed = await _config.GetIntAsync(
            "Schedule.OrTools.RandomSeed", FALLBACK_RANDOM_SEED, cancellationToken);
        var randomSeed = reducedRequest.Seed ?? configuredSeed;
        var logToConsole = await _config.GetBoolAsync(
            "Schedule.OrTools.LogToConsole", FALLBACK_LOG_TO_CONSOLE, cancellationToken);

        var sw = Stopwatch.StartNew();
        var build = _modelBuilder.Build(reducedRequest);

        var solver = new CpSolver
        {
            StringParameters = string.Join(" ",
                $"num_workers:{numWorkers}",
                $"max_time_in_seconds:{timeLimit}",
                $"random_seed:{randomSeed}",
                $"log_search_progress:{(logToConsole ? "true" : "false")}")
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

        var solverResult = status switch
        {
            CpSolverStatus.Infeasible => _mapper.MapInfeasible(
                unsatCore: ExtractUnsatCore(solver, build.Assumptions, reducedRequest),
                reducedRequest,
                elapsedSeconds),
            CpSolverStatus.Optimal or CpSolverStatus.Feasible => BuildSuccessResult(
                solver, build, reducedRequest, elapsedSeconds, status),
            _ => BuildTimeoutResult(solver, build, reducedRequest, elapsedSeconds)
        };

        // Append preflight infeasibles as additional failures
        return AppendPreflightFailures(solverResult, preflight);
    }

    /// <summary>All sections were rejected at preflight — short-circuit return.</summary>
    private static SolverResult BuildAllInfeasibleResult(PreflightResult preflight, double elapsedSeconds)
    {
        var failures = preflight.InfeasibleSections.Select(infs => new Failure
        {
            SectionId = infs.Section.Id,
            Reason = infs.Reason
        }).ToList();

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
                UnsatCore = new List<UnsatCoreEntry>()
            }
        };
    }

    /// <summary>Append preflight-rejected sections to the solver result's failure list.</summary>
    private static SolverResult AppendPreflightFailures(SolverResult result, PreflightResult preflight)
    {
        if (preflight.InfeasibleSections.Count == 0) return result;

        var combinedFailures = result.Failures.ToList();
        foreach (var infs in preflight.InfeasibleSections)
        {
            combinedFailures.Add(new Failure
            {
                SectionId = infs.Section.Id,
                Reason = infs.Reason
            });
        }

        return result with { Failures = combinedFailures };
    }

    private IReadOnlyList<UnsatCoreEntry> ExtractUnsatCore(
        CpSolver solver,
        AssumptionRegistry registry,
        SolverRequest request)
    {
        // Plan 2 Task 16: Türkçe sebep mapping UnsatCoreInterpreter'a taşındı.
        return _unsatInterpreter.Interpret(solver, registry, request);
    }

    private SolverResult BuildSuccessResult(
        CpSolver solver,
        ScheduleModelBuildResult build,
        SolverRequest request,
        double elapsedSeconds,
        CpSolverStatus status)
    {
        return _mapper.MapSuccess(solver, build, request, elapsedSeconds, status);
    }

    private SolverResult BuildTimeoutResult(
        CpSolver solver,
        ScheduleModelBuildResult build,
        SolverRequest request,
        double elapsedSeconds)
    {
        return _mapper.MapTimeout(solver, build, request, elapsedSeconds);
    }
}
