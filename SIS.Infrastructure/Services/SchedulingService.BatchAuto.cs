using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Constraints;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling;

namespace SIS.Infrastructure.Services;

/// <summary>
/// SchedulingService — Batch auto-schedule akışı.
/// Birden fazla section'ı topluca otomatik programlamak için. AutoScheduleEngine'i
/// arka planda çağırır, sonuçları toplar, audit log + denetim verisi tutar.
/// İçinde inner DTO record'ları (AutoScheduleSummary vb) da var.
/// </summary>
public partial class SchedulingService
{
    public async Task<BatchAutoScheduleResultDto> BatchAutoScheduleAsync(BatchAutoScheduleRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = new BatchAutoScheduleResultDto();

        var resolvedTerm = await ResolveTermAsync(request.TermId, cancellationToken);
        if (resolvedTerm == null)
        {
            result.Notes.Add(request.TermId.HasValue
                ? "Belirtilen akademik dönem bulunamadı."
                : "Aktif akademik dönem bulunamadı.");
            return result;
        }

        result.Notes.Add($"Donem: {resolvedTerm.Name}");

        // ── Step 0: Load all sections for the scope ──────────────────────
        var sectionsQuery = _context.CourseSections
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .Where(cs => cs.TermId == resolvedTerm.Id && cs.Status == CourseSectionStatus.Active && cs.InstructorId != null);

        // Scope: faculty-based, department-based, or term-wide
        int? scopeFacultyId = request.FacultyId;

        if (request.FacultyId.HasValue)
        {
            // Faculty-based: schedule all departments in this faculty together
            var facultyDeptIds = await _context.Departments.AsNoTracking()
                .Where(d => d.FacultyId == request.FacultyId.Value)
                .Select(d => d.Id)
                .ToListAsync(cancellationToken);

            sectionsQuery = sectionsQuery.Where(cs =>
                cs.Course.DepartmentId.HasValue && facultyDeptIds.Contains(cs.Course.DepartmentId.Value));

            result.Notes.Add($"Kapsam: Fakulte ID {request.FacultyId.Value} ({facultyDeptIds.Count} bolum)");
        }
        else if (request.DepartmentId.HasValue)
        {
            sectionsQuery = sectionsQuery.Where(cs => cs.Course.DepartmentId == request.DepartmentId.Value);

            // Resolve faculty for this department
            scopeFacultyId = await _context.Departments.AsNoTracking()
                .Where(d => d.Id == request.DepartmentId.Value)
                .Select(d => (int?)d.FacultyId)
                .FirstOrDefaultAsync(cancellationToken);

            result.Notes.Add($"Kapsam: Bolum ID {request.DepartmentId.Value}");
        }

        var allSections = await sectionsQuery.ToListAsync(cancellationToken);

        // ── ForceRegenerate: clear non-locked schedules ──────────────────
        if (request.ForceRegenerate)
        {
            foreach (var sec in allSections.Where(s => !s.IsScheduleLocked))
            {
                sec.ClassroomId = null;

                // Clear SectionSchedule records
                var existingRecords = await _context.SectionSchedules
                    .Where(ss => ss.CourseSectionId == sec.Id)
                    .ToListAsync(cancellationToken);
                if (existingRecords.Count > 0)
                    _context.SectionSchedules.RemoveRange(existingRecords);
            }
            await _context.SaveChangesAsync(cancellationToken);
            result.Notes.Add("ForceRegenerate: Kilit disi tum section'larin programi temizlendi.");
        }

        // Determine which sections already have SectionSchedule records
        var allSectionIds = allSections.Select(s => s.Id).ToList();
        var scheduledSectionIds = await _context.SectionSchedules
            .AsNoTracking()
            .Where(ss => allSectionIds.Contains(ss.CourseSectionId))
            .Select(ss => ss.CourseSectionId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var scheduledSet = scheduledSectionIds.ToHashSet();

        // Separate locked and target sections
        var lockedSections = allSections.Where(s => s.IsScheduleLocked).ToList();
        var targetSections = allSections
            .Where(s => !s.IsScheduleLocked
                && (!s.ClassroomId.HasValue || !scheduledSet.Contains(s.Id)))
            .ToList();

        result.TotalSections = targetSections.Count;

        if (targetSections.Count == 0)
        {
            result.Notes.Add("Programsiz section bulunamadi, tum section'lar zaten programli veya kilitli.");
            result.SkippedCount = lockedSections.Count;
            return result;
        }

        result.Notes.Add($"{targetSections.Count} programsiz section bulundu, {lockedSections.Count} section kilitli.");

        // ── Load shared data ─────────────────────────────────────────────
        var timeSlots = await _context.TimeSlots.AsNoTracking()
            .Where(ts => ts.TermId == resolvedTerm.Id)
            .OrderBy(ts => ts.DayOfWeek).ThenBy(ts => ts.SlotOrder)
            .ToListAsync(cancellationToken);

        if (timeSlots.Count == 0)
        {
            result.Notes.Add("Bu donem icin zaman slotu tanimi bulunamadigindan otomatik programlama yapilamadi.");
            result.FailedCount = targetSections.Count;
            foreach (var sec in targetSections)
            {
                result.Results.Add(new BatchAutoScheduleItemDto
                {
                    SectionId = sec.Id,
                    CourseCode = sec.Course.Code,
                    CourseName = sec.Course.Name,
                    Success = false,
                    FailureReason = "Donem icin zaman slotu tanimi bulunamadi."
                });
            }
            return result;
        }

        var classroomAssignments = await _context.ClassroomTermAssignments.AsNoTracking()
            .Where(cta => cta.TermId == resolvedTerm.Id)
            .ToListAsync(cancellationToken);

        var deptFacultyMap = await _context.Departments.AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.FacultyId, cancellationToken);

        var allClassrooms = await _context.Classrooms.AsNoTracking()
            .Where(c => c.Capacity > 0)
            .ToListAsync(cancellationToken);

        // Load compulsory course info from active curricula
        var compulsoryCourseIds = await _context.CurriculumCourses.AsNoTracking()
            .Where(cc => cc.Curriculum.IsActive && cc.IsCompulsory)
            .Select(cc => cc.CourseId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var compulsorySet = new HashSet<int>(compulsoryCourseIds);

        var corequisites = await _context.CoursePrerequisites.AsNoTracking()
            .Where(cp => cp.IsCorequisite)
            .ToListAsync(cancellationToken);

        var softConfig = await LoadSoftConstraintConfigAsync(cancellationToken);
        var hardConfig = await LoadHardConstraintConfigAsync(cancellationToken);
        var constraintState = await _constraintEvaluation.LoadConstraintStateAsync(
            null, null, null, resolvedTerm.Id, cancellationToken);
        var systemConfig = await LoadSystemConfigAsync(cancellationToken);
        var maxBacktrackingDepth = GetInt(systemConfig, "Scheduling.MaxBacktrackingDepth", 3);

        // Load instructor names
        var instructorIds = allSections
            .Where(s => s.InstructorId.HasValue)
            .Select(s => s.InstructorId!.Value)
            .Distinct().ToList();
        var instructorNameMap = await LoadInstructorNameMapAsync(instructorIds, cancellationToken);

        // ── Step 1: Priority scoring ─────────────────────────────────────
        var scoredSections = new List<(CourseSection Section, double Score)>();

        foreach (var section in targetSections)
        {
            double score = 0;

            // 1. Classroom constraint tightness (MRV)
            var sectionFacultyId = section.Course.DepartmentId.HasValue
                ? deptFacultyMap.GetValueOrDefault(section.Course.DepartmentId.Value)
                : 0;

            var availableClassrooms = GetAvailableClassrooms(
                allClassrooms, classroomAssignments, sectionFacultyId, section.Capacity, section.Course);
            var domainSize = availableClassrooms.Count * timeSlots.Count;
            score += (1.0 / Math.Max(domainSize, 1)) * 1000;

            // 2. Compulsory course
            if (compulsorySet.Contains(section.CourseId))
                score += 500;

            // 3. Large course (amphitheater required)
            if (section.Capacity >= hardConfig.AmphiThreshold)
                score += 400;

            // 4. Lab course (only lab classrooms, limited)
            if (section.Course.LabHours > 0 && section.Course.TheoryHours == 0)
                score += 300;

            // 5. Multi-component course (lecture+lab together)
            var hasSiblingComponents = allSections.Any(s =>
                s.CourseId == section.CourseId
                && s.Id != section.Id
                && IsLabSection(s.Course) != IsLabSection(section.Course));
            if (hasSiblingComponents)
                score += 200;

            scoredSections.Add((section, score));
        }

        // Sort by priority score descending (highest priority first)
        scoredSections.Sort((a, b) => b.Score.CompareTo(a.Score));

        // ── Build initial term snapshot (includes locked + already scheduled) ──
        var termSnapshot = await BuildTermSnapshotAsync(resolvedTerm.Id, null, null, cancellationToken);
        double totalSoftPenalty = 0;
        int backtrackingAttempts = 0;

        // Track assignments for backtracking: sectionId -> (slots, classroomId, score)
        var assignedMap = new Dictionary<int, BatchAssignment>();

        // ── Step 2 & 3: Schedule each section ────────────────────────────
        foreach (var (section, priorityScore) in scoredSections)
        {
            var item = new BatchAutoScheduleItemDto
            {
                SectionId = section.Id,
                CourseCode = section.Course.Code,
                CourseName = section.Course.Name,
            };

            var sectionFacultyId = section.Course.DepartmentId.HasValue
                ? deptFacultyMap.GetValueOrDefault(section.Course.DepartmentId.Value)
                : 0;

            var availableClassrooms = GetAvailableClassrooms(
                allClassrooms, classroomAssignments, sectionFacultyId, section.Capacity, section.Course);

            if (availableClassrooms.Count == 0)
            {
                // Try without classroom assignment filter as fallback
                availableClassrooms = allClassrooms
                    .Where(c => c.Capacity >= section.Capacity && IsClassroomTypeCompatible(c, section.Course))
                    .OrderBy(c => c.Capacity - section.Capacity)
                    .ThenBy(c => c.Name)
                    .ToList();
            }

            var requiredHours = ResolveTotalRequiredHours(section);
            var currentBlocks = await LoadSectionTimeBlocksAsync(section.Id, cancellationToken);
            var slotCombinations = BuildTimeSlotCombinations(requiredHours, currentBlocks, timeSlots);

            // Find the best (slot, classroom) pair
            var bestPair = FindBestPair(
                section, availableClassrooms, slotCombinations,
                termSnapshot, softConfig, hardConfig, constraintState, instructorNameMap);

            if (bestPair != null)
            {
                // Apply assignment
                await ApplyBatchAssignment(section, bestPair, cancellationToken);
                UpdateSnapshotWithAssignment(termSnapshot, section, bestPair, instructorNameMap);
                assignedMap[section.Id] = bestPair;
                totalSoftPenalty += bestPair.SoftPenalty;

                item.Success = true;
                item.ClassroomName = bestPair.Classroom.Name;
                item.Slots = bestPair.Slots.Select(MapTimeBlock).ToList();
                result.ScheduledCount++;
            }
            else
            {
                // ── Step 3: Backtracking ─────────────────────────────────
                var backtrackResult = await TryBacktrackAsync(
                    section, priorityScore, scoredSections, assignedMap,
                    termSnapshot, availableClassrooms, slotCombinations,
                    allClassrooms, classroomAssignments, deptFacultyMap, timeSlots,
                    softConfig, hardConfig, constraintState, instructorNameMap,
                    maxBacktrackingDepth, cancellationToken);

                backtrackingAttempts += backtrackResult.Attempts;

                if (backtrackResult.Success)
                {
                    totalSoftPenalty += backtrackResult.SoftPenaltyDelta;

                    item.Success = true;
                    item.ClassroomName = backtrackResult.AssignedClassroomName;
                    item.Slots = backtrackResult.AssignedSlots;
                    result.ScheduledCount++;
                }
                else
                {
                    item.Success = false;
                    item.FailureReason = backtrackResult.FailureReason
                        ?? "Cakisma olmayan derslik + slot kombinasyonu bulunamadi.";
                    result.FailedCount++;
                }
            }

            result.Results.Add(item);
        }

        result.SkippedCount = lockedSections.Count;
        result.SoftPenaltyScore = Math.Round(totalSoftPenalty, 2);
        result.BacktrackingAttempts = backtrackingAttempts;
        result.Notes.Add($"Toplam soft penalty skoru: {result.SoftPenaltyScore}");
        if (backtrackingAttempts > 0)
            result.Notes.Add($"Backtracking deneme sayisi: {backtrackingAttempts}");

        return result;
    }

    // ── BatchAutoSchedule helper methods ────────────────────────────────

    private static bool IsLabSection(Course course) =>
        course.LabHours > 0 && course.TheoryHours == 0;

    private static bool IsClassroomTypeCompatible(Classroom classroom, Course course)
    {
        var isLab = course.LabHours > 0 && course.TheoryHours == 0;
        if (isLab)
            return classroom.Type == ClassroomType.Lab || classroom.Type == ClassroomType.Computer;

        // Large courses need amphitheater — but capacity check is separate
        return classroom.Type != ClassroomType.Lab;
    }

    private static List<Classroom> GetAvailableClassrooms(
        List<Classroom> allClassrooms,
        List<ClassroomTermAssignment> assignments,
        int facultyId,
        int requiredCapacity,
        Course course)
    {
        IEnumerable<Classroom> pool;

        if (assignments.Count > 0 && facultyId > 0)
        {
            var assignedIds = new HashSet<int>(
                assignments.Where(a => a.FacultyId == facultyId).Select(a => a.ClassroomId));
            pool = allClassrooms.Where(c => assignedIds.Contains(c.Id));
        }
        else
        {
            // No assignments configured — use all classrooms
            pool = allClassrooms;
        }

        return pool
            .Where(c => c.Capacity >= requiredCapacity && IsClassroomTypeCompatible(c, course))
            .OrderBy(c => c.Capacity - requiredCapacity)
            .ThenBy(c => c.Name)
            .ToList();
    }

    private BatchAssignment? FindBestPair(
        CourseSection section,
        List<Classroom> classrooms,
        List<RankedSlotCombination> slotCombinations,
        TermSnapshot snapshot,
        SoftConstraintConfig softConfig,
        HardConstraintConfig hardConfig,
        ConstraintState constraintState,
        IReadOnlyDictionary<int, string> instructorNameMap)
    {
        if (classrooms.Count == 0 || slotCombinations.Count == 0)
            return null;

        BatchAssignment? best = null;
        double bestPenalty = double.MaxValue;

        foreach (var slotCombo in slotCombinations)
        {
            foreach (var classroom in classrooms)
            {
                var candidate = BuildCandidateSnapshot(
                    section,
                    section.Instructor,
                    section.InstructorId.HasValue && instructorNameMap.TryGetValue(section.InstructorId.Value, out var name) ? name : string.Empty,
                    classroom,
                    slotCombo.Slots,
                    ResolveTotalRequiredHours(section),
                    new List<string>(),
                    suggestedSemester: snapshot.CourseSuggestedSemestersByDepartment.TryGetValue(section.CourseId, out var semestersByDept)
                        ? semestersByDept.Values.Min()
                        : null,
                    suggestedSemestersByDepartment: GetSuggestedSemestersByDepartment(section.CourseId, snapshot.CourseSuggestedSemestersByDepartment),
                    accessibleDepartmentIds: GetAccessibleDepartments(section.CourseId, section.Course.DepartmentId, snapshot.CourseAccessibleDepartments));

                var (conflicts, softNotes) = EvaluateCandidateConflicts(candidate, snapshot, softConfig, hardConfig, constraintState);
                if (conflicts.Count > 0)
                    continue;

                // Calculate soft penalty score
                double penalty = slotCombo.PreferenceScore;
                penalty += CalculateSoftPenalty(candidate, snapshot, softConfig, constraintState);

                if (penalty < bestPenalty)
                {
                    bestPenalty = penalty;
                    best = new BatchAssignment
                    {
                        Slots = slotCombo.Slots,
                        Classroom = classroom,
                        SoftPenalty = penalty,
                        SoftNotes = softNotes
                    };
                }
            }
        }

        return best;
    }

    private double CalculateSoftPenalty(
        SectionSnapshot candidate,
        TermSnapshot snapshot,
        SoftConstraintConfig softConfig,
        ConstraintState? constraintState)
    {
        var config = BuildConstraintConfig(softConfig, null);
        return _constraintEvaluation.CalculatePenalty(candidate, snapshot, config, constraintState);
    }

    private async Task ApplyBatchAssignment(
        CourseSection section,
        BatchAssignment assignment,
        CancellationToken cancellationToken)
    {
        section.ClassroomId = assignment.Classroom.Id;

        await WriteSectionScheduleRecordsAsync(
            section.Id, section.TermId, assignment.Classroom.Id, assignment.Slots, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task UnapplyBatchAssignment(
        CourseSection section,
        CancellationToken cancellationToken)
    {
        section.ClassroomId = null;

        var existing = await _context.SectionSchedules
            .Where(ss => ss.CourseSectionId == section.Id)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0)
            _context.SectionSchedules.RemoveRange(existing);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static void UpdateSnapshotWithAssignment(
        TermSnapshot snapshot,
        CourseSection section,
        BatchAssignment assignment,
        IReadOnlyDictionary<int, string> instructorNameMap)
    {
        // Remove any existing snapshot entry for this section
        snapshot.Sections.RemoveAll(s => s.SectionId == section.Id);

        // Add updated entry
        snapshot.Sections.Add(new SectionSnapshot
        {
            SectionId = section.Id,
            CourseId = section.CourseId,
            CourseCode = section.Course.Code,
            CourseName = section.Course.Name,
            TermId = section.TermId,
            TheoryHours = section.Course.TheoryHours,
            LabHours = section.Course.LabHours,
            Capacity = section.Capacity,
            EnrolledCount = section.EnrolledCount,
            ClassroomId = assignment.Classroom.Id,
            ClassroomName = assignment.Classroom.Name,
            ClassroomCapacity = assignment.Classroom.Capacity,
            ClassroomType = assignment.Classroom.Type,
            TheoryClassroomType = section.Course.TheoryClassroomType,
            LabClassroomType = section.Course.LabClassroomType,
            InstructorId = section.InstructorId,
            InstructorName = section.InstructorId.HasValue && instructorNameMap.TryGetValue(section.InstructorId.Value, out var n) ? n : string.Empty,
            InstructorMaxWeeklyHours = section.Instructor?.MaxWeeklyHours ?? 0,
            DepartmentId = section.Course.DepartmentId,
            SuggestedSemester = snapshot.CourseSuggestedSemestersByDepartment.TryGetValue(section.CourseId, out var semestersByDept)
                ? semestersByDept.Values.Min()
                : null,
            SuggestedSemestersByDepartment = GetSuggestedSemestersByDepartment(section.CourseId, snapshot.CourseSuggestedSemestersByDepartment),
            AccessibleDepartmentIds = GetAccessibleDepartments(section.CourseId, section.Course.DepartmentId, snapshot.CourseAccessibleDepartments),
            TimeBlocks = assignment.Slots,
            Notes = new List<string>()
        });
    }

    private static void RemoveFromSnapshot(TermSnapshot snapshot, int sectionId)
    {
        snapshot.Sections.RemoveAll(s => s.SectionId == sectionId);
    }

    private async Task<BacktrackResult> TryBacktrackAsync(
        CourseSection section,
        double sectionPriorityScore,
        List<(CourseSection Section, double Score)> scoredSections,
        Dictionary<int, BatchAssignment> assignedMap,
        TermSnapshot snapshot,
        List<Classroom> sectionClassrooms,
        List<RankedSlotCombination> sectionSlotCombinations,
        List<Classroom> allClassrooms,
        List<ClassroomTermAssignment> classroomAssignments,
        Dictionary<int, int> deptFacultyMap,
        List<TimeSlot> timeSlots,
        SoftConstraintConfig softConfig,
        HardConstraintConfig hardConfig,
        ConstraintState constraintState,
        IReadOnlyDictionary<int, string> instructorNameMap,
        int maxDepth,
        CancellationToken cancellationToken)
    {
        if (maxDepth <= 0)
        {
            return new BacktrackResult
            {
                Success = false,
                FailureReason = "Cakisma olmayan derslik + slot kombinasyonu bulunamadi (backtracking limiti)."
            };
        }

        // Find sections that conflict with any possible placement of the current section
        var conflictingSectionIds = new HashSet<int>();

        foreach (var slotCombo in sectionSlotCombinations)
        {
            foreach (var classroom in sectionClassrooms)
            {
                var candidate = BuildCandidateSnapshot(
                    section,
                    section.Instructor,
                    section.InstructorId.HasValue && instructorNameMap.TryGetValue(section.InstructorId.Value, out var name) ? name : string.Empty,
                    classroom,
                    slotCombo.Slots,
                    ResolveTotalRequiredHours(section),
                    new List<string>(),
                    suggestedSemester: snapshot.CourseSuggestedSemestersByDepartment.TryGetValue(section.CourseId, out var semestersByDept)
                        ? semestersByDept.Values.Min()
                        : null,
                    suggestedSemestersByDepartment: GetSuggestedSemestersByDepartment(section.CourseId, snapshot.CourseSuggestedSemestersByDepartment),
                    accessibleDepartmentIds: GetAccessibleDepartments(section.CourseId, section.Course.DepartmentId, snapshot.CourseAccessibleDepartments));

                var (conflicts, _) = EvaluateCandidateConflicts(candidate, snapshot, softConfig, hardConfig, constraintState);
                foreach (var conflict in conflicts)
                {
                    if (conflict.RelatedSectionId.HasValue && assignedMap.ContainsKey(conflict.RelatedSectionId.Value))
                        conflictingSectionIds.Add(conflict.RelatedSectionId.Value);
                }
            }
        }

        if (conflictingSectionIds.Count == 0)
        {
            return new BacktrackResult
            {
                Success = false,
                FailureReason = "Cakisma olmayan derslik + slot kombinasyonu bulunamadi.",
                Attempts = 1
            };
        }

        // Among conflicting sections, find the one with the lowest priority score
        var conflictCandidates = scoredSections
            .Where(s => conflictingSectionIds.Contains(s.Section.Id) && !s.Section.IsScheduleLocked)
            .OrderBy(s => s.Score)
            .ToList();

        int attempts = 0;

        foreach (var (victimSection, victimScore) in conflictCandidates)
        {
            if (attempts >= maxDepth)
                break;

            attempts++;

            // Save victim's assignment for potential restore
            var victimAssignment = assignedMap[victimSection.Id];

            // Unassign the victim
            RemoveFromSnapshot(snapshot, victimSection.Id);
            await UnapplyBatchAssignment(victimSection, cancellationToken);
            assignedMap.Remove(victimSection.Id);

            // Try to place the current section
            var bestPair = FindBestPair(
                section, sectionClassrooms, sectionSlotCombinations,
                snapshot, softConfig, hardConfig, constraintState, instructorNameMap);

            if (bestPair != null)
            {
                // Place current section
                await ApplyBatchAssignment(section, bestPair, cancellationToken);
                UpdateSnapshotWithAssignment(snapshot, section, bestPair, instructorNameMap);
                assignedMap[section.Id] = bestPair;

                // Try to re-place the victim in a different slot
                var victimFacultyId = victimSection.Course.DepartmentId.HasValue
                    ? deptFacultyMap.GetValueOrDefault(victimSection.Course.DepartmentId.Value)
                    : 0;

                var victimClassrooms = GetAvailableClassrooms(
                    allClassrooms, classroomAssignments, victimFacultyId,
                    victimSection.Capacity, victimSection.Course);

                var victimRequiredHours = ResolveTotalRequiredHours(victimSection);
                var victimSlotCombinations = BuildTimeSlotCombinations(
                    victimRequiredHours, new List<TimeBlock>(), timeSlots);

                var victimNewPair = FindBestPair(
                    victimSection, victimClassrooms, victimSlotCombinations,
                    snapshot, softConfig, hardConfig, constraintState, instructorNameMap);

                if (victimNewPair != null)
                {
                    await ApplyBatchAssignment(victimSection, victimNewPair, cancellationToken);
                    UpdateSnapshotWithAssignment(snapshot, victimSection, victimNewPair, instructorNameMap);
                    assignedMap[victimSection.Id] = victimNewPair;

                    var penaltyDelta = bestPair.SoftPenalty + victimNewPair.SoftPenalty - victimAssignment.SoftPenalty;

                    return new BacktrackResult
                    {
                        Success = true,
                        Attempts = attempts,
                        SoftPenaltyDelta = penaltyDelta,
                        AssignedClassroomName = bestPair.Classroom.Name,
                        AssignedSlots = bestPair.Slots.Select(MapTimeBlock).ToList()
                    };
                }
                else
                {
                    // Victim could not be re-placed — revert everything
                    RemoveFromSnapshot(snapshot, section.Id);
                    await UnapplyBatchAssignment(section, cancellationToken);
                    assignedMap.Remove(section.Id);

                    // Restore victim
                    await ApplyBatchAssignment(victimSection, victimAssignment, cancellationToken);
                    UpdateSnapshotWithAssignment(snapshot, victimSection, victimAssignment, instructorNameMap);
                    assignedMap[victimSection.Id] = victimAssignment;
                }
            }
            else
            {
                // Couldn't place current section even after removing victim — restore victim
                await ApplyBatchAssignment(victimSection, victimAssignment, cancellationToken);
                UpdateSnapshotWithAssignment(snapshot, victimSection, victimAssignment, instructorNameMap);
                assignedMap[victimSection.Id] = victimAssignment;
            }
        }

        return new BacktrackResult
        {
            Success = false,
            Attempts = attempts,
            FailureReason = "Backtracking sonrasinda da uygun derslik + slot kombinasyonu bulunamadi."
        };
    }

    private sealed class BatchAssignment
    {
        public List<TimeBlock> Slots { get; init; } = new();
        public Classroom Classroom { get; init; } = null!;
        public double SoftPenalty { get; init; }
        public List<string> SoftNotes { get; init; } = new();
    }

    private sealed class BacktrackResult
    {
        public bool Success { get; init; }
        public int Attempts { get; init; }
        public double SoftPenaltyDelta { get; init; }
        public string? FailureReason { get; init; }
        public string? AssignedClassroomName { get; init; }
        public List<ScheduleSlotDto>? AssignedSlots { get; init; }
    }
}
