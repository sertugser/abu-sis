using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Scheduling.Legacy;

namespace SIS.Infrastructure.Services.Scheduling;

public sealed class AutoScheduleEngine
{
    private readonly AppDbContext _context;
    private readonly ConstraintEvaluationService _constraintService;
    private readonly ISystemConfigProvider _configProvider;
    private readonly ISolverEngine _solverEngine;
    private readonly IScheduleProgressReporter _progressReporter;
    private readonly IScheduleCancelRegistry? _cancelRegistry;

    public AutoScheduleEngine(
        AppDbContext context,
        ConstraintEvaluationService constraintService,
        ISystemConfigProvider configProvider,
        ISolverEngine solverEngine,
        IScheduleProgressReporter? progressReporter = null,
        IScheduleCancelRegistry? cancelRegistry = null)
    {
        _context = context;
        _constraintService = constraintService;
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
        // Cancel registry'e kayıt — Hub.Cancel() çağırılınca bu CTS tetiklenir.
        // CreateLinkedTokenSource: dış ct (request abort) ile bizim cancel mesajımızı
        // birleştirir; ikisinden biri tetiklenirse algoritma durur.
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (!string.IsNullOrEmpty(userId))
            _cancelRegistry?.Register(userId, linkedCts);
        ct = linkedCts.Token;

        // Progress reporter shortcut — userId yoksa veya reporter NoOp ise hiçbir şey yapmaz.
        async Task ReportAsync(string stage, int percent, int current = 0, int total = 0, string? detail = null)
        {
            if (!string.IsNullOrEmpty(userId))
                await _progressReporter.ReportAsync(userId,
                    new ScheduleProgressEvent(stage, percent, current, total, detail), ct);
        }

        // Periodic progress callback — algoritma loop'larından (Construction/SA/LNS)
        // sync olarak çağrılır; içeride fire-and-forget reporter'a yönlendirir, böylece
        // algoritma akışı SignalR send'ini beklemez.
        ScheduleProgressCallback? periodicCallback = null;
        if (!string.IsNullOrEmpty(userId))
        {
            periodicCallback = (stage, percent, current, total, detail) =>
            {
                _ = _progressReporter.ReportAsync(userId,
                    new ScheduleProgressEvent(stage, percent, current, total, detail), ct);
            };
        }

        try
        {
            await ReportAsync("started", 0, detail: "Auto-schedule başlatılıyor...");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 1. Determine seed (v1 ve v2 path'lerinde ortak — DTO'ya yazılır)
        var seed = request.Seed ?? (int)(DateTime.UtcNow.Ticks % int.MaxValue);

        // ── Engine flag dispatch (Plan 2 Task 19: real v2 dispatch) ──────
        // SystemConfig.Schedule.Engine == "v2-ortools" → CpSatSolverEngine kullan,
        // sonucu DTO'ya map'le, dön. Default v1-mrv → mevcut akış aşağıda devam eder.
        var engineFlag = await _configProvider.GetStringAsync(
            "Schedule.Engine", "v1-mrv", ct);

        if (engineFlag == "v2-ortools")
        {
            await ReportAsync("v2-building-request", 10,
                detail: "v2-ortools: SolverRequest DB'den inşa ediliyor...");

            var requestBuilder = new SIS.Infrastructure.Services.Scheduling.OrTools.SolverRequestBuilder(_context, _configProvider);
            var solverRequest = await requestBuilder.BuildAsync(
                request.TermId,
                request.FacultyId,
                request.DepartmentId,
                request.Seed,
                ct);

            await ReportAsync("v2-solving", 50,
                detail: $"v2-ortools: CP-SAT çözücü çalışıyor ({solverRequest.Sections.Count} section)...");

            var solverResult = await _solverEngine.SolveAsync(solverRequest, ct);

            await ReportAsync("complete", 100,
                detail: $"v2-ortools tamamlandı ({solverResult.Status}, {solverResult.Diagnostics.ElapsedSeconds:F2}s).");

            return MapSolverResultToPreviewDto(solverResult, solverRequest, seed);
        }
        // ── Mevcut v1 akışı aynen devam ──────────────────────────────────

        // 2. Load all SystemConfig parameters
        var splitThreshold = await _configProvider.GetIntAsync("Schedule.TheoryLabSplitThreshold", 4, ct);
        var backtrackLimit = await _configProvider.GetIntAsync("Schedule.SA.BacktrackLimit", 50, ct);
        var baseIterations = await _configProvider.GetIntAsync("Schedule.SA.MaxIterations", 10000, ct);
        var initialTemperature = await _configProvider.GetIntAsync("Schedule.SA.InitialTemperature", 100, ct);
        var timeoutSeconds = await _configProvider.GetIntAsync("Schedule.SA.TimeoutSeconds", 90, ct);
        var stagnationLimit = await _configProvider.GetIntAsync("Schedule.SA.StagnationLimit", 2000, ct);
        var relocateWeight = await _configProvider.GetIntAsync("Schedule.SA.RelocateWeight", 50, ct);
        var swapWeight = await _configProvider.GetIntAsync("Schedule.SA.SwapWeight", 35, ct);
        var chainWeight = await _configProvider.GetIntAsync("Schedule.SA.ChainWeight", 15, ct);

        var lnsIterations = await _configProvider.GetIntAsync("Schedule.LNS.MaxIterations", 100, ct);
        var softLookaheadCount = await _configProvider.GetIntAsync("Schedule.Construction.SoftLookaheadCount", 10, ct);

        // Faz 2: aday üretici limitleri config'ten — sabit hardcoded değil
        var candMaxCombinations = await _configProvider.GetIntAsync("Schedule.Candidate.MaxCombinations", 500, ct);
        var candMaxWindowsPerDay = await _configProvider.GetIntAsync("Schedule.Candidate.MaxWindowsPerDay", 10, ct);
        var candMaxClassroomsPerType = await _configProvider.GetIntAsync("Schedule.Candidate.MaxClassroomsPerType", 25, ct);
        var labMaxBlockHours = await _configProvider.GetIntAsync("Schedule.Lab.MaxBlockHours", 2, ct);
        var lnsTimeoutSeconds = await _configProvider.GetIntAsync("Schedule.LNS.TimeoutSeconds", 30, ct);
        var lnsDestroySize = await _configProvider.GetIntAsync("Schedule.LNS.DestroySize", 8, ct);

        var mrvWeights = new MrvWeights
        {
            ClassroomWeight = await _configProvider.GetIntAsync("Schedule.MRV.ClassroomWeight", 3, ct),
            SlotWeight = await _configProvider.GetIntAsync("Schedule.MRV.SlotWeight", 2, ct),
            LabWeight = await _configProvider.GetIntAsync("Schedule.MRV.LabWeight", 2, ct),
            CapacityWeight = await _configProvider.GetIntAsync("Schedule.MRV.CapacityWeight", 1, ct),
        };

        // 3. Load SchedulingConstraintConfig (same pattern as SchedulingService)
        var constraintConfig = await LoadSchedulingConstraintConfigAsync(ct);

        // 4. Load ConstraintState
        var constraintState = await _constraintService.LoadConstraintStateAsync(
            null, null, null, request.TermId, ct);

        // 5. Load faculty's classrooms
        var assignedClassroomIds = await _context.ClassroomTermAssignments
            .AsNoTracking()
            .Where(cta => cta.TermId == request.TermId && cta.FacultyId == request.FacultyId)
            .Select(cta => cta.ClassroomId)
            .ToListAsync(ct);

        var facultyClassroomIds = assignedClassroomIds.ToHashSet();

        var classrooms = await _context.Classrooms
            .AsNoTracking()
            .Where(c => assignedClassroomIds.Contains(c.Id))
            .ToListAsync(ct);

        // 6. Load TimeSlots for the term, grouped by DayOfWeek
        var timeSlotsByDay = (await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == request.TermId)
            .OrderBy(ts => ts.DayOfWeek)
            .ThenBy(ts => ts.StartTime)
            .ToListAsync(ct))
            .GroupBy(ts => ts.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 7. Load sections
        var departmentIds = await _context.Departments
            .AsNoTracking()
            .Where(d => d.FacultyId == request.FacultyId)
            .Select(d => d.Id)
            .ToListAsync(ct);

        var departmentIdSet = departmentIds.ToHashSet();

        var sectionsQuery = _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .Include(cs => cs.SectionSchedules).ThenInclude(ss => ss.TimeSlot)
            .Where(cs => cs.TermId == request.TermId
                         && !cs.IsFinalized
                         && cs.Status == CourseSectionStatus.Active
                         && cs.Course.DepartmentId.HasValue
                         && departmentIdSet.Contains(cs.Course.DepartmentId.Value));

        if (request.DepartmentId.HasValue)
        {
            sectionsQuery = sectionsQuery.Where(cs =>
                cs.Course.DepartmentId == request.DepartmentId.Value);
        }

        var sections = await sectionsQuery.ToListAsync(ct);

        if (sections.Count == 0)
        {
            stopwatch.Stop();
            return new AutoSchedulePreviewDto
            {
                Seed = seed,
                Stats = new AutoScheduleStats { ElapsedSeconds = stopwatch.Elapsed.TotalSeconds }
            };
        }

        // Load instructor names
        var instructorIds = sections
            .Where(s => s.InstructorId.HasValue)
            .Select(s => s.InstructorId!.Value)
            .Distinct()
            .ToList();

        var instructorNameMap = await LoadInstructorNameMapAsync(instructorIds, ct);

        // Load course scheduling metadata
        var courseMetadata = await LoadCourseSchedulingMetadataAsync(
            request.TermId,
            sections.Select(s => s.CourseId),
            ct);

        // Load department code map for output
        var departmentCodeMap = await _context.Departments
            .AsNoTracking()
            .Where(d => departmentIdSet.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Code, ct);

        // Build SectionSnapshot lookup
        var sectionSnapshots = new Dictionary<int, SectionSnapshot>();
        foreach (var section in sections)
        {
            var snap = BuildSectionSnapshot(section, instructorNameMap, courseMetadata);
            sectionSnapshots[section.Id] = snap;
        }

        // 8. Build base TermSnapshot
        var termName = await _context.AcademicTerms
            .AsNoTracking()
            .Where(t => t.Id == request.TermId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var classroomAssignments = await _context.ClassroomTermAssignments
            .AsNoTracking()
            .Where(cta => cta.TermId == request.TermId)
            .ToListAsync(ct);

        var deptFacultyMap = await _context.Departments
            .AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.FacultyId, ct);

        var corequisites = await _context.CoursePrerequisites
            .AsNoTracking()
            .Where(cp => cp.IsCorequisite)
            .ToListAsync(ct);

        // S4 enabled + threshold tek otorite olarak ConstraintDefinitions tablosunda:
        // IsEnabled flag'i + Parameters JSON'undaki "S4MinCoPairThreshold". Daha önce
        // SchedulingAlgorithmConfigs'tan okunuyordu, ama o key migration ile silindi (artık
        // soft eşikler kısıt kartından yönetiliyor — bkz. MoveSoftThresholdsToConstraintParameters).
        var s4Definition = await _context.ConstraintDefinitions
            .AsNoTracking()
            .Where(d => d.Code == "S4")
            .Select(d => new { d.IsEnabled, d.Parameters })
            .FirstOrDefaultAsync(ct);
        var s4Enabled = s4Definition?.IsEnabled ?? true;
        var s4Threshold = ParseS4Threshold(s4Definition?.Parameters, defaultValue: 20);
        var coEnrollmentPairs = s4Enabled
            ? await LoadCoEnrollmentPairsAsync(request.TermId, s4Threshold, ct)
            : new List<CoEnrollmentPair>();

        var distributionConstraints = await _context.DistributionConstraints
            .AsNoTracking()
            .Include(d => d.Courses)
            .Where(d => d.TermId == request.TermId)
            .ToListAsync(ct);

        var slotAllowedModes = timeSlotsByDay
            .SelectMany(kvp => kvp.Value)
            .ToDictionary(
                ts => (ts.DayOfWeek, ts.StartTime),
                ts => ts.AllowedMode);

        var baseSnapshot = new TermSnapshot
        {
            TermId = request.TermId,
            TermName = termName,
            ScopeName = "AutoSchedule",
            Sections = new List<SectionSnapshot>(),
            ClassroomAssignments = classroomAssignments,
            DeptFacultyMap = deptFacultyMap,
            Corequisites = corequisites,
            CoEnrollmentPairs = coEnrollmentPairs,
            CourseSuggestedSemestersByDepartment = courseMetadata.SuggestedSemestersByDepartment,
            CourseAccessibleDepartments = courseMetadata.AccessibleDepartments,
            DistributionConstraints = distributionConstraints,
            SlotAllowedModes = slotAllowedModes,
        };

        // 8b. CurriculumConflictMode'a göre çakışma çiftlerini snapshot'a yükle.
        // Tek otorite ConflictPairsBuilder; SchedulingService.BuildTermSnapshotAsync da
        // aynı çağrıyı yapar — preview ve apply yolları artık tutarlı.
        await ConflictPairsBuilder.ApplyToSnapshotAsync(
            baseSnapshot,
            constraintConfig.CurriculumConflictMode,
            sectionSnapshots.Values.ToList(),
            courseMetadata.SuggestedSemestersByDepartment,
            courseMetadata.AccessibleDepartments,
            constraintConfig.EnrollmentConflictHardThreshold,
            _context, request.TermId, ct);

        // 9a. Kural ihlali retroaktif doğrulama — mevcut atamaları SlotAllowedModes'a göre kontrol et.
        // Örn: slot AllowedMode=OnlineOnly olduktan sonra eski FaceToFace atamaları temizlenir, yeniden atanır.
        var invalidationReasons = await InvalidateAssignmentsViolatingSlotModeAsync(sections, slotAllowedModes, ct);

        // 9. Populate MutableScheduleState
        var state = new MutableScheduleState(request.TermId);
        state.SetClassroomLookup(classrooms.ToDictionary(c => c.Id));
        var targetSectionIds = new HashSet<int>();
        var requestedSectionIds = request.SectionIds?.ToHashSet();

        foreach (var section in sections)
        {
            var snap = sectionSnapshots[section.Id];
            // SectionSchedules temizlenmiş olabilir (invalidation pass sonrası) — Count'u yeniden oku
            var isScheduled = section.SectionSchedules.Count > 0;
            var wasInvalidated = invalidationReasons.ContainsKey(section.Id);

            if (isScheduled && !wasInvalidated)
            {
                var blocks = BuildBlockAssignments(section);

                // If specific sections requested and this is one of them, treat as target (re-schedule)
                if (requestedSectionIds != null && requestedSectionIds.Contains(section.Id))
                {
                    targetSectionIds.Add(section.Id);
                    // Don't assign - will be scheduled by construction solver
                }
                else
                {
                    // Existing scheduled section: pinned if IsPinned or IsScheduleLocked
                    var isPinned = section.IsPinned || section.IsScheduleLocked;
                    state.Assign(section.Id, snap, blocks, isPinned: isPinned);
                }
            }
            else
            {
                // Atanmamış VEYA kural ihlali nedeniyle temizlenmiş → target
                targetSectionIds.Add(section.Id);
            }
        }

        var targetSections = targetSectionIds
            .Select(id => sectionSnapshots[id])
            .ToList();

        await ReportAsync("construction", 10,
            current: 0, total: targetSections.Count,
            detail: $"Aday kombinasyonlar üretiliyor ({targetSections.Count} ders işlenecek)...");

        // 10. Run ConstructionSolver — multi-restart with seed perturbation.
        // Construction tek deterministik akış; tek seed = tek sonuç. Farklı tie-break
        // sıralamalarıyla 3-5 restart koşulup en iyisi seçilir → CENG 102 gibi yüksek-yüklü
        // section'ların farklı ordering'lerde yer bulma şansı artar.
        var candidateGen = new CandidateGenerator();
        // Faz 2: config'ten gelen limitleri uygula (eski hardcoded sabitler yerine)
        candidateGen.Configure(candMaxCombinations, candMaxWindowsPerDay, candMaxClassroomsPerType, labMaxBlockHours);
        var constructionSolver = new ConstructionSolver(candidateGen, _constraintService);

        // RestartCount=1 default: r=0 deterministik tek koşu. Multi-restart faydası
        // benchmark'larda ölçülmedi (perturbation tek başına yapısal fail'leri çözmüyor),
        // overhead getirmesin. Config'ten 3-5 yapılabilir, gelişmiş senaryolar için.
        var restartCount = await _configProvider.GetIntAsync("Schedule.Construction.RestartCount", 1, ct);
        restartCount = Math.Max(1, restartCount);

        // Pinned section'lar yerleştirilmiş state'in snapshot'ı; her restart fresh clone alır.
        var basePinnedState = state.Clone();

        ConstructionResult? bestResult = null;
        MutableScheduleState? bestState = null;

        for (var r = 0; r < restartCount; r++)
        {
            var trialState = r == 0 ? state : basePinnedState.Clone();
            // İlk restart deterministik (perturbationSeed=0) — eski davranışla bit-exact
            // uyum sağlar. Sonraki restart'lar farklı seed ile tie-break'lerde çeşitlenir.
            var perturbSeed = r == 0 ? 0 : seed + r * 31337;

            // Progress callback'i sadece ilk restart'ta (deterministik) gönderiyoruz —
            // multi-restart'ta birden çok restart UI'ya çelişen yüzde gönderirdi.
            var trialResult = constructionSolver.Solve(
                targetSections, trialState, classrooms, timeSlotsByDay,
                constraintConfig, constraintState, baseSnapshot,
                facultyClassroomIds, splitThreshold, backtrackLimit, mrvWeights,
                softLookaheadCount, perturbSeed,
                progressCallback: r == 0 ? periodicCallback : null);

            if (bestResult == null
                || trialResult.SuccessCount > bestResult.SuccessCount
                || (trialResult.SuccessCount == bestResult.SuccessCount && r == 0))
            {
                bestResult = trialResult;
                bestState = trialState;
            }
        }

        // En iyi restart'ın state'ine bağlan; SA + LNS bu state üstünde çalışacak.
        state = bestState!;
        var constructionResult = bestResult!;

        await ReportAsync("construction", 35,
            current: constructionResult.SuccessCount, total: targetSections.Count,
            detail: $"Yerleştirildi: {constructionResult.SuccessCount}/{targetSections.Count} (başarısız: {constructionResult.FailedCount})");

        // 11. Run SimulatedAnnealingOptimizer
        var nonPinnedCount = state.GetAssignedNonPinnedSectionIds().Count;
        var scaledIterations = Math.Max(1000, baseIterations * nonPinnedCount / 100);
        SAResult saResult;

        if (nonPinnedCount >= 2)
        {
            await ReportAsync("sa", 40,
                current: 0, total: scaledIterations,
                detail: $"Yerleşim optimize ediliyor (Simulated Annealing, {scaledIterations} iterasyon)...");

            var optimizer = new SimulatedAnnealingOptimizer(candidateGen, _constraintService);
            saResult = optimizer.Optimize(
                state, classrooms, timeSlotsByDay,
                constraintConfig, constraintState, baseSnapshot,
                facultyClassroomIds, splitThreshold, seed,
                scaledIterations, timeoutSeconds, stagnationLimit,
                initialTemperature, relocateWeight, swapWeight, chainWeight,
                progressCallback: periodicCallback);
        }
        else
        {
            saResult = new SAResult(0, 0, 0, 0);
        }

        await ReportAsync("sa", 65,
            current: saResult.Iterations, total: scaledIterations,
            detail: $"Optimizasyon tamamlandı ({saResult.Iterations} iterasyon, kalite +{Math.Round(saResult.InitialPenalty - saResult.FinalPenalty, 1)} puan)");

        // 11b. Run LNS to recover failed sections
        LNSResult lnsResult;
        if (constructionResult.FailedSectionIds.Count > 0 && lnsIterations > 0)
        {
            await ReportAsync("lns", 70,
                current: 0, total: constructionResult.FailedSectionIds.Count,
                detail: $"Çakışma çözümü ({constructionResult.FailedSectionIds.Count} ders kurtarılmaya çalışılıyor)...");

            var lns = new LargeNeighborhoodSearch(candidateGen, _constraintService);
            var failedIds = new List<int>(constructionResult.FailedSectionIds);

            lnsResult = lns.Search(
                state, targetSections, failedIds,
                classrooms, timeSlotsByDay,
                constraintConfig, constraintState, baseSnapshot,
                facultyClassroomIds, splitThreshold, seed,
                lnsIterations, lnsTimeoutSeconds, lnsDestroySize,
                softLookaheadCount,
                progressCallback: periodicCallback);

            // Update failed list — remove sections that LNS recovered
            constructionResult.FailedSectionIds.RemoveAll(id => state.IsAssigned(id));

            await ReportAsync("lns", 90,
                current: lnsResult.SectionsRecovered, total: constructionResult.FailedCount,
                detail: $"Çakışma çözümü tamamlandı ({lnsResult.SectionsRecovered} ders kurtarıldı)");
        }
        else
        {
            lnsResult = new LNSResult(0, 0, 0, 0);
        }

        // 12. Run FailureAnalyzer if there are failed sections
        var failureResults = new List<FailureAnalyzerResult>();
        if (constructionResult.FailedSectionIds.Count > 0)
        {
            var analyzer = new FailureAnalyzer(candidateGen, _constraintService);
            failureResults = analyzer.Analyze(
                constructionResult.FailedSectionIds,
                sectionSnapshots,
                state, classrooms, timeSlotsByDay,
                constraintConfig, constraintState, baseSnapshot,
                facultyClassroomIds, splitThreshold);
        }

        // 13. Build AutoSchedulePreviewDto
        stopwatch.Stop();

        var assignments = new List<AutoSchedulePreviewItemDto>();
        foreach (var sectionId in targetSectionIds)
        {
            if (constructionResult.FailedSectionIds.Contains(sectionId))
                continue;

            var placement = state.GetPlacement(sectionId);
            if (placement == null)
                continue;

            var snap = placement.Snapshot;
            var deptCode = snap.DepartmentId.HasValue && departmentCodeMap.TryGetValue(snap.DepartmentId.Value, out var dc)
                ? dc
                : string.Empty;

            // Determine primary classroom (online bloklarda null)
            var primaryClassroomId = placement.Blocks.Count > 0
                ? placement.Blocks[0].ClassroomId
                : null;
            var primaryClassroom = primaryClassroomId.HasValue
                ? classrooms.FirstOrDefault(c => c.Id == primaryClassroomId.Value)
                : null;

            // Paralel split: aynı pencerede birden fazla classroom varsa "L139+L141" formatında birleşik göster
            var classroomDisplayName = BuildClassroomDisplayName(placement.Blocks, classrooms, primaryClassroom?.Name);

            // Build schedule slots
            var scheduleSlots = placement.Blocks
                .SelectMany(b =>
                {
                    var classroom = b.ClassroomId.HasValue
                        ? classrooms.FirstOrDefault(c => c.Id == b.ClassroomId.Value)
                        : null;
                    return new[]
                    {
                        new ScheduleSlotDto
                        {
                            Day = b.Day,
                            StartTime = b.StartTime,
                            EndTime = b.EndTime,
                            ClassroomId = b.ClassroomId,
                            ClassroomName = classroom?.Name
                        }
                    };
                })
                .ToList();

            // Build schedule summary
            var summary = BuildScheduleSummary(placement.Blocks, classrooms);

            assignments.Add(new AutoSchedulePreviewItemDto
            {
                SectionId = sectionId,
                SectionNumber = snap.SectionNumber,
                CourseCode = snap.CourseCode,
                CourseName = snap.CourseName,
                DepartmentCode = deptCode,
                ClassroomId = primaryClassroomId,
                ClassroomName = classroomDisplayName,
                ScheduleSlots = scheduleSlots,
                ScheduleSummary = summary,
                InstructorId = snap.InstructorId,
                InstructorName = snap.InstructorName,
                FitScore = 0 // Will be calculated if penalty info is needed per-section
            });
        }

        var failures = failureResults.Select(f => new AutoScheduleFailureDto
        {
            SectionId = f.SectionId,
            SectionNumber = f.SectionNumber,
            Capacity = f.Capacity,
            CourseCode = f.CourseCode,
            CourseName = f.CourseName,
            Reason = f.Reason,
            Suggestions = f.Suggestions
        }).ToList();

        var invalidatedList = invalidationReasons
            .Select(kvp =>
            {
                var snap = sectionSnapshots.TryGetValue(kvp.Key, out var s) ? s : null;
                return new InvalidatedAssignmentDto
                {
                    SectionId = kvp.Key,
                    SectionNumber = snap?.SectionNumber ?? 0,
                    CourseCode = snap?.CourseCode ?? string.Empty,
                    CourseName = snap?.CourseName ?? string.Empty,
                    Reason = kvp.Value,
                };
            })
            .ToList();

        await ReportAsync("complete", 100,
            current: assignments.Count, total: targetSectionIds.Count,
            detail: $"Tamamlandı: {assignments.Count}/{targetSectionIds.Count} ders yerleştirildi ({Math.Round(stopwatch.Elapsed.TotalSeconds, 1)}s)");

        return new AutoSchedulePreviewDto
        {
            TotalProcessed = targetSectionIds.Count,
            SuccessCount = assignments.Count,
            FailedCount = failures.Count,
            Assignments = assignments,
            Failures = failures,
            InvalidatedAssignments = invalidatedList,
            Seed = seed,
            TotalPenalty = saResult.FinalPenalty,
            Stats = new AutoScheduleStats
            {
                ConstructionPlaced = constructionResult.SuccessCount,
                ConstructionFailed = constructionResult.FailedCount,
                SAIterations = saResult.Iterations,
                SAInitialPenalty = saResult.InitialPenalty,
                SAFinalPenalty = saResult.FinalPenalty,
                ElapsedSeconds = stopwatch.Elapsed.TotalSeconds
            }
        };
        }
        catch (OperationCanceledException)
        {
            // Hub.Cancel() veya request abort tetiklendi — kullanıcıya temiz bilgi ver, exception'ı yukarı fırlat.
            if (!string.IsNullOrEmpty(userId))
                await _progressReporter.ReportAsync(userId,
                    new ScheduleProgressEvent("failed", 0, 0, 0, "İşlem iptal edildi."), CancellationToken.None);
            throw;
        }
        finally
        {
            // Registry'den temizle — sonraki run aynı kullanıcı için yeni CTS register edebilsin.
            if (!string.IsNullOrEmpty(userId))
                _cancelRegistry?.Unregister(userId);
        }
    }

