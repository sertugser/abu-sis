using Microsoft.EntityFrameworkCore;
using SIS.Application.StudentEligibility.DTOs;
using SIS.Application.StudentEligibility.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Application.Common.Services;
using SIS.Application.Common.Time;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class StudentEligibilityService : IStudentEligibilityService
{
    private readonly AppDbContext _context;
    private readonly IGpaCalculationService _gpaCalculationService;
    private readonly ISystemConfigProvider _config;
    private readonly IDateTimeProvider _clock;

    public StudentEligibilityService(
        AppDbContext context,
        IGpaCalculationService gpaCalculationService,
        IDateTimeProvider clock,
        ISystemConfigProvider? config = null)
    {
        _context = context;
        _gpaCalculationService = gpaCalculationService;
        _clock = clock;
        _config = config ?? new NullSystemConfigProvider();
    }

    public async Task<CourseEligibilityReportDto?> GetCourseEligibilityReportAsync(
        int courseId,
        int? departmentId = null,
        CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);

        if (course == null)
            return null;

        var eligibilityContext = await BuildEligibilityContextAsync(cancellationToken);
        var report = new CourseEligibilityReportDto
        {
            CourseId = course.Id,
            CourseCode = course.Code,
            CourseName = course.Name,
            Ects = course.Ects
        };

        var breakdown = new EligibilityBreakdownDto();

        foreach (var snapshot in eligibilityContext.StudentSnapshots)
        {
            if (departmentId.HasValue && snapshot.ProgramDepartmentId != departmentId.Value)
                continue;

            var result = EvaluateEligibility(snapshot, course, eligibilityContext);

            if (result.IsEligible)
            {
                report.EligibleStudents.Add(result);
                switch (result.TakeType)
                {
                    case "MustRetake": breakdown.MustRetakeCount++; break;
                    case "BehindSchedule": breakdown.BehindScheduleCount++; break;
                    case "UpperCourse": breakdown.UpperCourseCount++; break;
                    default: breakdown.RegularCount++; break;
                }
            }
            else
            {
                report.IneligibleStudents.Add(result);
                foreach (var reason in result.Reasons)
                {
                    switch (reason.Reason)
                    {
                        case IneligibilityReason.PrerequisiteNotMet: breakdown.PrerequisiteNotMetCount++; break;
                        case IneligibilityReason.EctsLimitExceeded: breakdown.EctsLimitExceededCount++; break;
                        case IneligibilityReason.AlreadyPassed: breakdown.AlreadyPassedCount++; break;
                        case IneligibilityReason.LowCgpaRestriction: breakdown.LowCgpaRestrictionCount++; break;
                        case IneligibilityReason.FinancialBlock: breakdown.FinancialBlockCount++; break;
                        default: breakdown.OtherIneligibleCount++; break;
                    }
                }
            }
        }

        report.TotalEligible = report.EligibleStudents.Count;
        report.TotalIneligible = report.IneligibleStudents.Count;
        report.Breakdown = breakdown;
        return report;
    }

    public async Task<List<StudentCourseEligibilityDto>> GetStudentEligibleCoursesAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);

        if (student == null)
            return new List<StudentCourseEligibilityDto>();

        var eligibilityContext = await BuildEligibilityContextAsync(cancellationToken);

        var snapshot = eligibilityContext.StudentSnapshots
            .FirstOrDefault(s => s.StudentId == studentId);

        if (snapshot == null)
            return new List<StudentCourseEligibilityDto>();

        var results = new List<StudentCourseEligibilityDto>();

        // Evaluate all courses in the student's curriculum
        if (eligibilityContext.CurriculumCoursesByCurriculumId
            .TryGetValue(snapshot.CurriculumId, out var curriculumCourses))
        {
            foreach (var cc in curriculumCourses)
            {
                if (!eligibilityContext.Courses.TryGetValue(cc.CourseId, out var course))
                    continue;

                var result = EvaluateEligibility(snapshot, course, eligibilityContext);
                results.Add(result);
            }
        }

        return results;
    }

    private StudentCourseEligibilityDto EvaluateEligibility(
        EligibilityStudentSnapshot snapshot,
        Course course,
        EligibilityContextData context)
    {
        var user = context.UsersByStudentId.GetValueOrDefault(snapshot.StudentId);
        var result = new StudentCourseEligibilityDto
        {
            StudentId = snapshot.StudentId,
            StudentNumber = snapshot.StudentNumber,
            FullName = user != null ? $"{user.FirstName} {user.LastName}" : snapshot.StudentNumber,
            CourseId = course.Id,
            CourseCode = course.Code,
            CourseName = course.Name,
            Ects = course.Ects,
            CurrentSemester = snapshot.CurrentSemester,
            Cgpa = snapshot.Cgpa,
            CurrentTermEcts = snapshot.CurrentTermEcts,
            EctsLimit = snapshot.EctsLimit,
            PreviousGrade = snapshot.LastGradeByCourse.GetValueOrDefault(course.Id),
            IsEligible = true
        };
        var curriculumCourse = GetCurriculumCourse(course.Id, snapshot.CurriculumId, context);
        result.IsCompulsory = curriculumCourse?.IsCompulsory ?? false;
        result.SuggestedSemester = curriculumCourse?.SuggestedSemester ?? 0;

        // 1. Already passed?
        if (snapshot.PassedCourseIds.Contains(course.Id))
        {
            result.IsEligible = false;
            result.TakeType = "AlreadyPassed";
            result.Reasons.Add(new IneligibilityReasonDto
            {
                Reason = IneligibilityReason.AlreadyPassed,
                Message = "Ders daha once basariyla tamamlanmis."
            });
            return result;
        }

        // 2. Financial block?
        if (snapshot.HasFinancialBlock)
        {
            result.IsEligible = false;
            result.Reasons.Add(new IneligibilityReasonDto
            {
                Reason = IneligibilityReason.FinancialBlock,
                Message = "Finansal bloke mevcut."
            });
            return result;
        }

        // 3. Student active?
        if (snapshot.Status != StudentStatus.Active)
        {
            result.IsEligible = false;
            result.Reasons.Add(new IneligibilityReasonDto
            {
                Reason = IneligibilityReason.StudentNotActive,
                Message = $"Ogrenci durumu aktif degil: {snapshot.Status}"
            });
            return result;
        }

        var reasons = new List<IneligibilityReasonDto>();

        // 4. Prerequisite check
        if (context.PrerequisitesByCourseId.TryGetValue(course.Id, out var prereqIds))
        {
            foreach (var prereqId in prereqIds)
            {
                var met = snapshot.PassedCourseIds.Contains(prereqId);
                if (!met && context.EquivalentCourseIdsByCourseId.TryGetValue(prereqId, out var equivIds))
                    met = equivIds.Any(snapshot.PassedCourseIds.Contains);

                if (!met)
                {
                    var prereqName = context.Courses.TryGetValue(prereqId, out var pc)
                        ? $"{pc.Code} - {pc.Name}" : $"CourseId={prereqId}";
                    reasons.Add(new IneligibilityReasonDto
                    {
                        Reason = IneligibilityReason.PrerequisiteNotMet,
                        Message = $"Onkosul saglanmadi: {prereqName}"
                    });
                }
            }
        }

        // 4A. Min Kredi Ön Koşul (Proliz "Diğer Ön Koşul")
        if (course.MinCreditsRequired.HasValue && snapshot.PassedCredits < course.MinCreditsRequired.Value)
        {
            reasons.Add(new IneligibilityReasonDto
            {
                Reason = IneligibilityReason.MinCreditsNotMet,
                Message = $"Min kredi sarti saglanmadi: {snapshot.PassedCredits} < {course.MinCreditsRequired.Value}"
            });
        }

        // 4B. Min AKTS Ön Koşul (Proliz "Diğer Ön Koşul")
        if (course.MinEctsRequired.HasValue && snapshot.PassedEcts < course.MinEctsRequired.Value)
        {
            reasons.Add(new IneligibilityReasonDto
            {
                Reason = IneligibilityReason.MinEctsNotMet,
                Message = $"Min AKTS sarti saglanmadi: {snapshot.PassedEcts} < {course.MinEctsRequired.Value}"
            });
        }

        // 4C. Alttan Ders Kuralı (Proliz "Alttan Dersi Olmayanlar Alabilir")
        if (course.RequiresNoFailedLowerCourses && snapshot.FailedCourseIds.Count > 0)
        {
            reasons.Add(new IneligibilityReasonDto
            {
                Reason = IneligibilityReason.HasFailedLowerCourses,
                Message = "Basarisiz (alttan) ders mevcut: bu ders sadece alttan dersi olmayan ogrencilere acik."
            });
        }

        // Not: CanBeTakenStandalone burada kontrol edilmez — eligibility "alabilir miyim" sorusudur,
        // tekil alma kuralı kayıt anında diğer dersler bilindiğinde RegistrationValidationService'de uygulanır.

        // 5. CGPA restriction
        if (RegistrationRulePolicy.HasLowCgpaRestriction(snapshot.Cgpa, snapshot.CompletedSemesters, context.RetentionThreshold))
        {
            var suggestedSemester = GetSuggestedSemester(course.Id, snapshot.CurriculumId, context);
            var isFailed = snapshot.FailedCourseIds.Contains(course.Id);

            if (!RegistrationRulePolicy.IsAllowedUnderLowCgpaRestriction(
                    course, suggestedSemester, snapshot.CompletedSemesters,
                    snapshot.ProgramDepartmentId, isFailed, false))
            {
                reasons.Add(new IneligibilityReasonDto
                {
                    Reason = IneligibilityReason.LowCgpaRestriction,
                    Message = $"CGPA < {context.RetentionThreshold:F2} kisitlamasi: ust donem derslerine kayit yapilamaz."
                });
            }
        }

        // 6. ECTS limit check
        if (snapshot.CurrentTermEcts + course.Ects > snapshot.EctsLimit)
        {
            reasons.Add(new IneligibilityReasonDto
            {
                Reason = IneligibilityReason.EctsLimitExceeded,
                Message = $"AKTS limiti asilir: mevcut {snapshot.CurrentTermEcts} + {course.Ects} > limit {snapshot.EctsLimit}"
            });
        }

        if (reasons.Count > 0)
        {
            result.IsEligible = false;
            result.Reasons = reasons;
            result.TakeType = "Ineligible";
            return result;
        }

        // Classify take type
        var isMustRetake = snapshot.FailedCourseIds.Contains(course.Id);
        if (isMustRetake)
        {
            result.TakeType = "MustRetake";
        }
        else
        {
            var suggested = GetSuggestedSemester(course.Id, snapshot.CurriculumId, context);
            if (suggested.HasValue)
            {
                if (suggested.Value < snapshot.CurrentSemester)
                    result.TakeType = "BehindSchedule";
                else if (suggested.Value > snapshot.CurrentSemester)
                    result.TakeType = "UpperCourse";
                else
                    result.TakeType = "Regular";
            }
            else
            {
                result.TakeType = "Regular";
            }
        }

        return result;
    }

    private static int? GetSuggestedSemester(
        int courseId,
        int curriculumId,
        EligibilityContextData context)
    {
        return GetCurriculumCourse(courseId, curriculumId, context)?.SuggestedSemester;
    }

    private static CurriculumCourse? GetCurriculumCourse(
        int courseId,
        int curriculumId,
        EligibilityContextData context)
    {
        if (!context.CurriculumCoursesByCurriculumId.TryGetValue(curriculumId, out var courses))
            return null;

        return courses.FirstOrDefault(cc => cc.CourseId == courseId);
    }

    private async Task<EligibilityContextData> BuildEligibilityContextAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var activeTerm = await _context.AcademicTerms
            .AsNoTracking()
            .ActiveTerm()
            .FirstOrDefaultAsync(cancellationToken);

        var students = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .Where(s => s.Status == StudentStatus.Active)
            .ToListAsync(cancellationToken);

        var studentUserIds = students.Select(s => s.UserId).ToList();
        var users = await _context.Users
            .AsNoTracking()
            .Where(u => studentUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var usersByStudentId = students
            .Where(s => users.ContainsKey(s.UserId))
            .ToDictionary(s => s.Id, s => users[s.UserId]);

        var courses = await _context.Courses
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var activeStudentIds = students.Select(s => s.Id).ToList();

        var registrations = activeStudentIds.Count == 0
            ? new List<CourseRegistration>()
            : await _context.CourseRegistrations
                .AsNoTracking()
                .Include(r => r.CourseSection)
                    .ThenInclude(s => s.Course)
                .Where(r => activeStudentIds.Contains(r.StudentId))
                .ToListAsync(cancellationToken);

        var registrationsByStudentId = registrations
            .GroupBy(r => r.StudentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var prerequisites = await _context.CoursePrerequisites
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var prerequisitesByCourseId = prerequisites
            .GroupBy(p => p.CourseId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.RequiredCourseId).ToHashSet());

        var equivalencyPairs = await _context.CourseEquivalencies
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var equivalentCourseIdsByCourseId = BuildEquivalentCourseMap(equivalencyPairs);

        var activeCurriculumIds = students
            .SelectMany(s => s.StudentPrograms)
            .Where(sp => sp.Type == StudentProgramType.Main && sp.IsActive)
            .Select(sp => sp.CurriculumId)
            .Distinct()
            .ToList();

        var curriculumCourses = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => activeCurriculumIds.Contains(cc.CurriculumId))
            .ToListAsync(cancellationToken);

        var curriculumCoursesByCurriculumId = curriculumCourses
            .GroupBy(cc => cc.CurriculumId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var snapshots = new List<EligibilityStudentSnapshot>();
        var cgpaCache = new Dictionary<int, decimal>();

        var ectsLimitBelow200 = await _config.GetIntAsync("Registration.EctsLimit.CgpaBelow200", 30, cancellationToken);
        var ectsLimit200To249 = await _config.GetIntAsync("Registration.EctsLimit.Cgpa200To249", 42, cancellationToken);
        var ectsLimitAbove250 = await _config.GetIntAsync("Registration.EctsLimit.CgpaAbove250", 48, cancellationToken);
        var retentionThreshold = await _config.GetDecimalAsync("GPA.RetentionThreshold", 1.70m, cancellationToken);

        foreach (var student in students)
        {
            var mainProgram = student.StudentPrograms
                .FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive);

            if (mainProgram == null) continue;

            var studentRegistrations = registrationsByStudentId
                .GetValueOrDefault(student.Id, new List<CourseRegistration>());

            var cgpa = await GetCgpaAsync(student, cgpaCache);
            var ectsLimit = RegistrationRulePolicy.GetEctsLimit(cgpa, ectsLimitBelow200, ectsLimit200To249, ectsLimitAbove250);
            var completedSemesters = Math.Max(0, student.CompletedSemesters);
            var currentSemester = completedSemesters + 1;

            var passedCourseIds = studentRegistrations
                .Where(r => RegistrationRulePolicy.IsPassedGrade(r.Grade))
                .Select(r => r.CourseSection.CourseId)
                .ToHashSet();

            var failedCourseIds = studentRegistrations
                .Where(r => RegistrationRulePolicy.IsFailedGrade(r.Grade))
                .Select(r => r.CourseSection.CourseId)
                .ToHashSet();

            failedCourseIds.ExceptWith(passedCourseIds);

            // Tamamlanan kredi/AKTS — course-level Min* kontrolü için (Proliz "Diğer Ön Koşul")
            var passedEcts = studentRegistrations
                .Where(r => RegistrationRulePolicy.IsPassedGrade(r.Grade))
                .Sum(r => r.EctsSnapshot ?? r.CourseSection.Course?.Ects ?? 0);
            var passedCredits = studentRegistrations
                .Where(r => RegistrationRulePolicy.IsPassedGrade(r.Grade))
                .Sum(r => r.CourseSection.Course?.NationalCredit ?? 0);

            var lastGradeByCourse = studentRegistrations
                .GroupBy(r => r.CourseSection.CourseId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(r => r.CreatedAt).First().Grade);

            var currentTermEcts = activeTerm == null ? 0 :
                studentRegistrations
                    .Where(r => r.CourseSection.TermId == activeTerm.Id
                        && RegistrationRulePolicy.IsDemandStatus(r.Status))
                    .Sum(r => r.CourseSection.Course?.Ects ?? 0);

            snapshots.Add(new EligibilityStudentSnapshot
            {
                StudentId = student.Id,
                StudentNumber = student.StudentNumber,
                Status = student.Status,
                HasFinancialBlock = student.HasFinancialBlock,
                Cgpa = cgpa,
                EctsLimit = ectsLimit,
                CompletedSemesters = completedSemesters,
                CurrentSemester = currentSemester,
                CurrentTermEcts = currentTermEcts,
                CurriculumId = mainProgram.CurriculumId,
                ProgramDepartmentId = mainProgram.Program?.DepartmentId,
                PassedCourseIds = passedCourseIds,
                FailedCourseIds = failedCourseIds,
                LastGradeByCourse = lastGradeByCourse!,
                PassedEcts = passedEcts,
                PassedCredits = passedCredits
            });
        }

        return new EligibilityContextData
        {
            Courses = courses,
            StudentSnapshots = snapshots,
            PrerequisitesByCourseId = prerequisitesByCourseId,
            EquivalentCourseIdsByCourseId = equivalentCourseIdsByCourseId,
            CurriculumCoursesByCurriculumId = curriculumCoursesByCurriculumId,
            UsersByStudentId = usersByStudentId,
            RetentionThreshold = retentionThreshold
        };
    }

    private async Task<decimal> GetCgpaAsync(Student student, IDictionary<int, decimal> cgpaCache)
    {
        if (cgpaCache.TryGetValue(student.Id, out var cached))
            return cached;

        var cgpa = await _gpaCalculationService.CalculateCgpaAsync(student.Id);
        cgpaCache[student.Id] = cgpa;
        return cgpa;
    }

    private static Dictionary<int, HashSet<int>> BuildEquivalentCourseMap(IEnumerable<CourseEquivalency> pairs)
    {
        var map = new Dictionary<int, HashSet<int>>();
        foreach (var eq in pairs)
        {
            AddEquiv(map, eq.CourseId, eq.EquivalentCourseId);
            AddEquiv(map, eq.EquivalentCourseId, eq.CourseId);
        }
        return map;
    }

    private static void AddEquiv(Dictionary<int, HashSet<int>> map, int courseId, int equivalentId)
    {
        if (!map.TryGetValue(courseId, out var set))
        {
            set = new HashSet<int>();
            map[courseId] = set;
        }
        set.Add(equivalentId);
    }

    private sealed class EligibilityContextData
    {
        public required Dictionary<int, Course> Courses { get; init; }
        public required List<EligibilityStudentSnapshot> StudentSnapshots { get; init; }
        public required Dictionary<int, HashSet<int>> PrerequisitesByCourseId { get; init; }
        public required Dictionary<int, HashSet<int>> EquivalentCourseIdsByCourseId { get; init; }
        public required Dictionary<int, List<CurriculumCourse>> CurriculumCoursesByCurriculumId { get; init; }
        public required Dictionary<int, ApplicationUser> UsersByStudentId { get; init; }
        public decimal RetentionThreshold { get; init; }
    }

    private sealed class EligibilityStudentSnapshot
    {
        public int StudentId { get; init; }
        public string StudentNumber { get; init; } = string.Empty;
        public StudentStatus Status { get; init; }
        public bool HasFinancialBlock { get; init; }
        public decimal Cgpa { get; init; }
        public int EctsLimit { get; init; }
        public int CompletedSemesters { get; init; }
        public int CurrentSemester { get; init; }
        public int CurrentTermEcts { get; init; }
        public int CurriculumId { get; init; }
        public int? ProgramDepartmentId { get; init; }
        public HashSet<int> PassedCourseIds { get; init; } = new();
        public HashSet<int> FailedCourseIds { get; init; } = new();
        public Dictionary<int, GradeCode?> LastGradeByCourse { get; init; } = new();
        // Course.Min* kontrolü için (Proliz "Diğer Ön Koşul")
        public int PassedEcts { get; init; }
        public int PassedCredits { get; init; }
    }
}
