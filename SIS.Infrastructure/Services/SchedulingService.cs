using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Scheduling;

namespace SIS.Infrastructure.Services;

public class SchedulingService : ISchedulingService
{
    private readonly AppDbContext _context;
    private readonly ConstraintEvaluationService _constraintEvaluation;

    public SchedulingService(AppDbContext context, ConstraintEvaluationService constraintEvaluation)
    {
        _context = context;
        _constraintEvaluation = constraintEvaluation;
    }

    public async Task<List<SchedulingConflictDto>> GetConflictsAsync(int? termId = null, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateExistingAsync(termId, cancellationToken);
        return validation.Conflicts;
    }

    public async Task<List<SchedulingConflictDto>> GetConflictsByDepartmentAsync(int departmentId, int? termId = null, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateExistingByDepartmentAsync(departmentId, termId, cancellationToken);
        return validation.Conflicts;
    }

    public async Task<ScheduleValidationResultDto> ValidateAsync(ScheduleValidationRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = new ScheduleValidationResultDto { IsValid = true };

        if (request.SectionId <= 0)
        {
            result.IsValid = false;
            result.Notes.Add("Geçerli bir section ID gönderilmelidir.");
            return result;
        }

        var section = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, cancellationToken);

        if (section == null)
        {
            result.IsValid = false;
            result.Notes.Add("Section bulunamadı.");
            return result;
        }

        var snapshot = await BuildTermSnapshotAsync(section.TermId, section.Id, null, cancellationToken);
        var candidate = await BuildCandidateSnapshotAsync(section, request.InstructorId, request.ClassroomId, request.ScheduleSlots, cancellationToken);
        var softConfig = await LoadSoftConstraintConfigAsync(cancellationToken);
        var hardConfig = await LoadHardConstraintConfigAsync(cancellationToken);

        result.Notes.Add($"Degerlendirilen donem: {snapshot.TermName}");
        result.Notes.AddRange(candidate.Notes);

