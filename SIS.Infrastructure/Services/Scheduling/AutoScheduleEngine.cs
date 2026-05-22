using SIS.Application.Common.Services;
using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling;

/// <summary>
/// Auto-schedule engine — Plan 9 sonrası tek-path V2 (Google OR-Tools CP-SAT).
/// Eski v1 (MRV + SimulatedAnnealing + LNS + Construction + FailureAnalyzer) silindi.
/// </summary>
public sealed class AutoScheduleEngine
{
    private readonly AppDbContext _context;
    private readonly ISystemConfigProvider _configProvider;
    private readonly ISolverEngine _solverEngine;
    private readonly IScheduleProgressReporter _progressReporter;
    private readonly IScheduleCancelRegistry? _cancelRegistry;

    public AutoScheduleEngine(
        AppDbContext context,
        ISystemConfigProvider configProvider,
        ISolverEngine solverEngine,
        IScheduleProgressReporter? progressReporter = null,
        IScheduleCancelRegistry? cancelRegistry = null)
    {
        _context = context;
        _configProvider = configProvider;
        _solverEngine = solverEngine;
        _progressReporter = progressReporter ?? new NoOpScheduleProgressReporter();
        _cancelRegistry = cancelRegistry;
    }

    public async Task<AutoSchedulePreviewDto> RunAsync(
        AutoScheduleRequest request,
        string? userId = null,
        CancellationToken ct = default)
    {
        // Cancel registry — Hub.Cancel() çağrısı linkedCts'i tetikleyip algoritmayı durdurur.
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (!string.IsNullOrEmpty(userId))
            _cancelRegistry?.Register(userId, linkedCts);
        ct = linkedCts.Token;

        async Task ReportAsync(string stage, int percent, int current = 0, int total = 0, string? detail = null)
        {
            if (!string.IsNullOrEmpty(userId))
                await _progressReporter.ReportAsync(userId,
                    new ScheduleProgressEvent(stage, percent, current, total, detail), ct);
        }

        try
        {
            await ReportAsync("started", 0, detail: "Program oluşturma başlatılıyor...");

            var engineKey = await ResolveEngineAsync(ct);

            var seed = request.Seed ?? (int)(DateTime.UtcNow.Ticks % int.MaxValue);

            await ReportAsync("v2-building-request", 10,
                detail: "Dersler, derslikler ve zaman dilimleri yükleniyor...");

            var requestBuilder = new OrTools.SolverRequestBuilder(_context, _configProvider);
            var solverRequest = await requestBuilder.BuildAsync(
                request.TermId,
                request.FacultyId,
                request.DepartmentId,
                request.Seed,
                ct);
            ValidateSolverRequest(solverRequest);

            var sectionCount = solverRequest.Sections.Count;

            await ReportAsync("v2-solving", 50,
                detail: $"{sectionCount} ders için en uygun program aranıyor...");

            // ── Solver progress wiring (v2-solving aşaması, 50→90) ──
            // İki kaynak monotonik olarak yüzdeyi günceller:
            //   1) CP-SAT improving solution callback (her iyileşmede)
            //   2) Heartbeat task (sessiz solver / UNSAT senaryosu için zamana göre fake ilerleme)
            // Her ikisi de aynı state'i (lastEmittedPercent) lock altında günceller; geriye
            // sayım yok → frontend smooth interpolation rahat takip eder.
            var progressLock = new object();
            var lastEmittedPercent = 50;

            var solveTimeLimit = await _configProvider.GetIntAsync(
                "Schedule.OrTools.TimeLimitSeconds", 30, ct);
            if (solveTimeLimit <= 0) solveTimeLimit = 30;

            void EmitSolvingProgress(int percent, string detail, int current)
            {
                if (string.IsNullOrEmpty(userId)) return;
                lock (progressLock)
                {
                    if (percent <= lastEmittedPercent) return;
                    lastEmittedPercent = percent;
                }
                // Fire-and-forget: solver thread'i veya heartbeat task'ı blokumayalım.
                // SignalRScheduleProgressReporter zaten exception swallow ediyor.
                _ = _progressReporter.ReportAsync(userId,
                    new ScheduleProgressEvent("v2-solving", percent, current, sectionCount, detail), ct);
            }

            void OnSolverProgress(SolverProgressInfo info)
            {
                var ratio = info.MaxTimeSeconds > 0
                    ? Math.Min(1.0, info.WallTimeSeconds / info.MaxTimeSeconds)
                    : 0.0;
                var percent = 50 + (int)(ratio * 40); // 50..90
                var detail = $"{info.NumSolutionsFound} çözüm bulundu, iyileştiriliyor... ({sectionCount} ders)";
                EmitSolvingProgress(percent, detail, current: info.NumSolutionsFound);
            }

            using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var heartbeatStarted = DateTime.UtcNow;
            var heartbeatTask = Task.Run(async () =>
            {
                try
                {
                    // İlk tick'i 1.5s sonra at — solver hızlıca biterse heartbeat hiç emit etmesin.
                    while (!heartbeatCts.IsCancellationRequested)
                    {
                        await Task.Delay(1500, heartbeatCts.Token);
                        if (heartbeatCts.IsCancellationRequested) break;
                        var elapsedSec = (DateTime.UtcNow - heartbeatStarted).TotalSeconds;
                        var ratio = Math.Min(1.0, elapsedSec / solveTimeLimit);
                        var percent = 50 + (int)(ratio * 40);
                        EmitSolvingProgress(percent,
                            $"En uygun program aranıyor... ({sectionCount} ders)",
                            current: 0);
                    }
                }
                catch (OperationCanceledException) { /* normal shutdown */ }
            }, heartbeatCts.Token);

            SolverResult solverResult;
            try
            {
                solverResult = await _solverEngine.SolveAsync(solverRequest, ct, OnSolverProgress);
            }
            finally
            {
                heartbeatCts.Cancel();
                try { await heartbeatTask; } catch (OperationCanceledException) { }
            }

            await ReportAsync("complete", 100,
                detail: $"Program oluşturuldu ({FormatSolverStatus(solverResult.Status)}, {solverResult.Diagnostics.ElapsedSeconds:F1} sn).");

            return MapSolverResultToPreviewDto(solverResult, solverRequest, seed, engineKey);
        }
        catch (OperationCanceledException)
        {
            if (!string.IsNullOrEmpty(userId))
                await _progressReporter.ReportAsync(userId,
                    new ScheduleProgressEvent("failed", 0, 0, 0, "İşlem iptal edildi."), CancellationToken.None);
            throw;
        }
        finally
        {
            if (!string.IsNullOrEmpty(userId))
                _cancelRegistry?.Unregister(userId);
        }
    }