    /// <summary>
    /// Faz 0: Mevcut atanmış section'ların her bloğu için slot AllowedMode kontrolü yapar.
    /// FaceToFace section'ı OnlineOnly slotta ise (veya FaceToFaceOnly slotta Online ders ise) ihlal.
    /// İhlal bulunan section'ların tüm SectionSchedules kayıtlarını siler, in-memory Collection'ı da
    /// temizler ki main loop "atanmamış" olarak işleyip target listesine alsın.
    /// Döner: sectionId → neden açıklaması.
    /// </summary>
    private async Task<Dictionary<int, string>> InvalidateAssignmentsViolatingSlotModeAsync(
        List<CourseSection> sections,
        Dictionary<(DayOfWeek Day, TimeOnly StartTime), Domain.Enums.TimeSlotAllowedMode> slotAllowedModes,
        CancellationToken ct)
    {
        var invalidated = new Dictionary<int, string>();
        if (slotAllowedModes.Count == 0) return invalidated;

        var toDelete = new List<SectionSchedule>();

        // ── Pass 1: HARD ihlaller ──
        // OnlineOnly slot + FaceToFace ders, veya FaceToFaceOnly slot + Online ders → her zaman invalidate.
        foreach (var section in sections)
        {
            if (section.SectionSchedules.Count == 0) continue;

            string? reason = null;
            foreach (var ss in section.SectionSchedules)
            {
                var ts = ss.TimeSlot;
                if (ts == null) continue;
                if (!slotAllowedModes.TryGetValue((ts.DayOfWeek, ts.StartTime), out var mode))
                    continue;

                var method = section.Course.TeachingMethod;
                if (mode == Domain.Enums.TimeSlotAllowedMode.OnlineOnly && method == TeachingMethod.FaceToFace)
                {
                    reason = $"Slot {ts.DayOfWeek} {ts.StartTime:HH\\:mm}–{ts.EndTime:HH\\:mm} artık OnlineOnly; yüz-yüze ders atanamaz.";
                    break;
                }
                if (mode == Domain.Enums.TimeSlotAllowedMode.FaceToFaceOnly && method == TeachingMethod.Online)
                {
                    reason = $"Slot {ts.DayOfWeek} {ts.StartTime:HH\\:mm}–{ts.EndTime:HH\\:mm} artık FaceToFaceOnly; online ders atanamaz.";
                    break;
                }
            }

            if (reason != null)
            {
                invalidated[section.Id] = reason;
                toDelete.AddRange(section.SectionSchedules);
                section.SectionSchedules.Clear();
            }
        }

        // ── Pass 2: PREFERENCE mismatch (Online/Hybrid Both slot'ta) ──
        // Online dersler OnlineOnly slot'a yerleşmeli (priority kuralı). Eğer mevcut atamalar
        // tamamen Both slot'taysa VE yeterli sayıda boş OnlineOnly slot varsa → invalidate edilir
        // ki auto-schedule re-arrange ederken OnlineOnly priority tetiklensin. Hybrid için de
        // benzer kural: gündüz Both slot'larını gereksiz işgal etmesin, OnlineOnly tercih edilsin.
        var totalOnlineOnlySlots = slotAllowedModes
            .Count(kvp => kvp.Value == Domain.Enums.TimeSlotAllowedMode.OnlineOnly);

        if (totalOnlineOnlySlots > 0)
        {
            // OnlineOnly slot'ların ne kadarı şu an dolu (henüz invalidate edilmemiş atamalar)
            var usedOnlineOnly = sections
                .Where(s => !invalidated.ContainsKey(s.Id))
                .SelectMany(s => s.SectionSchedules)
                .Count(ss => ss.TimeSlot != null
                    && slotAllowedModes.TryGetValue((ss.TimeSlot.DayOfWeek, ss.TimeSlot.StartTime), out var m)
                    && m == Domain.Enums.TimeSlotAllowedMode.OnlineOnly);

            var freeOnlineOnly = totalOnlineOnlySlots - usedOnlineOnly;

            // Online ders'leri öncelik (tam priority kuralı), sonra Hybrid'ler.
            var preferenceCandidates = sections
                .Where(s => !invalidated.ContainsKey(s.Id) && s.SectionSchedules.Count > 0)
                .Where(s => s.Course.TeachingMethod == TeachingMethod.Online
                         || s.Course.TeachingMethod == TeachingMethod.Hybrid)
                .OrderBy(s => s.Course.TeachingMethod == TeachingMethod.Online ? 0 : 1)
                .ToList();

            foreach (var section in preferenceCandidates)
            {
                if (freeOnlineOnly <= 0) break;

                // Atamalardan kaçı OnlineOnly slot'ta? Hepsi Both ise priority kuralı ihlal.
                var schedules = section.SectionSchedules.ToList();
                var slotModes = schedules
                    .Where(ss => ss.TimeSlot != null)
                    .Select(ss => slotAllowedModes.TryGetValue((ss.TimeSlot!.DayOfWeek, ss.TimeSlot.StartTime), out var m) ? m : Domain.Enums.TimeSlotAllowedMode.Both)
                    .ToList();

                var allInBothSlots = slotModes.Count > 0
                    && slotModes.All(m => m == Domain.Enums.TimeSlotAllowedMode.Both);

                if (!allInBothSlots) continue;

                var requiredSlots = schedules.Count;
                if (freeOnlineOnly < requiredSlots) continue;

                var methodLabel = section.Course.TeachingMethod == TeachingMethod.Online ? "Online" : "Hybrid";
                invalidated[section.Id] = $"{methodLabel} ders Both slot'ta — OnlineOnly slot kapasitesi yeterli, yeniden yerleştirilecek.";
                toDelete.AddRange(schedules);
                section.SectionSchedules.Clear();
                freeOnlineOnly -= requiredSlots;
            }
        }

        if (toDelete.Count > 0)
        {
            // Set-based delete — RemoveRange tracking pollution yarattığı için kullanılmaz
            // (Pass 2 birden fazla section'ı toplu invalidate ettiğinde Course graph attacher
            // duplicate-key çakışması üretir). ExecuteDeleteAsync EF tracking'e dokunmadan SQL
            // ile siler; in-memory section.SectionSchedules.Clear() zaten yukarıda yapıldı.
            var idsToDelete = toDelete.Select(ss => ss.Id).ToList();
            await _context.SectionSchedules
                .Where(ss => idsToDelete.Contains(ss.Id))
                .ExecuteDeleteAsync(ct);
        }

        return invalidated;
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private SectionSnapshot BuildSectionSnapshot(
        CourseSection section,
        IReadOnlyDictionary<int, string> instructorNameMap,
        CourseSchedulingMetadata courseMetadata)
    {
        var timeBlocks = ScheduleHelper.ToTimeBlocks(section.SectionSchedules);
        var instructorName = section.InstructorId.HasValue
            && instructorNameMap.TryGetValue(section.InstructorId.Value, out var n) ? n : string.Empty;

        var suggestedSemestersByDept = courseMetadata.SuggestedSemestersByDepartment
            .TryGetValue(section.CourseId, out var deptSems)
            ? deptSems.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            : new Dictionary<int, int>();

        var accessibleDepts = courseMetadata.AccessibleDepartments
            .TryGetValue(section.CourseId, out var adepts)
            ? adepts.ToHashSet()
            : new HashSet<int>();
        if (section.Course.DepartmentId.HasValue)
            accessibleDepts.Add(section.Course.DepartmentId.Value);

        var suggestedSemester = courseMetadata.MinSuggestedSemesters
            .TryGetValue(section.CourseId, out var sem) ? sem : (int?)null;

        return SectionSnapshotFactory.FromCourseSection(
            section, section.Classroom, section.Instructor, instructorName,
            timeBlocks, suggestedSemester, suggestedSemestersByDept, accessibleDepts);
    }

    private static List<BlockAssignment> BuildBlockAssignments(CourseSection section)
    {
        var schedules = section.SectionSchedules
            .OrderBy(ss => ss.TimeSlot.DayOfWeek)
            .ThenBy(ss => ss.TimeSlot.StartTime)
            .ToList();

        if (schedules.Count == 0)
            return new List<BlockAssignment>();

        // Group by (DayOfWeek, ClassroomId) and merge consecutive slots into blocks
        var groups = schedules
            .GroupBy(ss => (ss.TimeSlot.DayOfWeek, ss.ClassroomId))
            .ToList();

        var blocks = new List<BlockAssignment>();
        foreach (var group in groups)
        {
            var slots = group.OrderBy(ss => ss.TimeSlot.StartTime).ToList();
            var start = slots[0].TimeSlot.StartTime;
            var end = slots[^1].TimeSlot.EndTime;

            blocks.Add(new BlockAssignment(
                Type: blocks.Count == 0 && section.Course.TheoryHours > 0
                    ? BlockType.Theory
                    : BlockType.Lab,
                ClassroomId: group.Key.ClassroomId,
                Day: group.Key.DayOfWeek,
                StartTime: start,
                EndTime: end));
        }

        return blocks;
    }

    /// <summary>
    /// Paralel split'te birden fazla oda birleşik gösterilir ("L139+L141").
    /// Tek oda varsa sadece adı, hiç yoksa boş string döner.
    /// </summary>
    private static string BuildClassroomDisplayName(
        List<BlockAssignment> blocks,
        List<Classroom> classrooms,
        string? fallbackName)
    {
        var classroomNames = blocks
            .Where(b => b.ClassroomId.HasValue)
            .Select(b => classrooms.FirstOrDefault(c => c.Id == b.ClassroomId!.Value)?.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .Cast<string>()
            .Distinct()
            .ToList();

        if (classroomNames.Count == 0)
            return fallbackName ?? string.Empty;
        if (classroomNames.Count == 1)
            return classroomNames[0];
        return string.Join("+", classroomNames);
    }

    private static string BuildScheduleSummary(
        List<BlockAssignment> blocks,
        List<Classroom> classrooms)
    {
        if (blocks.Count == 0)
            return "Zamanlanmamis";

        var classroomLookup = classrooms.ToDictionary(c => c.Id);

        // Paralel split desteği: aynı (Day, StartTime, EndTime) pencerelerinde birden fazla
        // classroom varsa bunları tek girdide "L139+L141" şeklinde birleştir.
        var parts = blocks
            .GroupBy(b => (b.Day, b.StartTime, b.EndTime))
            .OrderBy(g => g.Key.Day)
            .ThenBy(g => g.Key.StartTime)
            .Select(g =>
            {
                var dayName = SchedulingHelpers.GetDayOfWeekName(g.Key.Day);
                var classroomNames = g
                    .Select(b =>
                    {
                        if (!b.ClassroomId.HasValue) return "Online";
                        if (classroomLookup.TryGetValue(b.ClassroomId.Value, out var cr)) return cr.Name;
                        return "?";
                    })
                    .Distinct()
                    .ToList();
                var roomLabel = string.Join("+", classroomNames);
                return $"{dayName} {g.Key.StartTime:HH:mm}-{g.Key.EndTime:HH:mm} ({roomLabel})";
            });

        return string.Join(", ", parts);
    }

    private static int ParseS4Threshold(string? parametersJson, int defaultValue)
    {
        if (string.IsNullOrWhiteSpace(parametersJson)) return defaultValue;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(parametersJson);
            if (doc.RootElement.TryGetProperty("S4MinCoPairThreshold", out var prop)
                && prop.ValueKind == System.Text.Json.JsonValueKind.Number
                && prop.TryGetInt32(out var v))
            {
                return v;
            }
        }
        catch { /* malformed JSON → default'a düş */ }
        return defaultValue;
    }

    private async Task<SchedulingConstraintConfig> LoadSchedulingConstraintConfigAsync(CancellationToken ct)
    {
        // Algoritma key'leri artık SchedulingAlgorithmConfigs tablosunda — tek source-of-truth.
        var config = await _context.SchedulingAlgorithmConfigs
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Key, c => c.Value, ct);
        return SchedulingConstraintConfig.FromSystemConfig(config);
    }

