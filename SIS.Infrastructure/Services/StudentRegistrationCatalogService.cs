using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.Common.Time;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;
using StudentEntity = SIS.Domain.Entities.Student;
using StudentProgramEntity = SIS.Domain.Entities.StudentProgram;

namespace SIS.Infrastructure.Services;

/// <inheritdoc />
public sealed class StudentRegistrationCatalogService : IStudentRegistrationCatalogService
{
    // ── Inner records ────────────────────────────────────────────────────────

    /// <summary>
    /// Carries a built row together with the grouping fields needed to slot it
    /// into the curriculum tree (classYear / parity) and the flag that marks it
    /// as a currently-failed course for the RetakeCourses list.
    /// </summary>
    private sealed record CatalogRowBuild(
        CatalogCourseRowDto Row,
        int ClassYear,
        string Parity,
        bool IsCurrentlyFailed);

    /// <summary>
    /// Typed result returned by <see cref="BuildClassYearsAsync"/> —
    /// replaces the previous 5-element value tuple.
    /// </summary>
    private sealed record BuildClassYearsResult(
        IReadOnlyList<CatalogClassYearDto> ClassYears,
        IReadOnlyList<CatalogRowBuild> FlatRows,
        Dictionary<int, bool> CompulsoryMap,
        List<CurriculumElectivePool> CurriculumPools,
        List<CurriculumCourse> CurriculumCourses);

    private readonly AppDbContext _context;
    private readonly IEctsLimitResolver _ectsLimitResolver;
    private readonly IRegistrationGate _registrationGate;
    private readonly IElectivePoolProgressCalculator _poolProgressCalculator;
    private readonly IDateTimeProvider _clock;
    private readonly IWebModuleControlService _webModuleControls;

    public StudentRegistrationCatalogService(
        AppDbContext context,
        IEctsLimitResolver ectsLimitResolver,
        IRegistrationGate registrationGate,
        IElectivePoolProgressCalculator poolProgressCalculator,
        IDateTimeProvider clock,
        IWebModuleControlService webModuleControls)
    {
        _context = context;
        _ectsLimitResolver = ectsLimitResolver;
        _registrationGate = registrationGate;
        _poolProgressCalculator = poolProgressCalculator;
        _clock = clock;
        _webModuleControls = webModuleControls;
    }

    public async Task<StudentRegistrationCatalogDto> BuildAsync(
        StudentEntity student,
        StudentProgramEntity activeProgram,
        CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        // 1. Resolve active term (must include AcademicCalendar for phase methods)
        var term = await _context.AcademicTerms
            .Include(t => t.AcademicCalendar)
            .ActiveTerm()
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("ActiveTermNotFound");

        // 2. Curriculum for name + ECTS-limit override
        var curriculum = await _context.Curricula
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == activeProgram.CurriculumId, ct);