    /// <summary>
    /// SolverStatus → kullanıcı dostu Türkçe metin. "complete" event'inde detail mesajına gömülür.
    /// İngilizce enum isimleri ("Optimal", "Feasible"…) son kullanıcı için anlamsız; bu mapping
    /// solver sonucunu insan diline çevirir.
    /// </summary>
    private static string FormatSolverStatus(SolverStatus status) => status switch
    {
        SolverStatus.Optimal    => "en iyi sonuç",
        SolverStatus.Feasible   => "uygun çözüm",
        SolverStatus.Infeasible => "çözüm bulunamadı",
        SolverStatus.Timeout    => "süre doldu",
        SolverStatus.Error      => "hata",
        _ => status.ToString().ToLowerInvariant()
    };

    private async Task<string> ResolveEngineAsync(CancellationToken ct)
    {
        var configuredEngine = await _configProvider.GetStringAsync(
            "Schedule.Engine",
            "v2-ortools",
            ct);

        var effectiveEngine = string.IsNullOrWhiteSpace(configuredEngine)
            ? "v2-ortools"
            : configuredEngine.Trim();
        var normalized = effectiveEngine.ToLowerInvariant();
        return normalized switch
        {
            "v2-ortools" or "cp-sat" => normalized,
            _ => throw new InvalidOperationException(
                $"Geçersiz Schedule.Engine değeri: '{configuredEngine}'. Desteklenen değerler: v2-ortools, cp-sat.")
        };
    }

    private static void ValidateSolverRequest(SolverRequest solverRequest)
    {
        if (solverRequest.Sections.Count == 0)
            return;

        if (solverRequest.TimeSlots.Count == 0)
        {
            throw new InvalidOperationException(
                "Secili donem icin zaman dilimi tanimli degil. Ders programi olusturmadan once zaman dilimlerini olusturun.");
        }

        var hasPhysicalSection = solverRequest.Sections.Any(section =>
            section.TeachingMethod != TeachingMethod.Online
            && (section.TheoryHours > 0 || section.LabHours > 0));

        if (hasPhysicalSection && solverRequest.Classrooms.Count == 0)
        {
            throw new InvalidOperationException(
                "Bu fakulte ve donem icin atanmis derslik bulunamadi. Ders programi olusturmadan once derslik donem atamalarini kontrol edin.");
        }
    }

