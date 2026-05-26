using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.Scheduling.Solver;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Scheduling.OrTools;

/// <summary>
/// EF Core AppDbContext'ten Domain entity'lerini okuyup CP-SAT için <see cref="SolverRequest"/>
/// DTO'sunu inşa eder. <see cref="CpSatSolverEngine"/> bu DTO ile beslenir; AutoScheduleEngine
/// (Plan 2 Task 19) ve Run 303 integration test (Task 20) bu builder'ı kullanır.
///
/// Plan 2 scope:
/// <list type="bullet">
///   <item>Sections: Active, !IsFinalized, course faculty'nin department'larında, opsiyonel dept filter.</item>
///   <item>Classrooms: ClassroomTermAssignments üzerinden TermId + FacultyId filter'ı.</item>
///   <item>TimeSlots: term-spesifik grid, day+slotOrder ordering ile deterministic.</item>
///   <item>ConstraintFlags: tüm ConstraintDefinitions Code → IsEnabled dictionary.</item>
///   <item>Corequisites: boş — Domain'de CourseRelationship entity yok (Plan 3'te entegrasyon).</item>
///   <item>SectionInput.Year: CurriculumCourse.SuggestedSemester'dan türetilir (Plan 6 Task 1).</item>
///   <item>SectionInput.ProgramId: Plan 2'de null (ileride curriculum-program mapping eklendiğinde doldurulacak).</item>
/// </list>
/// </summary>
public sealed class SolverRequestBuilder
{
    private readonly AppDbContext _context;
    private readonly ISystemConfigProvider _systemConfigProvider;

    public SolverRequestBuilder(AppDbContext context, ISystemConfigProvider systemConfigProvider)
    {
        _context = context;
        _systemConfigProvider = systemConfigProvider;
    }

    public async Task<SolverRequest> BuildAsync(
        int termId,
        int facultyId,
        int? departmentId,
        int? seed,
        CancellationToken cancellationToken)
    {
        // 1) Faculty'ye bağlı department ID'leri (downstream filter için)
        var departmentIds = await _context.Departments
            .AsNoTracking()
            .Where(d => d.FacultyId == facultyId)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        // 2) Sections — Active + !IsFinalized + course faculty department'larında
        var sectionsQuery = _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Where(cs => cs.TermId == termId
                         && !cs.IsFinalized
                         && cs.Status == CourseSectionStatus.Active
                         && cs.Course.DepartmentId.HasValue
                         && departmentIds.Contains(cs.Course.DepartmentId.Value));

        if (departmentId.HasValue)
        {
            sectionsQuery = sectionsQuery.Where(cs => cs.Course.DepartmentId == departmentId.Value);
        }

        var sectionEntities = await sectionsQuery.ToListAsync(cancellationToken);

        // 3) Classrooms — faculty'nin term-assigned derslikleri
        // (Section mapping'den önce yüklüyoruz; LabGroupCount hesabı için lab capacity'lere ihtiyacımız var.)
        var classroomEntities = await _context.ClassroomTermAssignments
            .AsNoTracking()
            .Where(cta => cta.TermId == termId && cta.FacultyId == facultyId)
            .Select(cta => cta.Classroom)
            .ToListAsync(cancellationToken);

        var classrooms = classroomEntities
            .Where(c => c != null)
            .Select(MapClassroom)
            .ToList();

        // Plan 4 Task 1 — paralel lab support için lab classroom kapasitelerini topla.
        // Lab + Computer derslikler lab component'i barındırabilir.
        var labClassroomCapacities = classroomEntities
            .Where(c => c != null && (c.Type == ClassroomType.Lab || c.Type == ClassroomType.Computer))
            .Select(c => c.Capacity)
            .ToList();

        // Plan 5 Task 1 — instructor max weekly hours override map.
        // Section'ın hocası varsa Instructor.MaxWeeklyHours'tan yüklenir; 0 ise default kullanılır.
        var instructorIds = sectionEntities
            .Where(cs => cs.InstructorId.HasValue)
            .Select(cs => cs.InstructorId!.Value)
            .Distinct()
            .ToList();

        var instructorMaxHours = await _context.Instructors
            .AsNoTracking()
            .Where(i => instructorIds.Contains(i.Id))
            .ToDictionaryAsync(
                i => i.Id,
                i => i.MaxWeeklyHours,
                cancellationToken);

        // Plan 6 Task 1 — Course → Year mapping from CurriculumCourses.
        // year = (SuggestedSemester - 1) / 2 (0..3 = 1.-4. sınıf).
        // Bir ders birden çok müfredatta olabilir; en küçük SuggestedSemester'ı seçeriz
        // (heuristic — H5 için cohort gruplaması yeterli).
        // null = curriculum mapping yok → H5 atlar.
        var courseIds = sectionEntities.Select(cs => cs.CourseId).Distinct().ToList();

        var courseYearMap = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => courseIds.Contains(cc.CourseId))
            .Where(cc => cc.SuggestedSemester > 0)
            .GroupBy(cc => cc.CourseId)
            .Select(g => new
            {
                CourseId = g.Key,
                MinSemester = g.Min(cc => cc.SuggestedSemester)
            })
            .ToDictionaryAsync(x => x.CourseId, x => (x.MinSemester - 1) / 2, cancellationToken);

