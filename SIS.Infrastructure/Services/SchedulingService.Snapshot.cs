using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling;

namespace SIS.Infrastructure.Services;

/// <summary>
/// SchedulingService'in TermSnapshot / SectionSnapshot inşa eden partial bölümü.
/// Auto-schedule motoru ile ApplyScheduleAsync (manuel atama) AYNI snapshot yapısını
/// kullanmalı; bu dosya ortak inşa kodunu izole eder.
///
/// Method'lar:
///   - BuildTermSnapshotAsync       (TermSnapshot inşası, mode-aware ConflictPairsBuilder çağırır)
///   - BuildExistingSectionSnapshot (DB'deki section'dan SectionSnapshot)
///   - BuildCandidateSnapshotAsync  (validate için aday section)
///   - BuildCandidateSnapshot       (saf inşa, IO yok)
/// </summary>
public partial class SchedulingService
{
    /// <summary>
    /// Plan 13 Faz 8 helper — TermSnapshot inşa et + RuntimeSnapshotAdapter ile çevir.
    /// 9 caller'da tekrar eden 2-adımı tek method'a sarar. İlerideki Faz 8 tam refactor
    /// (V1 TermSnapshot/SectionSnapshot tipleri silme) bu method'un içeriğini direkt DB→Runtime
    /// dönüşümüne değiştirir; caller'lar değişmez.
    /// </summary>
    internal async Task<(TermSnapshot TermSnapshot, SIS.Application.Scheduling.Rules.RuntimeSnapshot RuntimeSnapshot)>
        BuildRuntimeSnapshotAsync(
            int termId,
            int? excludedSectionId,
            int? departmentId,
            string? curriculumConflictMode,
            CancellationToken cancellationToken)
    {
        var termSnapshot = await BuildTermSnapshotAsync(termId, excludedSectionId, departmentId, cancellationToken);
        var runtimeSnapshot = SIS.Infrastructure.Services.Scheduling.Rules.RuntimeSnapshotAdapter.FromTermSnapshot(
            termSnapshot,
            curriculumConflictMode: curriculumConflictMode ?? "curriculum");
        return (termSnapshot, runtimeSnapshot);
    }

    internal async Task<TermSnapshot> BuildTermSnapshotAsync(int termId, int? excludedSectionId, int? departmentId, CancellationToken cancellationToken)
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

        // H17 FaceToFaceSlotRestriction için slot allowed mode haritası.
        // Bu dict boş kalırsa H17 evaluator bypass olur ve FaceToFace dersler OnlineOnly slot'lara yerleşebilir.
        // Duplicate (Day, StartTime) pair'leri varsa (test fixture veya bozuk veri) son değer üstüne yazılır;
        // çakışma durumunda OnlineOnly'yi tercih et (daha kısıtlayıcı, güvenli).
        var slotRecords = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == termId)
            .Select(ts => new { ts.DayOfWeek, ts.StartTime, ts.AllowedMode })
            .ToListAsync(cancellationToken);

        var slotAllowedModes = new Dictionary<(DayOfWeek, TimeOnly), TimeSlotAllowedMode>();
        foreach (var slot in slotRecords)
        {
            var key = (slot.DayOfWeek, slot.StartTime);
            if (!slotAllowedModes.TryGetValue(key, out var existing)
                || slot.AllowedMode == TimeSlotAllowedMode.OnlineOnly)
            {
                slotAllowedModes[key] = slot.AllowedMode;
            }
        }

        var snapshots = sections
            .Select(section => BuildExistingSectionSnapshot(
                section,
                instructorNameMap,
                courseMetadata.MinSuggestedSemesters,
                courseMetadata.SuggestedSemestersByDepartment,
                courseMetadata.AccessibleDepartments))
            .ToList();

        var snapshot = new TermSnapshot
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
            SlotAllowedModes = slotAllowedModes,
        };

        // Manuel atama (Apply) yolu da AutoScheduleEngine ile AYNI mode'u kullanmalı.
        // Tek otorite ConflictPairsBuilder; mode-branching tek yer.
        var hardCfg = await LoadHardConstraintConfigAsync(cancellationToken);
        var rawConfig = await LoadSystemConfigAsync(cancellationToken);
        var fullCfg = SchedulingConstraintConfig.FromSystemConfig(rawConfig);
        await ConflictPairsBuilder.ApplyToSnapshotAsync(
            snapshot,
            hardCfg.CurriculumConflictMode,
            snapshots,
            courseMetadata.SuggestedSemestersByDepartment,
            courseMetadata.AccessibleDepartments,
            fullCfg.EnrollmentConflictHardThreshold,
            _context, termId, cancellationToken);

        return snapshot;
    }

    private static SectionSnapshot BuildExistingSectionSnapshot(
        CourseSection section,
        IReadOnlyDictionary<int, string> instructorNameMap,
        IReadOnlyDictionary<int, int> semesterMap,
        IReadOnlyDictionary<int, Dictionary<int, int>> suggestedSemestersByDepartment,
        IReadOnlyDictionary<int, HashSet<int>> accessibleDepartments)
    {
        var timeBlocks = ScheduleHelper.ToTimeBlocks(section.SectionSchedules);
        var instructorName = section.InstructorId.HasValue
            && instructorNameMap.TryGetValue(section.InstructorId.Value, out var n) ? n : string.Empty;
        var suggestedSemester = semesterMap.TryGetValue(section.CourseId, out var sem) ? sem : (int?)null;

        return SectionSnapshotFactory.FromCourseSection(
            section, section.Classroom, section.Instructor, instructorName,
            timeBlocks, suggestedSemester,
            GetSuggestedSemestersByDepartment(section.CourseId, suggestedSemestersByDepartment),
            GetAccessibleDepartments(section.CourseId, section.Course.DepartmentId, accessibleDepartments));
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
            // Plan 11 sonrası: H1 ClassroomConflict per-block ClassroomId üzerinden eşleşiyor
            // (paralel-lab desteği). Manuel atama akışında dto.ClassroomId boş gelirse top-level
            // request.ClassroomId (classroomIdOverride) yansıtılır — aksi halde block.ClassroomId
            // null kalır ve H1 sessizce skip eder.
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
        // Not: Buradaki assignedTimeSlotCount caller'dan geliyor (genelde
        // ResolveAssignedTimeSlotCount). Factory paralel-split aware kendi hesaplar
        // ama caller'ı korumak için override etmiyoruz; sadece field-by-field clone.
        return SectionSnapshotFactory.FromCourseSection(
            section, classroom, instructor, instructorName,
            scheduleSlots, suggestedSemester,
            suggestedSemestersByDepartment ?? new Dictionary<int, int>(),
            accessibleDepartmentIds ?? new HashSet<int>(),
            classroomLookup: null,
            notes: notes);
    }
}