        var (conflicts, softNotes) = EvaluateCandidateConflicts(candidate, snapshot, softConfig, hardConfig);
        result.Conflicts = conflicts;
        result.Notes.AddRange(softNotes);
        result.IsValid = conflicts.Count == 0;
        return result;
    }

    public async Task<ScheduleValidationResultDto> ValidateExistingAsync(int? termId = null, CancellationToken cancellationToken = default)
    {
        var result = new ScheduleValidationResultDto { IsValid = true };
        var resolvedTerm = await ResolveTermAsync(termId, cancellationToken);

        if (resolvedTerm == null)
        {
            result.IsValid = false;
            result.Notes.Add(termId.HasValue
                ? "Belirtilen akademik dönem bulunamadı."
                : "Aktif akademik dönem bulunamadı.");
            return result;
        }

        var snapshot = await BuildTermSnapshotAsync(resolvedTerm.Id, null, null, cancellationToken);
        var evaluation = await EvaluateExistingConflictsAsync(snapshot, cancellationToken);

        evaluation.Notes.Insert(0, $"Degerlendirilen donem: {snapshot.TermName}");
        return evaluation;
    }

    public async Task<ScheduleValidationResultDto> ValidateExistingByDepartmentAsync(int departmentId, int? termId = null, CancellationToken cancellationToken = default)
    {
        var result = new ScheduleValidationResultDto { IsValid = true };
        if (departmentId <= 0)
        {
            result.IsValid = false;
            result.Notes.Add("Geçerli bir bölüm ID gönderilmelidir.");
            return result;
        }

        var resolvedTerm = await ResolveTermAsync(termId, cancellationToken);
        if (resolvedTerm == null)
        {
            result.IsValid = false;
            result.Notes.Add(termId.HasValue
                ? "Belirtilen akademik dönem bulunamadı."
                : "Aktif akademik dönem bulunamadı.");
            return result;
        }

        var snapshot = await BuildTermSnapshotAsync(resolvedTerm.Id, null, departmentId, cancellationToken);
        var evaluation = await EvaluateExistingConflictsAsync(snapshot, cancellationToken);
        evaluation.Notes.Insert(0, $"Degerlendirilen donem: {snapshot.TermName}");
        evaluation.Notes.Insert(1, $"Kapsam: {snapshot.ScopeName}");
        return evaluation;
    }

    public async Task<ScheduleSuggestionDto> SuggestAsync(ScheduleSuggestionRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = new ScheduleSuggestionDto
        {
            IsFeasible = false,
            SectionId = request.SectionId
        };

        if (request.SectionId <= 0)
        {
            response.Notes.Add("Geçerli bir section ID gönderilmelidir.");
            return response;
        }

        var section = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, cancellationToken);

        if (section == null)
        {
            response.Notes.Add("Section bulunamadı.");
            return response;
        }

        var instructorId = request.InstructorId ?? section.InstructorId;
        if (!instructorId.HasValue)
        {
            response.Notes.Add("Öneri üretilebilmesi için section üzerinde bir öğretim üyesi tanımlı olmalı veya istek ile instructorId gönderilmelidir.");
            return response;
        }

        var instructor = await _context.Instructors
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == instructorId.Value, cancellationToken);

        if (instructor == null)
        {
            response.Notes.Add("Öğretim üyesi bulunamadı.");
            return response;
        }

        var instructorName = await GetInstructorNameAsync(instructor, cancellationToken);

        // Load TimeSlots for this term
        var timeSlots = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == section.TermId)
            .OrderBy(ts => ts.DayOfWeek)
            .ThenBy(ts => ts.SlotOrder)
            .ToListAsync(cancellationToken);

        response.InstructorId = instructor.Id;
        response.InstructorName = instructorName;

        if (timeSlots.Count == 0)
        {
            response.Notes.Add("Bu dönem için zaman slotu tanımı bulunmadığından öneri üretilemedi.");
            return response;
        }

        var existingSnapshot = await BuildTermSnapshotAsync(section.TermId, section.Id, null, cancellationToken);
        var softConfig = await LoadSoftConstraintConfigAsync(cancellationToken);
        var hardConfig = await LoadHardConstraintConfigAsync(cancellationToken);
        var candidateCourseMetadata = await LoadCourseSchedulingMetadataAsync(section.TermId, [section.CourseId], cancellationToken);
        var currentBlocks = await LoadSectionTimeBlocksAsync(section.Id, cancellationToken);
        var requiredHours = ResolveTotalRequiredHours(section);
        var slotCombinations = BuildTimeSlotCombinations(requiredHours, currentBlocks, timeSlots);

        if (slotCombinations.Count == 0)
        {
            response.Notes.Add("Zaman grid'inde uygun slot kombinasyonu üretilemedi.");
            return response;
        }

        var requiredCapacity = Math.Max(section.Capacity, 0);

        // Fakülteye atanmış dersliklerden seç (H10 ön-filtre)
        var courseDeptId = section.Course.DepartmentId;
        int? facultyId = null;
        if (courseDeptId.HasValue)
        {
            facultyId = await _context.Departments.AsNoTracking()
                .Where(d => d.Id == courseDeptId.Value)
                .Select(d => d.FacultyId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var assignedClassroomIds = await _context.ClassroomTermAssignments
            .AsNoTracking()
            .Where(cta => cta.TermId == section.TermId
                && (!facultyId.HasValue || cta.FacultyId == facultyId.Value))
            .Select(cta => cta.ClassroomId)
            .ToListAsync(cancellationToken);

        IQueryable<Classroom> classroomQuery;
        if (assignedClassroomIds.Count > 0)
        {
            // Fakülteye atanmış dersliklerden seç
            classroomQuery = _context.Classrooms.AsNoTracking()
                .Where(c => assignedClassroomIds.Contains(c.Id) && c.Capacity >= requiredCapacity);
        }
        else
        {
            // Henüz atama yoksa tüm dersliklerden seç (fallback)
            classroomQuery = _context.Classrooms.AsNoTracking()
                .Where(c => c.Capacity > 0 && c.Capacity >= requiredCapacity);
        }

        var classrooms = await classroomQuery
            .OrderBy(c => c.Capacity - requiredCapacity)
            .ThenBy(c => c.Id == section.ClassroomId ? 0 : 1)
            .ThenBy(c => c.Name)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

        if (classrooms.Count == 0)
        {
            response.Notes.Add("Section kontenjanını karşılayan uygun bir derslik bulunamadı.");
            return response;
        }

        var currentWeeklyHours = ResolveWeeklyHours(currentBlocks, section.Course);

        foreach (var slotCombination in slotCombinations)
        {
            foreach (var classroom in classrooms)
            {
                var candidate = BuildCandidateSnapshot(
                    section,
                    instructor,
                    instructorName,
                    classroom,
                    slotCombination.Slots,
                    requiredHours,
                    new List<string>(),
                    suggestedSemester: candidateCourseMetadata.MinSuggestedSemesters.TryGetValue(section.CourseId, out var suggestedSemester)
                        ? suggestedSemester
                        : null,
                    suggestedSemestersByDepartment: GetSuggestedSemestersByDepartment(section.CourseId, candidateCourseMetadata.SuggestedSemestersByDepartment),
                    accessibleDepartmentIds: GetAccessibleDepartments(section.CourseId, section.Course.DepartmentId, candidateCourseMetadata.AccessibleDepartments));

                var (conflicts, softNotes) = EvaluateCandidateConflicts(candidate, existingSnapshot, softConfig, hardConfig);
                if (conflicts.Count > 0)
                {
                    continue;
                }

                var candidateWeeklyHours = ResolveWeeklyHours(candidate.TimeBlocks, section.Course);
                response.IsFeasible = true;
                response.SuggestedClassroomId = classroom.Id;
                response.SuggestedClassroomName = classroom.Name;
                response.SuggestedSlots = candidate.TimeBlocks.Select(MapTimeBlock).ToList();
                response.EstimatedWeeklyHoursImpact = candidateWeeklyHours - currentWeeklyHours;
                response.Notes.AddRange(candidate.Notes);
                response.Notes.AddRange(softNotes);
                response.Notes.Add("Slotlar zaman grid'inden uygun kombinasyon olarak seçildi.");
                response.Notes.Add($"Derslik secimi kapasite uyumu esas alinarak yapildi ({classroom.Capacity}/{requiredCapacity}).");
                return response;
            }
        }

        response.Notes.Add("Çakışma olmayan derslik + slot kombinasyonu bulunamadı.");
        return response;
    }

    public async Task<ScheduleValidationResultDto> ApplyAsync(ApplyScheduleRequestDto request, CancellationToken cancellationToken = default)
    {
        var validationRequest = new ScheduleValidationRequestDto
        {
            SectionId = request.SectionId,
            ClassroomId = request.ClassroomId,
            ScheduleSlots = request.ScheduleSlots,
        };

        var validation = await ValidateAsync(validationRequest, cancellationToken);

        if (!validation.IsValid)
        {
            validation.Notes.Add("Çakışma tespit edildiğinden program uygulanmadı.");
            return validation;
        }

        var section = await _context.CourseSections
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, cancellationToken);

        if (section == null)
        {
            return new ScheduleValidationResultDto
            {
                IsValid = false,
                Notes = { "Section bulunamadı." }
            };
        }

        var slots = request.ScheduleSlots.Select(s => new TimeBlock
        {
            Day = s.Day,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
        }).ToList();

        section.ClassroomId = request.ClassroomId;

        // Write SectionSchedule records (the authoritative schedule store)
        await WriteSectionScheduleRecordsAsync(section.Id, section.TermId, request.ClassroomId, slots, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        validation.Notes.Add("Program başarıyla uygulanarak kaydedildi.");
        return validation;
    }

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
            .Where(cs => cs.TermId == resolvedTerm.Id && cs.IsActive && cs.InstructorId != null);

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
                termSnapshot, softConfig, hardConfig, instructorNameMap);

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
                    softConfig, hardConfig, instructorNameMap,
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

                var (conflicts, softNotes) = EvaluateCandidateConflicts(candidate, snapshot, softConfig, hardConfig);
                if (conflicts.Count > 0)
                    continue;

                // Calculate soft penalty score
                double penalty = slotCombo.PreferenceScore;
                penalty += CalculateSoftPenalty(candidate, snapshot, softConfig);

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
        SoftConstraintConfig softConfig)
    {
        var config = BuildConstraintConfig(softConfig, null);
        return _constraintEvaluation.CalculatePenalty(candidate, snapshot, config);
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

                var (conflicts, _) = EvaluateCandidateConflicts(candidate, snapshot, softConfig, hardConfig);
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
                snapshot, softConfig, hardConfig, instructorNameMap);

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
                    snapshot, softConfig, hardConfig, instructorNameMap);

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

    public async Task<ClassroomDashboardDto> GetClassroomDashboardAsync(
        int? termId = null,
        int? facultyId = null,
        string? buildingName = null,
        string? classroomType = null,
        int? minCapacity = null,
        int? maxCapacity = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ClassroomDashboardDto();
        var resolvedTerm = await ResolveTermAsync(termId, cancellationToken);
        if (resolvedTerm == null)
        {
            return result;
        }

        var snapshot = await BuildTermSnapshotAsync(resolvedTerm.Id, null, null, cancellationToken);
        var evaluation = await EvaluateExistingConflictsAsync(snapshot, cancellationToken);
        result.ConflictsCount = evaluation.Conflicts?.Count ?? 0;

        // Fakulte filtresi: ClassroomTermAssignment uzerinden
        var classroomIdsForTerm = await _context.ClassroomTermAssignments
            .AsNoTracking()
            .Where(a => a.TermId == resolvedTerm.Id)
            .Where(a => !facultyId.HasValue || a.FacultyId == facultyId.Value)
            .Select(a => new { a.ClassroomId, a.FacultyId, FacultyName = a.Faculty.Name })
            .ToListAsync(cancellationToken);

        var facultyLookup = classroomIdsForTerm
            .GroupBy(x => x.ClassroomId)
            .ToDictionary(g => g.Key, g => g.First());

        var classroomQuery = _context.Classrooms.AsNoTracking().AsQueryable();

        if (facultyId.HasValue)
        {
            var assignedIds = classroomIdsForTerm.Select(x => x.ClassroomId).ToHashSet();
            classroomQuery = classroomQuery.Where(c => assignedIds.Contains(c.Id));
        }

        if (!string.IsNullOrWhiteSpace(buildingName))
            classroomQuery = classroomQuery.Where(c => c.BuildingName == buildingName);

        if (!string.IsNullOrWhiteSpace(classroomType) && Enum.TryParse<ClassroomType>(classroomType, out var parsedType))
            classroomQuery = classroomQuery.Where(c => c.Type == parsedType);

        if (minCapacity.HasValue)
            classroomQuery = classroomQuery.Where(c => c.Capacity >= minCapacity.Value);

        if (maxCapacity.HasValue)
            classroomQuery = classroomQuery.Where(c => c.Capacity <= maxCapacity.Value);

        var classrooms = await classroomQuery.ToListAsync(cancellationToken);
        result.TotalClassrooms = classrooms.Count;

        // Her derslik için o donemdeki dersleri haritala
        var classroomSchedules = snapshot.Sections
            .Where(s => s.ClassroomId.HasValue)
            .GroupBy(s => s.ClassroomId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(s => s.TimeBlocks.Select(slot => new ClassroomScheduleSlotDto
                {
                    Day = (int)slot.Day,
                    StartHour = slot.StartTime.Hour,
                    EndHour = slot.EndTime.Minute > 0 ? slot.EndTime.Hour : slot.EndTime.Hour - 1,
                    CourseCode = s.CourseCode,
                    CourseName = s.CourseName,
                    InstructorName = s.InstructorName
                })).ToList()
            );

        result.Classrooms = classrooms.Select(c =>
        {
            var fac = facultyLookup.TryGetValue(c.Id, out var f) ? f : null;
            return new ClassroomInfoDto
            {
                Id = c.Id,
                Name = c.Name,
                Capacity = c.Capacity,
                Type = c.Type.ToString(),
                BuildingName = c.BuildingName,
                Floor = c.Floor,
                FacultyId = fac?.FacultyId ?? 0,
                FacultyName = fac?.FacultyName ?? string.Empty,
                Schedules = classroomSchedules.TryGetValue(c.Id, out var schedules) ? schedules : new()
            };
        }).OrderBy(c => c.Name).ToList();

        var filteredClassroomIds = classrooms.Select(c => c.Id).ToHashSet();

        // TimeSlot'lardan heatmap grid'i oluştur
        var timeSlots = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == resolvedTerm.Id)
            .OrderBy(ts => ts.DayOfWeek).ThenBy(ts => ts.SlotOrder)
            .ToListAsync(cancellationToken);

        var distinctSlots = timeSlots
            .GroupBy(ts => ts.SlotOrder)
            .Select(g => g.First())
            .OrderBy(ts => ts.SlotOrder)
            .ToList();

        var workDayIndices = timeSlots
            .Select(ts => (int)ts.DayOfWeek - 1)
            .Distinct()
            .Where(d => d >= 0 && d <= 4)
            .OrderBy(d => d)
            .ToList();

        // TimeSlot yoksa fallback: 8-17
        if (distinctSlots.Count == 0)
        {
            for (int d = 0; d < 5; d++) workDayIndices.Add(d);
            workDayIndices = workDayIndices.Distinct().OrderBy(d => d).ToList();
            for (int h = 8; h <= 17; h++)
            {
                distinctSlots.Add(new Domain.Entities.TimeSlot
                {
                    SlotOrder = h - 7,
                    StartTime = new TimeOnly(h, 0),
                    EndTime = new TimeOnly(h, 50),
                });
            }
        }

        var heatmapMap = new Dictionary<(int day, int slotOrder), int>();
        foreach (var dayIdx in workDayIndices)
        {
            foreach (var slot in distinctSlots)
            {
                heatmapMap[(dayIdx, slot.SlotOrder)] = 0;
            }
        }

        var timeSlotsPerDay = timeSlots
            .GroupBy(ts => (int)ts.DayOfWeek - 1)
            .ToDictionary(g => g.Key, g => g.OrderBy(ts => ts.SlotOrder).ToList());

        var occupationMap = new Dictionary<(int day, int slotOrder), List<ClassroomOccupationDto>>();
        foreach (var section in snapshot.Sections)
        {
            if (section.ClassroomId.HasValue && filteredClassroomIds.Contains(section.ClassroomId.Value))
            {
                foreach (var block in section.TimeBlocks)
                {
                    int dayIndex = (int)block.Day - 1;
                    if (dayIndex >= 0 && dayIndex <= 4)
                    {
                        if (timeSlotsPerDay.TryGetValue(dayIndex, out var daySlots))
                        {
                            foreach (var ts in daySlots)
                            {
                                if (block.StartTime < ts.EndTime && ts.StartTime < block.EndTime)
                                {
                                    var key = (dayIndex, ts.SlotOrder);
                                    if (!heatmapMap.ContainsKey(key)) heatmapMap[key] = 0;
                                    heatmapMap[key]++;

                                    if (!occupationMap.ContainsKey(key)) occupationMap[key] = new List<ClassroomOccupationDto>();
                                    occupationMap[key].Add(new ClassroomOccupationDto
                                    {
                                        ClassroomName = section.ClassroomName,
                                        CourseCode = section.CourseCode,
                                        CourseName = section.CourseName,
                                        EnrolledCount = section.EnrolledCount,
                                        Capacity = section.ClassroomCapacity
                                    });
                                }
                            }
                        }
                        else
                        {
                            // Fallback: TimeSlot yoksa saat bazlı eşleştir
                            int startHour = block.StartTime.Hour;
                            int endHour = block.EndTime.Minute > 0 ? block.EndTime.Hour : block.EndTime.Hour - 1;
                            foreach (var s in distinctSlots)
                            {
                                if (startHour < s.EndTime.Hour + 1 && s.StartTime.Hour <= endHour)
                                {
                                    var key = (dayIndex, s.SlotOrder);
                                    if (!heatmapMap.ContainsKey(key)) heatmapMap[key] = 0;
                                    heatmapMap[key]++;

                                    if (!occupationMap.ContainsKey(key)) occupationMap[key] = new List<ClassroomOccupationDto>();
                                    occupationMap[key].Add(new ClassroomOccupationDto
                                    {
                                        ClassroomName = section.ClassroomName,
                                        CourseCode = section.CourseCode,
                                        CourseName = section.CourseName,
                                        EnrolledCount = section.EnrolledCount,
                                        Capacity = section.ClassroomCapacity
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        int slotsPerDay = distinctSlots.Count;
        int totalDays = workDayIndices.Count;
        int totalSlots = result.TotalClassrooms * totalDays * slotsPerDay;
        int usedSlots = heatmapMap.Values.Sum();
        result.EmptySlots = Math.Max(0, totalSlots - usedSlots);
        result.OccupancyRate = totalSlots > 0 ? Math.Round((double)usedSlots / totalSlots * 100) : 0;
        result.SlotsPerDay = slotsPerDay;

        var slotLookup = distinctSlots.ToDictionary(s => s.SlotOrder);
        foreach (var kvp in heatmapMap)
        {
            var day = kvp.Key.day;
            var slotOrder = kvp.Key.slotOrder;
            var used = kvp.Value;
            var total = result.TotalClassrooms;
            var slotDef = slotLookup[slotOrder];

            result.Heatmap.Add(new ClassroomHeatmapCellDto
            {
                Day = day,
                Hour = slotDef.StartTime.Hour,
                SlotOrder = slotOrder,
                StartTime = slotDef.StartTime.ToString("HH:mm"),
                EndTime = slotDef.EndTime.ToString("HH:mm"),
                Label = $"{slotDef.StartTime:HH:mm}-{slotDef.EndTime:HH:mm}",
                UsedClassrooms = used,
                Occupancy = total > 0 ? Math.Min(1, (double)used / total) : 0,
                Details = $"{GetDayName(day)} {slotDef.StartTime:HH:mm}-{slotDef.EndTime:HH:mm} — {used}/{total} derslik dolu",
                Occupations = occupationMap.TryGetValue((day, slotOrder), out var occList) ? occList : new()
            });
        }

        return result;
    }

    public async Task<ClassroomFilterOptionsDto> GetClassroomFilterOptionsAsync(int termId, CancellationToken cancellationToken = default)
    {
        var assignments = await _context.ClassroomTermAssignments
            .AsNoTracking()
            .Include(a => a.Faculty)
            .Include(a => a.Classroom)
            .Where(a => a.TermId == termId)
            .ToListAsync(cancellationToken);

        var faculties = assignments
            .Select(a => new ClassroomFacultyOption { Id = a.FacultyId, Name = a.Faculty.Name })
            .DistinctBy(f => f.Id)
            .OrderBy(f => f.Name)
            .ToList();

        var buildings = assignments
            .Select(a => a.Classroom.BuildingName)
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Distinct()
            .OrderBy(b => b)
            .ToList();

        // Client-side evaluation: EF Core cannot translate enum .ToString() to SQL
        var classroomTypes = (await _context.Classrooms
            .AsNoTracking()
            .Select(c => c.Type)
            .Distinct()
            .ToListAsync(cancellationToken))
            .Select(t => t.ToString())
            .OrderBy(t => t)
            .ToList();

        return new ClassroomFilterOptionsDto
        {
            Faculties = faculties,
            Buildings = buildings,
            ClassroomTypes = classroomTypes
        };
    }

    public async Task<List<AvailableClassroomResultDto>> SearchAvailableClassroomsAsync(
        int termId,
        List<int> days,
        int startHour,
        int endHour,
        int? minCapacity = null,
        string? classroomType = null,
        int? facultyId = null,
        string? buildingName = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await BuildTermSnapshotAsync(termId, null, null, cancellationToken);

        var classroomQuery = _context.Classrooms.AsNoTracking().AsQueryable();

        if (facultyId.HasValue)
        {
            var assignedIds = await _context.ClassroomTermAssignments
                .AsNoTracking()
                .Where(a => a.TermId == termId && a.FacultyId == facultyId.Value)
                .Select(a => a.ClassroomId)
                .ToListAsync(cancellationToken);
            classroomQuery = classroomQuery.Where(c => assignedIds.Contains(c.Id));
        }

        if (!string.IsNullOrWhiteSpace(buildingName))
            classroomQuery = classroomQuery.Where(c => c.BuildingName == buildingName);

        if (!string.IsNullOrWhiteSpace(classroomType) && Enum.TryParse<ClassroomType>(classroomType, out var parsedType))
            classroomQuery = classroomQuery.Where(c => c.Type == parsedType);

        if (minCapacity.HasValue)
            classroomQuery = classroomQuery.Where(c => c.Capacity >= minCapacity.Value);

        var classrooms = await classroomQuery.ToListAsync(cancellationToken);

        var occupiedSlots = snapshot.Sections
            .Where(s => s.ClassroomId.HasValue)
            .SelectMany(s => s.TimeBlocks.Select(tb => new
            {
                ClassroomId = s.ClassroomId!.Value,
                Day = (int)tb.Day,
                StartHour = tb.StartTime.Hour,
                EndHour = tb.EndTime.Minute > 0 ? tb.EndTime.Hour : tb.EndTime.Hour - 1
            }))
            .GroupBy(x => x.ClassroomId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var weeklyUsage = occupiedSlots.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Sum(s => s.EndHour - s.StartHour + 1));

        var results = new List<AvailableClassroomResultDto>();

        foreach (var classroom in classrooms)
        {
            var slots = occupiedSlots.TryGetValue(classroom.Id, out var s) ? s : new();

            bool isAvailable = true;
            foreach (var day in days)
            {
                for (int h = startHour; h <= endHour && isAvailable; h++)
                {
                    if (slots.Any(sl => sl.Day == day && h >= sl.StartHour && h <= sl.EndHour))
                    {
                        isAvailable = false;
                    }
                }
            }

            if (!isAvailable) continue;

            int totalWeeklySlots = 5 * 10;
            int used = weeklyUsage.TryGetValue(classroom.Id, out var u) ? u : 0;

            var classroomSchedules = snapshot.Sections
                .Where(sec => sec.ClassroomId == classroom.Id)
                .SelectMany(sec => sec.TimeBlocks.Select(tb => new ClassroomScheduleSlotDto
                {
                    Day = (int)tb.Day,
                    StartHour = tb.StartTime.Hour,
                    EndHour = tb.EndTime.Minute > 0 ? tb.EndTime.Hour : tb.EndTime.Hour - 1,
                    CourseCode = sec.CourseCode,
                    CourseName = sec.CourseName,
                    InstructorName = sec.InstructorName
                }))
                .OrderBy(sl => sl.Day).ThenBy(sl => sl.StartHour)
                .ToList();

            results.Add(new AvailableClassroomResultDto
            {
                Id = classroom.Id,
                Name = classroom.Name,
                Capacity = classroom.Capacity,
                Type = classroom.Type.ToString(),
                BuildingName = classroom.BuildingName,
                Floor = classroom.Floor,
                WeeklyOccupancyRate = totalWeeklySlots > 0 ? Math.Round((double)used / totalWeeklySlots * 100) : 0,
                Schedules = classroomSchedules
            });
        }

        return results.OrderBy(r => r.Name).ToList();
    }

    // ── Score Breakdown ────────────────────────────────────────────────

    public async Task<ScoreBreakdownDto> GetScoreBreakdownAsync(int termId, CancellationToken cancellationToken = default)
    {
        var snapshot = await BuildTermSnapshotAsync(termId, null, null, cancellationToken);
        var softConfig = await LoadSoftConstraintConfigAsync(cancellationToken);
        var hardConfig = await LoadHardConstraintConfigAsync(cancellationToken);

        if (softConfig.S4Enabled)
            snapshot.CoEnrollmentPairs = await LoadCoEnrollmentPairsAsync(snapshot.TermId, softConfig.S4MinCoPairThreshold, cancellationToken);

        var config = BuildConstraintConfig(softConfig, hardConfig);
        var state = await _constraintEvaluation.LoadConstraintStateAsync(null, null, null, termId, cancellationToken);
        var weightMap = state.Definitions.ToDictionary(d => d.Key, d => d.Value.Weight);

        return _constraintEvaluation.EvaluateWithBreakdown(snapshot, config, state, weightMap);
    }

    // ── Private helpers ──────────────────────────────────────────────────

    internal async Task<TermSnapshot> BuildTermSnapshotAsync(int termId, int? excludedSectionId, int? departmentId, CancellationToken cancellationToken)
    {
        var sectionsQuery = _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .Include(cs => cs.SectionSchedules).ThenInclude(ss => ss.TimeSlot)
            .Where(cs => cs.TermId == termId && cs.IsActive)
            .Where(cs => !excludedSectionId.HasValue || cs.Id != excludedSectionId.Value);

        if (departmentId.HasValue)
        {
            sectionsQuery = sectionsQuery.Where(cs =>
                cs.Course.DepartmentId == departmentId.Value
                || (cs.Instructor != null && cs.Instructor.DepartmentId == departmentId.Value));
        }

        var sections = await sectionsQuery.ToListAsync(cancellationToken);

        var termName = await _context.AcademicTerms
            .AsNoTracking()
            .Where(t => t.Id == termId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var scopeName = departmentId.HasValue
            ? await _context.Departments
                .AsNoTracking()
                .Where(department => department.Id == departmentId.Value)
                .Select(department => department.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? "Bolum"
            : "Tum sistem";

        var instructorIds = sections
            .Where(section => section.InstructorId.HasValue)
            .Select(section => section.InstructorId!.Value)
            .Distinct()
            .ToList();

        var instructorNameMap = await LoadInstructorNameMapAsync(instructorIds, cancellationToken);

        var courseMetadata = await LoadCourseSchedulingMetadataAsync(
            termId,
            sections.Select(section => section.CourseId),
            cancellationToken);

        // Load classroom-term assignments for H10
        var classroomAssignments = await _context.ClassroomTermAssignments
            .AsNoTracking()
            .Where(cta => cta.TermId == termId)
            .ToListAsync(cancellationToken);

        // Load department→faculty mapping for H10
        var deptFacultyMap = await _context.Departments
            .AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.FacultyId, cancellationToken);

        // Load corequisite relationships for H11
        var corequisites = await _context.CoursePrerequisites
            .AsNoTracking()
            .Where(cp => cp.IsCorequisite)
            .ToListAsync(cancellationToken);

        // Load co-enrollment pairs for S4 (using default threshold, will be filtered by config later)
        var softCfg = await LoadSoftConstraintConfigAsync(cancellationToken);
        var coEnrollmentPairs = softCfg.S4Enabled
            ? await LoadCoEnrollmentPairsAsync(termId, softCfg.S4MinCoPairThreshold, cancellationToken)
            : new List<CoEnrollmentPair>();

        // Load distribution constraints for DIST evaluator
        var distributionConstraints = await _context.DistributionConstraints
            .AsNoTracking()
            .Include(d => d.Courses)
            .Where(d => d.TermId == termId)
            .ToListAsync(cancellationToken);

        var snapshots = sections
            .Select(section => BuildExistingSectionSnapshot(
                section,
                instructorNameMap,
                courseMetadata.MinSuggestedSemesters,
                courseMetadata.SuggestedSemestersByDepartment,
                courseMetadata.AccessibleDepartments))
            .ToList();

        return new TermSnapshot
        {
            TermId = termId,
            TermName = termName,
            ScopeName = scopeName,
            Sections = snapshots,
            ClassroomAssignments = classroomAssignments,
            DeptFacultyMap = deptFacultyMap,
            Corequisites = corequisites,
            CoEnrollmentPairs = coEnrollmentPairs,
            CourseSuggestedSemestersByDepartment = courseMetadata.SuggestedSemestersByDepartment,
            CourseAccessibleDepartments = courseMetadata.AccessibleDepartments,
            DistributionConstraints = distributionConstraints,
        };
    }

    private static SectionSnapshot BuildExistingSectionSnapshot(
        CourseSection section,
        IReadOnlyDictionary<int, string> instructorNameMap,
        IReadOnlyDictionary<int, int> semesterMap,
        IReadOnlyDictionary<int, Dictionary<int, int>> suggestedSemestersByDepartment,
        IReadOnlyDictionary<int, HashSet<int>> accessibleDepartments)
    {
        var timeBlocks = ScheduleHelper.ToTimeBlocks(section.SectionSchedules);
        var instructorId = section.InstructorId;

        return new SectionSnapshot
        {
            SectionId = section.Id,
            CourseId = section.CourseId,
            CourseCode = section.Course.Code,
            CourseName = section.Course.Name,
            TermId = section.TermId,
            TheoryHours = section.Course.TheoryHours,
            LabHours = section.Course.LabHours,
            Capacity = section.Capacity,
            ClassroomId = section.ClassroomId,
            ClassroomName = section.Classroom?.Name ?? string.Empty,
            ClassroomCapacity = section.Classroom?.Capacity ?? 0,
            ClassroomType = section.Classroom?.Type ?? ClassroomType.Lecture,
            TheoryClassroomType = section.Course.TheoryClassroomType,
            LabClassroomType = section.Course.LabClassroomType,
            InstructorId = instructorId,
            InstructorName = instructorId.HasValue && instructorNameMap.TryGetValue(instructorId.Value, out var instructorName)
                ? instructorName
                : string.Empty,
            InstructorMaxWeeklyHours = section.Instructor?.MaxWeeklyHours ?? 0,
            DepartmentId = section.Course.DepartmentId,
            SuggestedSemester = semesterMap.TryGetValue(section.CourseId, out var sem) ? sem : null,
            SuggestedSemestersByDepartment = GetSuggestedSemestersByDepartment(section.CourseId, suggestedSemestersByDepartment),
            AccessibleDepartmentIds = GetAccessibleDepartments(section.CourseId, section.Course.DepartmentId, accessibleDepartments),
            AssignedTimeSlotCount = ResolveAssignedTimeSlotCount(timeBlocks),
            TimeBlocks = timeBlocks,
            Notes = new List<string>()
        };
    }

    private async Task<SectionSnapshot> BuildCandidateSnapshotAsync(
        CourseSection section,
        int? instructorIdOverride,
        int? classroomIdOverride,
        List<ScheduleSlotDto>? scheduleSlotOverrides,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var instructorId = instructorIdOverride ?? section.InstructorId;
        var classroomId = classroomIdOverride ?? section.ClassroomId;

        Instructor? instructor = null;
        string instructorName = string.Empty;

        if (instructorId.HasValue)
        {
            instructor = await _context.Instructors
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == instructorId.Value, cancellationToken);

            if (instructor == null)
            {
                notes.Add("Seçilen öğretim üyesi bulunamadı.");
            }
            else
            {
                instructorName = await GetInstructorNameAsync(instructor, cancellationToken);
            }
        }
        else
        {
            notes.Add("Section için öğretim üyesi atanmamış.");
        }

        Classroom? classroom = null;
        if (classroomId.HasValue)
        {
            classroom = await _context.Classrooms
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == classroomId.Value, cancellationToken);

            if (classroom == null)
            {
                notes.Add("Seçilen derslik bulunamadı.");
            }
        }
        else
        {
            notes.Add("Section için derslik atanmamış.");
        }

        var scheduleSlots = new List<TimeBlock>();
        var assignedTimeSlotCount = 0;
        string? parseError = null;

        if (scheduleSlotOverrides != null)
        {
            var overrideValidation = ValidateTimeBlocks(scheduleSlotOverrides
                .Select(dto => new TimeBlock
                {
                    Day = dto.Day,
                    StartTime = dto.StartTime,
                    EndTime = dto.EndTime
                })
                .ToList());

            scheduleSlots = overrideValidation.Slots;
            parseError = overrideValidation.ErrorMessage;
            assignedTimeSlotCount = await CountMatchedTimeSlotsAsync(section.TermId, scheduleSlots, cancellationToken);
        }
        else
        {
            scheduleSlots = await LoadSectionTimeBlocksAsync(section.Id, cancellationToken);
            assignedTimeSlotCount = ResolveAssignedTimeSlotCount(scheduleSlots);
        }

        if (scheduleSlots.Count == 0)
        {
            notes.Add("Section için program slot tanımı bulunmuyor.");
        }

        var courseMetadata = await LoadCourseSchedulingMetadataAsync(section.TermId, [section.CourseId], cancellationToken);
        var suggestedSemester = courseMetadata.MinSuggestedSemesters.TryGetValue(section.CourseId, out var semester)
            ? semester
            : (int?)null;

        return BuildCandidateSnapshot(
            section,
            instructor,
            instructorName,
            classroom,
            scheduleSlots,
            assignedTimeSlotCount,
            notes,
            suggestedSemester,
            GetSuggestedSemestersByDepartment(section.CourseId, courseMetadata.SuggestedSemestersByDepartment),
            GetAccessibleDepartments(section.CourseId, section.Course.DepartmentId, courseMetadata.AccessibleDepartments));
    }

    private static SectionSnapshot BuildCandidateSnapshot(
        CourseSection section,
        Instructor? instructor,
        string instructorName,
        Classroom? classroom,
        IReadOnlyList<TimeBlock> scheduleSlots,
        int assignedTimeSlotCount,
        List<string> notes,
        int? suggestedSemester = null,
        Dictionary<int, int>? suggestedSemestersByDepartment = null,
        HashSet<int>? accessibleDepartmentIds = null)
    {
        return new SectionSnapshot
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
            ClassroomId = classroom?.Id,
            ClassroomName = classroom?.Name ?? string.Empty,
            ClassroomCapacity = classroom?.Capacity ?? 0,
            ClassroomType = classroom?.Type ?? ClassroomType.Lecture,
            TheoryClassroomType = section.Course.TheoryClassroomType,
            LabClassroomType = section.Course.LabClassroomType,
            InstructorId = instructor?.Id,
            InstructorName = instructorName,
            InstructorMaxWeeklyHours = instructor?.MaxWeeklyHours ?? 0,
            DepartmentId = section.Course.DepartmentId,
            SuggestedSemester = suggestedSemester,
            SuggestedSemestersByDepartment = suggestedSemestersByDepartment ?? new Dictionary<int, int>(),
            AccessibleDepartmentIds = accessibleDepartmentIds ?? new HashSet<int>(),
            AssignedTimeSlotCount = assignedTimeSlotCount,
            TimeBlocks = scheduleSlots.ToList(),
            Notes = notes
        };
    }

    private async Task<ScheduleValidationResultDto> EvaluateExistingConflictsAsync(TermSnapshot snapshot, CancellationToken ct)
    {
        var softConfig = await LoadSoftConstraintConfigAsync(ct);
        var hardConfig = await LoadHardConstraintConfigAsync(ct);

        if (softConfig.S4Enabled)
            snapshot.CoEnrollmentPairs = await LoadCoEnrollmentPairsAsync(snapshot.TermId, softConfig.S4MinCoPairThreshold, ct);

        var config = BuildConstraintConfig(softConfig, hardConfig);
        var (conflicts, notes) = _constraintEvaluation.EvaluateAllExisting(snapshot, config);

        return new ScheduleValidationResultDto
        {
            IsValid = conflicts.Count == 0,
            Conflicts = conflicts
                .OrderBy(conflict => conflict.SectionId)
                .ThenBy(conflict => conflict.ConflictType)
                .ThenBy(conflict => conflict.Day)
                .ThenBy(conflict => conflict.StartTime)
                .ToList(),
            Notes = notes.Distinct().OrderBy(note => note).ToList()
        };
    }

    private (List<SchedulingConflictDto> Conflicts, List<string> SoftNotes) EvaluateCandidateConflicts(
        SectionSnapshot candidate, TermSnapshot snapshot, SoftConstraintConfig softConfig, HardConstraintConfig? hardConfig = null)
    {
        var config = BuildConstraintConfig(softConfig, hardConfig);
        var (conflicts, notes, _) = _constraintEvaluation.EvaluateCandidate(candidate, snapshot, config);
        return (conflicts, notes);
    }

    internal SchedulingConstraintConfig BuildConstraintConfig(SoftConstraintConfig soft, HardConstraintConfig? hard)
    {
        return new SchedulingConstraintConfig
        {
            LunchBreakIsHard = hard?.LunchBreakIsHard ?? false,
            LunchBreakStart = hard?.LunchBreakStart ?? new TimeOnly(11, 40),
            LunchBreakEnd = hard?.LunchBreakEnd ?? new TimeOnly(12, 20),
            MaxConsecutiveHours = hard?.MaxConsecutiveHours ?? 4,
            BreakDurationMinutes = hard?.BreakDurationMinutes ?? 5,
            AmphiThreshold = hard?.AmphiThreshold ?? 100,
            TheoryLabSplitThreshold = hard?.TheoryLabSplitThreshold ?? 4,
            AllowSameCourseSectionOverlap = hard?.AllowSameCourseSectionOverlap ?? false,
            CurriculumConflictMode = hard?.CurriculumConflictMode ?? "curriculum",
            S1Enabled = soft.S1Enabled,
            S1MinOccupancy = soft.S1MinOccupancy,
            S1MaxOccupancy = soft.S1MaxOccupancy,
            S2Enabled = soft.S2Enabled,
            S2MaxDailyHours = soft.S2MaxDailyHours,
            S3Enabled = soft.S3Enabled,
            S3MaxSectionsPerSlot = soft.S3MaxSectionsPerSlot,
            S4Enabled = soft.S4Enabled,
            S4MinCoPairThreshold = soft.S4MinCoPairThreshold,
            S5Enabled = soft.S5Enabled,
            S7Enabled = soft.S7Enabled,
            S7MaxGapSlots = soft.S7MaxGapSlots,
            S8Enabled = soft.S8Enabled,
            S9Enabled = soft.S9Enabled,
            S9MorningStart = soft.S9MorningStart,
            S9EveningStart = soft.S9EveningStart,
            S12Enabled = soft.S12Enabled,
            S12MaxImbalancePercent = soft.S12MaxImbalancePercent,
        };
    }

    // ── TimeSlot-based suggestion helpers ────────────────────────────────

    /// <summary>
    /// Determines total required hours for a course section (TheoryHours + LabHours).
    /// </summary>
    private static int ResolveTotalRequiredHours(CourseSection section)
    {
        var total = section.Course.TheoryHours + section.Course.LabHours;
        return total > 0 ? total : 1;
    }

    /// <summary>
    /// Resolves how the required hours should be split across day blocks.
    /// Returns a list of consecutive-slot counts per day block:
    ///   1 hour -> [1]
    ///   2 hours -> [2]
    ///   3 hours -> [3]
    ///   4 hours -> [2, 2]
    ///   5 hours -> [3, 2]
    ///   6+ hours -> [3, 3] (then more blocks if needed)
    /// </summary>
    private static IReadOnlyList<int> ResolveDayBlocks(int totalHours)
    {
        if (totalHours <= 3)
        {
            return new[] { totalHours };
        }

        var blocks = new List<int>();
        var remaining = totalHours;

        while (remaining > 0)
        {
            if (remaining >= 6)
            {
                blocks.Add(3);
                remaining -= 3;
            }
            else if (remaining >= 4)
            {
                // 4 -> [2,2], 5 -> [3,2]
                var first = remaining == 4 ? 2 : 3;
                blocks.Add(first);
                remaining -= first;
            }
            else
            {
                // remaining is 1, 2, or 3
                blocks.Add(remaining);
                remaining = 0;
            }
        }

        return blocks;
    }

    /// <summary>
    /// Groups TimeSlots by day and finds all possible consecutive-slot windows of a given size.
    /// A consecutive window means slots whose SlotOrder values are sequential (e.g., 1,2,3).
    /// </summary>
    private static List<List<TimeSlot>> FindConsecutiveWindows(IReadOnlyList<TimeSlot> daySlots, int requiredConsecutive)
    {
        var windows = new List<List<TimeSlot>>();
        if (daySlots.Count < requiredConsecutive)
        {
            return windows;
        }

        var sorted = daySlots.OrderBy(ts => ts.SlotOrder).ToList();

        for (int i = 0; i <= sorted.Count - requiredConsecutive; i++)
        {
            var window = new List<TimeSlot>();
            bool valid = true;

            for (int j = 0; j < requiredConsecutive; j++)
            {
                if (j > 0 && sorted[i + j].SlotOrder != sorted[i + j - 1].SlotOrder + 1)
                {
                    valid = false;
                    break;
                }
                window.Add(sorted[i + j]);
            }

            if (valid)
            {
                windows.Add(window);
            }
        }

        return windows;
    }

    /// <summary>
    /// Converts a list of consecutive TimeSlots into a single TimeBlock covering them.
    /// </summary>
    private static TimeBlock TimeSlotWindowToTimeBlock(IReadOnlyList<TimeSlot> window)
    {
        return new TimeBlock
        {
            Day = window[0].DayOfWeek,
            StartTime = window[0].StartTime,
            EndTime = window[^1].EndTime
        };
    }

    /// <summary>
    /// Builds slot combinations from the TimeSlot grid for the given required hours.
    /// If currentSlots exist and are valid, they are included as the first (preferred) combination.
    /// </summary>
    private static List<RankedSlotCombination> BuildTimeSlotCombinations(
        int totalRequiredHours,
        IReadOnlyList<TimeBlock> currentSlots,
        IReadOnlyList<TimeSlot> timeSlots)
    {
        var combinations = new List<RankedSlotCombination>();
        var signatures = new HashSet<string>(StringComparer.Ordinal);

        // Include the current schedule as first option if valid
        if (currentSlots.Count > 0)
        {
            var currentValidation = ValidateTimeBlocks(currentSlots.ToList());
            if (currentValidation.ErrorMessage == null)
            {
                AddCombination(combinations, signatures, currentValidation.Slots, 0);
            }
        }

        var dayBlocks = ResolveDayBlocks(totalRequiredHours);

        // Group time slots by day
        var slotsByDay = timeSlots
            .GroupBy(ts => ts.DayOfWeek)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TimeSlot>)g.OrderBy(ts => ts.SlotOrder).ToList());

        // For each day block size, find all possible consecutive windows across all days
        var perBlockCandidates = dayBlocks
            .Select(blockSize =>
            {
                var candidates = new List<RankedSlot>();
                foreach (var kvp in slotsByDay)
                {
                    var windows = FindConsecutiveWindows(kvp.Value, blockSize);
                    foreach (var window in windows)
                    {
                        candidates.Add(new RankedSlot
                        {
                            Slot = TimeSlotWindowToTimeBlock(window),
                            PreferencePenalty = 0
                        });
                    }
                }
                return candidates
                    .OrderBy(c => c.Slot.Day)
                    .ThenBy(c => c.Slot.StartTime)
                    .ToList();
            })
            .ToList();

        if (perBlockCandidates.Any(candidateList => candidateList.Count == 0))
        {
            return combinations;
        }

        // Build combinations recursively, ensuring no overlap and different days for multi-block
        var current = new List<RankedSlot>();
        BuildCombinationsRecursive(perBlockCandidates, 0, current, combinations, signatures);

        return combinations
            .OrderBy(combination => combination.PreferenceScore)
            .ThenBy(combination => BuildSlotSignature(combination.Slots))
            .ToList();
    }

    // ── SectionSchedule write helper ─────────────────────────────────────

    /// <summary>
    /// Loads existing SectionSchedule records for a section and converts them to TimeBlocks.
    /// </summary>
    private async Task<List<TimeBlock>> LoadSectionTimeBlocksAsync(int sectionId, CancellationToken cancellationToken)
    {
        var schedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => ss.CourseSectionId == sectionId)
            .ToListAsync(cancellationToken);

        return ScheduleHelper.ToTimeBlocks(schedules);
    }

    private async Task<int> CountMatchedTimeSlotsAsync(
        int termId,
        IReadOnlyList<TimeBlock> slots,
        CancellationToken cancellationToken)
    {
        if (slots.Count == 0)
        {
            return 0;
        }

        var termTimeSlots = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == termId)
            .ToListAsync(cancellationToken);

        if (termTimeSlots.Count == 0)
        {
            return ResolveAssignedTimeSlotCount(slots);
        }

        return termTimeSlots
            .Where(ts => slots.Any(slot =>
                ts.DayOfWeek == slot.Day
                && ts.StartTime < slot.EndTime
                && slot.StartTime < ts.EndTime))
            .Select(ts => ts.Id)
            .Distinct()
            .Count();
    }

    /// <summary>
    /// Writes SectionSchedule records.
    /// Deletes existing records for this section first, then creates new ones
    /// by matching each TimeBlock to TimeSlots in the term.
    /// </summary>
    private async Task WriteSectionScheduleRecordsAsync(
        int sectionId,
        int termId,
        int? classroomId,
        IReadOnlyList<TimeBlock> slots,
        CancellationToken cancellationToken)
    {
        // Remove old SectionSchedule records for this section
        var existing = await _context.SectionSchedules
            .Where(ss => ss.CourseSectionId == sectionId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _context.SectionSchedules.RemoveRange(existing);
        }

        if (!classroomId.HasValue || slots.Count == 0)
        {
            return;
        }

        // Load term time slots for matching
        var termTimeSlots = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == termId)
            .ToListAsync(cancellationToken);

        foreach (var slot in slots)
        {
            // Find all TimeSlots that overlap with this TimeBlock on the same day
            var matchingTimeSlots = termTimeSlots
                .Where(ts => ts.DayOfWeek == slot.Day
                    && ts.StartTime < slot.EndTime
                    && slot.StartTime < ts.EndTime)
                .OrderBy(ts => ts.SlotOrder)
                .ToList();

            foreach (var ts in matchingTimeSlots)
            {
                _context.SectionSchedules.Add(new SectionSchedule
                {
                    CourseSectionId = sectionId,
                    TimeSlotId = ts.Id,
                    ClassroomId = classroomId.Value
                });
            }
        }
    }

    // ── Shared slot helpers ──────────────────────────────────────────────

    private static void BuildCombinationsRecursive(
        IReadOnlyList<List<RankedSlot>> perBlockCandidates,
        int index,
        List<RankedSlot> current,
        List<RankedSlotCombination> results,
        HashSet<string> signatures)
    {
        if (results.Count >= 200)
        {
            return;
        }

        if (index == perBlockCandidates.Count)
        {
            AddCombination(results, signatures, current.Select(slot => slot.Slot).ToList(), current.Sum(slot => slot.PreferencePenalty));
            return;
        }

        foreach (var candidate in perBlockCandidates[index])
        {
            if (current.Any(existing => SlotsOverlap(existing.Slot, candidate.Slot)))
            {
                continue;
            }

            current.Add(candidate);
            BuildCombinationsRecursive(perBlockCandidates, index + 1, current, results, signatures);
            current.RemoveAt(current.Count - 1);
        }
    }

    private static void AddCombination(
        List<RankedSlotCombination> combinations,
        HashSet<string> signatures,
        List<TimeBlock> slots,
        int preferenceScore)
    {
        var normalizedSlots = slots
            .OrderBy(slot => slot.Day)
            .ThenBy(slot => slot.StartTime)
            .Select(slot => new TimeBlock
            {
                Day = slot.Day,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime
            })
            .ToList();

        var signature = BuildSlotSignature(normalizedSlots);
        if (!signatures.Add(signature))
        {
            return;
        }

        combinations.Add(new RankedSlotCombination
        {
            Slots = normalizedSlots,
            PreferenceScore = preferenceScore
        });
    }

    private static ScheduleParseResult ValidateTimeBlocks(List<TimeBlock> slots)
    {
        var normalizedSlots = slots
            .OrderBy(slot => slot.Day)
            .ThenBy(slot => slot.StartTime)
            .ToList();

        foreach (var slot in normalizedSlots)
        {
            if (slot.EndTime <= slot.StartTime)
            {
                return new ScheduleParseResult
                {
                    ErrorMessage = "Slot bitiş saati başlangıç saatinden sonra olmalıdır."
                };
            }
        }

        for (var i = 0; i < normalizedSlots.Count; i++)
        {
            for (var j = i + 1; j < normalizedSlots.Count; j++)
            {
                if (SlotsOverlap(normalizedSlots[i], normalizedSlots[j]))
                {
                    return new ScheduleParseResult
                    {
                        ErrorMessage = "Aynı section içindeki slotlar birbiriyle çakışamaz."
                    };
                }
            }
        }

        return new ScheduleParseResult
        {
            Slots = normalizedSlots
        };
    }

    private static List<TimeBlock> GetOverlapSlots(IReadOnlyList<TimeBlock> firstSchedule, IReadOnlyList<TimeBlock> secondSchedule) => SchedulingHelpers.GetOverlapSlots(firstSchedule, secondSchedule);

    private static bool TryGetOverlap(TimeBlock first, TimeBlock second, out TimeBlock overlap) => SchedulingHelpers.TryGetOverlap(first, second, out overlap);

    private static bool SlotsOverlap(TimeBlock first, TimeBlock second) => SchedulingHelpers.SlotsOverlap(first, second);

    private static int ResolveAssignedTimeSlotCount(IReadOnlyList<TimeBlock> slots) => SchedulingHelpers.ResolveAssignedTimeSlotCount(slots);

    private static decimal ResolveWeeklyHours(IReadOnlyList<TimeBlock> slots, Course course) => SchedulingHelpers.ResolveWeeklyHours(slots, course);

    private static decimal ResolveWeeklyHours(IReadOnlyList<TimeBlock> slots, SectionSnapshot section) => SchedulingHelpers.ResolveWeeklyHours(slots, section);

    private static ScheduleSlotDto MapTimeBlock(TimeBlock slot) =>
        new()
        {
            Day = slot.Day,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime
        };

    private static SchedulingConflictDto BuildConflict(
        SectionSnapshot section,
        string conflictType,
        string message,
        TimeBlock? slot,
        int? relatedSectionId) => SchedulingHelpers.BuildConflict(section, conflictType, message, slot, relatedSectionId);

    private static bool TryGetSharedCurriculumDepartment(
        SectionSnapshot first,
        SectionSnapshot second,
        out int sharedDepartmentId) => SchedulingHelpers.TryGetSharedCurriculumDepartment(first, second, out sharedDepartmentId);

    private static Dictionary<int, int> GetSuggestedSemestersByDepartment(
        int courseId,
        IReadOnlyDictionary<int, Dictionary<int, int>> suggestedSemestersByDepartment)
    {
        return suggestedSemestersByDepartment.TryGetValue(courseId, out var values)
            ? values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            : new Dictionary<int, int>();
    }

    private static HashSet<int> GetAccessibleDepartments(
        int courseId,
        int? owningDepartmentId,
        IReadOnlyDictionary<int, HashSet<int>> accessibleDepartments)
    {
        var result = accessibleDepartments.TryGetValue(courseId, out var values)
            ? values.ToHashSet()
            : new HashSet<int>();

        if (owningDepartmentId.HasValue)
            result.Add(owningDepartmentId.Value);

        return result;
    }

    private static string BuildSlotSignature(IEnumerable<TimeBlock> slots) =>
        string.Join("|", slots
            .OrderBy(slot => slot.Day)
            .ThenBy(slot => slot.StartTime)
            .Select(slot => $"{(int)slot.Day}:{slot.StartTime:HH\\:mm}-{slot.EndTime:HH\\:mm}"));

    private async Task<CourseSchedulingMetadata> LoadCourseSchedulingMetadataAsync(
        int termId,
        IEnumerable<int> courseIds,
        CancellationToken cancellationToken)
    {
        var ids = courseIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new CourseSchedulingMetadata();
        }

        var curriculumRows = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => ids.Contains(cc.CourseId) && cc.Curriculum.IsActive && cc.IsCompulsory)
            .Select(cc => new
            {
                cc.CourseId,
                DepartmentId = cc.Curriculum.Program.DepartmentId,
                cc.SuggestedSemester
            })
            .ToListAsync(cancellationToken);

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
            .Where(course => ids.Contains(course.Id))
            .Select(course => new { course.Id, course.DepartmentId })
            .ToDictionaryAsync(course => course.Id, course => course.DepartmentId, cancellationToken);

        var policyRows = await _context.CourseAccessPolicies
            .AsNoTracking()
            .Where(policy => policy.TermId == termId && ids.Contains(policy.CourseId))
            .Select(policy => new { policy.CourseId, policy.DepartmentId, policy.FacultyId })
            .ToListAsync(cancellationToken);

        var facultyIds = policyRows
            .Where(policy => !policy.DepartmentId.HasValue && policy.FacultyId.HasValue)
            .Select(policy => policy.FacultyId!.Value)
            .Distinct()
            .ToList();

        var facultyDepartmentMap = facultyIds.Count == 0
            ? new Dictionary<int, List<int>>()
            : await _context.Departments
                .AsNoTracking()
                .Where(department => facultyIds.Contains(department.FacultyId))
                .GroupBy(department => department.FacultyId)
                .ToDictionaryAsync(
                    group => group.Key,
                    group => group.Select(department => department.Id).ToList(),
                    cancellationToken);

        var policiesByCourse = policyRows
            .GroupBy(policy => policy.CourseId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var accessibleDepartments = new Dictionary<int, HashSet<int>>();
        foreach (var courseId in ids)
        {
            var departmentIds = new HashSet<int>();

            if (suggestedSemestersByDepartment.TryGetValue(courseId, out var suggestedByDept))
            {
                departmentIds.UnionWith(suggestedByDept.Keys);
            }

            if (courseDepartmentIds.TryGetValue(courseId, out var owningDepartmentId) && owningDepartmentId.HasValue)
            {
                departmentIds.Add(owningDepartmentId.Value);
            }

            if (policiesByCourse.TryGetValue(courseId, out var policies))
            {
                foreach (var policy in policies)
                {
                    if (policy.DepartmentId.HasValue)
                    {
                        departmentIds.Add(policy.DepartmentId.Value);
                    }
                    else if (policy.FacultyId.HasValue
                        && facultyDepartmentMap.TryGetValue(policy.FacultyId.Value, out var facultyDepartments))
                    {
                        departmentIds.UnionWith(facultyDepartments);
                    }
                }
            }

            accessibleDepartments[courseId] = departmentIds;
        }

        return new CourseSchedulingMetadata
        {
            MinSuggestedSemesters = minSuggestedSemesters,
            SuggestedSemestersByDepartment = suggestedSemestersByDepartment,
            AccessibleDepartments = accessibleDepartments
        };
    }

    private async Task<AcademicTerm?> ResolveTermAsync(int? termId, CancellationToken cancellationToken)
    {
        if (termId.HasValue)
        {
            return await _context.AcademicTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(term => term.Id == termId.Value, cancellationToken);
        }

        return await _context.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(term => term.IsActive, cancellationToken);
    }

    private async Task<Dictionary<int, string>> LoadInstructorNameMapAsync(List<int> instructorIds, CancellationToken cancellationToken)
    {
        if (instructorIds.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        return await _context.Instructors
            .AsNoTracking()
            .Where(instructor => instructorIds.Contains(instructor.Id))
            .Join(
                _context.Users.AsNoTracking(),
                instructor => instructor.UserId,
                user => user.Id,
                (instructor, user) => new
                {
                    instructor.Id,
                    FullName = $"{instructor.Title} {user.FirstName} {user.LastName}".Trim()
                })
            .ToDictionaryAsync(item => item.Id, item => item.FullName, cancellationToken);
    }

    private async Task<string> GetInstructorNameAsync(Instructor instructor, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(currentUser => currentUser.Id == instructor.UserId, cancellationToken);

        return user == null
            ? instructor.Title
            : $"{instructor.Title} {user.FirstName} {user.LastName}".Trim();
    }

    private static string GetDayName(int day) => SchedulingHelpers.GetDayName(day);

    // ── Soft constraint config helpers ────────────────────────────────────

    internal async Task<Dictionary<string, string>> LoadSystemConfigAsync(CancellationToken ct)
    {
        return await _context.SystemConfigs.AsNoTracking()
            .ToDictionaryAsync(c => c.Key, c => c.Value, ct);
    }

    internal async Task<SoftConstraintConfig> LoadSoftConstraintConfigAsync(CancellationToken ct)
    {
        var config = await LoadSystemConfigAsync(ct);

        var s9MorningStart = config.TryGetValue("SoftConstraint.S9.MorningStart", out var ms) && TimeOnly.TryParse(ms, out var mst)
            ? mst : new TimeOnly(8, 0);
        var s9EveningStart = config.TryGetValue("SoftConstraint.S9.EveningStart", out var es) && TimeOnly.TryParse(es, out var est)
            ? est : new TimeOnly(17, 0);

        return new SoftConstraintConfig(
            S1Enabled: GetBool(config, "SoftConstraint.S1.Enabled", true),
            S1MinOccupancy: GetInt(config, "Classroom.OccupancyTarget.Min", 60),
            S1MaxOccupancy: GetInt(config, "Classroom.OccupancyTarget.Max", 85),
            S2Enabled: GetBool(config, "SoftConstraint.S2.Enabled", true),
            S2MaxDailyHours: GetInt(config, "SoftConstraint.S2.MaxDailyHours", 6),
            S3Enabled: GetBool(config, "SoftConstraint.S3.Enabled", true),
            S3MaxSectionsPerSlot: GetInt(config, "SoftConstraint.S3.MaxSectionsPerSlot", 5),
            S4Enabled: GetBool(config, "SoftConstraint.S4.Enabled", true),
            S4MinCoPairThreshold: GetInt(config, "SoftConstraint.S4.MinCoPairThreshold", 20),
            S5Enabled: GetBool(config, "SoftConstraint.S5.Enabled", true),
            S7Enabled: GetBool(config, "SoftConstraint.S7.Enabled", true),
            S7MaxGapSlots: GetInt(config, "SoftConstraint.S7.MaxGapSlots", 3),
            S8Enabled: GetBool(config, "SoftConstraint.S8.Enabled", true),
            S9Enabled: GetBool(config, "SoftConstraint.S9.Enabled", true),
            S9MorningStart: s9MorningStart,
            S9EveningStart: s9EveningStart,
            S12Enabled: GetBool(config, "SoftConstraint.S12.Enabled", true),
            S12MaxImbalancePercent: GetInt(config, "SoftConstraint.S12.MaxImbalancePercent", 20));
    }

    /// <summary>
    /// Loads co-enrollment pairs from the last 3 terms (before the given term).
    /// Finds course pairs frequently taken by the same student in the same term.
    /// </summary>
    internal async Task<List<CoEnrollmentPair>> LoadCoEnrollmentPairsAsync(int currentTermId, int minThreshold, CancellationToken ct)
    {
        // Get last 3 term IDs before the current term
        var recentTermIds = await _context.AcademicTerms.AsNoTracking()
            .Where(t => t.Id < currentTermId)
            .OrderByDescending(t => t.Id)
            .Take(3)
            .Select(t => t.Id)
            .ToListAsync(ct);

        if (recentTermIds.Count == 0)
            return new List<CoEnrollmentPair>();

        // Get all active registrations from recent terms: (studentId, courseId, termId)
        var registrations = await _context.CourseRegistrations.AsNoTracking()
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

        // Group by (studentId, termId) to find courses taken together
        var coPairCounts = new Dictionary<(int, int), int>();

        foreach (var termGroup in registrations.GroupBy(r => (r.StudentId, r.TermId)))
        {
            var courseIds = termGroup.Select(r => r.CourseId).Distinct().OrderBy(x => x).ToList();
            for (int i = 0; i < courseIds.Count; i++)
            {
                for (int j = i + 1; j < courseIds.Count; j++)
                {
                    var key = (courseIds[i], courseIds[j]);
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

    internal async Task<HardConstraintConfig> LoadHardConstraintConfigAsync(CancellationToken ct)
    {
        var config = await LoadSystemConfigAsync(ct);

        var lunchIsHard = GetBool(config, "Schedule.LunchBreak.IsHard", false);
        var lunchStart = config.TryGetValue("Schedule.LunchBreakStart", out var ls) && TimeOnly.TryParse(ls, out var lst)
            ? lst : new TimeOnly(11, 40);
        var lunchEnd = config.TryGetValue("Schedule.LunchBreakEnd", out var le) && TimeOnly.TryParse(le, out var let)
            ? let : new TimeOnly(12, 20);
        var maxConsecutive = GetInt(config, "Schedule.MaxConsecutiveHours", 4);
        var breakDuration = GetInt(config, "Schedule.BreakDurationMinutes", 5);
        var amphiThreshold = GetInt(config, "Classroom.AmphiThreshold", 100);
        var theoryLabSplitThreshold = GetInt(config, "Schedule.TheoryLabSplitThreshold", 4);
        var allowSectionOverlap = GetBool(config, "Schedule.AllowSameCourseSectionOverlap", false);
        var conflictMode = config.TryGetValue("Schedule.CurriculumConflictMode", out var cm) ? cm : "curriculum";

        return new HardConstraintConfig(lunchIsHard, lunchStart, lunchEnd, maxConsecutive, breakDuration, amphiThreshold, theoryLabSplitThreshold, allowSectionOverlap, conflictMode);
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

    private static string GetDayOfWeekName(DayOfWeek day) => SchedulingHelpers.GetDayOfWeekName(day);

    // ── Constants & inner types ──────────────────────────────────────────

    internal record SoftConstraintConfig(
        bool S1Enabled, int S1MinOccupancy, int S1MaxOccupancy,
        bool S2Enabled, int S2MaxDailyHours,
        bool S3Enabled, int S3MaxSectionsPerSlot,
        bool S4Enabled, int S4MinCoPairThreshold,
        bool S5Enabled,
        bool S7Enabled, int S7MaxGapSlots,
        bool S8Enabled,
        bool S9Enabled, TimeOnly S9MorningStart, TimeOnly S9EveningStart,
        bool S12Enabled, int S12MaxImbalancePercent);

    internal record HardConstraintConfig(
        bool LunchBreakIsHard, TimeOnly LunchBreakStart, TimeOnly LunchBreakEnd,
        int MaxConsecutiveHours, int BreakDurationMinutes, int AmphiThreshold,
        int TheoryLabSplitThreshold = 4,
        bool AllowSameCourseSectionOverlap = false,
        string CurriculumConflictMode = "curriculum");


    private static class ConflictTypes
    {
        public const string ClassroomConflict = "ClassroomConflict";
        public const string InstructorConflict = "InstructorConflict";
        public const string CapacityConflict = "CapacityConflict";
        public const string WeeklyLoadConflict = "WeeklyLoadConflict";
        public const string InvalidScheduleJson = "InvalidScheduleJson";
        public const string ClassroomTypeConflict = "ClassroomTypeConflict";             // H4
        public const string CurriculumConflict = "CurriculumConflict";                   // H5
        public const string SameCourseComponentConflict = "SameCourseComponentConflict"; // H6
        public const string SameCourseSectionConflict = "SameCourseSectionConflict";     // H8
        public const string FacultyClassroomConflict = "FacultyClassroomConflict";       // H10
        public const string CorequisiteConflict = "CorequisiteConflict";                 // H11
        public const string CreditHourMismatch = "CreditHourMismatch";                   // H12
        public const string LunchBreakConflict = "LunchBreakConflict";                   // H13
        public const string MaxConsecutiveHoursConflict = "MaxConsecutiveHoursConflict"; // H15
        public const string DailyLoadConflict = "DailyLoadConflict";                       // S-DL: günlük 9 saat aşımı
        public const string NoDayOffConflict = "NoDayOffConflict";                         // S-DO: haftalık boş gün yok
    }

}