    /// <summary>
    /// SolverResult → AutoSchedulePreviewDto mapping. CP-SAT'ın ürettiği Assignment ve Failure
    /// listelerini frontend'in beklediği DTO formatına çevirir.
    /// </summary>
    private static AutoSchedulePreviewDto MapSolverResultToPreviewDto(
        SolverResult result,
        SolverRequest solverRequest,
        int seed,
        string engineKey)
    {
        var sectionLookup = solverRequest.Sections.ToDictionary(s => s.Id, s => s);
        var classroomLookup = solverRequest.Classrooms.ToDictionary(c => c.Id, c => c);
        var timeSlotLookup = solverRequest.TimeSlots.ToDictionary(ts => ts.Id, ts => ts);

        var assignmentDtos = new List<AutoSchedulePreviewItemDto>();
        foreach (var asn in result.Assignments)
        {
            if (!sectionLookup.TryGetValue(asn.SectionId, out var section))
                continue;

            // 2026-05-20: SolutionMapper Online section'lar için ClassroomId=0 (NoRoomSentinel)
            // emit ediyor. Preview DTO + Apply pipeline'ına null akmalı (FK violation engellenir
            // ve ScheduleWriter `?? defaultClassroomId` doğru çalışır). Sentinel'i burada,
            // pipeline'a girmeden önce null'a normalize ediyoruz.
            var rawPrimaryId = asn.Slots.Count > 0 ? asn.Slots[0].ClassroomId : (int?)null;
            int? primaryClassroomId = (rawPrimaryId is null or 0) ? null : rawPrimaryId;
            ClassroomInput? primaryClassroom = null;
            if (primaryClassroomId.HasValue
                && classroomLookup.TryGetValue(primaryClassroomId.Value, out var cr))
            {
                primaryClassroom = cr;
            }

            var scheduleSlots = asn.Slots
                .Select(s =>
                {
                    var ts = timeSlotLookup.TryGetValue(s.TimeSlotId, out var slot) ? slot : null;
                    int? slotClassroomId = s.ClassroomId == 0 ? null : s.ClassroomId;
                    var crName = slotClassroomId.HasValue
                        && classroomLookup.TryGetValue(slotClassroomId.Value, out var c) ? c.Name : null;
                    return new ScheduleSlotDto
                    {
                        Day = (int)(ts?.DayOfWeek ?? DayOfWeek.Monday),
                        StartTime = ts?.StartTime ?? TimeOnly.MinValue,
                        EndTime = ts?.EndTime ?? TimeOnly.MinValue,
                        ClassroomId = slotClassroomId,
                        ClassroomName = crName
                    };
                })
                .ToList();

            // Online section'larda ClassroomName null → özet metninde "Online" göster.
            var summary = string.Join(", ", scheduleSlots.Select(ss =>
                $"{ss.Day} {ss.StartTime:HH\\:mm}-{ss.EndTime:HH\\:mm} ({ss.ClassroomName ?? "Online"})"));

            assignmentDtos.Add(new AutoSchedulePreviewItemDto
            {
                SectionId = section.Id,
                SectionNumber = section.SectionNumber,
                CourseCode = section.CourseCode,
                CourseName = section.CourseName,
                DepartmentCode = string.Empty,
                ClassroomId = primaryClassroomId,
                ClassroomName = primaryClassroomId == 0 ? "Cevrim ici" : primaryClassroom?.Name ?? string.Empty,
                ScheduleSlots = scheduleSlots,
                ScheduleSummary = summary,
                InstructorId = section.InstructorId,
                InstructorName = null,
                FitScore = 0
            });
        }

        var failureDtos = new List<AutoScheduleFailureDto>();
        foreach (var f in result.Failures)
        {
            if (!sectionLookup.TryGetValue(f.SectionId, out var section))
                continue;
            failureDtos.Add(new AutoScheduleFailureDto
            {
                SectionId = section.Id,
                SectionNumber = section.SectionNumber,
                Capacity = section.Capacity,
                CourseCode = section.CourseCode,
                CourseName = section.CourseName,
                Reason = f.Reason,
                Suggestions = new List<FailureSuggestionDto>()
            });
        }

        // Sonuç paneli istatistikleri — frontend hardcoded sayım yapmaz, hepsi buradan gelir.
        // DistinctCourseCount: işlenen tüm şubelerin (success + failure) kapsadığı ayrık Course sayısı.
        // UsedClassroomCount: başarılı atamaların değdiği ayrık Classroom sayısı (failure'ların odası yok).
        var distinctCourseCount = result.Assignments
            .Select(a => sectionLookup.TryGetValue(a.SectionId, out var s) ? (int?)s.CourseId : null)
            .Concat(result.Failures
                .Select(f => sectionLookup.TryGetValue(f.SectionId, out var s) ? (int?)s.CourseId : null))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .Count();

        var usedClassroomCount = result.Assignments
            .SelectMany(a => a.Slots.Select(s => s.ClassroomId))
            .Where(id => id > 0)
            .Distinct()
            .Count();

        return new AutoSchedulePreviewDto
        {
            Seed = seed,
            TotalProcessed = assignmentDtos.Count + failureDtos.Count,
            SuccessCount = assignmentDtos.Count,
            FailedCount = failureDtos.Count,
            TotalPenalty = result.Diagnostics.ObjectiveValue,
            Assignments = assignmentDtos,
            Failures = failureDtos,
            Stats = new AutoScheduleStats
            {
                ElapsedSeconds = result.Diagnostics.ElapsedSeconds,
                DistinctCourseCount = distinctCourseCount,
                UsedClassroomCount = usedClassroomCount,
                Engine = engineKey,
                SearchStrategy = result.Diagnostics.SearchStrategy ?? "auto",
                UnsatCoreEnabled = result.Diagnostics.UnsatCoreEnabled ?? true
            }
        };
    }
}