        // 3. ApplicationUser for FullName
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == student.UserId, ct);
        var fullName = user is ApplicationUser au
            ? $"{au.FirstName} {au.LastName}".Trim()
            : student.StudentNumber;

        // 4. Program name (navigate via activeProgram.Program if loaded, else query)
        var programName = activeProgram.Program?.Name
            ?? await _context.AcademicPrograms
                .AsNoTracking()
                .Where(p => p.Id == activeProgram.ProgramId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(ct)
            ?? string.Empty;

        // 5. Degree-level-driven normal duration (no hardcoded cap)
        var degreeLevel = activeProgram.Program?.DegreeLevel
            ?? await _context.AcademicPrograms
                .AsNoTracking()
                .Where(p => p.Id == activeProgram.ProgramId)
                .Select(p => p.DegreeLevel)
                .FirstOrDefaultAsync(ct);

        var normalDurationSemesters = degreeLevel switch
        {
            DegreeLevel.Associate => 4,
            DegreeLevel.Bachelor  => 8,
            DegreeLevel.Master    => 4,
            DegreeLevel.Doctorate => 8,
            _                     => 8,
        };

        var classYear = Math.Min(
            student.CompletedSemesters / 2 + 1,
            normalDurationSemesters / 2);

        // 6. ECTS limit (delegated to resolver — curriculum override or SystemConfig)
        var ectsLimit = await _ectsLimitResolver.ResolveAsync(student.Cgpa, curriculum, ct);

        // 7. Build header
        var header = new RegistrationStudentHeaderDto(
            Id:                  student.Id,
            StudentNumber:       student.StudentNumber,
            FullName:            fullName,
            ProgramName:         programName,
            CurriculumName:      curriculum?.Name ?? string.Empty,
            ClassYear:           classYear,
            CompletedSemesters:  student.CompletedSemesters,
            Cgpa:                student.Cgpa,
            EctsLimit:           ectsLimit,
            FinancialStatus:     student.HasFinancialBlock ? "Bloke" : "Uygun",
            StudyDurationStatus: student.CompletedSemesters < normalDurationSemesters
                                     ? "Normal Süresi İçinde"
                                     : "Süre Aşımı");

        // 8. Term info — openness sourced from IWebModuleControlService (same as CourseController.GetActiveTerm
        //    and write-path _webModuleControls checks) so TopBar and catalog agree on registration state.
        //    TODO: port per-student-state code resolution (new/extended) from CourseController if needed.
        var regAccess     = await _webModuleControls.EvaluateAsync("student-course-registration", term.Id, ct);
        var addDropAccess = await _webModuleControls.EvaluateAsync("student-add-drop", term.Id, ct);

        var termInfo = new RegistrationTermInfoDto(
            Id:                  term.Id,
            Name:                term.Name,
            Year:                term.Year,
            TermType:            term.TermType.ToString(),
            IsRegistrationOpen:  regAccess.IsOpen,
            IsAddDropOpen:       addDropAccess.IsOpen,
            HasAcademicCalendar: term.AcademicCalendar != null,
            RegistrationMessage: regAccess.Message);

        // 9. Build curriculum tree (classYears / semesters / course rows)
        // Also surfaces intermediate data (flatRows, compulsoryMap, pools, allCurriculumCourses) for reuse.
        var built = await BuildClassYearsAsync(student, activeProgram, term, ct);
        var classYears       = built.ClassYears;
        var failedCourseIds  = built.FlatRows
            .Where(b => b.IsCurrentlyFailed)
            .Select(b => b.Row.CourseId)
            .ToHashSet();
        var compulsoryMap        = built.CompulsoryMap;
        var curriculumPools      = built.CurriculumPools;
        var allCurriculumCourses = built.CurriculumCourses;

        // 10. RetakeCourses — same row objects from the flat list; ClassYear, IsCompulsory,
        //     and PreviousAttemptInfo are now consistent with the curriculum tree (single build path).
        var retakeCourses = built.FlatRows
            .Where(b => b.IsCurrentlyFailed)
            .Select(b => b.Row)
            .OrderBy(r => r.Code)
            .ToList();

        // 11. ElectivePoolSlots — load pools once; reuse for ElectiveGroupCode lookup in SelectedCourses.
        var poolIds = curriculumPools.Select(p => p.Id).ToList();
        var poolEctsPerCourse = curriculumPools.ToDictionary(p => p.Id, p => p.EctsPerCourse);
        var progress = await _poolProgressCalculator.CalculateAsync(
            student.Id, poolIds, poolEctsPerCourse, ct);

        var electivePoolSlots = curriculumPools.Select(p =>
        {
            var pr = progress[p.Id];
            var isFulfilled =
                (p.RequiredCourseCount == 0 || pr.CompletedCourseCount >= p.RequiredCourseCount)
             && (p.RequiredEcts == 0 || pr.EarnedEcts >= p.RequiredEcts);

            return new CatalogPoolSlotDto(
                PoolId:               p.Id,
                GroupCode:            p.ElectiveGroup.Code,
                GroupName:            p.ElectiveGroup.Name,
                SlotName:             p.SlotName,
                SuggestedSemesterStart: p.SuggestedSemesterStart,
                SuggestedSemesterEnd:   p.SuggestedSemesterEnd,
                RequiredCourseCount:  p.RequiredCourseCount,
                CompletedCourseCount: pr.CompletedCourseCount,
                RequiredEcts:         p.RequiredEcts,
                EarnedEcts:           pr.EarnedEcts,
                EctsPerCourse:        p.EctsPerCourse,
                IsFulfilled:          isFulfilled);
        }).ToList();

        // 12. SelectedCourses — current-term registrations (Draft/PendingAdvisorApproval/Active/Rejected).
        //     Dropped and Withdrawn are excluded (rule #24).
        var poolGroupCodeByPoolId = curriculumPools.ToDictionary(p => p.Id, p => p.ElectiveGroup.Code);
        // SuggestedSemester lookup from already-loaded curriculum courses (no extra DB round-trip).
        var sugSemesterByCourseId = allCurriculumCourses.ToDictionary(cc => cc.CourseId, cc => cc.SuggestedSemester);

        var currentTermRegs = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.OpeningReasonSnapshot)
            .Where(cr => cr.StudentId == student.Id
                && cr.CourseSection.TermId == term.Id
                && (cr.Status == RegistrationStatus.Draft
                    || cr.Status == RegistrationStatus.PendingAdvisorApproval
                    || cr.Status == RegistrationStatus.Active
                    || cr.Status == RegistrationStatus.Rejected))
            .AsNoTracking()
            .ToListAsync(ct);

        var selectedCourses = currentTermRegs.Select(cr =>
        {
            var course = cr.CourseSection.Course;
            var cid = course.Id;
            var isCompulsory = compulsoryMap.TryGetValue(cid, out var comp) && comp;
            var sugSemester = sugSemesterByCourseId.GetValueOrDefault(cid, 0);
            var ects = cr.EctsSnapshot ?? course.Ects; // snapshot-first (#22)
            string? electiveGroupCode = cr.AppliedElectivePoolId.HasValue
                && poolGroupCodeByPoolId.TryGetValue(cr.AppliedElectivePoolId.Value, out var egCode)
                ? egCode
                : null;

            return new SelectedCourseDto(
                RegistrationId:    cr.Id,
                CourseSectionId:   cr.CourseSectionId,
                CourseId:          cid,
                CourseCode:        course.Code,
                CourseName:        course.Name,
                SectionNumber:     cr.CourseSection.SectionNumber,
                IsCompulsory:      isCompulsory,
                TheoryHours:       course.TheoryHours,
                LabHours:          course.LabHours,
                Ects:              ects,
                NationalCredit:    cr.CreditSnapshot ?? course.NationalCredit, // snapshot-first
                SuggestedSemester: sugSemester,
                TakeType:          failedCourseIds.Contains(cid) ? "Alttan" : "IlkDefa",
                Status:            cr.Status.ToString(),
                OpeningReasonName: cr.OpeningReasonSnapshot?.Name,
                ElectiveGroupCode: electiveGroupCode);
        }).ToList();

        return new StudentRegistrationCatalogDto(
            Student:          header,
            Term:             termInfo,
            ClassYears:       classYears,
            RetakeCourses:    retakeCourses,
            ElectivePoolSlots: electivePoolSlots,
            SelectedCourses:  selectedCourses);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Private helpers — ported faithfully from CourseController.BuildStudentCatalogAsync
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the curriculum tree: classYear → semester(Guz/Bahar) → course rows.
    /// Also returns failedCourseIds, compulsoryMap and curriculumPools for reuse in BuildAsync.
    /// Ported from CourseController.BuildStudentCatalogAsync lines 221-492.
    /// HasOpenSections is computed via IRegistrationGate per-section (single batch load).
    /// </summary>
    private async Task<BuildClassYearsResult> BuildClassYearsAsync(
        StudentEntity student,
        StudentProgramEntity activeProgram,
        AcademicTerm term,
        CancellationToken ct)
    {
        // ── Step 1: Load curriculum courses (with Course nav) ─────────────
        // Ported from controller lines 221-227
        var curriculumCourses = await _context.CurriculumCourses
            .Include(cc => cc.Course)
            .Where(cc => cc.CurriculumId == activeProgram.CurriculumId)
            .AsNoTracking()
            .ToListAsync(ct);

        // ── Step 2: Pool slots — courseId → applicable slot list ──────────
        // Loaded unconditionally so BuildAsync can build ElectivePoolSlots even when curriculum has no courses.
        // Ported from controller lines 229-259
        var curriculumPools = await _context.CurriculumElectivePools
            .AsNoTracking()
            .Where(p => p.CurriculumId == activeProgram.CurriculumId)
            .Include(p => p.ElectiveGroup)
                .ThenInclude(eg => eg.Courses)
            .ToListAsync(ct);

        if (curriculumCourses.Count == 0)
            return new BuildClassYearsResult(
                ClassYears:       Array.Empty<CatalogClassYearDto>(),
                FlatRows:         Array.Empty<CatalogRowBuild>(),
                CompulsoryMap:    new Dictionary<int, bool>(),
                CurriculumPools:  curriculumPools,
                CurriculumCourses: curriculumCourses);

        var compulsoryMap = curriculumCourses.ToDictionary(cc => cc.CourseId, cc => cc.IsCompulsory);

        var courseToPoolSlots = new Dictionary<int, List<ApplicablePoolSlotRefDto>>();
        foreach (var pool in curriculumPools)
        {
            var slotRef = new ApplicablePoolSlotRefDto(
                PoolId:       pool.Id,
                SlotName:     pool.SlotName,
                GroupCode:    pool.ElectiveGroup.Code,
                GroupName:    pool.ElectiveGroup.Name,
                EctsPerCourse: pool.EctsPerCourse);

            foreach (var groupCourse in pool.ElectiveGroup.Courses)
            {
                if (!courseToPoolSlots.TryGetValue(groupCourse.CourseId, out var list))
                {
                    list = new List<ApplicablePoolSlotRefDto>();
                    courseToPoolSlots[groupCourse.CourseId] = list;
                }
                list.Add(slotRef);
            }
        }

        // ── Step 3: Past registrations → grade maps ───────────────────────
        // Ported from controller lines 261-296
        var pastRegistrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
            .Where(cr => cr.StudentId == student.Id
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Dropped)
            .AsNoTracking()
            .ToListAsync(ct);

        // Latest grade per course (most recent registration with a non-withdrawn grade)
        var courseGradeMap = new Dictionary<int, (GradeCode grade, string gradeStr)>();
        var failedCourseIds = new HashSet<int>();

        // Also track attempt count per course (for PreviousAttemptInfo)
        var attemptCountMap = new Dictionary<int, int>();

        foreach (var group in pastRegistrations.GroupBy(r => r.CourseSection.CourseId))
        {
            var courseId = group.Key;

            // Count graded (non-withdrawn) attempts
            var gradedAttempts = group
                .Where(r => r.Grade != null && r.Status != RegistrationStatus.Withdrawn)
                .ToList();
            if (gradedAttempts.Count > 0)
                attemptCountMap[courseId] = gradedAttempts.Count;

            var latestWithGrade = gradedAttempts
                .OrderByDescending(r => r.Id)
                .FirstOrDefault();

            if (latestWithGrade?.Grade == null) continue;

            var grade = latestWithGrade.Grade.Value;
            courseGradeMap[courseId] = (grade, grade.ToString());

            if (RegistrationRulePolicy.IsFailedGrade(grade))
                failedCourseIds.Add(courseId);
        }

        // Passed courses = latest grade that is NOT FF/NA/U/W/I
        var passedCourseIds = courseGradeMap
            .Where(kv => RegistrationRulePolicy.IsPassedGrade(kv.Value.grade))
            .Select(kv => kv.Key)
            .ToHashSet();

        // ── Step 4: Prerequisites & equivalencies ─────────────────────────
        // Ported from controller lines 298-305
        var prereqMap = (await _context.CoursePrerequisites.AsNoTracking().ToListAsync(ct))
            .GroupBy(p => p.CourseId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.RequiredCourseId).ToHashSet());

        // Önkoşul derslerinin kodları (tooltip'te gerçek kod gösterimi için; başka bölümden olabilir).
        var prereqCourseIds = prereqMap.Values.SelectMany(ids => ids).Distinct().ToList();
        var prereqCodeById = prereqCourseIds.Count == 0
            ? new Dictionary<int, string>()
            : await _context.Courses.AsNoTracking()
                .Where(c => prereqCourseIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Code, ct);

        var equivMap = (await _context.CourseEquivalencies.AsNoTracking().ToListAsync(ct))
            .GroupBy(e => e.CourseId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.EquivalentCourseId).ToHashSet());

        // Intibak (PreviousCourseId) map for prerequisite coverage
        var previousMap = PreviousCourseResolver.BuildMap(curriculumCourses);

        // ── Step 5: Active-term registrations → ActiveRegistrationStatus ──
        // Current term registrations in demand states (Draft/PendingAdvisor/Active)
        var currentTermSectionIds = await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.TermId == term.Id)
            .Select(cs => new { cs.Id, cs.CourseId })
            .ToListAsync(ct);

        var termSectionByCourseId = currentTermSectionIds
            .GroupBy(cs => cs.CourseId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToHashSet());

        var activeRegCourseIds = new HashSet<int>();
        if (currentTermSectionIds.Count > 0)
        {
            var allTermSectionIds = currentTermSectionIds.Select(x => x.Id).ToHashSet();
            var activeRegs = await _context.CourseRegistrations
                .AsNoTracking()
                .Where(cr => cr.StudentId == student.Id
                    && allTermSectionIds.Contains(cr.CourseSectionId)
                    && (cr.Status == RegistrationStatus.Draft
                        || cr.Status == RegistrationStatus.PendingAdvisorApproval
                        || cr.Status == RegistrationStatus.Active))
                .Select(cr => cr.CourseSectionId)
                .ToListAsync(ct);

            // Map section → course
            var sectionToCourse = currentTermSectionIds.ToDictionary(x => x.Id, x => x.CourseId);
            foreach (var sid in activeRegs)
                if (sectionToCourse.TryGetValue(sid, out var cid))
                    activeRegCourseIds.Add(cid);
        }

        // ── Step 5b: HasOpenSections — yalnızca Active şubeleri tek seferde yükle, gate'i TOPLU değerlendir ─
        // Status==Active DB'de filtrelenir (Draft/Cancelled gizli); gate (fakülte onayı/suspend) tek
        // toplu sorguda değerlendirilir (N+1 yok). AsNoTracking — salt okuma.
        var curriculumCourseIds = curriculumCourses.Select(cc => cc.CourseId).ToList();
        var activeSections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Where(cs => cs.TermId == term.Id
                && curriculumCourseIds.Contains(cs.CourseId)
                && cs.Status == CourseSectionStatus.Active)
            .ToListAsync(ct);

        var coursesWithOpenSection = new HashSet<int>();
        var gateResults = await _registrationGate.EvaluateSectionsAsync(activeSections, ct);
        foreach (var section in activeSections)
        {
            if (gateResults[section.Id].IsOpen)
                coursesWithOpenSection.Add(section.CourseId);
        }

        // ── Step 6: Build course rows ─────────────────────────────────────
        // Single construction path — rows are shared by the tree AND RetakeCourses.
        var rows = new List<CatalogRowBuild>();

        foreach (var cc in curriculumCourses)
        {
            var cid = cc.CourseId;
            var course = cc.Course;
            var sugSemester = cc.SuggestedSemester;

            // Class-year + parity (Guz/Bahar)
            // If SuggestedSemester==0 → derive year from first digit of course Code
            int rowClassYear;
            string rowParity;
            if (sugSemester > 0)
            {
                rowClassYear = (sugSemester - 1) / 2 + 1;
                rowParity = sugSemester % 2 == 1 ? "Guz" : "Bahar";
            }
            else
            {
                // Fallback: first digit of code (memory project_course_code_year_rule)
                rowClassYear = CourseCodeHelper.DeriveClassYearFromCode(course.Code);
                rowParity = "Guz";
            }

            // TakeStatus — ported from controller lines 413-429
            string takeStatus;
            string? previousAttemptInfo = null;

            if (failedCourseIds.Contains(cid))
            {
                takeStatus = "Alttan";
                if (courseGradeMap.TryGetValue(cid, out var fg))
                {
                    var count = attemptCountMap.GetValueOrDefault(cid, 1);
                    previousAttemptInfo = $"{count}.Tekrar ({fg.gradeStr})";
                }
            }
            else if (courseGradeMap.TryGetValue(cid, out var pg))
            {
                takeStatus = "OncedenAlindi";
                var count = attemptCountMap.GetValueOrDefault(cid, 1);
                previousAttemptInfo = $"{count}.Tekrar ({pg.gradeStr})";
            }
            else
            {
                takeStatus = "IlkDefa";
            }

            // PrerequisitesMet — ported from controller lines 441-451
            // Skip check if student already passed (directly or via intibak)
            var coveredByPrevious = PreviousCourseResolver.IsCovered(cid, passedCourseIds, previousMap);
            bool prereqsMet = true;
            var missingPrerequisites = new List<string>(); // karşılanmamış önkoşul ders kodları
            if (!coveredByPrevious && prereqMap.TryGetValue(cid, out var requiredIds))
            {
                foreach (var reqId in requiredIds)
                {
                    // Direct pass OR equivalency OR intibak coverage
                    bool passed = passedCourseIds.Contains(reqId);
                    if (!passed && equivMap.TryGetValue(reqId, out var equivIds))
                        passed = equivIds.Any(eid => passedCourseIds.Contains(eid));
                    if (!passed && previousMap.TryGetValue(reqId, out var prevReqId))
                        passed = passedCourseIds.Contains(prevReqId);
                    if (!passed)
                    {
                        prereqsMet = false;
                        missingPrerequisites.Add(prereqCodeById.GetValueOrDefault(reqId, $"#{reqId}"));
                    }
                }
            }

            // ActiveRegistrationStatus
            string? activeRegStatus = activeRegCourseIds.Contains(cid) ? "Kayıtlı" : null;

            // Pool slots for this course
            IReadOnlyList<ApplicablePoolSlotRefDto> poolSlots =
                courseToPoolSlots.TryGetValue(cid, out var slots)
                    ? slots
                    : Array.Empty<ApplicablePoolSlotRefDto>();

            var row = new CatalogCourseRowDto(
                CourseId:              cid,
                Code:                  course.Code,
                Name:                  course.Name,
                IsCompulsory:          compulsoryMap.TryGetValue(cid, out var isComp) && isComp,
                TheoryHours:           course.TheoryHours,
                LabHours:              course.LabHours,
                NationalCredit:        course.NationalCredit,
                Ects:                  course.Ects,
                SuggestedSemester:     sugSemester,
                ClassYear:             rowClassYear,
                TakeStatus:            takeStatus,
                PrerequisitesMet:      prereqsMet,
                MissingPrerequisites:  missingPrerequisites,
                PreviousAttemptInfo:   previousAttemptInfo,
                ActiveRegistrationStatus: activeRegStatus,
                HasOpenSections:       coursesWithOpenSection.Contains(cid),
                ApplicablePoolSlots:   poolSlots);

            rows.Add(new CatalogRowBuild(
                Row:               row,
                ClassYear:         rowClassYear,
                Parity:            rowParity,
                IsCurrentlyFailed: failedCourseIds.Contains(cid)));
        }

        // ── Step 7: Group into tree ───────────────────────────────────────
        var tree = rows
            .GroupBy(r => r.ClassYear)
            .OrderBy(g => g.Key)
            .Select(yearGroup =>
            {
                var semesters = yearGroup
                    .GroupBy(r => r.Parity)
                    // Guz (odd) before Bahar (even)
                    .OrderBy(g => g.Key == "Guz" ? 0 : 1)
                    .Select(parityGroup =>
                    {
                        var orderedCourses = parityGroup
                            .Select(r => r.Row)
                            .OrderBy(r => r.Code)
                            .ToList();

                        // SemesterNumber = smallest positive SuggestedSemester in the group;
                        // fall back to the first element's value (may be 0) if none are positive.
                        var positiveSemesters = orderedCourses
                            .Where(r => r.SuggestedSemester > 0)
                            .Select(r => r.SuggestedSemester)
                            .ToList();
                        var semNum = positiveSemesters.Count > 0
                            ? positiveSemesters.Min()
                            : orderedCourses.First().SuggestedSemester;

                        return new CatalogSemesterDto(
                            Parity:         parityGroup.Key,
                            SemesterNumber: semNum,
                            Courses:        orderedCourses);
                    })
                    .ToList();

                return new CatalogClassYearDto(
                    ClassYear:  yearGroup.Key,
                    Semesters:  semesters);
            })
            .ToList();

        return new BuildClassYearsResult(
            ClassYears:        tree,
            FlatRows:          rows,
            CompulsoryMap:     compulsoryMap,
            CurriculumPools:   curriculumPools,
            CurriculumCourses: curriculumCourses);
    }

}
