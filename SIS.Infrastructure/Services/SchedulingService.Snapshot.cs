using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Rules;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling;

namespace SIS.Infrastructure.Services;

/// <summary>
/// SchedulingService'in <see cref="RuntimeSnapshot"/> / <see cref="RuntimeCandidate"/>
/// inşa eden partial bölümü. Auto-schedule motoru ile ApplyScheduleAsync (manuel atama)
/// AYNI snapshot yapısını kullanır; bu dosya ortak inşa kodunu izole eder.
///
/// Method'lar:
///   - BuildRuntimeSnapshotAsync          (term-scope read-only snapshot, ConflictPairsBuilder integrated)
///   - BuildExistingRuntimeCandidate      (DB'deki section'dan RuntimeCandidate)
///   - BuildCandidateRuntimeAsync         (validate için aday section, notes ayrı tuple)
///   - BuildCandidateRuntime              (saf inşa, IO yok)
/// </summary>
public partial class SchedulingService
{
    internal async Task<RuntimeSnapshot> BuildRuntimeSnapshotAsync(
        int termId,
        int? excludedSectionId,
        int? departmentId,
        CancellationToken cancellationToken)
    {
        var sectionsQuery = _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .Include(cs => cs.SectionSchedules).ThenInclude(ss => ss.TimeSlot)
            .Where(cs => cs.TermId == termId && cs.Status == CourseSectionStatus.Active)
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

        // H10 — ClassroomTermAssignment + dept→faculty map.
        var classroomAssignmentEntities = await _context.ClassroomTermAssignments
            .AsNoTracking()
            .Where(cta => cta.TermId == termId)
            .ToListAsync(cancellationToken);

        var classroomAssignments = classroomAssignmentEntities
            .Select(cta => new ClassroomTermAssignmentInfo(cta.ClassroomId, cta.FacultyId))
            .ToList();

        var deptFacultyMap = await _context.Departments
            .AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.FacultyId, cancellationToken);

        // H11 — Corequisite course çiftleri.
        var corequisites = await _context.CoursePrerequisites
            .AsNoTracking()
            .Where(cp => cp.IsCorequisite)
            .Select(cp => new CorequisitePair(cp.CourseId, cp.RequiredCourseId))
            .ToListAsync(cancellationToken);

        // S4 — Co-enrollment pairs (config-aware).
        var softCfg = await LoadSoftConstraintConfigAsync(cancellationToken);
        var coEnrollmentRawPairs = softCfg.S4Enabled
            ? await LoadCoEnrollmentPairsAsync(termId, softCfg.S4MinCoPairThreshold, cancellationToken)
            : new List<CoEnrollmentPair>();

        var coEnrollmentRuntimePairs = coEnrollmentRawPairs
            .Select(p => new CoEnrollmentRuntimePair(p.CourseId1, p.CourseId2, p.StudentCount))
            .ToList();

        // DIST — Distribution constraints.
        var distributionEntities = await _context.DistributionConstraints
            .AsNoTracking()
            .Include(d => d.Courses)
            .Where(d => d.TermId == termId)
            .ToListAsync(cancellationToken);

        var distributionConstraints = distributionEntities
            .Select(dc => new RuntimeDistributionConstraint(
                DistributionType: dc.DistributionType,
                PreferenceLevel: dc.PreferenceLevel,
                Courses: dc.Courses
                    .OrderBy(c => c.SortOrder)
                    .Select(c => new DistributionConstraintCourseInfo(c.CourseId, c.CourseSectionId, c.SortOrder))
                    .ToList()))
            .ToList();

