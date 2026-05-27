using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.DTOs;
using SIS.Application.Common.Services;
using SIS.Application.StudentCard.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Öğrencinin müfredat ilerleme durumunu (curriculum status) kuran servis.
/// Logic <c>StudentController.GetCurriculumStatus</c>'tan birebir taşındı; admin endpoint'lerce
/// yeniden kullanılabilmesi için <c>studentId</c> ile anahtarlanır.
/// </summary>
public class CurriculumStatusService : ICurriculumStatusService
{
    private readonly AppDbContext _context;
    private readonly IGradeVisibilityResolver _gradeVisibilityResolver;

    public CurriculumStatusService(
        AppDbContext context,
        IGradeVisibilityResolver? gradeVisibilityResolver = null)
    {
        _context = context;
        _gradeVisibilityResolver = gradeVisibilityResolver
            ?? new GradeVisibilityResolver(new NullWebModuleControlService());
    }

    public async Task<CurriculumStatusDto?> GetCurriculumStatusAsync(int studentId, CancellationToken ct = default)
    {
        var student = await _context.Students
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        var activeProgram = student?.StudentPrograms.FirstOrDefault(sp => sp.IsActive);
        if (student == null || activeProgram == null)
            return null;

        var user = await _context.Users.FindAsync(new object?[] { student.UserId }, ct);
        if (user == null)
            return null;

        // 1. Get curriculum courses
        var curriculumCourses = await _context.CurriculumCourses
            .Include(cc => cc.Course)
            .Where(cc => cc.CurriculumId == activeProgram.CurriculumId && cc.SuggestedSemester > 0)
            .OrderBy(cc => cc.SuggestedSemester)
            .ThenBy(cc => cc.Course.Code)
            .ToListAsync(ct);

        // 1b. Get curriculum elective pool slots (TE-1, TE-2, NDE-1, vs.) — ders gibi semester'lara dağıt
        var curriculumPools = await _context.CurriculumElectivePools
            .Include(p => p.ElectiveGroup)
            .Where(p => p.CurriculumId == activeProgram.CurriculumId && p.SuggestedSemesterStart > 0)
            .OrderBy(p => p.SuggestedSemesterStart)
            .ThenBy(p => p.SlotName)
            .ToListAsync(ct);

        // 2. Get ALL student registrations (excluding dropped)
        var allRegistrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
            .Where(cr => cr.StudentId == student.Id
                && cr.Status != RegistrationStatus.Dropped)
            .ToListAsync(ct);

        var gradeVisibilityByTerm = await _gradeVisibilityResolver.BuildMapAsync(
            allRegistrations.Select(r => r.CourseSection.TermId).Distinct().ToList());
        bool HasVisibleLetterGrade(CourseRegistration registration) =>
            registration.Grade != null && gradeVisibilityByTerm.GetValueOrDefault(registration.CourseSection.TermId, true);

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
            .ToListAsync(ct))
            .GroupBy(p => p.CourseId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 6. Equivalencies
        var equivMap = (await _context.CourseEquivalencies.ToListAsync(ct))
            .GroupBy(e => e.CourseId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.EquivalentCourseId).ToHashSet());

        // 6b. İntibak: (yeni CourseId → eski PreviousCourseId) — eski ders geçildiyse yeni ders karşılanmış sayılır
        var previousCourseMap = PreviousCourseResolver.BuildMap(curriculumCourses);

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

                    // Doğrudan ders kaydı VEYA intibak: eski (PreviousCourse) ders kaydı bu satırı karşılar
                    if (courseRegMap.TryGetValue(cid, out var regInfo)
                        || (cc.PreviousCourseId.HasValue && courseRegMap.TryGetValue(cc.PreviousCourseId.Value, out regInfo)))
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
                            if (!PreviousCourseResolver.IsCovered(cid, passedCourseIds, previousCourseMap))
                            {
                                var met = PreviousCourseResolver.IsCovered(p.RequiredCourseId, passedCourseIds, previousCourseMap);
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
            CurriculumVersion = (await _context.Curricula.FindAsync(new object?[] { activeProgram.CurriculumId }, ct))?.Version ?? string.Empty,
            TotalCredits = allTakenCredits,
            TotalEcts = allTakenEcts,
            Cgpa = student.Cgpa,
            PassedCredits = passedCreditsSum,
            PassedEcts = passedEctsSum,
            Semesters = semesterGroups,
        };

        return dto;
    }

}
