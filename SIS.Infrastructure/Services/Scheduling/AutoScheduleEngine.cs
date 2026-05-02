using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling;

public sealed class AutoScheduleEngine
{
    private readonly AppDbContext _context;
    private readonly ConstraintEvaluationService _constraintService;
    private readonly ISystemConfigProvider _configProvider;

    public AutoScheduleEngine(
        AppDbContext context,
        ConstraintEvaluationService constraintService,
        ISystemConfigProvider configProvider)
    {
        _context = context;
        _constraintService = constraintService;
        _configProvider = configProvider;
    }

    public async Task<AutoSchedulePreviewDto> RunAsync(
        AutoScheduleRequest request,
        CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 1. Determine seed
        var seed = request.Seed ?? (int)(DateTime.UtcNow.Ticks % int.MaxValue);

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

        var s4Enabled = await _configProvider.GetBoolAsync("SoftConstraint.S4.Enabled", true, ct);
        var s4Threshold = await _configProvider.GetIntAsync("SoftConstraint.S4.MinCoPairThreshold", 20, ct);
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

        // 8b. Build cohort assignments if cohort mode enabled
        if (constraintConfig.CurriculumConflictMode == "cohort")
        {
            var cohortBuilder = new CohortBuilder();
            var allSnaps = sectionSnapshots.Values.ToList();
            var cohortAssignment = cohortBuilder.Build(
                allSnaps,
                courseMetadata.SuggestedSemestersByDepartment,
                courseMetadata.AccessibleDepartments);
            baseSnapshot.CohortConflictPairs = cohortAssignment.ConflictPairs;
        }
        else if (constraintConfig.CurriculumConflictMode == "enrollment")
        {
            var allSectionIds = sections.Select(s => s.Id);
            var enrollmentMatrix = await EnrollmentConflictMatrix.BuildAsync(
                _context, request.TermId, allSectionIds,
                constraintConfig.EnrollmentConflictHardThreshold, ct);
            baseSnapshot.EnrollmentConflicts = enrollmentMatrix;
        }

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

        // 10. Run ConstructionSolver
        var candidateGen = new CandidateGenerator();
        var constructionSolver = new ConstructionSolver(candidateGen, _constraintService);

        var constructionResult = constructionSolver.Solve(
            targetSections, state, classrooms, timeSlotsByDay,
            constraintConfig, constraintState, baseSnapshot,
            facultyClassroomIds, splitThreshold, backtrackLimit, mrvWeights);

        // 11. Run SimulatedAnnealingOptimizer
        var nonPinnedCount = state.GetAssignedNonPinnedSectionIds().Count;
        var scaledIterations = Math.Max(1000, baseIterations * nonPinnedCount / 100);
        SAResult saResult;

        if (nonPinnedCount >= 2)
        {
            var optimizer = new SimulatedAnnealingOptimizer(candidateGen, _constraintService);
            saResult = optimizer.Optimize(
                state, classrooms, timeSlotsByDay,
                constraintConfig, constraintState, baseSnapshot,
                facultyClassroomIds, splitThreshold, seed,
                scaledIterations, timeoutSeconds, stagnationLimit,
                initialTemperature, relocateWeight, swapWeight, chainWeight);
        }
        else
        {
            saResult = new SAResult(0, 0, 0, 0);
        }

        // 11b. Run LNS to recover failed sections
        LNSResult lnsResult;
        if (constructionResult.FailedSectionIds.Count > 0 && lnsIterations > 0)
        {
            var lns = new LargeNeighborhoodSearch(candidateGen, _constraintService);
            var failedIds = new List<int>(constructionResult.FailedSectionIds);

            lnsResult = lns.Search(
                state, targetSections, failedIds,
                classrooms, timeSlotsByDay,
                constraintConfig, constraintState, baseSnapshot,
                facultyClassroomIds, splitThreshold, seed,
                lnsIterations, lnsTimeoutSeconds, lnsDestroySize);

            // Update failed list — remove sections that LNS recovered
            constructionResult.FailedSectionIds.RemoveAll(id => state.IsAssigned(id));
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
                section.SectionSchedules.Clear(); // in-memory da temizle ki isScheduled=false olsun
            }
        }