    private async Task<Dictionary<int, string>> LoadInstructorNameMapAsync(
        List<int> instructorIds, CancellationToken ct)
    {
        if (instructorIds.Count == 0)
            return new Dictionary<int, string>();

        return await _context.Instructors
            .AsNoTracking()
            .Where(i => instructorIds.Contains(i.Id))
            .Join(
                _context.Users.AsNoTracking(),
                i => i.UserId,
                u => u.Id,
                (i, u) => new
                {
                    i.Id,
                    FullName = $"{i.Title} {u.FirstName} {u.LastName}".Trim()
                })
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
    }

    private async Task<CourseSchedulingMetadata> LoadCourseSchedulingMetadataAsync(
        int termId, IEnumerable<int> courseIds, CancellationToken ct)
    {
        var ids = courseIds.Distinct().ToList();
        if (ids.Count == 0)
            return new CourseSchedulingMetadata();

        var curriculumRows = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => ids.Contains(cc.CourseId) && cc.Curriculum.IsActive && cc.IsCompulsory)
            .Select(cc => new
            {
                cc.CourseId,
                DepartmentId = cc.Curriculum.Program.DepartmentId,
                cc.SuggestedSemester
            })
            .ToListAsync(ct);

        var suggestedSemestersByDepartment = curriculumRows
            .GroupBy(row => row.CourseId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(row => row.DepartmentId)
                    .ToDictionary(deptGroup => deptGroup.Key, deptGroup => deptGroup.Min(row => row.SuggestedSemester)));

