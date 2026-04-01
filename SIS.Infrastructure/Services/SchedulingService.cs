using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class SchedulingService : ISchedulingService
{
    private readonly AppDbContext _context;

    public SchedulingService(AppDbContext context)
    {
        _context = context;
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

    private static double CalculateSoftPenalty(
        SectionSnapshot candidate,
        TermSnapshot snapshot,
        SoftConstraintConfig softConfig)
    {
        double penalty = 0;

        // S1: Occupancy deviation from ideal range
        if (softConfig.S1Enabled && candidate.ClassroomId.HasValue && candidate.ClassroomCapacity > 0)
        {
            var occupancy = candidate.Capacity * 100.0 / candidate.ClassroomCapacity;
            if (occupancy < softConfig.S1MinOccupancy)
                penalty += (softConfig.S1MinOccupancy - occupancy) * 0.5;
            else if (occupancy > softConfig.S1MaxOccupancy)
                penalty += (occupancy - softConfig.S1MaxOccupancy) * 0.5;
        }

        // S2: Instructor daily load imbalance
        if (softConfig.S2Enabled && candidate.InstructorId.HasValue && candidate.TimeBlocks.Count > 0)
        {
            var existingSlots = snapshot.Sections
                .Where(s => s.InstructorId == candidate.InstructorId)
                .SelectMany(s => s.TimeBlocks)
                .ToList();

            var allSlots = existingSlots.Concat(candidate.TimeBlocks);

            foreach (var dayGroup in allSlots.GroupBy(s => s.Day))
            {
                var dailyHours = dayGroup.Sum(s => (s.EndTime - s.StartTime).TotalHours);
                if (dailyHours > softConfig.S2MaxDailyHours)
                    penalty += (dailyHours - softConfig.S2MaxDailyHours) * 10;
            }
        }

        // S3: Prime-time congestion
        if (softConfig.S3Enabled && candidate.TimeBlocks.Count > 0)
        {
            var existingSlotKeys = snapshot.Sections
                .SelectMany(s => s.TimeBlocks.Select(slot => (slot.Day, slot.StartTime)))
                .ToList();

            foreach (var cSlot in candidate.TimeBlocks)
            {
                var key = (cSlot.Day, cSlot.StartTime);
                var count = existingSlotKeys.Count(k => k == key) + 1;
                if (count > softConfig.S3MaxSectionsPerSlot)
                    penalty += (count - softConfig.S3MaxSectionsPerSlot) * 5;
            }
        }

        // S4: Co-enrollment overlap penalty (uses preloaded pairs from snapshot)
        if (softConfig.S4Enabled && candidate.TimeBlocks.Count > 0 && snapshot.CoEnrollmentPairs.Count > 0)
        {
            var relevantPairs = snapshot.CoEnrollmentPairs
                .Where(p => p.CourseId1 == candidate.CourseId || p.CourseId2 == candidate.CourseId);

            foreach (var pair in relevantPairs)
            {
                var pairedCourseId = pair.CourseId1 == candidate.CourseId ? pair.CourseId2 : pair.CourseId1;
                var pairedSections = snapshot.Sections
                    .Where(s => s.CourseId == pairedCourseId && s.TimeBlocks.Count > 0);

                foreach (var pairedSec in pairedSections)
                {
                    if (GetOverlapSlots(candidate.TimeBlocks, pairedSec.TimeBlocks).Count > 0)
                        penalty += pair.StudentCount * 0.5;
                }
            }
        }

        // S5: Lunch break preference penalty
        if (softConfig.S5Enabled && candidate.TimeBlocks.Count > 0)
        {
            foreach (var slot in candidate.TimeBlocks)
            {
                // Use default lunch times for penalty calculation
                var lunchStart = new TimeOnly(11, 40);
                var lunchEnd = new TimeOnly(12, 20);
                if (slot.StartTime < lunchEnd && slot.EndTime > lunchStart)
                    penalty += 8;
            }
        }

        // S7: Student gap penalty
        if (softConfig.S7Enabled && candidate.TimeBlocks.Count > 0
            && candidate.DepartmentId.HasValue && candidate.SuggestedSemester.HasValue)
        {
            var candidateYear = (candidate.SuggestedSemester.Value - 1) / 2;
            var sameDeptYearSlots = snapshot.Sections
                .Where(s => s.DepartmentId == candidate.DepartmentId
                    && s.SuggestedSemester.HasValue
                    && (s.SuggestedSemester.Value - 1) / 2 == candidateYear
                    && s.TimeBlocks.Count > 0)
                .SelectMany(s => s.TimeBlocks)
                .Concat(candidate.TimeBlocks)
                .GroupBy(s => s.Day);

            foreach (var dayGroup in sameDeptYearSlots)
            {
                var sorted = dayGroup.OrderBy(s => s.StartTime).ToList();
                for (int idx = 1; idx < sorted.Count; idx++)
                {
                    var gapHours = (sorted[idx].StartTime - sorted[idx - 1].EndTime).TotalHours;
                    if (gapHours >= softConfig.S7MaxGapSlots)
                        penalty += (gapHours - softConfig.S7MaxGapSlots + 1) * 3;
                }
            }
        }

        // S8: Instructor day spread penalty
        if (softConfig.S8Enabled && candidate.InstructorId.HasValue && candidate.TimeBlocks.Count > 0)
        {
            var existingSlots = snapshot.Sections
                .Where(s => s.InstructorId == candidate.InstructorId)
                .SelectMany(s => s.TimeBlocks)
                .ToList();

            var allSlots = existingSlots.Concat(candidate.TimeBlocks).ToList();
            var usedDays = allSlots.Select(s => s.Day).Distinct().Count();
            var totalHours = allSlots.Sum(s => (s.EndTime - s.StartTime).TotalHours);
            var minPossibleDays = Math.Max(1, (int)Math.Ceiling(totalHours / (double)softConfig.S2MaxDailyHours));

            if (usedDays > minPossibleDays)
                penalty += (usedDays - minPossibleDays) * 4;
        }

        // S9: Early morning / late evening penalty
        if (softConfig.S9Enabled && candidate.TimeBlocks.Count > 0)
        {
            foreach (var slot in candidate.TimeBlocks)
            {
                if (slot.StartTime <= softConfig.S9MorningStart)
                    penalty += 6;
                else if (slot.StartTime >= softConfig.S9EveningStart)
                    penalty += 6;
            }
        }

        // S12: Weekday balance penalty
        if (softConfig.S12Enabled && candidate.TimeBlocks.Count > 0)
        {
            var existingSlots = snapshot.Sections.SelectMany(s => s.TimeBlocks).ToList();
            var allSlots = existingSlots.Concat(candidate.TimeBlocks);

            var slotsPerDay = allSlots
                .GroupBy(s => s.Day)
                .Select(g => g.Count())
                .ToList();

            if (slotsPerDay.Count > 1)
            {
                var avg = slotsPerDay.Average();
                var maxCount = slotsPerDay.Max();
                var minCount = slotsPerDay.Min();
                var diff = maxCount - minCount;

                if (avg > 0 && diff > avg * softConfig.S12MaxImbalancePercent / 100.0)
                    penalty += diff * 2;
            }
        }

        return penalty;
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

    public async Task<ClassroomDashboardDto> GetClassroomDashboardAsync(int? termId = null, CancellationToken cancellationToken = default)
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

        var classrooms = await _context.Classrooms.AsNoTracking().ToListAsync(cancellationToken);
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

        result.Classrooms = classrooms.Select(c => new ClassroomInfoDto
        {
            Id = c.Id,
            Name = c.Name,
            Capacity = c.Capacity,
            Type = c.Type.ToString(),
            Schedules = classroomSchedules.TryGetValue(c.Id, out var schedules) ? schedules : new()
        }).OrderBy(c => c.Name).ToList();

        var heatmapMap = new Dictionary<(int day, int hour), int>();
        for (int day = 0; day < 5; day++)
        {
            for (int hour = 8; hour <= 17; hour++)
            {
                heatmapMap[(day, hour)] = 0;
            }
        }

        var occupationMap = new Dictionary<(int day, int hour), List<ClassroomOccupationDto>>();
        foreach (var section in snapshot.Sections)
        {
            if (section.ClassroomId.HasValue)
            {
                foreach (var slot in section.TimeBlocks)
                {
                    int dayIndex = (int)slot.Day - 1;
                    if (dayIndex >= 0 && dayIndex <= 4)
                    {
                        int startHour = slot.StartTime.Hour;
                        int endHour = slot.EndTime.Minute > 0 ? slot.EndTime.Hour : slot.EndTime.Hour - 1;

                        for (int h = startHour; h <= endHour && h <= 17; h++)
                        {
                            if (h >= 8)
                            {
                                var key = (dayIndex, h);
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

        int totalSlots = result.TotalClassrooms * 5 * 10;
        int usedSlots = heatmapMap.Values.Sum();
        result.EmptySlots = Math.Max(0, totalSlots - usedSlots);
        result.OccupancyRate = totalSlots > 0 ? Math.Round((double)usedSlots / totalSlots * 100) : 0;

        foreach (var kvp in heatmapMap)
        {
            var day = kvp.Key.day;
            var hour = kvp.Key.hour;
            var used = kvp.Value;
            var total = result.TotalClassrooms;

            result.Heatmap.Add(new ClassroomHeatmapCellDto
            {
                Day = day,
                Hour = hour,
                UsedClassrooms = used,
                Occupancy = total > 0 ? Math.Min(1, (double)used / total) : 0,
                Details = $"{GetDayName(day)} {hour:D2}:00 — {used}/{total} derslik dolu",
                Occupations = occupationMap.TryGetValue((day, hour), out var occList) ? occList : new()
            });
        }

        return result;
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private async Task<TermSnapshot> BuildTermSnapshotAsync(int termId, int? excludedSectionId, int? departmentId, CancellationToken cancellationToken)
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
            CourseAccessibleDepartments = courseMetadata.AccessibleDepartments
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
            InstructorId = instructorId,
            InstructorName = instructorId.HasValue && instructorNameMap.TryGetValue(instructorId.Value, out var instructorName)
                ? instructorName
                : string.Empty,
            InstructorMaxWeeklyHours = section.Instructor?.MaxWeeklyHours ?? 0,
            DepartmentId = section.Course.DepartmentId,
            SuggestedSemester = semesterMap.TryGetValue(section.CourseId, out var sem) ? sem : null,
            SuggestedSemestersByDepartment = GetSuggestedSemestersByDepartment(section.CourseId, suggestedSemestersByDepartment),
            AccessibleDepartmentIds = GetAccessibleDepartments(section.CourseId, section.Course.DepartmentId, accessibleDepartments),
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
        }
        else
        {
            scheduleSlots = await LoadSectionTimeBlocksAsync(section.Id, cancellationToken);
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
            InstructorId = instructor?.Id,
            InstructorName = instructorName,
            InstructorMaxWeeklyHours = instructor?.MaxWeeklyHours ?? 0,
            DepartmentId = section.Course.DepartmentId,
            SuggestedSemester = suggestedSemester,
            SuggestedSemestersByDepartment = suggestedSemestersByDepartment ?? new Dictionary<int, int>(),
            AccessibleDepartmentIds = accessibleDepartmentIds ?? new HashSet<int>(),
            TimeBlocks = scheduleSlots.ToList(),
            Notes = notes
        };
    }

    private async Task<ScheduleValidationResultDto> EvaluateExistingConflictsAsync(TermSnapshot snapshot, CancellationToken ct)
    {
        var conflicts = new List<SchedulingConflictDto>();
        var notes = new List<string>();
        var hardConfig = await LoadHardConstraintConfigAsync(ct);

        foreach (var section in snapshot.Sections)
        {
            if (section.ClassroomId.HasValue && section.ClassroomCapacity < section.Capacity)
            {
                conflicts.Add(BuildConflict(
                    section,
                    ConflictTypes.CapacityConflict,
                    $"Derslik kapasitesi section kontenjanini karsilamiyor ({section.ClassroomCapacity}/{section.Capacity}).",
                    null,
                    null));
            }

            // H4: Derslik tipi uyumu — Lab dersi Lab/Computer derslikte olmali
            if (section.ClassroomId.HasValue
                && section.LabHours > 0 && section.TheoryHours == 0
                && section.ClassroomType != ClassroomType.Lab && section.ClassroomType != ClassroomType.Computer)
            {
                conflicts.Add(BuildConflict(
                    section,
                    ConflictTypes.ClassroomTypeConflict,
                    $"Lab section'u ({section.CourseCode}) Lab veya Computer tipinde bir dersliğe atanmalıdır, mevcut tip: {section.ClassroomType}.",
                    null,
                    null));
            }

            // H4+: Büyük dersler (kontenjan >= AmphiThreshold) → Amphitheater zorunlu
            if (section.ClassroomId.HasValue
                && section.Capacity >= hardConfig.AmphiThreshold
                && section.ClassroomType != ClassroomType.Amphitheater)
            {
                conflicts.Add(BuildConflict(
                    section,
                    ConflictTypes.ClassroomTypeConflict,
                    $"Kontenjanı {section.Capacity} olan ders için Amfi derslik gereklidir.",
                    null,
                    null));
            }

            // H10: Fakülteye atanmamış derslik kullanımı
            if (section.ClassroomId.HasValue
                && section.DepartmentId.HasValue
                && snapshot.ClassroomAssignments.Count > 0)
            {
                var sectionFacultyId = snapshot.DeptFacultyMap.GetValueOrDefault(section.DepartmentId.Value);
                if (sectionFacultyId > 0)
                {
                    var hasAssignment = snapshot.ClassroomAssignments
                        .Any(cta => cta.ClassroomId == section.ClassroomId.Value && cta.FacultyId == sectionFacultyId);
                    if (!hasAssignment)
                    {
                        conflicts.Add(BuildConflict(
                            section,
                            ConflictTypes.FacultyClassroomConflict,
                            $"Derslik '{section.ClassroomName}' bu dönemde ilgili fakülteye atanmamış.",
                            null,
                            null));
                    }
                }
            }
        }

        for (var i = 0; i < snapshot.Sections.Count; i++)
        {
            for (var j = i + 1; j < snapshot.Sections.Count; j++)
            {
                var first = snapshot.Sections[i];
                var second = snapshot.Sections[j];

                foreach (var overlap in GetOverlapSlots(first.TimeBlocks, second.TimeBlocks))
                {
                    if (first.ClassroomId.HasValue && first.ClassroomId == second.ClassroomId)
                    {
                        conflicts.Add(BuildConflict(
                            first,
                            ConflictTypes.ClassroomConflict,
                            $"Derslik '{first.ClassroomName}' aynı zaman aralığında '{second.CourseCode}' section'u ile çakışıyor.",
                            overlap,
                            second.SectionId));

                        conflicts.Add(BuildConflict(
                            second,
                            ConflictTypes.ClassroomConflict,
                            $"Derslik '{second.ClassroomName}' aynı zaman aralığında '{first.CourseCode}' section'u ile çakışıyor.",
                            overlap,
                            first.SectionId));
                    }

                    if (first.InstructorId.HasValue && first.InstructorId == second.InstructorId)
                    {
                        conflicts.Add(BuildConflict(
                            first,
                            ConflictTypes.InstructorConflict,
                            $"Öğretim üyesi '{first.InstructorName}' aynı zaman aralığında '{second.CourseCode}' section'u ile çakışıyor.",
                            overlap,
                            second.SectionId));

                        conflicts.Add(BuildConflict(
                            second,
                            ConflictTypes.InstructorConflict,
                            $"Öğretim üyesi '{second.InstructorName}' aynı zaman aralığında '{first.CourseCode}' section'u ile çakışıyor.",
                            overlap,
                            first.SectionId));
                    }

                    // H5: Aynı bölüm/yıl öğrencilerinin alabileceği dersler aynı slot'ta olamaz
                    if (first.CourseId != second.CourseId
                        && TryGetSharedCurriculumDepartment(first, second, out _))
                    {
                        conflicts.Add(BuildConflict(
                            first,
                            ConflictTypes.CurriculumConflict,
                            $"Aynı bölüm/yıl öğrencilerinin alabileceği '{first.CourseCode}' ile '{second.CourseCode}' dersleri aynı zaman aralığında çakışıyor.",
                            overlap,
                            second.SectionId));

                        conflicts.Add(BuildConflict(
                            second,
                            ConflictTypes.CurriculumConflict,
                            $"Aynı bölüm/yıl öğrencilerinin alabileceği '{second.CourseCode}' ile '{first.CourseCode}' dersleri aynı zaman aralığında çakışıyor.",
                            overlap,
                            first.SectionId));
                    }

                    // H6: Aynı dersin bileşenleri (lecture vs lab) birbiriyle çakışmamalı
                    if (first.CourseId == second.CourseId)
                    {
                        var firstIsLab = first.LabHours > 0 && first.TheoryHours == 0;
                        var secondIsLab = second.LabHours > 0 && second.TheoryHours == 0;
                        var firstIsLecture = first.TheoryHours > 0;
                        var secondIsLecture = second.TheoryHours > 0;

                        if ((firstIsLab && secondIsLecture) || (firstIsLecture && secondIsLab))
                        {
                            conflicts.Add(BuildConflict(
                                first,
                                ConflictTypes.SameCourseComponentConflict,
                                $"Aynı dersin ({first.CourseCode}) lecture ve lab bileşenleri aynı zaman aralığında çakışıyor.",
                                overlap,
                                second.SectionId));

                            conflicts.Add(BuildConflict(
                                second,
                                ConflictTypes.SameCourseComponentConflict,
                                $"Aynı dersin ({second.CourseCode}) lecture ve lab bileşenleri aynı zaman aralığında çakışıyor.",
                                overlap,
                                first.SectionId));
                        }
                    }

                    // H8: Aynı dersin farklı şubeleri aynı slot'ta olamaz
                    if (first.CourseId == second.CourseId && first.SectionId != second.SectionId)
                    {
                        conflicts.Add(BuildConflict(
                            first,
                            ConflictTypes.SameCourseSectionConflict,
                            $"Aynı dersin ({first.CourseCode}) farklı şubeleri aynı zaman aralığında çakışıyor.",
                            overlap,
                            second.SectionId));

                        conflicts.Add(BuildConflict(
                            second,
                            ConflictTypes.SameCourseSectionConflict,
                            $"Aynı dersin ({second.CourseCode}) farklı şubeleri aynı zaman aralığında çakışıyor.",
                            overlap,
                            first.SectionId));
                    }
                }
            }
        }

        foreach (var group in snapshot.Sections
                     .Where(section => section.InstructorId.HasValue)
                     .GroupBy(section => section.InstructorId!.Value))
        {
            var firstSection = group.First();
            if (firstSection.InstructorMaxWeeklyHours <= 0)
            {
                notes.Add($"Instructor haftalik yuk limiti tanimsiz veya sifir: {firstSection.InstructorName}.");
                continue;
            }

            var totalWeeklyHours = group.Sum(section => ResolveWeeklyHours(section.TimeBlocks, section));
            if (totalWeeklyHours <= firstSection.InstructorMaxWeeklyHours)
            {
                continue;
            }

            foreach (var section in group)
            {
                conflicts.Add(BuildConflict(
                    section,
                    ConflictTypes.WeeklyLoadConflict,
                    $"Instructor haftalik ders yuku limiti asiliyor ({totalWeeklyHours:0.##}/{firstSection.InstructorMaxWeeklyHours} saat).",
                    null,
                    null));
            }
        }

        // ── DailyLoad: Bir hocanın günlük ders yükü 9 saati geçmemeli ──
        foreach (var group in snapshot.Sections
                     .Where(s => s.InstructorId.HasValue && s.TimeBlocks.Count > 0)
                     .GroupBy(s => s.InstructorId!.Value))
        {
            var instructorSections = group.ToList();
            var dailyGroups = instructorSections
                .SelectMany(s => s.TimeBlocks.Select(tb => new { Section = s, tb.Day, tb.StartTime, tb.EndTime }))
                .GroupBy(x => x.Day);

            foreach (var dayGroup in dailyGroups)
            {
                var dailyHours = dayGroup.Sum(x => (x.EndTime - x.StartTime).TotalHours);
                if (dailyHours > 9)
                {
                    var firstSection = dayGroup.First().Section;
                    foreach (var section in instructorSections.Where(s => s.TimeBlocks.Any(tb => tb.Day == dayGroup.Key)))
                    {
                        conflicts.Add(BuildConflict(
                            section,
                            ConflictTypes.DailyLoadConflict,
                            $"Öğretim üyesi '{section.InstructorName}' {dayGroup.Key} günü {dailyHours:0.#} saat ders veriyor (maks 9 saat).",
                            null,
                            null));
                    }
                }
            }
        }

        // ── NoDayOff: Her hocanın haftada en az 1 günü boş olmalı ──
        foreach (var group in snapshot.Sections
                     .Where(s => s.InstructorId.HasValue && s.TimeBlocks.Count > 0)
                     .GroupBy(s => s.InstructorId!.Value))
        {
            var instructorSections = group.ToList();
            var usedDays = instructorSections
                .SelectMany(s => s.TimeBlocks)
                .Select(tb => tb.Day)
                .Distinct()
                .Count();

            if (usedDays >= 5)
            {
                foreach (var section in instructorSections)
                {
                    conflicts.Add(BuildConflict(
                        section,
                        ConflictTypes.NoDayOffConflict,
                        $"Öğretim üyesi '{section.InstructorName}' haftanın 5 gününde de ders veriyor, en az 1 gün boş olmalıdır.",
                        null,
                        null));
                }
            }
        }

        // ── H11: Corequisite Compatibility ──────────────────────────────
        foreach (var coreq in snapshot.Corequisites)
        {
            var courseASections = snapshot.Sections
                .Where(s => s.CourseId == coreq.CourseId && s.TimeBlocks.Count > 0)
                .ToList();
            var courseBSections = snapshot.Sections
                .Where(s => s.CourseId == coreq.RequiredCourseId && s.TimeBlocks.Count > 0)
                .ToList();

            if (courseASections.Count == 0 || courseBSections.Count == 0)
                continue;

            var hasNonConflicting = courseASections.Any(a =>
                courseBSections.Any(b =>
                    GetOverlapSlots(a.TimeBlocks, b.TimeBlocks).Count == 0));

            if (!hasNonConflicting)
            {
                var firstA = courseASections[0];
                var firstB = courseBSections[0];
                foreach (var sec in courseASections)
                {
                    conflicts.Add(BuildConflict(
                        sec,
                        ConflictTypes.CorequisiteConflict,
                        $"Corequisite dersler ({firstA.CourseCode} ve {firstB.CourseCode}) arasında çakışmasız bir şube kombinasyonu bulunmuyor.",
                        null,
                        null));
                }
                foreach (var sec in courseBSections)
                {
                    conflicts.Add(BuildConflict(
                        sec,
                        ConflictTypes.CorequisiteConflict,
                        $"Corequisite dersler ({firstA.CourseCode} ve {firstB.CourseCode}) arasında çakışmasız bir şube kombinasyonu bulunmuyor.",
                        null,
                        null));
                }
            }
        }

        // ── H12: Credit-Hour Block Compatibility ────────────────────────
        foreach (var section in snapshot.Sections)
        {
            if (section.TimeBlocks.Count == 0)
                continue;

            var expectedHours = section.TheoryHours + section.LabHours;
            if (expectedHours <= 0)
                continue;

            var actualMinutes = section.TimeBlocks.Sum(slot => (slot.EndTime - slot.StartTime).TotalMinutes);
            var actualHours = (int)Math.Round(actualMinutes / 60.0);

            if (actualHours != expectedHours)
            {
                conflicts.Add(BuildConflict(
                    section,
                    ConflictTypes.CreditHourMismatch,
                    $"Dersin saat gereksinimine uygun slot bloğu atanmamış ({section.CourseCode}: beklenen {expectedHours} saat, atanan {actualHours} saat).",
                    null,
                    null));
            }
        }

        // ── H13: Lunch Break Protection ─────────────────────────────────

        if (hardConfig.LunchBreakIsHard)
        {
            foreach (var section in snapshot.Sections)
            {
                foreach (var slot in section.TimeBlocks)
                {
                    if (slot.StartTime < hardConfig.LunchBreakEnd && slot.EndTime > hardConfig.LunchBreakStart)
                    {
                        conflicts.Add(BuildConflict(
                            section,
                            ConflictTypes.LunchBreakConflict,
                            "Öğle arası saatlerinde ders atanamaz.",
                            slot,
                            null));
                    }
                }
            }
        }

        // ── H15: Max Consecutive Hours ──────────────────────────────────
        foreach (var instructorGroup in snapshot.Sections
                     .Where(s => s.InstructorId.HasValue && s.TimeBlocks.Count > 0)
                     .GroupBy(s => s.InstructorId!.Value))
        {
            var instructorName = instructorGroup.First().InstructorName;

            var allSlots = instructorGroup
                .SelectMany(s => s.TimeBlocks.Select(slot => new { s.SectionId, Slot = slot }))
                .GroupBy(x => x.Slot.Day);

            foreach (var dayGroup in allSlots)
            {
                var sortedSlots = dayGroup.OrderBy(x => x.Slot.StartTime).ToList();
                if (sortedSlots.Count <= 1)
                    continue;

                var blockStartIdx = 0;
                var blockMinutes = (sortedSlots[0].Slot.EndTime - sortedSlots[0].Slot.StartTime).TotalMinutes;

                for (int idx = 1; idx < sortedSlots.Count; idx++)
                {
                    var gap = (sortedSlots[idx].Slot.StartTime - sortedSlots[idx - 1].Slot.EndTime).TotalMinutes;
                    if (gap <= hardConfig.BreakDurationMinutes)
                    {
                        blockMinutes += (sortedSlots[idx].Slot.EndTime - sortedSlots[idx].Slot.StartTime).TotalMinutes;
                    }
                    else
                    {
                        var blockHours = blockMinutes / 60.0;
                        if (blockHours > hardConfig.MaxConsecutiveHours)
                        {
                            var dayName = GetDayOfWeekName(dayGroup.Key);
                            for (int k = blockStartIdx; k < idx; k++)
                            {
                                var sec = snapshot.Sections.First(s => s.SectionId == sortedSlots[k].SectionId);
                                conflicts.Add(BuildConflict(
                                    sec,
                                    ConflictTypes.MaxConsecutiveHoursConflict,
                                    $"Öğretim üyesi '{instructorName}' {dayName} günü {hardConfig.MaxConsecutiveHours} saatten fazla aralıksız ders veriyor ({blockHours:0.#} saat).",
                                    null,
                                    null));
                            }
                        }
                        blockStartIdx = idx;
                        blockMinutes = (sortedSlots[idx].Slot.EndTime - sortedSlots[idx].Slot.StartTime).TotalMinutes;
                    }
                }

                // Check the last block
                var lastBlockHours = blockMinutes / 60.0;
                if (lastBlockHours > hardConfig.MaxConsecutiveHours)
                {
                    var dayName = GetDayOfWeekName(dayGroup.Key);
                    for (int k = blockStartIdx; k < sortedSlots.Count; k++)
                    {
                        var sec = snapshot.Sections.First(s => s.SectionId == sortedSlots[k].SectionId);
                        conflicts.Add(BuildConflict(
                            sec,
                            ConflictTypes.MaxConsecutiveHoursConflict,
                            $"Öğretim üyesi '{instructorName}' {dayName} günü {hardConfig.MaxConsecutiveHours} saatten fazla aralıksız ders veriyor ({lastBlockHours:0.#} saat).",
                            null,
                            null));
                    }
                }
            }
        }

        // ── Soft Constraints ────────────────────────────────────────────
        var softConfig = await LoadSoftConstraintConfigAsync(ct);

        // S1: Derslik doluluk oranı
        if (softConfig.S1Enabled)
        {
            foreach (var section in snapshot.Sections)
            {
                if (!section.ClassroomId.HasValue || section.ClassroomCapacity <= 0)
                    continue;

                var occupancyRate = section.Capacity * 100 / section.ClassroomCapacity;
                if (occupancyRate < softConfig.S1MinOccupancy)
                {
                    notes.Add($"S1 Uyarı: {section.CourseCode} ({section.ClassroomName}) doluluk oranı düşük (%{occupancyRate}, hedef >=%{softConfig.S1MinOccupancy})");
                }
                else if (occupancyRate > softConfig.S1MaxOccupancy)
                {
                    notes.Add($"S1 Uyarı: {section.CourseCode} ({section.ClassroomName}) doluluk oranı yüksek (%{occupancyRate}, hedef <=%{softConfig.S1MaxOccupancy})");
                }
            }
        }

        // S2: Hoca günlük yoğunluk dengesi
        if (softConfig.S2Enabled)
        {
            foreach (var instructorGroup in snapshot.Sections
                         .Where(s => s.InstructorId.HasValue && s.TimeBlocks.Count > 0)
                         .GroupBy(s => s.InstructorId!.Value))
            {
                var instructorName = instructorGroup.First().InstructorName;

                var slotsByDay = instructorGroup
                    .SelectMany(s => s.TimeBlocks)
                    .GroupBy(slot => slot.Day);

                foreach (var dayGroup in slotsByDay)
                {
                    var dailyHours = dayGroup.Sum(slot => (decimal)(slot.EndTime - slot.StartTime).TotalHours);
                    if (dailyHours > softConfig.S2MaxDailyHours)
                    {
                        var dayName = GetDayOfWeekName(dayGroup.Key);
                        notes.Add($"S2 Uyarı: {instructorName} hocanın {dayName} günündeki ders yükü fazla ({dailyHours:0.#} saat, max {softConfig.S2MaxDailyHours})");
                    }
                }
            }
        }

        // S3: Slot dağılım dengesi (prime-time yığılma)
        if (softConfig.S3Enabled)
        {
            var slotGroups = snapshot.Sections
                .SelectMany(s => s.TimeBlocks.Select(slot => new { slot.Day, slot.StartTime }))
                .GroupBy(x => (x.Day, x.StartTime));

            foreach (var group in slotGroups)
            {
                var count = group.Count();
                if (count > softConfig.S3MaxSectionsPerSlot)
                {
                    var dayName = GetDayOfWeekName(group.Key.Day);
                    notes.Add($"S3 Uyarı: {dayName} {group.Key.StartTime:HH\\:mm} slotunda {count} section yığılması var (max {softConfig.S3MaxSectionsPerSlot})");
                }
            }
        }

        // S4: Cross-Department Co-Enrollment Conflict
        if (softConfig.S4Enabled)
        {
            var coPairs = await LoadCoEnrollmentPairsAsync(snapshot.TermId, softConfig.S4MinCoPairThreshold, ct);
            var scheduledSections = snapshot.Sections
                .Where(s => s.TimeBlocks.Count > 0)
                .ToList();

            foreach (var pair in coPairs)
            {
                var sectionsA = scheduledSections.Where(s => s.CourseId == pair.CourseId1).ToList();
                var sectionsB = scheduledSections.Where(s => s.CourseId == pair.CourseId2).ToList();

                foreach (var secA in sectionsA)
                {
                    foreach (var secB in sectionsB)
                    {
                        var overlaps = GetOverlapSlots(secA.TimeBlocks, secB.TimeBlocks);
                        if (overlaps.Count > 0)
                        {
                            notes.Add($"S4 Uyarı: '{secA.CourseCode}' ve '{secB.CourseCode}' dersleri sık birlikte alınıyor ({pair.StudentCount} öğrenci) ancak zaman çakışması var.");
                        }
                    }
                }
            }
        }

        // S5: Lunch Break Preference (soft mode — only when H13 lunch break is NOT hard)
        if (softConfig.S5Enabled && !hardConfig.LunchBreakIsHard)
        {
            foreach (var section in snapshot.Sections)
            {
                foreach (var slot in section.TimeBlocks)
                {
                    if (slot.StartTime < hardConfig.LunchBreakEnd && slot.EndTime > hardConfig.LunchBreakStart)
                    {
                        notes.Add($"S5 Uyarı: {section.CourseCode} öğle arası saatlerinde programlanmış ({slot.StartTime:HH\\:mm}-{slot.EndTime:HH\\:mm}).");
                    }
                }
            }
        }

        // S7: Student Gap Minimization — same dept/year mandatory courses should not have large gaps on same day
        if (softConfig.S7Enabled)
        {
            var mandatorySections = snapshot.Sections
                .Where(s => s.DepartmentId.HasValue && s.SuggestedSemester.HasValue && s.TimeBlocks.Count > 0)
                .ToList();

            var deptYearGroups = mandatorySections
                .GroupBy(s => (DeptId: s.DepartmentId!.Value, Year: (s.SuggestedSemester!.Value - 1) / 2));

            foreach (var group in deptYearGroups)
            {
                var slotsByDay = group
                    .SelectMany(s => s.TimeBlocks.Select(slot => new { s.CourseCode, Slot = slot }))
                    .GroupBy(x => x.Slot.Day);

                foreach (var dayGroup in slotsByDay)
                {
                    var sorted = dayGroup.OrderBy(x => x.Slot.StartTime).ToList();
                    if (sorted.Count < 2) continue;

                    for (int idx = 1; idx < sorted.Count; idx++)
                    {
                        var gapHours = (sorted[idx].Slot.StartTime - sorted[idx - 1].Slot.EndTime).TotalHours;
                        if (gapHours >= softConfig.S7MaxGapSlots)
                        {
                            var dayName = GetDayOfWeekName(dayGroup.Key);
                            notes.Add($"S7 Uyarı: Bölüm {group.Key.DeptId}, {group.Key.Year + 1}. sınıf — {dayName} günü '{sorted[idx - 1].CourseCode}' ile '{sorted[idx].CourseCode}' arasında {gapHours:0.#} saatlik boşluk var.");
                        }
                    }
                }
            }
        }

        // S8: Instructor Day Minimization — prefer fewer teaching days
        if (softConfig.S8Enabled)
        {
            foreach (var instructorGroup in snapshot.Sections
                         .Where(s => s.InstructorId.HasValue && s.TimeBlocks.Count > 0)
                         .GroupBy(s => s.InstructorId!.Value))
            {
                var instructorName = instructorGroup.First().InstructorName;
                var allSlots = instructorGroup.SelectMany(s => s.TimeBlocks).ToList();
                var usedDays = allSlots.Select(s => s.Day).Distinct().Count();
                var totalHours = allSlots.Sum(s => (s.EndTime - s.StartTime).TotalHours);
                var maxHoursPerDay = (double)softConfig.S2MaxDailyHours;
                var minPossibleDays = Math.Max(1, (int)Math.Ceiling(totalHours / maxHoursPerDay));

                if (usedDays > minPossibleDays + 1)
                {
                    notes.Add($"S8 Uyarı: {instructorName} hocanın dersleri {usedDays} güne dağılmış (minimum {minPossibleDays} gün yeterli olabilir).");
                }
            }
        }

        // S9: Early Morning / Late Evening Penalty
        if (softConfig.S9Enabled)
        {
            foreach (var section in snapshot.Sections)
            {
                foreach (var slot in section.TimeBlocks)
                {
                    if (slot.StartTime <= softConfig.S9MorningStart)
                    {
                        notes.Add($"S9 Uyarı: {section.CourseCode} erken sabah slotuna ({slot.StartTime:HH\\:mm}) programlanmış.");
                    }
                    else if (slot.StartTime >= softConfig.S9EveningStart)
                    {
                        notes.Add($"S9 Uyarı: {section.CourseCode} geç akşam slotuna ({slot.StartTime:HH\\:mm}) programlanmış.");
                    }
                }
            }
        }

        // S12: Weekday Balance — sections should be distributed evenly across weekdays
        if (softConfig.S12Enabled)
        {
            var slotsPerDay = snapshot.Sections
                .SelectMany(s => s.TimeBlocks)
                .GroupBy(s => s.Day)
                .Select(g => g.Count())
                .ToList();

            if (slotsPerDay.Count > 1)
            {
                var avg = slotsPerDay.Average();
                var maxCount = slotsPerDay.Max();
                var minCount = slotsPerDay.Min();
                var diff = maxCount - minCount;

                if (avg > 0 && diff > avg * softConfig.S12MaxImbalancePercent / 100.0)
                {
                    notes.Add($"S12 Uyarı: Hafta içi slot dağılımı dengesiz (min: {minCount}, max: {maxCount}, fark: {diff}).");
                }
            }
        }

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
        var conflicts = new List<SchedulingConflictDto>();

        if (candidate.ClassroomId.HasValue && candidate.ClassroomCapacity < candidate.Capacity)
        {
            conflicts.Add(BuildConflict(
                candidate,
                ConflictTypes.CapacityConflict,
                $"Derslik kapasitesi section kontenjanini karsilamiyor ({candidate.ClassroomCapacity}/{candidate.Capacity}).",
                null,
                null));
        }

        // H4: Derslik tipi uyumu — Lab dersi Lab/Computer derslikte olmali
        if (candidate.ClassroomId.HasValue
            && candidate.LabHours > 0 && candidate.TheoryHours == 0
            && candidate.ClassroomType != ClassroomType.Lab && candidate.ClassroomType != ClassroomType.Computer)
        {
            conflicts.Add(BuildConflict(
                candidate,
                ConflictTypes.ClassroomTypeConflict,
                $"Lab section'u ({candidate.CourseCode}) Lab veya Computer tipinde bir dersliğe atanmalıdır, mevcut tip: {candidate.ClassroomType}.",
                null,
                null));
        }

        // H4+: Büyük dersler (kontenjan >= AmphiThreshold) → Amphitheater zorunlu
        if (candidate.ClassroomId.HasValue
            && hardConfig != null
            && candidate.Capacity >= hardConfig.AmphiThreshold
            && candidate.ClassroomType != ClassroomType.Amphitheater)
        {
            conflicts.Add(BuildConflict(
                candidate,
                ConflictTypes.ClassroomTypeConflict,
                $"Kontenjanı {candidate.Capacity} olan ders için Amfi derslik gereklidir.",
                null,
                null));
        }

        // H10: Fakülteye atanmamış derslik kullanımı
        if (candidate.ClassroomId.HasValue
            && candidate.DepartmentId.HasValue
            && snapshot.ClassroomAssignments.Count > 0)
        {
            var candidateFacultyId = snapshot.DeptFacultyMap.GetValueOrDefault(candidate.DepartmentId.Value);
            if (candidateFacultyId > 0)
            {
                var hasAssignment = snapshot.ClassroomAssignments
                    .Any(cta => cta.ClassroomId == candidate.ClassroomId.Value && cta.FacultyId == candidateFacultyId);
                if (!hasAssignment)
                {
                    conflicts.Add(BuildConflict(
                        candidate,
                        ConflictTypes.FacultyClassroomConflict,
                        $"Derslik '{candidate.ClassroomName}' bu dönemde ilgili fakülteye atanmamış.",
                        null,
                        null));
                }
            }
        }

        foreach (var existing in snapshot.Sections)
        {
            foreach (var overlap in GetOverlapSlots(candidate.TimeBlocks, existing.TimeBlocks))
            {
                if (candidate.ClassroomId.HasValue && candidate.ClassroomId == existing.ClassroomId)
                {
                    conflicts.Add(BuildConflict(
                        candidate,
                        ConflictTypes.ClassroomConflict,
                        $"Derslik '{candidate.ClassroomName}' aynı zaman aralığında '{existing.CourseCode}' section'u ile çakışıyor.",
                        overlap,
                        existing.SectionId));
                }

                if (candidate.InstructorId.HasValue && candidate.InstructorId == existing.InstructorId)
                {
                    conflicts.Add(BuildConflict(
                        candidate,
                        ConflictTypes.InstructorConflict,
                        $"Öğretim üyesi '{candidate.InstructorName}' aynı zaman aralığında '{existing.CourseCode}' section'u ile çakışıyor.",
                        overlap,
                        existing.SectionId));
                }

                // H5: Aynı bölüm/yıl öğrencilerinin alabileceği dersler aynı slot'ta olamaz
                if (candidate.CourseId != existing.CourseId
                    && TryGetSharedCurriculumDepartment(candidate, existing, out _))
                {
                    conflicts.Add(BuildConflict(
                        candidate,
                        ConflictTypes.CurriculumConflict,
                        $"Aynı bölüm/yıl öğrencilerinin alabileceği '{candidate.CourseCode}' ile '{existing.CourseCode}' dersleri aynı zaman aralığında çakışıyor.",
                        overlap,
                        existing.SectionId));
                }

                // H6: Aynı dersin bileşenleri (lecture vs lab) birbiriyle çakışmamalı
                if (candidate.CourseId == existing.CourseId)
                {
                    var candidateIsLab = candidate.LabHours > 0 && candidate.TheoryHours == 0;
                    var existingIsLab = existing.LabHours > 0 && existing.TheoryHours == 0;
                    var candidateIsLecture = candidate.TheoryHours > 0;
                    var existingIsLecture = existing.TheoryHours > 0;

                    if ((candidateIsLab && existingIsLecture) || (candidateIsLecture && existingIsLab))
                    {
                        conflicts.Add(BuildConflict(
                            candidate,
                            ConflictTypes.SameCourseComponentConflict,
                            $"Aynı dersin ({candidate.CourseCode}) lecture ve lab bileşenleri aynı zaman aralığında çakışıyor.",
                            overlap,
                            existing.SectionId));
                    }
                }

                // H8: Aynı dersin farklı şubeleri aynı slot'ta olamaz
                if (candidate.CourseId == existing.CourseId && candidate.SectionId != existing.SectionId)
                {
                    conflicts.Add(BuildConflict(
                        candidate,
                        ConflictTypes.SameCourseSectionConflict,
                        $"Aynı dersin ({candidate.CourseCode}) farklı şubeleri aynı zaman aralığında çakışıyor.",
                        overlap,
                        existing.SectionId));
                }
            }
        }

        if (candidate.InstructorId.HasValue && candidate.InstructorMaxWeeklyHours > 0)
        {
            var totalWeeklyHours = snapshot.Sections
                .Where(section => section.InstructorId == candidate.InstructorId)
                .Sum(section => ResolveWeeklyHours(section.TimeBlocks, section))
                + ResolveWeeklyHours(candidate.TimeBlocks, candidate);

            if (totalWeeklyHours > candidate.InstructorMaxWeeklyHours)
            {
                conflicts.Add(BuildConflict(
                    candidate,
                    ConflictTypes.WeeklyLoadConflict,
                    $"Instructor haftalik ders yuku limiti asiliyor ({totalWeeklyHours:0.##}/{candidate.InstructorMaxWeeklyHours} saat).",
                    null,
                    null));
            }
        }

        if (candidate.InstructorId.HasValue && candidate.TimeBlocks.Count > 0)
        {
            var instructorSections = snapshot.Sections
                .Where(section => section.InstructorId == candidate.InstructorId && section.TimeBlocks.Count > 0)
                .ToList();

            var dailyGroups = instructorSections
                .SelectMany(section => section.TimeBlocks.Select(slot => new
                {
                    slot.Day,
                    slot.StartTime,
                    slot.EndTime
                }))
                .Concat(candidate.TimeBlocks.Select(slot => new
                {
                    slot.Day,
                    slot.StartTime,
                    slot.EndTime
                }))
                .GroupBy(slot => slot.Day);

            foreach (var dayGroup in dailyGroups)
            {
                var dailyHours = dayGroup.Sum(slot => (slot.EndTime - slot.StartTime).TotalHours);
                if (dailyHours <= 9)
                {
                    continue;
                }

                var dayName = GetDayOfWeekName(dayGroup.Key);
                conflicts.Add(BuildConflict(
                    candidate,
                    ConflictTypes.DailyLoadConflict,
                    $"Öğretim üyesi '{candidate.InstructorName}' {dayName} günü {dailyHours:0.#} saat ders veriyor (maks 9 saat).",
                    null,
                    null));
            }

            var usedDays = instructorSections
                .SelectMany(section => section.TimeBlocks)
                .Select(slot => slot.Day)
                .Concat(candidate.TimeBlocks.Select(slot => slot.Day))
                .Distinct()
                .Count();

            if (usedDays >= 5)
            {
                conflicts.Add(BuildConflict(
                    candidate,
                    ConflictTypes.NoDayOffConflict,
                    $"Öğretim üyesi '{candidate.InstructorName}' haftanın 5 gününde de ders veriyor, en az 1 gün boş olmalıdır.",
                    null,
                    null));
            }
        }

        // ── H11: Corequisite Compatibility (candidate) ──────────────────
        foreach (var coreq in snapshot.Corequisites)
        {
            // Check if the candidate's course is part of a corequisite pair
            int? pairedCourseId = null;
            if (coreq.CourseId == candidate.CourseId)
                pairedCourseId = coreq.RequiredCourseId;
            else if (coreq.RequiredCourseId == candidate.CourseId)
                pairedCourseId = coreq.CourseId;

            if (!pairedCourseId.HasValue)
                continue;

            var pairedSections = snapshot.Sections
                .Where(s => s.CourseId == pairedCourseId.Value && s.TimeBlocks.Count > 0)
                .ToList();

            if (pairedSections.Count == 0)
                continue;

            // Get all sections of candidate's course (existing + the candidate itself)
            var candidateCourseSections = snapshot.Sections
                .Where(s => s.CourseId == candidate.CourseId && s.TimeBlocks.Count > 0)
                .ToList();

            // Add the candidate to the list (replacing any excluded section)
            var allCandidateCourseSections = candidateCourseSections
                .Where(s => s.SectionId != candidate.SectionId)
                .Append(candidate)
                .Where(s => s.TimeBlocks.Count > 0)
                .ToList();

            var hasNonConflicting = allCandidateCourseSections.Any(a =>
                pairedSections.Any(b =>
                    GetOverlapSlots(a.TimeBlocks, b.TimeBlocks).Count == 0));

            if (!hasNonConflicting)
            {
                var pairedCode = pairedSections[0].CourseCode;
                conflicts.Add(BuildConflict(
                    candidate,
                    ConflictTypes.CorequisiteConflict,
                    $"Corequisite dersler ({candidate.CourseCode} ve {pairedCode}) arasında çakışmasız bir şube kombinasyonu bulunmuyor.",
                    null,
                    null));
            }
        }

        // ── H12: Credit-Hour Block Compatibility (candidate) ────────────
        if (candidate.TimeBlocks.Count > 0)
        {
            var expectedHours = candidate.TheoryHours + candidate.LabHours;
            if (expectedHours > 0)
            {
                var actualMinutes = candidate.TimeBlocks.Sum(slot => (slot.EndTime - slot.StartTime).TotalMinutes);
                var actualHours = (int)Math.Round(actualMinutes / 60.0);

                if (actualHours != expectedHours)
                {
                    conflicts.Add(BuildConflict(
                        candidate,
                        ConflictTypes.CreditHourMismatch,
                        $"Dersin saat gereksinimine uygun slot bloğu atanmamış ({candidate.CourseCode}: beklenen {expectedHours} saat, atanan {actualHours} saat).",
                        null,
                        null));
                }
            }
        }

        // ── H13: Lunch Break Protection (candidate) ─────────────────────
        if (hardConfig != null && hardConfig.LunchBreakIsHard)
        {
            foreach (var slot in candidate.TimeBlocks)
            {
                if (slot.StartTime < hardConfig.LunchBreakEnd && slot.EndTime > hardConfig.LunchBreakStart)
                {
                    conflicts.Add(BuildConflict(
                        candidate,
                        ConflictTypes.LunchBreakConflict,
                        "Öğle arası saatlerinde ders atanamaz.",
                        slot,
                        null));
                }
            }
        }

        // ── H15: Max Consecutive Hours (candidate) ──────────────────────
        if (hardConfig != null && candidate.InstructorId.HasValue && candidate.TimeBlocks.Count > 0)
        {
            var existingSlots = snapshot.Sections
                .Where(s => s.InstructorId == candidate.InstructorId)
                .SelectMany(s => s.TimeBlocks.Select(slot => new { Slot = slot }))
                .ToList();

            var candidateSlots = candidate.TimeBlocks
                .Select(slot => new { Slot = slot })
                .ToList();

            var allSlots = existingSlots.Concat(candidateSlots);

            foreach (var dayGroup in allSlots.GroupBy(x => x.Slot.Day))
            {
                var sortedSlots = dayGroup.OrderBy(x => x.Slot.StartTime).ToList();
                if (sortedSlots.Count <= 1)
                    continue;

                var blockStartIdx = 0;
                var blockMinutes = (sortedSlots[0].Slot.EndTime - sortedSlots[0].Slot.StartTime).TotalMinutes;

                for (int idx = 1; idx < sortedSlots.Count; idx++)
                {
                    var gap = (sortedSlots[idx].Slot.StartTime - sortedSlots[idx - 1].Slot.EndTime).TotalMinutes;
                    if (gap <= hardConfig.BreakDurationMinutes)
                    {
                        blockMinutes += (sortedSlots[idx].Slot.EndTime - sortedSlots[idx].Slot.StartTime).TotalMinutes;
                    }
                    else
                    {
                        blockStartIdx = idx;
                        blockMinutes = (sortedSlots[idx].Slot.EndTime - sortedSlots[idx].Slot.StartTime).TotalMinutes;
                    }
                }

                var blockHours = blockMinutes / 60.0;
                if (blockHours > hardConfig.MaxConsecutiveHours)
                {
                    var dayName = GetDayOfWeekName(dayGroup.Key);
                    conflicts.Add(BuildConflict(
                        candidate,
                        ConflictTypes.MaxConsecutiveHoursConflict,
                        $"Öğretim üyesi '{candidate.InstructorName}' {dayName} günü {hardConfig.MaxConsecutiveHours} saatten fazla aralıksız ders veriyor ({blockHours:0.#} saat).",
                        null,
                        null));
                }
            }
        }

        // ── Candidate Soft Constraints ────────────────────────────────────
        var softNotes = new List<string>();

        // S1: Derslik doluluk oranı (candidate)
        if (softConfig.S1Enabled && candidate.ClassroomId.HasValue && candidate.ClassroomCapacity > 0)
        {
            var occupancyRate = candidate.Capacity * 100 / candidate.ClassroomCapacity;
            if (occupancyRate < softConfig.S1MinOccupancy)
            {
                softNotes.Add($"S1 Uyarı: {candidate.CourseCode} ({candidate.ClassroomName}) doluluk oranı düşük (%{occupancyRate}, hedef >=%{softConfig.S1MinOccupancy})");
            }
            else if (occupancyRate > softConfig.S1MaxOccupancy)
            {
                softNotes.Add($"S1 Uyarı: {candidate.CourseCode} ({candidate.ClassroomName}) doluluk oranı yüksek (%{occupancyRate}, hedef <=%{softConfig.S1MaxOccupancy})");
            }
        }

        // S2: Hoca günlük yoğunluk dengesi (candidate + existing)
        if (softConfig.S2Enabled && candidate.InstructorId.HasValue && candidate.TimeBlocks.Count > 0)
        {
            var existingSlots = snapshot.Sections
                .Where(s => s.InstructorId == candidate.InstructorId)
                .SelectMany(s => s.TimeBlocks)
                .ToList();

            var allSlots = existingSlots.Concat(candidate.TimeBlocks);

            foreach (var dayGroup in allSlots.GroupBy(slot => slot.Day))
            {
                var dailyHours = dayGroup.Sum(slot => (decimal)(slot.EndTime - slot.StartTime).TotalHours);
                if (dailyHours > softConfig.S2MaxDailyHours)
                {
                    var dayName = GetDayOfWeekName(dayGroup.Key);
                    softNotes.Add($"S2 Uyarı: {candidate.InstructorName} hocanın {dayName} günündeki ders yükü fazla ({dailyHours:0.#} saat, max {softConfig.S2MaxDailyHours})");
                }
            }
        }

        // S3: Slot dağılım dengesi (candidate + existing)
        if (softConfig.S3Enabled && candidate.TimeBlocks.Count > 0)
        {
            var existingSlotKeys = snapshot.Sections
                .SelectMany(s => s.TimeBlocks.Select(slot => (slot.Day, slot.StartTime)))
                .ToList();

            var candidateSlotKeys = candidate.TimeBlocks
                .Select(slot => (slot.Day, slot.StartTime))
                .ToList();

            var allSlotKeys = existingSlotKeys.Concat(candidateSlotKeys);

            foreach (var group in allSlotKeys.GroupBy(x => x))
            {
                var count = group.Count();
                if (count > softConfig.S3MaxSectionsPerSlot
                    && candidateSlotKeys.Contains(group.Key))
                {
                    var dayName = GetDayOfWeekName(group.Key.Day);
                    softNotes.Add($"S3 Uyarı: {dayName} {group.Key.StartTime:HH\\:mm} slotunda {count} section yığılması var (max {softConfig.S3MaxSectionsPerSlot})");
                }
            }
        }

        // S4: Cross-Department Co-Enrollment Conflict (candidate) — uses snapshot.CoEnrollmentPairs if preloaded
        if (softConfig.S4Enabled && candidate.TimeBlocks.Count > 0 && snapshot.CoEnrollmentPairs.Count > 0)
        {
            var relevantPairs = snapshot.CoEnrollmentPairs
                .Where(p => p.CourseId1 == candidate.CourseId || p.CourseId2 == candidate.CourseId)
                .ToList();

            foreach (var pair in relevantPairs)
            {
                var pairedCourseId = pair.CourseId1 == candidate.CourseId ? pair.CourseId2 : pair.CourseId1;
                var pairedSections = snapshot.Sections
                    .Where(s => s.CourseId == pairedCourseId && s.TimeBlocks.Count > 0)
                    .ToList();

                foreach (var pairedSec in pairedSections)
                {
                    var overlaps = GetOverlapSlots(candidate.TimeBlocks, pairedSec.TimeBlocks);
                    if (overlaps.Count > 0)
                    {
                        softNotes.Add($"S4 Uyarı: '{candidate.CourseCode}' ve '{pairedSec.CourseCode}' dersleri sık birlikte alınıyor ({pair.StudentCount} öğrenci) ancak zaman çakışması var.");
                    }
                }
            }
        }

        // S5: Lunch Break Preference (candidate — soft mode, only when H13 is NOT hard)
        if (softConfig.S5Enabled && candidate.TimeBlocks.Count > 0
            && (hardConfig == null || !hardConfig.LunchBreakIsHard))
        {
            var lunchStart = hardConfig?.LunchBreakStart ?? new TimeOnly(11, 40);
            var lunchEnd = hardConfig?.LunchBreakEnd ?? new TimeOnly(12, 20);

            foreach (var slot in candidate.TimeBlocks)
            {
                if (slot.StartTime < lunchEnd && slot.EndTime > lunchStart)
                {
                    softNotes.Add($"S5 Uyarı: {candidate.CourseCode} öğle arası saatlerinde programlanmış ({slot.StartTime:HH\\:mm}-{slot.EndTime:HH\\:mm}).");
                }
            }
        }

        // S7: Student Gap Minimization (candidate)
        if (softConfig.S7Enabled && candidate.TimeBlocks.Count > 0
            && candidate.DepartmentId.HasValue && candidate.SuggestedSemester.HasValue)
        {
            var candidateYear = (candidate.SuggestedSemester.Value - 1) / 2;
            var sameDeptYearSections = snapshot.Sections
                .Where(s => s.DepartmentId == candidate.DepartmentId
                    && s.SuggestedSemester.HasValue
                    && (s.SuggestedSemester.Value - 1) / 2 == candidateYear
                    && s.TimeBlocks.Count > 0)
                .ToList();

            var combinedSlots = sameDeptYearSections
                .SelectMany(s => s.TimeBlocks.Select(slot => new { s.CourseCode, Slot = slot }))
                .Concat(candidate.TimeBlocks.Select(slot => new { candidate.CourseCode, Slot = slot }))
                .GroupBy(x => x.Slot.Day);

            foreach (var dayGroup in combinedSlots)
            {
                var sorted = dayGroup.OrderBy(x => x.Slot.StartTime).ToList();
                if (sorted.Count < 2) continue;

                for (int idx = 1; idx < sorted.Count; idx++)
                {
                    var gapHours = (sorted[idx].Slot.StartTime - sorted[idx - 1].Slot.EndTime).TotalHours;
                    if (gapHours >= softConfig.S7MaxGapSlots)
                    {
                        var dayName = GetDayOfWeekName(dayGroup.Key);
                        softNotes.Add($"S7 Uyarı: {dayName} günü '{sorted[idx - 1].CourseCode}' ile '{sorted[idx].CourseCode}' arasında {gapHours:0.#} saatlik boşluk var.");
                    }
                }
            }
        }

        // S8: Instructor Day Minimization (candidate)
        if (softConfig.S8Enabled && candidate.InstructorId.HasValue && candidate.TimeBlocks.Count > 0)
        {
            var existingSlots = snapshot.Sections
                .Where(s => s.InstructorId == candidate.InstructorId)
                .SelectMany(s => s.TimeBlocks)
                .ToList();

            var allSlots = existingSlots.Concat(candidate.TimeBlocks).ToList();
            var usedDays = allSlots.Select(s => s.Day).Distinct().Count();
            var totalHours = allSlots.Sum(s => (s.EndTime - s.StartTime).TotalHours);
            var maxHoursPerDay = (double)softConfig.S2MaxDailyHours;
            var minPossibleDays = Math.Max(1, (int)Math.Ceiling(totalHours / maxHoursPerDay));

            if (usedDays > minPossibleDays + 1)
            {
                softNotes.Add($"S8 Uyarı: {candidate.InstructorName} hocanın dersleri {usedDays} güne dağılmış (minimum {minPossibleDays} gün yeterli olabilir).");
            }
        }

        // S9: Early Morning / Late Evening Penalty (candidate)
        if (softConfig.S9Enabled && candidate.TimeBlocks.Count > 0)
        {
            foreach (var slot in candidate.TimeBlocks)
            {
                if (slot.StartTime <= softConfig.S9MorningStart)
                {
                    softNotes.Add($"S9 Uyarı: {candidate.CourseCode} erken sabah slotuna ({slot.StartTime:HH\\:mm}) programlanmış.");
                }
                else if (slot.StartTime >= softConfig.S9EveningStart)
                {
                    softNotes.Add($"S9 Uyarı: {candidate.CourseCode} geç akşam slotuna ({slot.StartTime:HH\\:mm}) programlanmış.");
                }
            }
        }

        // S12: Weekday Balance (candidate)
        if (softConfig.S12Enabled && candidate.TimeBlocks.Count > 0)
        {
            var existingSlots = snapshot.Sections.SelectMany(s => s.TimeBlocks).ToList();
            var allSlots = existingSlots.Concat(candidate.TimeBlocks);

            var slotsPerDay = allSlots
                .GroupBy(s => s.Day)
                .Select(g => g.Count())
                .ToList();

            if (slotsPerDay.Count > 1)
            {
                var avg = slotsPerDay.Average();
                var maxCount = slotsPerDay.Max();
                var minCount = slotsPerDay.Min();
                var diff = maxCount - minCount;

                if (avg > 0 && diff > avg * softConfig.S12MaxImbalancePercent / 100.0)
                {
                    softNotes.Add($"S12 Uyarı: Hafta içi slot dağılımı dengesiz (min: {minCount}, max: {maxCount}, fark: {diff}).");
                }
            }
        }

        var orderedConflicts = conflicts
            .OrderBy(conflict => conflict.ConflictType)
            .ThenBy(conflict => conflict.Day)
            .ThenBy(conflict => conflict.StartTime)
            .ThenBy(conflict => conflict.RelatedSectionId)
            .ToList();

        return (orderedConflicts, softNotes);
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

    private static List<TimeBlock> GetOverlapSlots(IReadOnlyList<TimeBlock> firstSchedule, IReadOnlyList<TimeBlock> secondSchedule)
    {
        var overlaps = new List<TimeBlock>();

        foreach (var first in firstSchedule)
        {
            foreach (var second in secondSchedule)
            {
                if (!TryGetOverlap(first, second, out var overlap))
                {
                    continue;
                }

                overlaps.Add(overlap);
            }
        }

        return overlaps
            .GroupBy(slot => BuildSlotSignature(new[] { slot }))
            .Select(group => group.First())
            .OrderBy(slot => slot.Day)
            .ThenBy(slot => slot.StartTime)
            .ToList();
    }

    private static bool TryGetOverlap(TimeBlock first, TimeBlock second, out TimeBlock overlap)
    {
        overlap = default!;

        if (first.Day != second.Day)
        {
            return false;
        }

        var start = first.StartTime > second.StartTime ? first.StartTime : second.StartTime;
        var end = first.EndTime < second.EndTime ? first.EndTime : second.EndTime;

        if (start >= end)
        {
            return false;
        }

        overlap = new TimeBlock
        {
            Day = first.Day,
            StartTime = start,
            EndTime = end
        };

        return true;
    }

    private static bool SlotsOverlap(TimeBlock first, TimeBlock second) =>
        first.Day == second.Day
        && first.StartTime < second.EndTime
        && second.StartTime < first.EndTime;

    private static decimal ResolveWeeklyHours(IReadOnlyList<TimeBlock> slots, Course course)
    {
        if (slots.Count > 0)
        {
            var minutes = slots.Sum(slot => (decimal)(slot.EndTime - slot.StartTime).TotalMinutes);
            return minutes / 60m;
        }

        var catalogHours = course.TheoryHours + course.LabHours;
        return catalogHours > 0 ? catalogHours : 0m;
    }

    private static decimal ResolveWeeklyHours(IReadOnlyList<TimeBlock> slots, SectionSnapshot section)
    {
        if (slots.Count > 0)
        {
            var minutes = slots.Sum(slot => (decimal)(slot.EndTime - slot.StartTime).TotalMinutes);
            return minutes / 60m;
        }

        var catalogHours = section.TheoryHours + section.LabHours;
        return catalogHours > 0 ? catalogHours : 0m;
    }

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
        int? relatedSectionId)
    {
        return new SchedulingConflictDto
        {
            ConflictType = conflictType,
            SectionId = section.SectionId,
            RelatedSectionId = relatedSectionId,
            CourseCode = section.CourseCode,
            CourseName = section.CourseName,
            InstructorId = section.InstructorId,
            InstructorName = section.InstructorName,
            ClassroomId = section.ClassroomId,
            ClassroomName = section.ClassroomName,
            Day = slot?.Day,
            StartTime = slot?.StartTime,
            EndTime = slot?.EndTime,
            Message = message
        };
    }

    private static bool TryGetSharedCurriculumDepartment(
        SectionSnapshot first,
        SectionSnapshot second,
        out int sharedDepartmentId)
    {
        foreach (var departmentId in first.AccessibleDepartmentIds)
        {
            if (!second.AccessibleDepartmentIds.Contains(departmentId))
                continue;

            if (!first.SuggestedSemestersByDepartment.TryGetValue(departmentId, out var firstSemester))
                continue;

            if (!second.SuggestedSemestersByDepartment.TryGetValue(departmentId, out var secondSemester))
                continue;

            if ((firstSemester - 1) / 2 != (secondSemester - 1) / 2)
                continue;

            sharedDepartmentId = departmentId;
            return true;
        }

        if (first.DepartmentId.HasValue && second.DepartmentId.HasValue
            && first.DepartmentId == second.DepartmentId
            && first.SuggestedSemester.HasValue && second.SuggestedSemester.HasValue
            && (first.SuggestedSemester.Value - 1) / 2 == (second.SuggestedSemester.Value - 1) / 2)
        {
            sharedDepartmentId = first.DepartmentId.Value;
            return true;
        }

        sharedDepartmentId = 0;
        return false;
    }

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
            .Where(cc => ids.Contains(cc.CourseId) && cc.Curriculum.IsActive)
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

    private static string GetDayName(int day)
    {
        return day switch
        {
            0 => "Pazartesi",
            1 => "Salı",
            2 => "Çarşamba",
            3 => "Perşembe",
            4 => "Cuma",
            _ => "Bilinmeyen"
        };
    }

    // ── Soft constraint config helpers ────────────────────────────────────

    private async Task<Dictionary<string, string>> LoadSystemConfigAsync(CancellationToken ct)
    {
        return await _context.SystemConfigs.AsNoTracking()
            .ToDictionaryAsync(c => c.Key, c => c.Value, ct);
    }

    private async Task<SoftConstraintConfig> LoadSoftConstraintConfigAsync(CancellationToken ct)
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
    private async Task<List<CoEnrollmentPair>> LoadCoEnrollmentPairsAsync(int currentTermId, int minThreshold, CancellationToken ct)
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

    private async Task<HardConstraintConfig> LoadHardConstraintConfigAsync(CancellationToken ct)
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

        return new HardConstraintConfig(lunchIsHard, lunchStart, lunchEnd, maxConsecutive, breakDuration, amphiThreshold);
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

    private static string GetDayOfWeekName(DayOfWeek day)
    {
        return day switch
        {
            DayOfWeek.Monday => "Pazartesi",
            DayOfWeek.Tuesday => "Salı",
            DayOfWeek.Wednesday => "Çarşamba",
            DayOfWeek.Thursday => "Perşembe",
            DayOfWeek.Friday => "Cuma",
            DayOfWeek.Saturday => "Cumartesi",
            DayOfWeek.Sunday => "Pazar",
            _ => day.ToString()
        };
    }

    // ── Constants & inner types ──────────────────────────────────────────

    private record SoftConstraintConfig(
        bool S1Enabled, int S1MinOccupancy, int S1MaxOccupancy,
        bool S2Enabled, int S2MaxDailyHours,
        bool S3Enabled, int S3MaxSectionsPerSlot,
        bool S4Enabled, int S4MinCoPairThreshold,
        bool S5Enabled,
        bool S7Enabled, int S7MaxGapSlots,
        bool S8Enabled,
        bool S9Enabled, TimeOnly S9MorningStart, TimeOnly S9EveningStart,
        bool S12Enabled, int S12MaxImbalancePercent);

    private record HardConstraintConfig(
        bool LunchBreakIsHard, TimeOnly LunchBreakStart, TimeOnly LunchBreakEnd,
        int MaxConsecutiveHours, int BreakDurationMinutes, int AmphiThreshold);


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

    private sealed class TermSnapshot
    {
        public int TermId { get; init; }
        public string TermName { get; init; } = string.Empty;
        public string ScopeName { get; init; } = string.Empty;
        public List<SectionSnapshot> Sections { get; init; } = new();
        public List<ClassroomTermAssignment> ClassroomAssignments { get; init; } = new(); // for H10
        public Dictionary<int, int> DeptFacultyMap { get; init; } = new();                // for H10
        public List<CoursePrerequisite> Corequisites { get; init; } = new();              // for H11
        public List<CoEnrollmentPair> CoEnrollmentPairs { get; set; } = new();            // for S4
        public Dictionary<int, Dictionary<int, int>> CourseSuggestedSemestersByDepartment { get; init; } = new();
        public Dictionary<int, HashSet<int>> CourseAccessibleDepartments { get; init; } = new();
    }

    private sealed class CourseSchedulingMetadata
    {
        public Dictionary<int, int> MinSuggestedSemesters { get; init; } = new();
        public Dictionary<int, Dictionary<int, int>> SuggestedSemestersByDepartment { get; init; } = new();
        public Dictionary<int, HashSet<int>> AccessibleDepartments { get; init; } = new();
    }

    private sealed class CoEnrollmentPair
    {
        public int CourseId1 { get; init; }
        public int CourseId2 { get; init; }
        public int StudentCount { get; init; }
    }

    private sealed class SectionSnapshot
    {
        public int SectionId { get; init; }
        public int CourseId { get; init; }
        public string CourseCode { get; init; } = string.Empty;
        public string CourseName { get; init; } = string.Empty;
        public int TermId { get; init; }
        public int TheoryHours { get; init; }
        public int LabHours { get; init; }
        public int Capacity { get; init; }
        public int EnrolledCount { get; init; }
        public int? ClassroomId { get; init; }
        public string ClassroomName { get; init; } = string.Empty;
        public int ClassroomCapacity { get; init; }
        public ClassroomType ClassroomType { get; init; }         // for H4
        public int? InstructorId { get; init; }
        public string InstructorName { get; init; } = string.Empty;
        public int InstructorMaxWeeklyHours { get; init; }
        public int? DepartmentId { get; init; }                    // for H5, H8
        public int? SuggestedSemester { get; init; }               // for H5 (curriculum year)
        public Dictionary<int, int> SuggestedSemestersByDepartment { get; init; } = new();
        public HashSet<int> AccessibleDepartmentIds { get; init; } = new();
        public List<TimeBlock> TimeBlocks { get; init; } = new();
        public List<string> Notes { get; init; } = new();
    }

    private sealed class ScheduleParseResult
    {
        public List<TimeBlock> Slots { get; init; } = new();
        public string? ErrorMessage { get; init; }
    }

    private sealed class RankedSlot
    {
        public TimeBlock Slot { get; init; } = null!;
        public int PreferencePenalty { get; init; }
    }

    private sealed class RankedSlotCombination
    {
        public List<TimeBlock> Slots { get; init; } = new();
        public int PreferenceScore { get; init; }
    }

}