        // H17 — FaceToFaceSlotRestriction için (Day, StartTime) → AllowedMode lookup.
        // Duplicate (Day, StartTime) için OnlineOnly daha kısıtlayıcıdır → onu tercih et.
        var slotRecords = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == termId)
            .Select(ts => new { ts.DayOfWeek, ts.StartTime, ts.AllowedMode })
            .ToListAsync(cancellationToken);

        var slotAllowedModes = new Dictionary<(int Day, TimeOnly StartTime), TimeSlotAllowedMode>();
        foreach (var slot in slotRecords)
        {
            var key = ((int)slot.DayOfWeek, slot.StartTime);
            if (!slotAllowedModes.TryGetValue(key, out var existing)
                || slot.AllowedMode == TimeSlotAllowedMode.OnlineOnly)
            {
                slotAllowedModes[key] = slot.AllowedMode;
            }
        }

        // RuntimeCandidate'lara çevir (paralel-split capacity, primary classroom resolution).
        var classroomLookup = sections
            .Where(s => s.Classroom != null)
            .GroupBy(s => s.Classroom!.Id)
            .ToDictionary(g => g.Key, g => g.First().Classroom!);

        var candidates = sections
            .Select(section => BuildExistingRuntimeCandidate(
                section,
                instructorNameMap,
                courseMetadata.MinSuggestedSemesters,
                courseMetadata.SuggestedSemestersByDepartment,
                courseMetadata.AccessibleDepartments,
                classroomLookup))
            .ToList();

        // Manuel atama (Apply) yolu da AutoScheduleEngine ile AYNI mode'u kullanır.
        var hardCfg = await LoadHardConstraintConfigAsync(cancellationToken);
        var rawConfig = await LoadSystemConfigAsync(cancellationToken);
        var fullCfg = SchedulingConstraintConfig.FromSystemConfig(rawConfig);
        var curriculumConflictMode = hardCfg?.CurriculumConflictMode ?? "curriculum";

        var cohortRefs = candidates
            .Select(c => new CohortSectionRef(c.SectionId, c.SectionNumber, c.CourseId))
            .ToList();

        var conflictData = await ConflictPairsBuilder.BuildAsync(
            curriculumConflictMode,
            cohortRefs,
            courseMetadata.SuggestedSemestersByDepartment,
            courseMetadata.AccessibleDepartments,
            fullCfg.EnrollmentConflictHardThreshold,
            _context, termId, cancellationToken);

        return new RuntimeSnapshot
        {
            TermId = termId,
            TermName = termName,
            ScopeName = scopeName,
            Sections = candidates,
            ClassroomAssignments = classroomAssignments,
            DeptFacultyMap = deptFacultyMap,
            Corequisites = corequisites,
            SlotAllowedModes = slotAllowedModes,
            CohortConflictPairs = conflictData.CohortConflictPairs ?? new HashSet<(int, int)>(),
            CurriculumConflictMode = curriculumConflictMode,
            EnrollmentConflicts = conflictData.EnrollmentConflicts,
            CoEnrollmentPairs = coEnrollmentRuntimePairs,
            AdjacentYearSoftPairs = conflictData.AdjacentYearSoftPairs ?? new HashSet<(int, int)>(),
            DistributionConstraints = distributionConstraints,
        };
    }

    private static RuntimeCandidate BuildExistingRuntimeCandidate(
        CourseSection section,
        IReadOnlyDictionary<int, string> instructorNameMap,
        IReadOnlyDictionary<int, int> semesterMap,
        IReadOnlyDictionary<int, Dictionary<int, int>> suggestedSemestersByDepartment,
        IReadOnlyDictionary<int, HashSet<int>> accessibleDepartments,
        IReadOnlyDictionary<int, Classroom>? classroomLookup)
    {
        var timeBlocks = ScheduleHelper.ToTimeBlocks(section.SectionSchedules);
        var instructorName = section.InstructorId.HasValue
            && instructorNameMap.TryGetValue(section.InstructorId.Value, out var n) ? n : string.Empty;
        var suggestedSemester = semesterMap.TryGetValue(section.CourseId, out var sem) ? sem : (int?)null;

        return ProjectRuntimeCandidate(
            section,
            section.Classroom,
            section.Instructor,
            instructorName,
            timeBlocks,
            suggestedSemester,
            GetSuggestedSemestersByDepartment(section.CourseId, suggestedSemestersByDepartment),
            GetAccessibleDepartments(section.CourseId, section.Course.DepartmentId, accessibleDepartments),
            classroomLookup);
    }

    private async Task<(RuntimeCandidate Candidate, IReadOnlyList<string> Notes)> BuildCandidateRuntimeAsync(
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

        if (scheduleSlotOverrides != null)
        {
            // Plan 11 sonrası: H1 ClassroomConflict per-block ClassroomId üzerinden eşleşir
            // (paralel-lab desteği). Manuel atama akışında dto.ClassroomId boş gelirse top-level
            // request.ClassroomId yansıtılır — aksi halde block.ClassroomId null kalır.
            var overrideValidation = ValidateTimeBlocks(scheduleSlotOverrides
                .Select(dto => new TimeBlock
                {
                    Day = (DayOfWeek)dto.Day,
                    StartTime = dto.StartTime,
                    EndTime = dto.EndTime,
                    ClassroomId = dto.ClassroomId ?? classroomId
                })
                .ToList());

            scheduleSlots = overrideValidation.Slots;
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

        var candidate = BuildCandidateRuntime(
            section,
            instructor,
            instructorName,
            classroom,
            scheduleSlots,
            suggestedSemester,
            GetSuggestedSemestersByDepartment(section.CourseId, courseMetadata.SuggestedSemestersByDepartment),
            GetAccessibleDepartments(section.CourseId, section.Course.DepartmentId, courseMetadata.AccessibleDepartments));

        return (candidate, notes);
    }

    private static RuntimeCandidate BuildCandidateRuntime(
        CourseSection section,
        Instructor? instructor,
        string instructorName,
        Classroom? classroom,
        IReadOnlyList<TimeBlock> scheduleSlots,
        int? suggestedSemester = null,
        Dictionary<int, int>? suggestedSemestersByDepartment = null,
        HashSet<int>? accessibleDepartmentIds = null)
    {
        return ProjectRuntimeCandidate(
            section, classroom, instructor, instructorName,
            scheduleSlots, suggestedSemester,
            suggestedSemestersByDepartment ?? new Dictionary<int, int>(),
            accessibleDepartmentIds ?? new HashSet<int>(),
            classroomLookup: null);
    }

    /// <summary>
    /// CourseSection + classroom + instructor + time blocks → RuntimeCandidate.
    /// V1 <c>SectionSnapshotFactory.FromCourseSection</c> portu — paralel-split-aware primary
    /// classroom kapasite hesabı + RuntimeBlock listesi inşası.
    /// </summary>
    private static RuntimeCandidate ProjectRuntimeCandidate(
        CourseSection section,
        Classroom? classroom,
        Instructor? instructor,
        string instructorName,
        IReadOnlyList<TimeBlock> timeBlocks,
        int? suggestedSemester,
        Dictionary<int, int> suggestedSemestersByDept,
        HashSet<int> accessibleDepts,
        IReadOnlyDictionary<int, Classroom>? classroomLookup)
    {
        var (primaryId, primaryName, primaryCapacity, primaryType)
            = ResolvePrimaryClassroomMetadata(classroom, timeBlocks, classroomLookup);

        var blocks = timeBlocks.Select(b => BuildRuntimeBlock(b, classroomLookup, primaryName, primaryType, primaryCapacity)).ToList();

        return new RuntimeCandidate
        {
            SectionId = section.Id,
            SectionNumber = section.SectionNumber,
            CourseId = section.CourseId,
            CourseCode = section.Course.Code,
            CourseName = section.Course.Name,
            DepartmentId = section.Course.DepartmentId,
            InstructorId = instructor?.Id,
            InstructorName = instructorName,
            InstructorMaxWeeklyHours = instructor?.MaxWeeklyHours ?? 0,
            Capacity = section.Capacity,
            EnrolledCount = section.EnrolledCount,
            TheoryHours = section.Course.TheoryHours,
            LabHours = section.Course.LabHours,
            TeachingMethod = section.Course.TeachingMethod,
            RequiredTheoryClassroomType = (int?)section.Course.TheoryClassroomType,
            RequiredLabClassroomType = (int?)section.Course.LabClassroomType,
            SuggestedSemester = suggestedSemester,
            SuggestedSemestersByDepartment = suggestedSemestersByDept,
            AccessibleDepartmentIds = accessibleDepts,
            PrimaryClassroomId = primaryId,
            PrimaryClassroomName = primaryName,
            PrimaryClassroomCapacity = primaryCapacity,
            PrimaryClassroomType = primaryType,
            Blocks = blocks,
        };
    }

    private static RuntimeBlock BuildRuntimeBlock(
        TimeBlock b,
        IReadOnlyDictionary<int, Classroom>? classroomLookup,
        string primaryName,
        ClassroomType primaryType,
        int primaryCapacity)
    {
        // Block kendi classroom'unu çözer (paralel-split: her block farklı oda olabilir).
        string blockName = primaryName;
        ClassroomType blockType = primaryType;
        int blockCapacity = primaryCapacity;

        if (b.ClassroomId.HasValue && classroomLookup != null
            && classroomLookup.TryGetValue(b.ClassroomId.Value, out var cr))
        {
            blockName = cr.Name;
            blockType = cr.Type;
            blockCapacity = cr.Capacity;
        }

        return new RuntimeBlock(
            Day: (int)b.Day,
            StartTime: b.StartTime,
            EndTime: b.EndTime,
            ClassroomId: b.ClassroomId,
            ClassroomName: blockName,
            ClassroomType: blockType,
            ClassroomCapacity: blockCapacity);
    }

    /// <summary>
    /// Primary window (ilk blok) classroom metadata'sı. Paralel-split: aynı (Day, Start, End)
    /// penceresinde birden çok oda → kapasite toplamı. Bloklar boş ise primaryClassroom fallback.
    /// V1 <c>SectionSnapshotFactory.ResolveClassroomMetadata</c> portu.
    /// </summary>
    private static (int? Id, string Name, int Capacity, ClassroomType Type) ResolvePrimaryClassroomMetadata(
        Classroom? primaryClassroom,
        IReadOnlyList<TimeBlock> blocks,
        IReadOnlyDictionary<int, Classroom>? classroomLookup)
    {
        if (blocks.Count == 0)
        {
            return (
                primaryClassroom?.Id,
                primaryClassroom?.Name ?? string.Empty,
                primaryClassroom?.Capacity ?? 0,
                primaryClassroom?.Type ?? ClassroomType.Lecture
            );
        }

        var firstBlock = blocks[0];
        var primaryId = firstBlock.ClassroomId ?? primaryClassroom?.Id;

        Classroom? resolvedPrimary = primaryClassroom;
        if (resolvedPrimary == null && primaryId.HasValue && classroomLookup != null)
            classroomLookup.TryGetValue(primaryId.Value, out resolvedPrimary);

        var name = resolvedPrimary?.Name ?? string.Empty;
        var type = resolvedPrimary?.Type ?? ClassroomType.Lecture;

        var primaryWindowBlocks = blocks
            .Where(b => b.Day == firstBlock.Day
                && b.StartTime == firstBlock.StartTime
                && b.EndTime == firstBlock.EndTime)
            .ToList();

        var combinedCapacity = 0;
        foreach (var b in primaryWindowBlocks)
        {
            if (b.ClassroomId.HasValue && classroomLookup != null
                && classroomLookup.TryGetValue(b.ClassroomId.Value, out var cr))
            {
                combinedCapacity += cr.Capacity;
            }
        }

        var capacity = combinedCapacity > 0
            ? combinedCapacity
            : (resolvedPrimary?.Capacity ?? 0);

        return (primaryId, name, capacity, type);
    }
}
