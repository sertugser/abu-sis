using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.DTOs;
using SIS.Application.Common.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Student")]
public class StudentController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IGpaCalculationService _gpaCalculationService;
    private readonly ISystemConfigService _systemConfig;

    public StudentController(
        AppDbContext context,
        IGpaCalculationService gpaCalculationService,
        ISystemConfigService systemConfig)
    {
        _context = context;
        _gpaCalculationService = gpaCalculationService;
        _systemConfig = systemConfig;
    }

    /// <summary>
    /// GET api/student/me — returns the authenticated student's profile info.
    /// </summary>
    [HttpGet("me")]
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

        // ECTS limit based on CGPA — DB'den okunur (SistemConfig tablosu)
        var ectsLimit = await _systemConfig.GetEctsLimitAsync(student.Cgpa);

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

        // 2. Get ALL student registrations (excluding dropped)
        var allRegistrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
            .Where(cr => cr.StudentId == student.Id
                && cr.Status != RegistrationStatus.Dropped)
            .ToListAsync();

        // 3. Build per-course best registration map
        // For each course: prefer passed > current (no grade) > failed
        var courseRegMap = new Dictionary<int, (SIS.Domain.Entities.CourseRegistration reg, string gradeStatus)>();

        foreach (var group in allRegistrations.GroupBy(r => r.CourseSection.CourseId))
        {
            var courseId = group.Key;

            // Find the best registration to display
            var passed = group
                .Where(r => r.Grade != null && r.Grade != GradeCode.FF && r.Grade != GradeCode.NA
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
                .Where(r => r.Grade == null && (r.Status == RegistrationStatus.Active
                    || r.Status == RegistrationStatus.PendingAdvisorApproval))
                .OrderByDescending(r => r.Id)
                .FirstOrDefault();

            if (current != null)
            {
                courseRegMap[courseId] = (current, "current");
                continue;
            }

            // Failed (latest)
            var failed = group
                .Where(r => r.Grade != null)
                .OrderByDescending(r => r.Id)
                .FirstOrDefault();

            if (failed != null)
            {
                courseRegMap[courseId] = (failed, "failed");
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

        // 7. Build semester groups
        var semesterGroups = curriculumCourses
            .GroupBy(cc => cc.SuggestedSemester)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var semNum = g.Key;
                var classYear = (semNum - 1) / 2 + 1;
                var isGuz = semNum % 2 == 1;
                var label = $"{classYear}. Sinif {(isGuz ? "Guz" : "Bahar")}";

                var courses = g.Select(cc =>
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
                        var semChar = term.Semester == SemesterType.Fall ? "G" : "B";
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

                return new CurriculumSemesterDto
                {
                    SemesterNumber = semNum,
                    Label = label,
                    Courses = courses,
                    TotalCredits = courses.Sum(c => c.Credits),
                    TotalEcts = courses.Sum(c => c.Ects),
                    CourseCount = courses.Count,
                    CompulsoryCount = courses.Count(c => c.IsCompulsory),
                    ElectiveCount = courses.Count(c => !c.IsCompulsory),
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
    [Authorize(Roles = "Student")]
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
}