        // 2026-05-17 — Course → IsCompulsory mapping. Komşu yıl hard pair'leri için
        // CohortBuilder bu bilgiyi kullanıyor (seçmeli dersler hariç tutulur).
        // Heuristic: bir ders birden çok müfredatta yer alıyorsa "en az birinde zorunlu"
        // ise zorunlu sayılır (Any). Müfredat yokluğunda default false (seçmeli muamelesi).
        var courseCompulsoryMap = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => courseIds.Contains(cc.CourseId))
            .GroupBy(cc => cc.CourseId)
            .Select(g => new
            {
                CourseId = g.Key,
                IsCompulsory = g.Any(cc => cc.IsCompulsory)
            })
            .ToDictionaryAsync(x => x.CourseId, x => x.IsCompulsory, cancellationToken);

        var sections = sectionEntities
            .Select(cs => MapSection(cs, labClassroomCapacities, instructorMaxHours, courseYearMap))
            .ToList();

        // 4) TimeSlots — term grid (day + slot ordering deterministic indexing için)
        var timeSlotEntities = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == termId)
            .OrderBy(ts => ts.DayOfWeek)
            .ThenBy(ts => ts.SlotOrder)
            .ToListAsync(cancellationToken);
        var timeSlots = timeSlotEntities.Select(MapTimeSlot).ToList();

        // 5) Constraint flags — Code → IsEnabled, ayrıca Category (Plan 10 — Hard/Soft toggle).
        // Parameters JSON (H4 alt-toggle'ları için, 2026-05-19) da yüklenir.
        var constraintRows = await _context.ConstraintDefinitions
            .AsNoTracking()
            .Select(cd => new { cd.Code, cd.IsEnabled, cd.Category, cd.Parameters })
            .ToListAsync(cancellationToken);
        var constraintFlags = constraintRows.ToDictionary(r => r.Code, r => r.IsEnabled);
        var constraintCategories = constraintRows.ToDictionary(r => r.Code, r => r.Category);

        // H4 alt-toggle'ları (AmphiThresholdEnabled, ClassroomTypeRequirementEnabled) —
        // H4 row'unun Parameters JSON'undan parse edilir. AmphiThreshold (sayı) eski path'ten
        // (CpSatSolverEngine SystemConfigs okuma) gelir, drift kalmaması için tek kaynak.
        var (amphiThresholdEnabledFromH4, classroomTypeRequirementEnabledFromH4) =
            ParseH4Toggles(constraintRows.FirstOrDefault(r => r.Code == "H4")?.Parameters);

        // H5 — CurriculumConflictMode H5.Parameters JSON'undan (2026-05-19 dual-channel temizliği)
        var curriculumConflictModeFromH5 =
            ParseH5CurriculumMode(constraintRows.FirstOrDefault(r => r.Code == "H5")?.Parameters);

        // H6 — MaxLabBlockHours H6.Parameters JSON'undan (2026-05-19 SystemConfigs → H6)
        var maxLabBlockHoursFromH6 =
            ParseH6MaxLabBlockHours(constraintRows.FirstOrDefault(r => r.Code == "H6")?.Parameters);

        // 6) Corequisites — Plan 6 Task 8.
        //    Filter: IsCorequisite=true AND (CourseId or RequiredCourseId in current sections' courses).
        //    Yalnızca bu run'daki section'ların kurslarıyla ilişkili eşkoşullar yüklenir.
        var courseIdsForCorequisites = sections.Select(s => s.CourseId).Distinct().ToList();
        var corequisites = await _context.CoursePrerequisites
            .AsNoTracking()
            .Where(cp => cp.IsCorequisite)
            .Where(cp => courseIdsForCorequisites.Contains(cp.CourseId)
                      || courseIdsForCorequisites.Contains(cp.RequiredCourseId))
            .Select(cp => new CourseRelationship(cp.CourseId, cp.RequiredCourseId))
            .ToListAsync(cancellationToken);

        // 6b) Plan 6 Task 10 — Load distribution constraints for the term (DB has 0 rows currently;
        //     infrastructure-only, encoder stub Plan 6.5 will implement real encoding).
        var distributionConstraints = await _context.DistributionConstraints
            .AsNoTracking()
            .Where(dc => dc.TermId == termId)
            .Select(dc => new DistributionConstraintInput(
                dc.Id,
                dc.DistributionType,
                dc.PreferenceLevel,
                dc.Courses
                    .OrderBy(c => c.SortOrder)
                    .Select(c => new DistributionCourseInput(c.CourseId, c.CourseSectionId, c.SortOrder))
                    .ToList()))
            .ToListAsync(cancellationToken);

        // 7) Plan 5 Task 1 — Instructor weekly-hour cap default (SystemConfig).
        var defaultInstructorMaxWeeklyHours = await _systemConfigProvider.GetIntAsync(
            "Schedule.DefaultInstructorMaxWeeklyHours", 30, cancellationToken);

        // 8) Plan 6 Task 3 — Curriculum conflict mode (2026-05-19): H5.Parameters JSON
        //    tek otorite (Kısıtlar sayfası → H5 dropdown). Eski SystemConfigs yolu kaldırıldı.
        //    Default "curriculum" if H5 row yok veya key tanımlı değil.
        var curriculumConflictMode = curriculumConflictModeFromH5 ?? "curriculum";

        // 9) Plan 6 Task 6 — In cohort mode, materialize CohortConflictPairs via v1 CohortBuilder.
        //    Plan 8 Task 2 — assignment outer-scope'a alındı; AdjacentYearSoftPairs S20 için kullanılır.
        IReadOnlyList<(int, int)> cohortConflictPairs = [];
        CohortAssignment? cohortAssignment = null;
        if (curriculumConflictMode == "cohort" && sections.Count > 0)
        {
            var cohortCourseIds = sections.Select(s => s.CourseId).Distinct().ToList();
            var (courseSemestersByDept, courseAccessibleDepts) =
                await LoadCohortMetadataAsync(cohortCourseIds, cancellationToken);

            // Project SectionInput → CohortSectionRef. CohortBuilder komşu yıl hard pair
            // filtresi için IsCompulsory bilgisini de okur (yalnızca her iki tarafı da
            // zorunlu olan ders çiftleri komşu yıl hard pair'ine girer — seçmeli hariç).
            var cohortRefs = sections
                .Select(s => new CohortSectionRef(
                    s.Id, s.SectionNumber, s.CourseId,
                    courseCompulsoryMap.GetValueOrDefault(s.CourseId, false)))
                .ToList();

            var cohortBuilder = new CohortBuilder();
            cohortAssignment = cohortBuilder.Build(cohortRefs, courseSemestersByDept, courseAccessibleDepts);

            cohortConflictPairs = cohortAssignment.ConflictPairs
                .Select(p => (p.SectionId1, p.SectionId2))
                .ToList();
        }

        // 10) Plan 8 Task 2 — SoftThresholds (~25 SystemConfig key) + S4 CoEnrollmentPairs (port v1)
        //     + S20 AdjacentYearSoftPairs (cohort assignment'tan).
        // Plan 13 — S2/S8 clock-hour: slot ortalama dakikası SoftThresholds'a inject edilir.
        var loadedSoftThresholds = await LoadSoftThresholdsAsync(cancellationToken);
        var averageSlotDurationMinutes = timeSlots.Count > 0
            ? (int)Math.Round(timeSlots.Average(s => (s.EndTime - s.StartTime).TotalMinutes))
            : 50;
        var softThresholds = loadedSoftThresholds with
        {
            AverageSlotDurationMinutes = averageSlotDurationMinutes,
        };
        var coEnrollmentPairs = await LoadCoEnrollmentPairsAsync(
            termId, softThresholds.S4MinCoPairThreshold, cancellationToken);

        IReadOnlyList<(int, int)> adjacentYearSoftPairs = cohortAssignment?.AdjacentYearSoftPairs
            .Select(p => (p.SectionId1, p.SectionId2))
            .ToList() ?? [];

        return new SolverRequest
        {
            TermId = termId,
            FacultyId = facultyId,
            DepartmentId = departmentId,
            Seed = seed,
            Sections = sections,
            Classrooms = classrooms,
            TimeSlots = timeSlots,
            ConstraintFlags = constraintFlags,
            ConstraintCategories = constraintCategories,
            Corequisites = corequisites,
            DefaultInstructorMaxWeeklyHours = defaultInstructorMaxWeeklyHours,
            CurriculumConflictMode = curriculumConflictMode,
            CohortConflictPairs = cohortConflictPairs,
            DistributionConstraints = distributionConstraints,
            SoftThresholds = softThresholds,
            CoEnrollmentPairs = coEnrollmentPairs,
            AdjacentYearSoftPairs = adjacentYearSoftPairs,
            // 2026-05-19 — H4 alt-toggle'ları H4.Parameters JSON'undan (Kısıtlar sayfası).
            // CpSatSolverEngine bu değerleri SystemConfigs okumayı kestiği için request'ten alır.
            AmphiThresholdEnabled = amphiThresholdEnabledFromH4,
            ClassroomTypeRequirementEnabled = classroomTypeRequirementEnabledFromH4,
            // 2026-05-19 — MaxLabBlockHours H6.Parameters JSON'undan (Kısıtlar sayfası).
            MaxLabBlockHours = maxLabBlockHoursFromH6 ?? 2,
        };
    }

    /// <summary>
    /// H5 ConstraintDefinition.Parameters JSON'undan CurriculumConflictMode değerini parse eder.
    /// Eksik key veya bozuk JSON → null (caller default "curriculum" uygular).
    /// </summary>
    private static string? ParseH5CurriculumMode(string? parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters)) return null;
        try
        {
            using var doc = JsonDocument.Parse(parameters);
            if (doc.RootElement.TryGetProperty("CurriculumConflictMode", out var mode)
                && mode.ValueKind == JsonValueKind.String)
            {
                var val = mode.GetString();
                return string.IsNullOrWhiteSpace(val) ? null : val;
            }
        }
        catch (JsonException) { /* silent */ }
        return null;
    }

    /// <summary>
    /// H6 ConstraintDefinition.Parameters JSON'undan MaxLabBlockHours değerini parse eder.
    /// Eksik key, bozuk JSON veya ≤0 → null (caller default 2 uygular).
    /// </summary>
    private static int? ParseH6MaxLabBlockHours(string? parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters)) return null;
        try
        {
            using var doc = JsonDocument.Parse(parameters);
            if (doc.RootElement.TryGetProperty("MaxLabBlockHours", out var max)
                && max.ValueKind == JsonValueKind.Number
                && max.TryGetInt32(out var maxInt)
                && maxInt > 0)
            {
                return maxInt;
            }
        }
        catch (JsonException) { /* silent */ }
        return null;
    }

    /// <summary>
    /// H4 ConstraintDefinition.Parameters JSON'undan AmphiThresholdEnabled ve
    /// ClassroomTypeRequirementEnabled değerlerini parse eder. Eksik key veya bozuk JSON → false.
    /// </summary>
    private static (bool AmphiEnabled, bool TypeEnabled) ParseH4Toggles(string? parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters)) return (false, false);

        try
        {
            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;
            var amphiEnabled = root.TryGetProperty("AmphiThresholdEnabled", out var a)
                && a.ValueKind == JsonValueKind.True;
            var typeEnabled = root.TryGetProperty("ClassroomTypeRequirementEnabled", out var t)
                && t.ValueKind == JsonValueKind.True;
            return (amphiEnabled, typeEnabled);
        }
        catch (JsonException)
        {
            return (false, false);
        }
    }

    /// <summary>
    /// Plan 8 Task 2 — SystemConfig'ten ~25 anahtar okur ve <see cref="SoftThresholds"/>
    /// record'unu inşa eder. Eksik key fallback değere düşer (record default'larıyla aynı).
    /// </summary>
    private async Task<SoftThresholds> LoadSoftThresholdsAsync(CancellationToken ct)
    {
        // S1 Occupancy
        var s1MinOcc = await _systemConfigProvider.GetIntAsync("Classroom.OccupancyTarget.Min", 60, ct);
        var s1MaxOcc = await _systemConfigProvider.GetIntAsync("Classroom.OccupancyTarget.Max", 85, ct);
        var s1Weight = await _systemConfigProvider.GetIntAsync("Schedule.S1Weight", 1, ct);

        // S2 DailyLoadSoft
        // Plan 13 (2026-05-15) — V2 encoder V1 ile birebir clock-hour hesabı yapar
        // (kullanıcı A seçeneği). Cap admin için "saat" cinsinden okunur (default 6),
        // encoder içinde × 60 dakikaya çevrilir.
        var s2MaxDaily = await _systemConfigProvider.GetIntAsync("SoftConstraint.S2.MaxDailyHours", 6, ct);
        var s2Weight = await _systemConfigProvider.GetIntAsync("Schedule.S2Weight", 10, ct);

        // S3 SlotCongestion
        var s3MaxSec = await _systemConfigProvider.GetIntAsync("SoftConstraint.S3.MaxSectionsPerSlot", 5, ct);
        var s3Weight = await _systemConfigProvider.GetIntAsync("Schedule.S3Weight", 5, ct);

        // S4 CoEnrollment
        var s4MinPair = await _systemConfigProvider.GetIntAsync("SoftConstraint.S4.MinCoPairThreshold", 20, ct);
        var s4Weight = await _systemConfigProvider.GetIntAsync("Schedule.S4Weight", 1, ct);

        // S5 LunchBreak — H13 / Ders Saatleri Yönetimi ile TEK ortak anahtar.
        // 2026-05-24: Eski noktalı key ("Schedule.LunchBreak.Start") hiç yazılmadığı için soft
        // encoder her zaman default'a düşüyordu (admin'in öğle saati yok sayılıyordu). Hard encoder,
        // slot validasyonu ve H13 parametre formu zaten "Schedule.LunchBreakStart/End" kullanıyor.
        var s5LunchStart = await ReadTimeAsync("Schedule.LunchBreakStart", new TimeOnly(11, 40), ct);
        var s5LunchEnd = await ReadTimeAsync("Schedule.LunchBreakEnd", new TimeOnly(12, 20), ct);
        var s5Weight = await _systemConfigProvider.GetIntAsync("Schedule.S5Weight", 8, ct);

        // S7 StudentGap
        var s7MaxGap = await _systemConfigProvider.GetIntAsync("SoftConstraint.S7.MaxGapSlots", 3, ct);
        var s7Weight = await _systemConfigProvider.GetIntAsync("Schedule.S7Weight", 3, ct);

        // S8 InstructorDaySpread
        var s8Weight = await _systemConfigProvider.GetIntAsync("Schedule.S8Weight", 4, ct);

        // S12 WeekdayBalance
        var s12MaxImb = await _systemConfigProvider.GetIntAsync("SoftConstraint.S12.MaxImbalancePercent", 20, ct);
        var s12Weight = await _systemConfigProvider.GetIntAsync("Schedule.S12Weight", 2, ct);

        // S20 AdjacentYear
        var s20Penalty = await _systemConfigProvider.GetIntAsync("SoftConstraint.S20.PenaltyPerPair", 5, ct);
        var s20Weight = await _systemConfigProvider.GetIntAsync("Schedule.S20Weight", 1, ct);

        // S21 OnlineSlotPreference (2026-05-20)
        var s21Weight = await _systemConfigProvider.GetIntAsync("Schedule.S21Weight", 5, ct);

        // DIST tier weights
        var distDiscWeight = await _systemConfigProvider.GetIntAsync("Schedule.DistDiscouragedWeight", 50, ct);
        var distStrongWeight = await _systemConfigProvider.GetIntAsync("Schedule.DistStronglyDiscouragedWeight", 200, ct);

        // Plan 10 Phase 5 — Toggleable hard kuralların Soft tarafı için weight'ler.
        // Admin UI weight slider bu key'lere yazıyor (ConstraintParameterMapping.ResolveWeightKey).
        // Default'lar SoftThresholds field default'larıyla aynı (geriye uyum).
        var h4Weight = await _systemConfigProvider.GetIntAsync("Schedule.H4Weight", 5, ct);
        var h5Weight = await _systemConfigProvider.GetIntAsync("Schedule.H5Weight", 10, ct);
        var h8Weight = await _systemConfigProvider.GetIntAsync("Schedule.H8Weight", 8, ct);
        var h10Weight = await _systemConfigProvider.GetIntAsync("Schedule.H10Weight", 5, ct);
        var h15Weight = await _systemConfigProvider.GetIntAsync("Schedule.H15Weight", 6, ct);
        var h16Weight = await _systemConfigProvider.GetIntAsync("Schedule.H16Weight", 5, ct);
        var hwlWeight = await _systemConfigProvider.GetIntAsync("Schedule.HWLWeight", 8, ct);
        var hdoWeight = await _systemConfigProvider.GetIntAsync("Schedule.HDOWeight", 7, ct);

        return new SoftThresholds
        {
            S1MinOccupancyPercent = s1MinOcc,
            S1MaxOccupancyPercent = s1MaxOcc,
            S1Weight = s1Weight,
            S2MaxDailyHours = s2MaxDaily,
            S2Weight = s2Weight,
            S3MaxSectionsPerSlot = s3MaxSec,
            S3Weight = s3Weight,
            S4MinCoPairThreshold = s4MinPair,
            S4Weight = s4Weight,
            S5LunchBreakStart = s5LunchStart,
            S5LunchBreakEnd = s5LunchEnd,
            S5Weight = s5Weight,
            S7MaxGapSlots = s7MaxGap,
            S7Weight = s7Weight,
            S8Weight = s8Weight,
            S12MaxImbalancePercent = s12MaxImb,
            S12Weight = s12Weight,
            S20PenaltyPerPair = s20Penalty,
            S20Weight = s20Weight,
            S21Weight = s21Weight,
            DistDiscouragedWeight = distDiscWeight,
            DistStronglyDiscouragedWeight = distStrongWeight,
            H4Weight = h4Weight,
            H5Weight = h5Weight,
            H8Weight = h8Weight,
            H10Weight = h10Weight,
            H15Weight = h15Weight,
            H16Weight = h16Weight,
            HWLWeight = hwlWeight,
            HDOWeight = hdoWeight,
        };
    }

    /// <summary>
    /// Plan 8 Task 2 — ISystemConfigProvider üzerinde GetTimeAsync olmadığından string okur ve
    /// <see cref="TimeOnly"/>'a parse eder. Parse fail (boş veya geçersiz format) ⇒ default döner.
    /// </summary>
    private async Task<TimeOnly> ReadTimeAsync(string key, TimeOnly defaultValue, CancellationToken ct)
    {
        var raw = await _systemConfigProvider.GetStringAsync(key, defaultValue.ToString("HH:mm"), ct);
        return TimeOnly.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    /// <summary>
    /// Plan 8 Task 2 — v1 <c>AutoScheduleEngine.LoadCoEnrollmentPairsAsync</c> portu.
    /// Son 3 dönemde Active CourseRegistration'lar üzerinden student×course pair sayısını sayar
    /// ve <paramref name="minThreshold"/> üstündekileri canonical (CourseId1 &lt; CourseId2) form'da
    /// döner. CourseRegistrations boşsa empty döner (production'da default davranış).
    /// </summary>
    private async Task<List<CoEnrollmentPairInput>> LoadCoEnrollmentPairsAsync(
        int currentTermId, int minThreshold, CancellationToken ct)
    {
        // "Son 3 dönem" kronolojiye (TermStart) göre seçilmeli, Id'ye göre değil — Id oluşturma
        // sırasıdır; geç oluşturulan/backfill edilen bir dönem akademik olarak daha erken olabilir.
        var currentStart = await _context.AcademicTerms
            .AsNoTracking()
            .Where(t => t.Id == currentTermId)
            .Select(t => (DateTime?)t.TermStart)
            .FirstOrDefaultAsync(ct);

        if (currentStart == null)
            return new List<CoEnrollmentPairInput>();

        var recentTermIds = await _context.AcademicTerms
            .AsNoTracking()
            .Where(t => t.TermStart < currentStart.Value)
            .OrderByDescending(t => t.TermStart)
            .Take(3)
            .Select(t => t.Id)
            .ToListAsync(ct);

        if (recentTermIds.Count == 0)
            return new List<CoEnrollmentPairInput>();

        // EF Core InMemory + AsNoTracking üzerinde nav property auto-resolve garantisi yok;
        // Include ile relational provider'da da identity-map cache'e gerek kalmaz.
        var registrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(cr => cr.CourseSection)
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
            // Canonical pair: distinct + sort ⇒ i < j ile sadece ascending pair üretilir.
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
            .Select(kvp => new CoEnrollmentPairInput(kvp.Key.Item1, kvp.Key.Item2, kvp.Value))
            .ToList();
    }

    /// <summary>
    /// Plan 6 Task 6 — cohort metadata for v1 CohortBuilder reuse.
    /// courseSemestersByDept[courseId][deptId] = semester (1-8 typical for 4-year).
    /// courseAccessibleDepts[courseId] = set of dept ids that share this course in their curriculum.
    /// Plan 9'da v1 silindiğinde tek source olarak burada kalır.
    /// </summary>
    private async Task<(Dictionary<int, Dictionary<int, int>> courseSemestersByDept,
                        Dictionary<int, HashSet<int>> courseAccessibleDepts)>
        LoadCohortMetadataAsync(IReadOnlyList<int> courseIds, CancellationToken ct)
    {
        var courseSemestersByDept = new Dictionary<int, Dictionary<int, int>>();
        var courseAccessibleDepts = new Dictionary<int, HashSet<int>>();

        var rows = await _context.CurriculumCourses
            .AsNoTracking()
            .Include(cc => cc.Curriculum)
                .ThenInclude(c => c.Program)
            .Where(cc => courseIds.Contains(cc.CourseId))
            .Where(cc => cc.SuggestedSemester > 0)
            .Select(cc => new
            {
                cc.CourseId,
                DeptId = cc.Curriculum.Program.DepartmentId,
                cc.SuggestedSemester
            })
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            if (!courseSemestersByDept.TryGetValue(row.CourseId, out var perDept))
            {
                perDept = new Dictionary<int, int>();
                courseSemestersByDept[row.CourseId] = perDept;
            }
            // Same course in multiple curricula of the same dept → keep smallest semester.
            if (!perDept.ContainsKey(row.DeptId) || row.SuggestedSemester < perDept[row.DeptId])
                perDept[row.DeptId] = row.SuggestedSemester;

            if (!courseAccessibleDepts.TryGetValue(row.CourseId, out var depts))
            {
                depts = new HashSet<int>();
                courseAccessibleDepts[row.CourseId] = depts;
            }
            depts.Add(row.DeptId);
        }

        return (courseSemestersByDept, courseAccessibleDepts);
    }

    private static SectionInput MapSection(
        CourseSection cs,
        IReadOnlyList<int> labClassroomCapacities,
        IReadOnlyDictionary<int, int> instructorMaxHours,
        IReadOnlyDictionary<int, int> courseYearMap) => new()
    {
        Id = cs.Id,
        CourseId = cs.CourseId,
        CourseCode = cs.Course.Code,
        CourseName = cs.Course.Name,
        SectionNumber = cs.SectionNumber,
        Capacity = cs.Capacity,
        TheoryHours = cs.Course.TheoryHours,
        LabHours = cs.Course.LabHours,
        InstructorId = cs.InstructorId,
        DepartmentId = cs.Course.DepartmentId,
        TeachingMethod = cs.Course.TeachingMethod,
        IsPinned = cs.IsPinned,
        IsScheduleLocked = cs.IsScheduleLocked,
        // ProgramId — Plan 2/Plan 6 Task 1'de null (ileride curriculum-program mapping eklendiğinde doldurulacak)
        // Plan 7b Task 1 — Course üzerindeki tip gereksinimi int? olarak SectionInput'a aktarılır.
        //   null = Course tipi belirtmemiş; encoder kısıt eklemez.
        //   Set = ClassroomType enum int değeri; encoder uyumsuz odalara forbidden literal ekler.
        RequiredTheoryClassroomType = (int?)cs.Course.TheoryClassroomType,
        RequiredLabClassroomType = (int?)cs.Course.LabClassroomType,
        // Plan 6 Task 1 — CurriculumCourse.SuggestedSemester'dan türetilen yıl (0..3).
        Year = courseYearMap.TryGetValue(cs.CourseId, out var year) ? year : null,
        LabGroupCount = ComputeLabGroupCount(cs.Capacity, cs.Course.LabHours, labClassroomCapacities),
        // Plan 5 Task 1 — Instructor.MaxWeeklyHours override; 0 = default kullan.
        InstructorMaxWeeklyHours = cs.InstructorId.HasValue
            ? instructorMaxHours.GetValueOrDefault(cs.InstructorId.Value, 0)
            : 0
    };

    /// <summary>
    /// Plan 4 Task 1 — paralel lab grup sayısı: section kapasitesi en büyük lab odasını
    /// aşıyorsa ⌈capacity / max_lab_capacity⌉ döner; aksi takdirde 1.
    /// LabHours=0 (lab component yok) veya hiç lab odası yoksa default 1.
    /// </summary>
    private static int ComputeLabGroupCount(int sectionCapacity, int labHours, IReadOnlyList<int> labCapacities)
    {
        if (labHours <= 0 || labCapacities.Count == 0) return 1;
        var maxLabCap = labCapacities.Max();
        if (maxLabCap <= 0) return 1;
        if (maxLabCap >= sectionCapacity) return 1;
        return (int)Math.Ceiling((double)sectionCapacity / maxLabCap);
    }

    private static ClassroomInput MapClassroom(Classroom c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Capacity = c.Capacity,
        Type = c.Type
    };

    private static TimeSlotInput MapTimeSlot(TimeSlot ts) => new()
    {
        Id = ts.Id,
        DayOfWeek = ts.DayOfWeek,
        SlotOrder = ts.SlotOrder,
        StartTime = ts.StartTime,
        EndTime = ts.EndTime,
        AllowedMode = ts.AllowedMode
    };
}