        if (toDelete.Count > 0)
        {
            _context.SectionSchedules.RemoveRange(toDelete);
            await _context.SaveChangesAsync(ct);
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
        var instructorId = section.InstructorId;

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

        var totalHours = timeBlocks.Sum(b =>
            (int)Math.Ceiling((b.EndTime - b.StartTime).TotalHours));

        return new SectionSnapshot
        {
            SectionId = section.Id,
            SectionNumber = section.SectionNumber,
            CourseId = section.CourseId,
            CourseCode = section.Course.Code,
            CourseName = section.Course.Name,
            TermId = section.TermId,
            TeachingMethod = section.Course.TeachingMethod,
            TheoryHours = section.Course.TheoryHours,
            LabHours = section.Course.LabHours,
            Capacity = section.Capacity,
            EnrolledCount = section.EnrolledCount,
            ClassroomId = section.ClassroomId,
            ClassroomName = section.Classroom?.Name ?? string.Empty,
            ClassroomCapacity = section.Classroom?.Capacity ?? 0,
            ClassroomType = section.Classroom?.Type ?? ClassroomType.Lecture,
            TheoryClassroomType = section.Course.TheoryClassroomType,
            LabClassroomType = section.Course.LabClassroomType,
            InstructorId = instructorId,
            InstructorName = instructorId.HasValue && instructorNameMap.TryGetValue(instructorId.Value, out var instrName)
                ? instrName
                : string.Empty,
            InstructorMaxWeeklyHours = section.Instructor?.MaxWeeklyHours ?? 20,
            DepartmentId = section.Course.DepartmentId,
            SuggestedSemester = suggestedSemester,
            SuggestedSemestersByDepartment = suggestedSemestersByDept,
            AccessibleDepartmentIds = accessibleDepts,
            AssignedTimeSlotCount = totalHours,
            TimeBlocks = timeBlocks,
            Notes = new List<string>()
        };
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

    private async Task<SchedulingConstraintConfig> LoadSchedulingConstraintConfigAsync(CancellationToken ct)
    {
        var config = await _context.SystemConfigs
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Key, c => c.Value, ct);

        var lunchIsHard = GetBool(config, "Schedule.LunchBreak.IsHard", false);
        var lunchStart = config.TryGetValue("Schedule.LunchBreakStart", out var ls) && TimeOnly.TryParse(ls, out var lst)
            ? lst : new TimeOnly(11, 40);
        var lunchEnd = config.TryGetValue("Schedule.LunchBreakEnd", out var le) && TimeOnly.TryParse(le, out var let_)
            ? let_ : new TimeOnly(12, 20);

        var s9MorningStart = config.TryGetValue("SoftConstraint.S9.MorningStart", out var ms) && TimeOnly.TryParse(ms, out var mst)
            ? mst : new TimeOnly(8, 0);
        var s9EveningStart = config.TryGetValue("SoftConstraint.S9.EveningStart", out var es) && TimeOnly.TryParse(es, out var est)
            ? est : new TimeOnly(17, 0);

        return new SchedulingConstraintConfig
        {
            LunchBreakIsHard = lunchIsHard,
            LunchBreakStart = lunchStart,
            LunchBreakEnd = lunchEnd,
            MaxConsecutiveHours = GetInt(config, "Schedule.MaxConsecutiveHours", 4),
            BreakDurationMinutes = GetInt(config, "Schedule.BreakDurationMinutes", 5),
            AmphiThreshold = GetInt(config, "Classroom.AmphiThreshold", 100),
            TheoryLabSplitThreshold = GetInt(config, "Schedule.TheoryLabSplitThreshold", 4),
            S1Enabled = GetBool(config, "SoftConstraint.S1.Enabled", true),
            S1MinOccupancy = GetInt(config, "Classroom.OccupancyTarget.Min", 60),
            S1MaxOccupancy = GetInt(config, "Classroom.OccupancyTarget.Max", 85),
            S2Enabled = GetBool(config, "SoftConstraint.S2.Enabled", true),
            S2MaxDailyHours = GetInt(config, "SoftConstraint.S2.MaxDailyHours", 6),
            S3Enabled = GetBool(config, "SoftConstraint.S3.Enabled", true),
            S3MaxSectionsPerSlot = GetInt(config, "SoftConstraint.S3.MaxSectionsPerSlot", 5),
            S4Enabled = GetBool(config, "SoftConstraint.S4.Enabled", true),
            S4MinCoPairThreshold = GetInt(config, "SoftConstraint.S4.MinCoPairThreshold", 20),
            S5Enabled = GetBool(config, "SoftConstraint.S5.Enabled", true),
            S7Enabled = GetBool(config, "SoftConstraint.S7.Enabled", true),
            S7MaxGapSlots = GetInt(config, "SoftConstraint.S7.MaxGapSlots", 3),
            S8Enabled = GetBool(config, "SoftConstraint.S8.Enabled", true),
            S9Enabled = GetBool(config, "SoftConstraint.S9.Enabled", true),
            S9MorningStart = s9MorningStart,
            S9EveningStart = s9EveningStart,
            S12Enabled = GetBool(config, "SoftConstraint.S12.Enabled", true),
            S12MaxImbalancePercent = GetInt(config, "SoftConstraint.S12.MaxImbalancePercent", 20),
            AllowSameCourseSectionOverlap = GetBool(config, "Schedule.AllowSameCourseSectionOverlap", false),
            CurriculumConflictMode = config.TryGetValue("Schedule.CurriculumConflictMode", out var mode) ? mode : "curriculum",
            EnrollmentConflictHardThreshold = GetInt(config, "Schedule.EnrollmentConflictHardThreshold", 1),
            SplittableBlockTypes = ParseSplittableBlockTypes(
                config.TryGetValue("Schedule.SplittableBlockTypes", out var sbt) ? sbt : "Lab"),
        };
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

    private static bool GetBool(Dictionary<string, string> config, string key, bool defaultValue)
    {
        return config.TryGetValue(key, out var value)
            ? bool.TryParse(value, out var result) ? result : defaultValue
            : defaultValue;
    }

    private static int GetInt(Dictionary<string, string> config, string key, int defaultValue)
    {
        return config.TryGetValue(key, out var value)
            ? int.TryParse(value, out var result) ? result : defaultValue
            : defaultValue;
    }

    /// <summary>
    /// "Lab,Theory" gibi virgülle ayrılmış config değerini BlockType set'ine çevirir.
    /// Geçersiz/tanınmayan değerler sessizce atlanır. Boş string boş set döner (= split kapalı).
    /// </summary>
    private static HashSet<BlockType> ParseSplittableBlockTypes(string value)
    {
        var result = new HashSet<BlockType>();
        if (string.IsNullOrWhiteSpace(value))
            return result;

        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<BlockType>(token, ignoreCase: true, out var bt))
                result.Add(bt);
        }
        return result;
    }
}
