using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Time;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using StudentEntity = SIS.Domain.Entities.Student;
using StudentProgramEntity = SIS.Domain.Entities.StudentProgram;

namespace SIS.Infrastructure.Services;

/// <inheritdoc />
public sealed class StudentRegistrationCatalogService : IStudentRegistrationCatalogService
{
    private readonly AppDbContext _context;
    private readonly IEctsLimitResolver _ectsLimitResolver;
    private readonly IRegistrationGate _registrationGate;
    private readonly IElectivePoolProgressCalculator _poolProgressCalculator;
    private readonly IDateTimeProvider _clock;

    public StudentRegistrationCatalogService(
        AppDbContext context,
        IEctsLimitResolver ectsLimitResolver,
        IRegistrationGate registrationGate,
        IElectivePoolProgressCalculator poolProgressCalculator,
        IDateTimeProvider clock)
    {
        _context = context;
        _ectsLimitResolver = ectsLimitResolver;
        _registrationGate = registrationGate;
        _poolProgressCalculator = poolProgressCalculator;
        _clock = clock;
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

        // 8. Term info
        var termInfo = new RegistrationTermInfoDto(
            Id:                 term.Id,
            Name:               term.Name,
            Year:               term.Year,
            TermType:           term.TermType.ToString(),
            IsRegistrationOpen: term.IsRegistrationOpen(now),
            IsAddDropOpen:      term.IsAddDropOpen(now),
            HasAcademicCalendar: term.AcademicCalendar != null,
            RegistrationMessage: null);

        // 9. Build curriculum tree (classYears / semesters / course rows)
        var classYears = await BuildClassYearsAsync(student, activeProgram, term, ct);

        return new StudentRegistrationCatalogDto(
            Student:          header,
            Term:             termInfo,
            ClassYears:       classYears,
            RetakeCourses:    Array.Empty<CatalogCourseRowDto>(),
            ElectivePoolSlots: Array.Empty<CatalogPoolSlotDto>(),
            SelectedCourses:  Array.Empty<SelectedCourseDto>());
    }

    // ────────────────────────────────────────────────────────────────────────
    // Private helpers — ported faithfully from CourseController.BuildStudentCatalogAsync
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the curriculum tree: classYear → semester(Guz/Bahar) → course rows.
    /// Ported from CourseController.BuildStudentCatalogAsync lines 221-492.
    /// HasOpenSections is always false here (filled in task B5).
    /// </summary>
    private async Task<IReadOnlyList<CatalogClassYearDto>> BuildClassYearsAsync(
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

        if (curriculumCourses.Count == 0)
            return Array.Empty<CatalogClassYearDto>();

        var compulsoryMap = curriculumCourses.ToDictionary(cc => cc.CourseId, cc => cc.IsCompulsory);

        // ── Step 2: Pool slots — courseId → applicable slot list ──────────
        // Ported from controller lines 229-259
        var curriculumPools = await _context.CurriculumElectivePools
            .AsNoTracking()
            .Where(p => p.CurriculumId == activeProgram.CurriculumId)
            .Include(p => p.ElectiveGroup)
                .ThenInclude(eg => eg.Courses)
            .ToListAsync(ct);

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

        // ── Step 6: Build course rows ─────────────────────────────────────
        // Ported from controller lines 404-492 (TakeStatus, prereqsMet, curriculumType)
        var rows = new List<(CatalogCourseRowDto row, int classYear, string parity)>();

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
                rowClassYear = CourseCodeYearFallback(course.Code);
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
                    if (!passed) { prereqsMet = false; break; }
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
                NationalCredit:        course.NationalCredit,
                Ects:                  course.Ects,
                SuggestedSemester:     sugSemester,
                ClassYear:             rowClassYear,
                TakeStatus:            takeStatus,
                PrerequisitesMet:      prereqsMet,
                PreviousAttemptInfo:   previousAttemptInfo,
                ActiveRegistrationStatus: activeRegStatus,
                HasOpenSections:       false, // B5
                ApplicablePoolSlots:   poolSlots);

            rows.Add((row, rowClassYear, rowParity));
        }

        // ── Step 7: Group into tree ───────────────────────────────────────
        var tree = rows
            .GroupBy(r => r.classYear)
            .OrderBy(g => g.Key)
            .Select(yearGroup =>
            {
                var semesters = yearGroup
                    .GroupBy(r => r.parity)
                    // Guz (odd) before Bahar (even)
                    .OrderBy(g => g.Key == "Guz" ? 0 : 1)
                    .Select(parityGroup =>
                    {
                        var orderedCourses = parityGroup
                            .Select(r => r.row)
                            .OrderBy(r => r.Code)
                            .ToList();

                        // SemesterNumber = first SuggestedSemester in this group (or 0)
                        var semNum = orderedCourses.First().SuggestedSemester;

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

        return tree;
    }

    /// <summary>
    /// Derives class year from the first digit of a course code (1xx→1, 2xx→2, 3xx→3, 4xx→4).
    /// Falls back to 1 if the code does not start with a digit 1-4.
    /// Memory: project_course_code_year_rule.
    /// </summary>
    private static int CourseCodeYearFallback(string code)
    {
        foreach (var ch in code)
        {
            if (ch >= '1' && ch <= '4') return ch - '0';
            if (ch >= '0' && ch <= '9') break; // first digit not 1-4 → fallback to 1
        }
        return 1;
    }
}
