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
    private readonly ILogger<CpSatSolverEngine>? _logger;

    public CpSatSolverEngine(
        ScheduleModelBuilder modelBuilder,
        SolutionMapper mapper,
        ISystemConfigProvider config,
        UnsatCoreInterpreter unsatInterpreter,
        ILogger<CpSatSolverEngine>? logger = null)
    {
        _modelBuilder = modelBuilder;
        _mapper = mapper;
        _config = config;
        _unsatInterpreter = unsatInterpreter;
        _logger = logger;
    }

    public async Task<SolverResult> SolveAsync(
        SolverRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Read solver parameters from SystemConfig (5-min cached). Fallbacks only fire on missing keys.
        var timeLimit = await _config.GetIntAsync(
            "Schedule.OrTools.TimeLimitSeconds", FALLBACK_TIME_LIMIT_SECONDS, cancellationToken);
        var numWorkers = await _config.GetIntAsync(
            "Schedule.OrTools.NumWorkers", FALLBACK_NUM_WORKERS, cancellationToken);
        // SolverRequest.Seed (if provided) overrides the configured seed for caller-driven determinism.
        var configuredSeed = await _config.GetIntAsync(
            "Schedule.OrTools.RandomSeed", FALLBACK_RANDOM_SEED, cancellationToken);
        var randomSeed = request.Seed ?? configuredSeed;
        var logToConsole = await _config.GetBoolAsync(
            "Schedule.OrTools.LogToConsole", FALLBACK_LOG_TO_CONSOLE, cancellationToken);

        var sw = Stopwatch.StartNew();
        var build = _modelBuilder.Build(request);

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

        return status switch
        {
            CpSolverStatus.Infeasible => _mapper.MapInfeasible(
                unsatCore: ExtractUnsatCore(solver, build.Assumptions, request),
                request,
                elapsedSeconds),
            CpSolverStatus.Optimal or CpSolverStatus.Feasible => BuildSuccessResult(
                solver, build, request, elapsedSeconds, status),
            _ => BuildTimeoutResult(elapsedSeconds, request)
        };
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
