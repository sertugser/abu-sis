using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.CalendarGenerator.Services;
using SIS.Application.Common.DTOs;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudentController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IGpaCalculationService _gpaCalculationService;
    private readonly IAcademicCalendarRuleService _calendarRuleService;

    public StudentController(AppDbContext context, IGpaCalculationService gpaCalculationService, IAcademicCalendarRuleService calendarRuleService)
    {
        _context = context;
        _gpaCalculationService = gpaCalculationService;
        _calendarRuleService = calendarRuleService;
    }

    /// <summary>
    /// GET api/student/me — returns the authenticated student's profile info.
    /// </summary>
    [HttpGet("me")]
    [RequirePermission("menu.ana-sayfa.view")]
    public async Task<IActionResult> GetMe()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        var student = await _context.Students
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.Faculty)
            .Include(s => s.Advisor)
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (student == null)
            return NotFound(new { error = "Ogrenci bilgisi bulunamadi." });

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { error = "Kullanici bilgisi bulunamadi." });

        var activeProgram = student.StudentPrograms.FirstOrDefault(sp => sp.IsActive);

        // Class year: enrollment-date based, adjusted for frozen semesters
        var classYear = 1;
        if (activeProgram != null)
        {
            var now = DateTime.UtcNow;
            var currentAcademicYear = now.Month >= 9 ? now.Year : now.Year - 1;
            var enrollDate = activeProgram.EnrollmentDate;
            var enrollAcademicYear = enrollDate.Month >= 9 ? enrollDate.Year : enrollDate.Year - 1;
            var elapsedYears = currentAcademicYear - enrollAcademicYear + 1;
            var frozenYears = (int)Math.Floor(student.FrozenSemesterCount / 2.0);
            var maxYears = activeProgram.Program?.DegreeLevel == DegreeLevel.Associate ? 2 : 4;
            classYear = Math.Clamp(elapsedYears - frozenYears, 1, maxYears);
        }

        // ECTS limit based on CGPA
        var ectsLimit = student.Cgpa >= 2.50m ? 48 :
                        student.Cgpa >= 2.00m ? 42 : 30;

        // Advisor info
        string? advisorName = null;
        string? advisorTitle = null;
        if (student.Advisor != null)
        {
            var advisorUser = await _context.Users.FindAsync(student.Advisor.UserId);
            if (advisorUser != null)
            {
                advisorName = $"{advisorUser.FirstName} {advisorUser.LastName}";
                advisorTitle = student.Advisor.Title;
            }
        }

        var dto = new StudentInfoDto
        {
            Id = student.Id,
            StudentNumber = student.StudentNumber,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Cgpa = student.Cgpa,
            Sgpa = student.Sgpa,
            CompletedSemesters = student.CompletedSemesters,
            Status = student.Status.ToString(),
            IsScholarshipStudent = student.IsScholarshipStudent,
            HasFinancialBlock = student.HasFinancialBlock,
            ProgramName = activeProgram?.Program?.Name ?? string.Empty,
            DepartmentName = activeProgram?.Program?.Department?.Name ?? string.Empty,
            FacultyName = activeProgram?.Program?.Department?.Faculty?.Name ?? string.Empty,
            WithdrawalCount = student.WithdrawalCount,
            ProgramId = activeProgram?.ProgramId ?? 0,
            ClassYear = classYear,
            EctsLimit = ectsLimit,
            AdvisorName = advisorName,
            AdvisorTitle = advisorTitle
        };

        return Ok(dto);
    }

    /// <summary>
    /// GET api/student/curriculum-status — returns the student's curriculum progress.
    /// </summary>
    [HttpGet("curriculum-status")]
    [RequirePermission("menu.ders-kayit.view")]
    // Patch 2A: Permission kaldırıldı; data multi-role (sadece authenticated).
    public async Task<IActionResult> GetCurriculumStatus()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        var student = await _context.Students
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (student == null)
            return NotFound(new { error = "Ogrenci bilgisi bulunamadi." });

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { error = "Kullanici bilgisi bulunamadi." });

        var activeProgram = student.StudentPrograms.FirstOrDefault(sp => sp.IsActive);
        if (activeProgram == null)
            return NotFound(new { error = "Aktif program bulunamadi." });

        // 1. Get curriculum courses
        var curriculumCourses = await _context.CurriculumCourses
            .Include(cc => cc.Course)
            .Where(cc => cc.CurriculumId == activeProgram.CurriculumId && cc.SuggestedSemester > 0)
            .OrderBy(cc => cc.SuggestedSemester)
            .ThenBy(cc => cc.Course.Code)
            .ToListAsync();

        // 1b. Get curriculum elective pool slots (TE-1, TE-2, NDE-1, vs.) — ders gibi semester'lara dağıt
        var curriculumPools = await _context.CurriculumElectivePools
            .Include(p => p.ElectiveGroup)
            .Where(p => p.CurriculumId == activeProgram.CurriculumId && p.SuggestedSemesterStart > 0)
            .OrderBy(p => p.SuggestedSemesterStart)
            .ThenBy(p => p.SlotName)
            .ToListAsync();

        // 2. Get ALL student registrations (excluding dropped)
        var allRegistrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
            .Where(cr => cr.StudentId == student.Id
                && cr.Status != RegistrationStatus.Dropped)
            .ToListAsync();

        var gradeVisibilityByTerm = await BuildGradeVisibilityMapAsync(allRegistrations.Select(r => r.CourseSection.TermId));
        bool HasVisibleLetterGrade(CourseRegistration registration) =>
            registration.CourseSection.AreGradesPublished
            && registration.Grade != null
            && gradeVisibilityByTerm.GetValueOrDefault(registration.CourseSection.TermId, true);

        // 3. Build per-course best registration map
        // For each course: prefer passed > current (no grade) > failed
        var courseRegMap = new Dictionary<int, (SIS.Domain.Entities.CourseRegistration reg, string gradeStatus)>();

        foreach (var group in allRegistrations.GroupBy(r => r.CourseSection.CourseId))
        {
            var courseId = group.Key;

            // Find the best registration to display
            var passed = group
                .Where(r => HasVisibleLetterGrade(r) && r.Grade != GradeCode.FF && r.Grade != GradeCode.NA
                    && r.Grade != GradeCode.U && r.Grade != GradeCode.W && r.Grade != GradeCode.I)
                .OrderByDescending(r => r.Id)
                .FirstOrDefault();

            if (passed != null)
            {
                courseRegMap[courseId] = (passed, "passed");
                continue;
            }

            // Current semester (active, no grade)
            var current = group
                .Where(r => !HasVisibleLetterGrade(r) && (r.Status == RegistrationStatus.Active
                    || r.Status == RegistrationStatus.PendingAdvisorApproval
                    || r.Grade != null))
                .OrderByDescending(r => r.Id)
                .FirstOrDefault();

            if (current != null)
            {
                courseRegMap[courseId] = (current, "current");
                continue;
            }

            // Failed (latest)
            var failed = group
                .Where(HasVisibleLetterGrade)
                .OrderByDescending(r => r.Id)
                .FirstOrDefault();

            if (failed != null)
            {
                courseRegMap[courseId] = (failed, "failed");
            }
        }

        // 3b. Build per-pool best registration map (AppliedElectivePoolId üzerinden)
        // Öğrencinin bir slot için seçtiği ders → sağ tarafta "Alınan Ders" olarak görünür.
        var poolRegMap = new Dictionary<int, (CourseRegistration reg, string gradeStatus)>();
        foreach (var poolGroup in allRegistrations
            .Where(r => r.AppliedElectivePoolId.HasValue)
            .GroupBy(r => r.AppliedElectivePoolId!.Value))
        {
            var poolId = poolGroup.Key;

            var poolPassed = poolGroup
                .Where(r => HasVisibleLetterGrade(r) && r.Grade != GradeCode.FF && r.Grade != GradeCode.NA
                    && r.Grade != GradeCode.U && r.Grade != GradeCode.W && r.Grade != GradeCode.I)
                .OrderByDescending(r => r.Id)
                .FirstOrDefault();
            if (poolPassed != null)
            {
                poolRegMap[poolId] = (poolPassed, "passed");
                continue;
            }

            var poolCurrent = poolGroup
                .Where(r => !HasVisibleLetterGrade(r) && (r.Status == RegistrationStatus.Active
                    || r.Status == RegistrationStatus.PendingAdvisorApproval
                    || r.Grade != null))
                .OrderByDescending(r => r.Id)
                .FirstOrDefault();
            if (poolCurrent != null)
            {
                poolRegMap[poolId] = (poolCurrent, "current");
                continue;
            }

            var poolFailed = poolGroup
                .Where(HasVisibleLetterGrade)
                .OrderByDescending(r => r.Id)
                .FirstOrDefault();
            if (poolFailed != null)
            {
                poolRegMap[poolId] = (poolFailed, "failed");
            }
        }

        // 4. Passed course IDs for prerequisite checking
        var passedCourseIds = courseRegMap
            .Where(kv => kv.Value.gradeStatus == "passed")
            .Select(kv => kv.Key)
            .ToHashSet();

        // 5. Prerequisites
        var prereqMap = (await _context.CoursePrerequisites
            .Include(p => p.RequiredCourse)
            .ToListAsync())
            .GroupBy(p => p.CourseId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 6. Equivalencies
        var equivMap = (await _context.CourseEquivalencies.ToListAsync())
            .GroupBy(e => e.CourseId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.EquivalentCourseId).ToHashSet());

        // 7. Build semester groups — curriculum dersleri + pool slot'ları aynı dönemde
        var allSemesterNumbers = curriculumCourses.Select(cc => cc.SuggestedSemester)
            .Concat(curriculumPools.Select(p => p.SuggestedSemesterStart))
            .Where(s => s > 0)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        var coursesBySemester = curriculumCourses
            .GroupBy(cc => cc.SuggestedSemester)
            .ToDictionary(g => g.Key, g => g.ToList());
        var poolsBySemester = curriculumPools
            .GroupBy(p => p.SuggestedSemesterStart)
            .ToDictionary(g => g.Key, g => g.ToList());

        var semesterGroups = allSemesterNumbers
            .Select(semNum =>
            {
                var classYear = (semNum - 1) / 2 + 1;
                var isGuz = semNum % 2 == 1;
                var label = $"{classYear}. Sinif {(isGuz ? "Guz" : "Bahar")}";

                var coursesInSem = coursesBySemester.GetValueOrDefault(semNum) ?? new List<CurriculumCourse>();
                var poolsInSem = poolsBySemester.GetValueOrDefault(semNum) ?? new List<CurriculumElectivePool>();

                var courses = coursesInSem.Select(cc =>
                {
                    var course = cc.Course;
                    var cid = course.Id;

                    // Right side data
                    string? termLabel = null;
                    string? takenCode = null;
                    string? takenName = null;
                    bool? takenIsComp = null;
                    int? takenCredits = null;
                    int? takenEcts = null;
                    string? grade = null;
                    string? gradeStatus = null;

                    if (courseRegMap.TryGetValue(cid, out var regInfo))
                    {
                        var reg = regInfo.reg;
                        var section = reg.CourseSection;
                        var term = section.Term;
                        var takenCourse = section.Course;

                        // Term label: "23-24G" format
                        var y1 = term.Year % 100;
                        var y2 = (term.Year + 1) % 100;
                        var semChar = term.TermType == TermType.Fall ? "G" : "B";
                        termLabel = $"{y1:D2}-{y2:D2}{semChar}";

                        takenCode = takenCourse.Code;
                        takenName = takenCourse.Name;
                        takenIsComp = cc.IsCompulsory;
                        takenCredits = takenCourse.TheoryHours + takenCourse.LabHours;
                        takenEcts = takenCourse.Ects;
                        gradeStatus = regInfo.gradeStatus;
                        grade = regInfo.gradeStatus == "current" ? "--" : reg.Grade?.ToString();
                    }

                    // Prerequisites
                    var prereqCodes = new List<string>();
                    var prereqsMet = true;
                    if (prereqMap.TryGetValue(cid, out var prereqs))
                    {
                        foreach (var p in prereqs)
                        {
                            prereqCodes.Add(p.RequiredCourse.Code);
                            if (!passedCourseIds.Contains(cid))
                            {
                                var met = passedCourseIds.Contains(p.RequiredCourseId);
                                if (!met && equivMap.TryGetValue(p.RequiredCourseId, out var eqIds))
                                    met = eqIds.Any(eid => passedCourseIds.Contains(eid));
                                if (!met) prereqsMet = false;
                            }
                        }
                    }

                    return new CurriculumCourseStatusDto
                    {
                        CourseId = cid,
                        Code = course.Code,
                        Name = course.Name,
                        IsCompulsory = cc.IsCompulsory,
                        Credits = course.TheoryHours + course.LabHours,
                        Ects = course.Ects,
                        TakenTermLabel = termLabel,
                        TakenCourseCode = takenCode,
                        TakenCourseName = takenName,
                        TakenIsCompulsory = takenIsComp,
                        TakenCredits = takenCredits,
                        TakenEcts = takenEcts,
                        Grade = grade,
                        GradeStatus = gradeStatus,
                        Prerequisites = prereqCodes,
                        PrerequisitesMet = prereqsMet,
                    };
                }).ToList();

                // Pool slot satırları — ders gibi listede, sağ tarafta öğrencinin seçtiği ders.
                var poolEntries = poolsInSem.Select(pool =>
                {
                    string? termLabel = null;
                    string? takenCode = null;
                    string? takenName = null;
                    bool? takenIsComp = null;
                    int? takenCredits = null;
                    int? takenEcts = null;
                    string? grade = null;
                    string? gradeStatus = null;

                    if (poolRegMap.TryGetValue(pool.Id, out var poolRegInfo))
                    {
                        var reg = poolRegInfo.reg;
                        var section = reg.CourseSection;
                        var term = section.Term;
                        var takenCourse = section.Course;

                        var y1 = term.Year % 100;
                        var y2 = (term.Year + 1) % 100;
                        var semChar = term.TermType == TermType.Fall ? "G" : "B";
                        termLabel = $"{y1:D2}-{y2:D2}{semChar}";

                        takenCode = takenCourse.Code;
                        takenName = takenCourse.Name;
                        takenIsComp = false; // pool slot → seçmeli
                        takenCredits = takenCourse.TheoryHours + takenCourse.LabHours;
                        takenEcts = reg.EctsSnapshot ?? pool.EctsPerCourse ?? takenCourse.Ects;
                        gradeStatus = poolRegInfo.gradeStatus;
                        grade = poolRegInfo.gradeStatus == "current" ? "--" : reg.Grade?.ToString();
                    }

                    // Müfredat ders adı: slot'un tam adı = ElectiveGroup.Name + slot numarası.
                    // Örn: ElectiveGroup.Name = "Technical Elective", SlotName = "TE-1"
                    //   → "Technical Elective 1"
                    // Slot adının "PREFIX-N" formatında olduğunu varsayar; değilse sadece
                    // grup adı kullanılır.
                    var slotNumberToken = pool.SlotName.Split('-').LastOrDefault();
                    var hasSlotNumber = int.TryParse(slotNumberToken, out var slotNumber);
                    var poolDisplayName = hasSlotNumber
                        ? $"{pool.ElectiveGroup.Name} {slotNumber}"
                        : pool.ElectiveGroup.Name;

                    return new CurriculumCourseStatusDto
                    {
                        // Pool slot'ları için CourseId çakışmaması adına negative: -pool.Id
                        CourseId = -pool.Id,
                        Code = string.IsNullOrEmpty(pool.SlotName) ? pool.ElectiveGroup.Code : pool.SlotName,
                        Name = poolDisplayName,
                        IsCompulsory = false,
                        Credits = takenCredits ?? 0,
                        Ects = takenEcts ?? pool.EctsPerCourse ?? pool.RequiredEcts,
                        TakenTermLabel = termLabel,
                        TakenCourseCode = takenCode,
                        TakenCourseName = takenName,
                        TakenIsCompulsory = takenIsComp,
                        TakenCredits = takenCredits,
                        TakenEcts = takenEcts,
                        Grade = grade,
                        GradeStatus = gradeStatus,
                        Prerequisites = new List<string>(),
                        PrerequisitesMet = true,
                    };
                }).ToList();

                var allEntries = courses.Concat(poolEntries).ToList();

                return new CurriculumSemesterDto
                {
                    SemesterNumber = semNum,
                    Label = label,
                    Courses = allEntries,
                    TotalCredits = allEntries.Sum(c => c.Credits),
                    TotalEcts = allEntries.Sum(c => c.Ects),
                    CourseCount = allEntries.Count,
                    CompulsoryCount = allEntries.Count(c => c.IsCompulsory),
                    ElectiveCount = allEntries.Count(c => !c.IsCompulsory),
                };
            }).ToList();

        // 8. Summary stats — all taken courses (curriculum + non-curriculum)
        var allTakenCredits = 0;
        var allTakenEcts = 0;
        var passedCreditsSum = 0;
        var passedEctsSum = 0;

        foreach (var kv in courseRegMap)
        {
            var reg = kv.Value.reg;
            var course = reg.CourseSection.Course;
            var credits = course.TheoryHours + course.LabHours;

            if (kv.Value.gradeStatus != "current")
            {
                allTakenCredits += credits;
                allTakenEcts += course.Ects;
            }

            if (kv.Value.gradeStatus == "passed")
            {
                passedCreditsSum += credits;
                passedEctsSum += course.Ects;
            }
        }

        var dto = new CurriculumStatusDto
        {
            StudentName = $"{user.FirstName} {user.LastName}",
            ProgramName = activeProgram.Program?.Name ?? string.Empty,
            CurriculumVersion = (await _context.Curricula.FindAsync(activeProgram.CurriculumId))?.Version ?? string.Empty,
            TotalCredits = allTakenCredits,
            TotalEcts = allTakenEcts,
            Cgpa = student.Cgpa,
            PassedCredits = passedCreditsSum,
            PassedEcts = passedEctsSum,
            Semesters = semesterGroups,
        };

        return Ok(dto);
    }

    /// <summary>
    /// GET api/student/term-gpa-history — returns the student's term-based ANO/AGNO timeline.
    /// </summary>
    [HttpGet("term-gpa-history")]
    [RequirePermission("menu.donem-ortalamalari.view")]
    public async Task<IActionResult> GetTermGpaHistory()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        var student = await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (student == null)
            return NotFound(new { error = "Ogrenci bilgisi bulunamadi." });

        var history = await _gpaCalculationService.GetTermGpaHistoryAsync(student.Id);
        return Ok(history);
    }

    /// <summary>
    /// GET api/student/registrations/{registrationId}/grade-statistics - returns exam and grade statistics for the student's own registration.
    /// </summary>
    [HttpGet("registrations/{registrationId:int}/grade-statistics")]
    [RequirePermission("menu.alinan-dersler.statistics")]
    public async Task<IActionResult> GetGradeStatistics(int registrationId, [FromQuery] bool commonEvaluation = false)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.Faculty)
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (student == null)
            return NotFound(new { error = "Ogrenci bilgisi bulunamadi." });

        var registration = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Instructor)
            .FirstOrDefaultAsync(cr => cr.Id == registrationId && cr.StudentId == student.Id);

        if (registration == null)
            return NotFound(new { error = "Kayit bulunamadi." });

        var section = registration.CourseSection;
        var course = section.Course;
        var term = section.Term;

        var components = await _context.GradeComponents
            .AsNoTracking()
            .Where(component => component.CourseId == section.CourseId && component.AcademicTermId == section.TermId)
            .OrderBy(component => component.Order)
            .ThenBy(component => component.Id)
            .ToListAsync();

        var sectionRegistrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(item => item.CourseSectionId == section.Id
                && item.Status != RegistrationStatus.Draft
                && item.Status != RegistrationStatus.Dropped)
            .ToListAsync();

        var statisticRegistrations = commonEvaluation
            ? await _context.CourseRegistrations
                .AsNoTracking()
                .Where(item => item.CourseSection.CourseId == section.CourseId
                    && item.CourseSection.TermId == section.TermId
                    && item.Status != RegistrationStatus.Draft
                    && item.Status != RegistrationStatus.Dropped)
                .ToListAsync()
            : sectionRegistrations;

        var registrationIds = statisticRegistrations.Select(item => item.Id).ToList();
        var allGrades = registrationIds.Count == 0
            ? new List<StudentGrade>()
            : await _context.StudentGrades
                .AsNoTracking()
                .Where(grade => registrationIds.Contains(grade.CourseRegistrationId))
                .ToListAsync();

        var gradesByRegistration = allGrades
            .GroupBy(grade => grade.CourseRegistrationId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var totalComponentWeight = components.Sum(component => component.Weight);

        decimal? CalculateWeightedAverage(int courseRegistrationId, bool requireAllComponentScores = true)
        {
            if (components.Count == 0 || !gradesByRegistration.TryGetValue(courseRegistrationId, out var studentGrades))
                return null;

            if (requireAllComponentScores)
            {
                var hasAllScores = totalComponentWeight == 100m
                    && components.All(component => studentGrades.Any(grade =>
                        grade.GradeComponentId == component.Id && grade.Score != null));

                if (!hasAllScores)
                    return null;
            }

            decimal totalWeightedScore = 0;
            decimal totalWeight = 0;

            foreach (var component in components)
            {
                var score = studentGrades
                    .FirstOrDefault(grade => grade.GradeComponentId == component.Id)
                    ?.Score;

                if (score == null)
                    continue;

                totalWeightedScore += score.Value * component.Weight / 100m;
                totalWeight += component.Weight;
            }

            return totalWeight > 0
                ? Math.Round(totalWeightedScore * 100m / totalWeight, 2, MidpointRounding.AwayFromZero)
                : null;
        }

        var weightedAverages = statisticRegistrations
            .Select(item => CalculateWeightedAverage(item.Id))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        var criteria = await _context.GradingCriteria
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.CourseId == section.CourseId && item.AcademicTermId == section.TermId);
        var hasGradingCriteria = criteria != null;

        var gradeCounts = statisticRegistrations
            .Where(item => item.Grade != null)
            .GroupBy(item => item.Grade!.Value.ToString())
            .ToDictionary(group => group.Key, group => group.Count());

        var instructorName = string.Empty;
        if (section.Instructor != null)
        {
            var instructorUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.Id == section.Instructor.UserId);

            if (instructorUser != null)
                instructorName = $"{section.Instructor.Title} {instructorUser.FirstName} {instructorUser.LastName}".Trim();
        }

        var activeProgram = student.StudentPrograms.FirstOrDefault(program => program.IsActive);
        var programName = activeProgram?.Program?.Name ?? string.Empty;
        var facultyName = activeProgram?.Program?.Department?.Faculty?.Name ?? string.Empty;
        var ownGrades = gradesByRegistration.GetValueOrDefault(registration.Id, new List<StudentGrade>());
        var hasAnyComponentScore = ownGrades.Any(grade => grade.Score != null);
        var hasAllComponentScores = components.Count > 0
            && components.All(component => ownGrades.Any(grade =>
                grade.GradeComponentId == component.Id && grade.Score != null));
        var hasFinalizedGrade = section.IsFinalized
            && section.AreGradesPublished
            && hasGradingCriteria
            && registration.Grade != null
            && hasAllComponentScores;
        var isResultFinalized = await _calendarRuleService.CanStudentSeeLetterGradeAsync(
            section.TermId,
            hasFinalizedGrade,
            registration.Grade != null,
            DateTime.UtcNow);
        var studentWeightedAverage = hasFinalizedGrade ? CalculateWeightedAverage(registration.Id) : null;

        var componentStatistics = components
            .Select(component =>
            {
                var componentScores = allGrades
                    .Where(grade => grade.GradeComponentId == component.Id && grade.Score != null)
                    .Select(grade => grade.Score!.Value)
                    .ToList();
                var studentScore = gradesByRegistration.TryGetValue(registration.Id, out var ownGrades)
                    ? ownGrades.FirstOrDefault(grade => grade.GradeComponentId == component.Id)?.Score
                    : null;

                return new StudentComponentStatisticsDto
                {
                    ComponentId = component.Id,
                    Name = component.Name,
                    Weight = component.Weight,
                    TotalStudentCount = statisticRegistrations.Count,
                    EnteredStudentCount = componentScores.Count,
                    MissingStudentCount = statisticRegistrations.Count - componentScores.Count,
                    AverageScore = componentScores.Count > 0
                        ? Math.Round(componentScores.Average(), 2, MidpointRounding.AwayFromZero)
                        : null,
                    StudentScore = hasFinalizedGrade ? studentScore : null
                };
            })
            .ToList();

        var dto = new StudentGradeStatisticsDto
        {
            RegistrationId = registration.Id,
            CourseSectionId = section.Id,
            TermName = term.Name,
            FacultyProgram = string.IsNullOrWhiteSpace(facultyName)
                ? programName
                : $"{facultyName} - {programName}",
            InstructorName = instructorName,
            CourseCode = course.Code,
            CourseName = course.Name,
            SectionNumber = section.SectionNumber,
            StudentLetterGrade = isResultFinalized ? registration.Grade?.ToString() : null,
            StudentWeightedAverage = studentWeightedAverage,
            HasGradingCriteria = hasGradingCriteria,
            HasAnyComponentScore = hasAnyComponentScore,
            HasAllComponentScores = hasAllComponentScores,
            IsResultFinalized = isResultFinalized,
            IsCommonEvaluation = commonEvaluation,
            Result = new StudentResultInfoDto
            {
                Status = isResultFinalized ? "Sonuçlandırılmış" : "Sonuçlandırılmamış",
                StatusDate = isResultFinalized ? section.UpdatedAt ?? registration.UpdatedAt : null,
                EvaluationMethod = hasGradingCriteria
                    ? commonEvaluation ? "Mutlak - Ortak Değerlendirme" : "Mutlak"
                    : string.Empty
            },
            GradeRanges = hasGradingCriteria
                ? BuildGradeRanges(criteria!, isResultFinalized ? gradeCounts : new Dictionary<string, int>())
                : new List<StudentGradeRangeDto>(),
            Components = componentStatistics,
            Summary = new StudentSectionStatisticsSummaryDto
            {
                TotalStudentCount = statisticRegistrations.Count,
                GradedStudentCount = isResultFinalized
                    ? statisticRegistrations.Count(item => item.Grade != null)
                    : 0,
                ClassAverage = weightedAverages.Count > 0
                    ? Math.Round(weightedAverages.Average(), 2, MidpointRounding.AwayFromZero)
                    : null
            }
        };

        return Ok(dto);
    }

    /// <summary>
    /// GET api/student/transcript — returns the authenticated student's transcript document data.
    /// </summary>
    [HttpGet("transcript")]
    [RequirePermission("menu.transkript.view")]
    public async Task<IActionResult> GetTranscript()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Kullanici bilgisi bulunamadi." });

        var student = await _context.Students
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.Faculty)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (student == null)
            return NotFound(new { error = "Ogrenci bilgisi bulunamadi." });

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound(new { error = "Kullanici bilgisi bulunamadi." });

        var activeProgram = student.StudentPrograms.FirstOrDefault(sp => sp.IsActive);
        if (activeProgram == null)
            return NotFound(new { error = "Aktif program bulunamadi." });

        var registrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
            .Where(cr => cr.StudentId == student.Id
                && cr.Grade != null
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Dropped)
            .ToListAsync();

        var transcriptGradeVisibility = await BuildGradeVisibilityMapAsync(registrations.Select(r => r.CourseSection.TermId));
        registrations = registrations
            .Where(registration => registration.CourseSection.AreGradesPublished
                && transcriptGradeVisibility.GetValueOrDefault(registration.CourseSection.TermId, true))
            .ToList();

        var registrationIds = registrations.Select(registration => registration.Id).ToList();
        var courseIds = registrations
            .Select(registration => registration.CourseSection.CourseId)
            .Distinct()
            .ToList();
        var termIds = registrations
            .Select(registration => registration.CourseSection.TermId)
            .Distinct()
            .ToList();
        var equivalentCourseIdsByCourseId = courseIds.Count == 0
            ? new Dictionary<int, HashSet<int>>()
            : (await _context.CourseEquivalencies
                .AsNoTracking()
                .Where(equivalency => courseIds.Contains(equivalency.CourseId))
                .ToListAsync())
                .GroupBy(equivalency => equivalency.CourseId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(equivalency => equivalency.EquivalentCourseId).ToHashSet());
        var courseChainIdByCourseId = BuildCourseChainIdMap(courseIds, equivalentCourseIdsByCourseId);
        var registrationMetadataById = registrations.ToDictionary(
            registration => registration.Id,
            registration => (registration.CourseSection.CourseId, registration.CourseSection.TermId));
        var gradeComponentStatsByCourseTerm = courseIds.Count == 0 || termIds.Count == 0
            ? new Dictionary<(int CourseId, int TermId), (decimal TotalWeight, int ComponentCount)>()
            : (await _context.GradeComponents
                .AsNoTracking()
                .Where(component => courseIds.Contains(component.CourseId) && termIds.Contains(component.AcademicTermId))
                .Select(component => new
                {
                    component.CourseId,
                    TermId = component.AcademicTermId,
                    component.Id,
                    component.Weight
                })
                .ToListAsync())
                .GroupBy(component => (component.CourseId, component.TermId))
                .ToDictionary(
                    group => group.Key,
                    group => (
                        TotalWeight: group.Sum(component => component.Weight),
                        ComponentCount: group.Select(component => component.Id).Distinct().Count()));
        var weightedAverageMap = registrationIds.Count == 0
            ? new Dictionary<int, decimal?>()
            : (await _context.StudentGrades
                .AsNoTracking()
                .Include(grade => grade.GradeComponent)
                .Where(grade => registrationIds.Contains(grade.CourseRegistrationId) && grade.Score != null)
                .ToListAsync())
                .GroupBy(grade => grade.CourseRegistrationId)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        if (!registrationMetadataById.TryGetValue(group.Key, out var registrationMetadata))
                            return (decimal?)null;

                        if (!gradeComponentStatsByCourseTerm.TryGetValue(
                                (registrationMetadata.CourseId, registrationMetadata.TermId),
                                out var componentStats)
                            || componentStats.TotalWeight <= 0
                            || componentStats.ComponentCount <= 0)
                            return (decimal?)null;

                        var gradedComponents = group
                            .Where(item => item.GradeComponent != null)
                            .GroupBy(item => item.GradeComponentId)
                            .Select(componentGroup =>
                            {
                                var grade = componentGroup.First();
                                return new
                                {
                                    Score = grade.Score!.Value,
                                    grade.GradeComponent.Weight
                                };
                            })
                            .ToList();

                        var actualWeight = gradedComponents.Sum(item => item.Weight);
                        if (gradedComponents.Count != componentStats.ComponentCount || actualWeight != componentStats.TotalWeight)
                            return (decimal?)null;

                        var totalWeightedScore = gradedComponents.Sum(item => item.Score * item.Weight / 100m);
                        return Math.Round(
                            totalWeightedScore * 100m / componentStats.TotalWeight,
                            2,
                            MidpointRounding.AwayFromZero);
                    });

        var repeatedChainCounts = registrations
            .GroupBy(registration => courseChainIdByCourseId.GetValueOrDefault(registration.CourseSection.CourseId, registration.CourseSection.CourseId))
            .ToDictionary(group => group.Key, group => group.Count());
        var latestGradesByCourse = new Dictionary<int, CourseRegistration>();
        var termGroups = registrations
            .GroupBy(cr => cr.CourseSection.TermId)
            .Select(group => new
            {
                Term = group.First().CourseSection.Term,
                Registrations = group
                    .OrderBy(cr => cr.CourseSection.Course.Code)
                    .ThenBy(cr => cr.Id)
                    .ToList()
            })
            .OrderBy(group => group.Term.TermStart)
            .ThenBy(group => group.Term.Id)
            .ToList();

        var transcriptTerms = new List<StudentTranscriptTermDto>(termGroups.Count);

        foreach (var termGroup in termGroups)
        {
            foreach (var registration in termGroup.Registrations
                .OrderBy(registration => registration.Id))
            {
                var chainId = courseChainIdByCourseId.GetValueOrDefault(
                    registration.CourseSection.CourseId,
                    registration.CourseSection.CourseId);
                latestGradesByCourse[chainId] = registration;
            }

            var courseDtos = termGroup.Registrations
                .Select(registration =>
                {
                    var course = registration.CourseSection.Course;
                    var chainId = courseChainIdByCourseId.GetValueOrDefault(course.Id, course.Id);
                    var credits = course.TheoryHours + course.LabHours;
                    var grade = registration.Grade!.Value;
                    var gradePoint = GradePointMap.GetPoint(grade);
                    var weightedEcts = gradePoint.HasValue
                        ? Math.Round(course.Ects * gradePoint.Value, 2, MidpointRounding.AwayFromZero)
                        : (decimal?)null;

                    return new StudentTranscriptCourseDto
                    {
                        RegistrationId = registration.Id,
                        CourseId = course.Id,
                        CourseCode = course.Code,
                        CourseName = course.Name,
                        Credits = credits,
                        Ects = course.Ects,
                        EquivalentCourseIds = equivalentCourseIdsByCourseId.TryGetValue(course.Id, out var equivalentIds)
                            ? equivalentIds.OrderBy(id => id).ToList()
                            : new List<int>(),
                        AverageScore = weightedAverageMap.GetValueOrDefault(registration.Id),
                        Grade = grade.ToString(),
                        Status = registration.Status.ToString(),
                        IsRepeated = registration.IsRepeated || repeatedChainCounts.GetValueOrDefault(chainId) > 1,
                        IsPassed = IsPassedGrade(grade),
                        CountsTowardsGpa = gradePoint.HasValue,
                        GradePoint = gradePoint,
                        WeightedEcts = weightedEcts
                    };
                })
                .ToList();

            var attemptedCredits = courseDtos.Sum(course => course.Credits);
            var attemptedEcts = courseDtos.Sum(course => course.Ects);
            var earnedCredits = courseDtos.Where(course => course.IsPassed).Sum(course => course.Credits);
            var earnedEcts = courseDtos.Where(course => course.IsPassed).Sum(course => course.Ects);
            var gpaEcts = courseDtos.Where(course => course.CountsTowardsGpa).Sum(course => course.Ects);
            var gpaPoints = courseDtos.Sum(course => course.WeightedEcts ?? 0m);

            var cumulativeRegistrations = latestGradesByCourse.Values.ToList();
            var cumulativeGpaEcts = cumulativeRegistrations.Sum(GetGpaEcts);
            var cumulativeGpaPoints = cumulativeRegistrations.Sum(GetWeightedEcts);

            transcriptTerms.Add(new StudentTranscriptTermDto
            {
                TermId = termGroup.Term.Id,
                TermName = termGroup.Term.Name,
                Year = termGroup.Term.Year,
                Semester = termGroup.Term.TermType.ToString(),
                AttemptedCredits = attemptedCredits,
                AttemptedEcts = attemptedEcts,
                EarnedCredits = earnedCredits,
                EarnedEcts = earnedEcts,
                GpaEcts = gpaEcts,
                GpaPoints = Math.Round(gpaPoints, 2, MidpointRounding.AwayFromZero),
                CumulativeGpaEcts = cumulativeGpaEcts,
                CumulativeGpaPoints = Math.Round(cumulativeGpaPoints, 2, MidpointRounding.AwayFromZero),
                Sgpa = CalculateAverage(termGroup.Registrations),
                Cgpa = CalculateAverage(cumulativeRegistrations),
                Courses = courseDtos
            });
        }

        var latestRegistrations = latestGradesByCourse.Values.ToList();
        var totalGpaEcts = latestRegistrations.Sum(GetGpaEcts);
        var totalGpaPoints = latestRegistrations.Sum(GetWeightedEcts);

        var transcript = new StudentTranscriptDto
        {
            StudentId = student.Id,
            StudentNumber = student.StudentNumber,
            FullName = $"{user.FirstName} {user.LastName}",
            FacultyName = activeProgram.Program?.Department?.Faculty?.Name ?? string.Empty,
            DepartmentName = activeProgram.Program?.Department?.Name ?? string.Empty,
            ProgramName = activeProgram.Program?.Name ?? string.Empty,
            StudentStatus = student.Status.ToString(),
            EnrollmentDate = activeProgram.EnrollmentDate,
            GeneratedAt = DateTime.UtcNow,
            CurrentCgpa = transcriptTerms.Count > 0 ? transcriptTerms[^1].Cgpa : student.Cgpa,
            TotalAttemptedCredits = latestRegistrations.Sum(reg => reg.CourseSection.Course.TheoryHours + reg.CourseSection.Course.LabHours),
            TotalAttemptedEcts = latestRegistrations.Sum(reg => reg.CourseSection.Course.Ects),
            TotalEarnedCredits = latestRegistrations.Where(reg => IsPassedGrade(reg.Grade!.Value)).Sum(reg => reg.CourseSection.Course.TheoryHours + reg.CourseSection.Course.LabHours),
            TotalEarnedEcts = latestRegistrations.Where(reg => IsPassedGrade(reg.Grade!.Value)).Sum(reg => reg.CourseSection.Course.Ects),
            TotalGpaEcts = totalGpaEcts,
            TotalGpaPoints = Math.Round(totalGpaPoints, 2, MidpointRounding.AwayFromZero),
            Terms = transcriptTerms
        };

        return Ok(transcript);
    }

    private async Task<Dictionary<int, bool>> BuildGradeVisibilityMapAsync(IEnumerable<int> termIds)
    {
        var result = new Dictionary<int, bool>();
        foreach (var termId in termIds.Distinct())
        {
            result[termId] = await _calendarRuleService.CanStudentSeeLetterGradeAsync(
                termId,
                isFinalized: true,
                hasGrade: true,
                nowUtc: DateTime.UtcNow);
        }

        return result;
    }

    private static bool IsPassedGrade(GradeCode grade) =>
        grade != GradeCode.FF
        && grade != GradeCode.NA
        && grade != GradeCode.U
        && grade != GradeCode.W
        && grade != GradeCode.I;

    private static List<StudentGradeRangeDto> BuildGradeRanges(
        GradingCriteria criteria,
        IReadOnlyDictionary<string, int> gradeCounts)
    {
        static StudentGradeRangeDto Range(
            string grade,
            decimal min,
            decimal max,
            IReadOnlyDictionary<string, int> gradeCounts) => new()
            {
                Grade = grade,
                MinScore = min,
                MaxScore = max,
                StudentCount = gradeCounts.GetValueOrDefault(grade)
            };

        return
        [
            Range("AA", criteria.AAMin, 100m, gradeCounts),
            Range("BA", criteria.BAMin, criteria.AAMin - 0.01m, gradeCounts),
            Range("BB", criteria.BBMin, criteria.BAMin - 0.01m, gradeCounts),
            Range("CB", criteria.CBMin, criteria.BBMin - 0.01m, gradeCounts),
            Range("CC", criteria.CCMin, criteria.CBMin - 0.01m, gradeCounts),
            Range("DC", criteria.DCMin, criteria.CCMin - 0.01m, gradeCounts),
            Range("DD", criteria.DDMin, criteria.DCMin - 0.01m, gradeCounts),
            Range("FF", 0m, criteria.DDMin - 0.01m, gradeCounts)
        ];
    }

    private static int GetGpaEcts(CourseRegistration registration) =>
        GradePointMap.IsGpaIncluded(registration.Grade!.Value)
            ? registration.CourseSection.Course.Ects
            : 0;

    private static decimal GetWeightedEcts(CourseRegistration registration)
    {
        var point = GradePointMap.GetPoint(registration.Grade!.Value);
        return point.HasValue
            ? Math.Round(registration.CourseSection.Course.Ects * point.Value, 2, MidpointRounding.AwayFromZero)
            : 0m;
    }

    private static decimal CalculateAverage(IEnumerable<CourseRegistration> registrations)
    {
        var totalEcts = 0;
        decimal totalPoints = 0;

        foreach (var registration in registrations)
        {
            var point = GradePointMap.GetPoint(registration.Grade!.Value);
            if (!point.HasValue)
                continue;

            var ects = registration.CourseSection.Course.Ects;
            if (ects == 0)
                continue;

            totalEcts += ects;
            totalPoints += ects * point.Value;
        }

        if (totalEcts == 0)
            return 0m;

        return Math.Round(totalPoints / totalEcts, 2, MidpointRounding.AwayFromZero);
    }

    private static Dictionary<int, int> BuildCourseChainIdMap(
        IEnumerable<int> courseIds,
        IReadOnlyDictionary<int, HashSet<int>> equivalentCourseIdsByCourseId)
    {
        var distinctCourseIds = courseIds.Distinct().ToList();
        var map = distinctCourseIds.ToDictionary(courseId => courseId, courseId => courseId);
        var relevantCourseIds = distinctCourseIds.ToHashSet();
        var visited = new HashSet<int>();

        foreach (var courseId in distinctCourseIds)
        {
            if (!visited.Add(courseId))
                continue;

            var queue = new Queue<int>();
            var component = new List<int>();
            queue.Enqueue(courseId);

            while (queue.Count > 0)
            {
                var currentCourseId = queue.Dequeue();
                component.Add(currentCourseId);

                if (!equivalentCourseIdsByCourseId.TryGetValue(currentCourseId, out var equivalentIds))
                    continue;

                foreach (var equivalentCourseId in equivalentIds)
                {
                    if (!relevantCourseIds.Contains(equivalentCourseId) || !visited.Add(equivalentCourseId))
                        continue;

                    queue.Enqueue(equivalentCourseId);
                }
            }

            var chainId = component.Min();
            foreach (var componentCourseId in component)
            {
                map[componentCourseId] = chainId;
            }
        }

        return map;
    }
}