        var minSuggestedSemesters = suggestedSemestersByDepartment
            .ToDictionary(item => item.Key, item => item.Value.Values.Min());

        var courseDepartmentIds = await _context.Courses
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.DepartmentId })
            .ToDictionaryAsync(c => c.Id, c => c.DepartmentId, ct);

        var policyRows = await _context.CourseAccessPolicies
            .AsNoTracking()
            .Where(p => p.TermId == termId && ids.Contains(p.CourseId))
            .Select(p => new { p.CourseId, p.DepartmentId, p.FacultyId })
            .ToListAsync(ct);

        var facultyIds = policyRows
            .Where(p => !p.DepartmentId.HasValue && p.FacultyId.HasValue)
            .Select(p => p.FacultyId!.Value)
            .Distinct()
            .ToList();

        var facultyDepartmentMap = facultyIds.Count == 0
            ? new Dictionary<int, List<int>>()
            : await _context.Departments
                .AsNoTracking()
                .Where(d => facultyIds.Contains(d.FacultyId))
                .GroupBy(d => d.FacultyId)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.Select(d => d.Id).ToList(),
                    ct);

        var accessibleDepartments = new Dictionary<int, HashSet<int>>();
        foreach (var courseId in ids)
        {
            var depts = new HashSet<int>();

            // Add owning department
            if (courseDepartmentIds.TryGetValue(courseId, out var ownerDeptId) && ownerDeptId.HasValue)
                depts.Add(ownerDeptId.Value);

            // Add departments from access policies
            foreach (var policy in policyRows.Where(p => p.CourseId == courseId))
            {
                if (policy.DepartmentId.HasValue)
                {
                    depts.Add(policy.DepartmentId.Value);
                }
                else if (policy.FacultyId.HasValue && facultyDepartmentMap.TryGetValue(policy.FacultyId.Value, out var facDepts))
                {
                    foreach (var d in facDepts)
                        depts.Add(d);
                }
            }

            if (depts.Count > 0)
                accessibleDepartments[courseId] = depts;
        }

        return new CourseSchedulingMetadata
        {
            MinSuggestedSemesters = minSuggestedSemesters,
            SuggestedSemestersByDepartment = suggestedSemestersByDepartment,
            AccessibleDepartments = accessibleDepartments
        };
    }

    private async Task<List<CoEnrollmentPair>> LoadCoEnrollmentPairsAsync(
        int currentTermId, int minThreshold, CancellationToken ct)
    {
        var recentTermIds = await _context.AcademicTerms
            .AsNoTracking()
            .Where(t => t.Id < currentTermId)
            .OrderByDescending(t => t.Id)
            .Take(3)
            .Select(t => t.Id)
            .ToListAsync(ct);

        if (recentTermIds.Count == 0)
            return new List<CoEnrollmentPair>();

        var registrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(cr => cr.Status == RegistrationStatus.Active
                && cr.CourseSection != null
                && recentTermIds.Contains(cr.CourseSection.TermId))
            .Select(cr => new
            {
                cr.StudentId,
                cr.CourseSection.CourseId,
                cr.CourseSection.TermId
            })
            .ToListAsync(ct);

        var coPairCounts = new Dictionary<(int, int), int>();

        foreach (var termGroup in registrations.GroupBy(r => (r.StudentId, r.TermId)))
        {
            var courseIdList = termGroup.Select(r => r.CourseId).Distinct().OrderBy(x => x).ToList();
            for (var i = 0; i < courseIdList.Count; i++)
            {
                for (var j = i + 1; j < courseIdList.Count; j++)
                {
                    var key = (courseIdList[i], courseIdList[j]);
                    coPairCounts[key] = coPairCounts.GetValueOrDefault(key) + 1;
                }
            }
        }

        return coPairCounts
            .Where(kvp => kvp.Value >= minThreshold)
            .Select(kvp => new CoEnrollmentPair
            {
                CourseId1 = kvp.Key.Item1,
                CourseId2 = kvp.Key.Item2,
                StudentCount = kvp.Value
            })
            .ToList();
    }

    /// <summary>
    /// Plan 2 Task 19: SolverResult → AutoSchedulePreviewDto mapping.
    /// CP-SAT'ın ürettiği Assignment ve Failure listelerini frontend'in beklediği DTO formatına
    /// çevirir. Solver request içindeki section/classroom/timeslot lookup'larıyla zenginleştirir.
    /// Bilinmeyen id'leri sessizce atlar (defensive — solver malformed cevap üretirse engine
    /// patlamasın diye).
    /// </summary>
    private static AutoSchedulePreviewDto MapSolverResultToPreviewDto(
        SolverResult result,
        SolverRequest solverRequest,
        int seed)
    {
        var sectionLookup = solverRequest.Sections.ToDictionary(s => s.Id, s => s);
        var classroomLookup = solverRequest.Classrooms.ToDictionary(c => c.Id, c => c);
        var timeSlotLookup = solverRequest.TimeSlots.ToDictionary(ts => ts.Id, ts => ts);

        // ── Assignment mapping ───────────────────────────────────────────
        var assignmentDtos = new List<AutoSchedulePreviewItemDto>();
        foreach (var asn in result.Assignments)
        {
            if (!sectionLookup.TryGetValue(asn.SectionId, out var section))
            {
                continue; // unknown section id — defensive skip
            }

            var primaryClassroomId = asn.Slots.Count > 0 ? asn.Slots[0].ClassroomId : (int?)null;
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
                    var crName = classroomLookup.TryGetValue(s.ClassroomId, out var c) ? c.Name : "?";
                    return new ScheduleSlotDto
                    {
                        Day = ts?.DayOfWeek ?? DayOfWeek.Monday,
                        StartTime = ts?.StartTime ?? TimeOnly.MinValue,
                        EndTime = ts?.EndTime ?? TimeOnly.MinValue,
                        ClassroomId = s.ClassroomId,
                        ClassroomName = crName
                    };
                })
                .ToList();

            var summary = string.Join(", ", scheduleSlots.Select(ss =>
                $"{ss.Day} {ss.StartTime:HH\\:mm}-{ss.EndTime:HH\\:mm} ({ss.ClassroomName})"));

            assignmentDtos.Add(new AutoSchedulePreviewItemDto
            {
                SectionId = section.Id,
                SectionNumber = section.SectionNumber,
                CourseCode = section.CourseCode,
                CourseName = section.CourseName,
                DepartmentCode = string.Empty, // SolverRequest dept code taşımıyor (Plan 3'te eklenebilir)
                ClassroomId = primaryClassroomId,
                ClassroomName = primaryClassroom?.Name ?? string.Empty,
                ScheduleSlots = scheduleSlots,
                ScheduleSummary = summary,
                InstructorId = section.InstructorId,
                InstructorName = null,
                FitScore = 0
            });
        }

        // ── Failure mapping ──────────────────────────────────────────────
        var failureDtos = new List<AutoScheduleFailureDto>();
        foreach (var f in result.Failures)
        {
            if (!sectionLookup.TryGetValue(f.SectionId, out var section))
            {
                continue; // unknown section id — defensive skip
            }
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
                ElapsedSeconds = result.Diagnostics.ElapsedSeconds
            }
        };
    }

}
