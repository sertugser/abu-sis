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
        CancellationToken cancellationToken,
        Action<SolverProgressInfo>? onProgress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 2026-05-19: MaxLabBlockHours artık H6.Parameters JSON'undan SolverRequestBuilder
        // tarafından request'e set ediliyor (Kısıtlar sayfası → H6 dialog tek otorite).
        // Defensive clamp: tests/eski request'ler 0 gönderirse default 2 uygula.
        var maxLabBlockHours = request.MaxLabBlockHours > 0 ? request.MaxLabBlockHours : 2;

        // Solver config fail-fast: veri fizibilitesi ne olursa olsun geçersiz solver ayarları
        // kullanıcıya hemen döndürülmeli. Bu yüzden strateji/worker/time limit okumaları
        // preflight'tan önce yapılır.
        var timeLimit = await _config.GetIntAsync(
            "Schedule.OrTools.TimeLimitSeconds", FALLBACK_TIME_LIMIT_SECONDS, cancellationToken);
        var numWorkers = await _config.GetIntAsync(
            "Schedule.OrTools.NumWorkers", FALLBACK_NUM_WORKERS, cancellationToken);
        var configuredSeed = await _config.GetIntAsync(
            "Schedule.OrTools.RandomSeed", FALLBACK_RANDOM_SEED, cancellationToken);
        var logToConsole = await _config.GetBoolAsync(
            "Schedule.OrTools.LogToConsole", FALLBACK_LOG_TO_CONSOLE, cancellationToken);
        var searchStrategy = await ResolveSearchStrategyAsync(cancellationToken);
        var unsatCoreEnabled = await _config.GetBoolAsync(
            "Schedule.OrTools.UnsatCoreEnabled", true, cancellationToken);

        if (timeLimit <= 0)
        {
            _logger?.LogWarning(
                "Schedule.OrTools.TimeLimitSeconds resolved to {Value} (non-positive); using default {Default}.",
                timeLimit,
                FALLBACK_TIME_LIMIT_SECONDS);
            timeLimit = FALLBACK_TIME_LIMIT_SECONDS;
        }

        if (numWorkers <= 0)
        {
            _logger?.LogWarning(
                "Schedule.OrTools.NumWorkers resolved to {Value} (non-positive); using default {Default}.",
                numWorkers,
                FALLBACK_NUM_WORKERS);
            numWorkers = FALLBACK_NUM_WORKERS;
        }

        if (configuredSeed < 0)
        {
            _logger?.LogWarning(
                "Schedule.OrTools.RandomSeed resolved to {Value} (negative); using default {Default}.",
                configuredSeed,
                FALLBACK_RANDOM_SEED);
            configuredSeed = FALLBACK_RANDOM_SEED;
        }

        // ── Pre-flight feasibility check (Plan 3 Task 2) ──────────────────
        // Filter out sections that have NO valid classroom; mark them as failures.
        // Prevents CP-SAT 'all-or-nothing' infeasibility on real-world data.
        var preflight = _preflightChecker.Check(
            request.Sections, request.Classrooms, request.TimeSlots, maxLabBlockHours);

        if (preflight.FeasibleSections.Count == 0)
        {
            // All sections infeasible — return early with all as preflight failures
            return BuildAllInfeasibleResult(preflight, elapsedSeconds: 0);
        }

        // Reduce request.Sections to only feasible ones for solver
        var reducedRequest = request with
        {
            Sections = preflight.FeasibleSections,
            MaxLabBlockHours = maxLabBlockHours
        };

        // Plan 7b — Read AmphiThreshold from SystemConfig (default 100). Capacity ≥ threshold
        // forces theory component into Amphitheater rooms.
        // Defensive clamp (Plan 9 review polish, MaxLabBlockHours parity): a misconfigured
        // value of 0 or negative would mean every section forces Amphitheater when the flag
        // is later enabled — silent UNSAT. Treat non-positive as "use default 100".
        var amphiThreshold = await _config.GetIntAsync(
            "Classroom.AmphiThreshold", 100, cancellationToken);
        if (amphiThreshold <= 0)
        {
            _logger?.LogWarning(
                "Classroom.AmphiThreshold resolved to {Value} (non-positive); using default 100.",
                amphiThreshold);
            amphiThreshold = 100;
        }

        // 2026-05-19 — AmphiThresholdEnabled ve ClassroomTypeRequirementEnabled artık
        // H4.Parameters JSON'undan SolverRequestBuilder tarafından request'e set ediliyor
        // (Kısıtlar sayfası → H4 dialog). Burada SystemConfigs okuma yok, drift fix.
        reducedRequest = reducedRequest with
        {
            AmphiThreshold = amphiThreshold,
        };

        // SolverRequest.Seed (if provided) overrides the configured seed for caller-driven determinism.
        var randomSeed = reducedRequest.Seed ?? configuredSeed;

        var sw = Stopwatch.StartNew();
        var build = _modelBuilder.Build(reducedRequest);

        var solver = new CpSolver
        {
            StringParameters = string.Join(" ",
                $"num_workers:{numWorkers}",
                $"max_time_in_seconds:{timeLimit}",
                $"random_seed:{randomSeed}",
                $"search_branching:{searchStrategy.CpSatValue}",
                $"log_search_progress:{(logToConsole ? "true" : "false")}")
        };

        // Cancellation propagation: ct trip → solver.StopSearch()
        using var registration = cancellationToken.Register(() => solver.StopSearch());

        CpSolverStatus status;
        try
        {
            status = await Task.Run(() =>
            {
                if (onProgress is null)
                {
                    return solver.Solve(build.Model);
                }
                var callback = new ProgressSolutionCallback(
                    onProgress, cancellationToken, maxTimeSeconds: timeLimit, _logger);
                return solver.Solve(build.Model, callback);
            }, cancellationToken);
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
                unsatCore: ExtractUnsatCore(solver, build.Assumptions, reducedRequest, unsatCoreEnabled),
                reducedRequest,
                elapsedSeconds,
                build.Variables,
                searchStrategy.UiValue,
                unsatCoreEnabled),
            CpSolverStatus.Optimal or CpSolverStatus.Feasible => BuildSuccessResult(
                solver, build, reducedRequest, elapsedSeconds, status, searchStrategy.UiValue, unsatCoreEnabled),
            _ => BuildTimeoutResult(solver, build, reducedRequest, elapsedSeconds, searchStrategy.UiValue, unsatCoreEnabled)
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
        SolverRequest request,
        bool unsatCoreEnabled)
    {
        if (!unsatCoreEnabled)
            return [];

        // Plan 2 Task 16: Türkçe sebep mapping UnsatCoreInterpreter'a taşındı.
        return _unsatInterpreter.Interpret(solver, registry, request);
    }

    private SolverResult BuildSuccessResult(
        CpSolver solver,
        ScheduleModelBuildResult build,
        SolverRequest request,
        double elapsedSeconds,
        CpSolverStatus status,
        string searchStrategy,
        bool unsatCoreEnabled)
    {
        return _mapper.MapSuccess(solver, build, request, elapsedSeconds, status, searchStrategy, unsatCoreEnabled);
    }

    private SolverResult BuildTimeoutResult(
        CpSolver solver,
        ScheduleModelBuildResult build,
        SolverRequest request,
        double elapsedSeconds,
        string searchStrategy,
        bool unsatCoreEnabled)
    {
        return _mapper.MapTimeout(solver, build, request, elapsedSeconds, searchStrategy, unsatCoreEnabled);
    }

    private async Task<(string UiValue, string CpSatValue)> ResolveSearchStrategyAsync(CancellationToken cancellationToken)
    {
        var configuredValue = await _config.GetStringAsync(
            "Schedule.OrTools.SearchStrategy",
            "auto",
            cancellationToken);

        var effectiveValue = string.IsNullOrWhiteSpace(configuredValue)
            ? "auto"
            : configuredValue.Trim();
        var normalized = effectiveValue.ToLowerInvariant();
        return normalized switch
        {
            "auto" => ("auto", "AUTOMATIC_SEARCH"),
            "fixed" => ("fixed", "FIXED_SEARCH"),
            "portfolio" => ("portfolio", "PORTFOLIO_SEARCH"),
            _ => throw new InvalidOperationException(
                $"Geçersiz Schedule.OrTools.SearchStrategy değeri: '{configuredValue}'. Desteklenen değerler: auto, fixed, portfolio.")
        };
    }

    /// <summary>
    /// CP-SAT solver her improving solution bulduğunda tetiklenen callback.
    /// Solver kendi thread'inden senkron çağırır → blocking iş yok, throttle + try/catch
    /// ile native interop'a exception sızdırmıyoruz.
    ///
    /// Throttle: <see cref="MinIntervalMs"/> ms'den hızlı clusters'ı (özellikle ilk
    /// saniyelerde) yutar. Aksi halde milisaniye aralıkla 5-10 improving solution
    /// hub'a 5-10 ardışık SignalR push üretir — UI flicker + gereksiz network.
    /// </summary>
    private sealed class ProgressSolutionCallback : CpSolverSolutionCallback
    {
        private const int MinIntervalMs = 400;

        private readonly Action<SolverProgressInfo> _onProgress;
        private readonly CancellationToken _ct;
        private readonly double _maxTimeSeconds;
        private readonly ILogger<CpSatSolverEngine>? _logger;
        private int _solutionsFound;
        private long _lastReportedAtMs;

        public ProgressSolutionCallback(
            Action<SolverProgressInfo> onProgress,
            CancellationToken ct,
            double maxTimeSeconds,
            ILogger<CpSatSolverEngine>? logger)
        {
            _onProgress = onProgress;
            _ct = ct;
            _maxTimeSeconds = maxTimeSeconds;
            _logger = logger;
        }

        public override void OnSolutionCallback()
        {
            // Tüm gövde try/catch — OR-Tools .NET binding'inin callback exception
            // davranışı dokümante değil. SEHException / native interop crash riskini
            // sıfırlamak için her şeyi yutuyoruz, yalnızca log'a düşürüyoruz.
            try
            {
                _solutionsFound++;

                if (_ct.IsCancellationRequested) return;

                var nowMs = Environment.TickCount64;
                if (nowMs - _lastReportedAtMs < MinIntervalMs) return;
                _lastReportedAtMs = nowMs;

                var info = new SolverProgressInfo(
                    WallTimeSeconds: WallTime(),
                    MaxTimeSeconds: _maxTimeSeconds,
                    ObjectiveValue: ObjectiveValue(),
                    BestObjectiveBound: BestObjectiveBound(),
                    NumSolutionsFound: _solutionsFound,
                    Source: "solver");
                _onProgress(info);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex,
                    "ProgressSolutionCallback exception swallowed (solver akışı korunur).");
            }
        }
    }
}
