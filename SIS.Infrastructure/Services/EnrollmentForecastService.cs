using Microsoft.EntityFrameworkCore;
using SIS.Application.EnrollmentForecast.DTOs;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class EnrollmentForecastService : IEnrollmentForecastService
{
    private readonly AppDbContext _context;
    private readonly IGpaCalculationService _gpaCalculationService;

    public EnrollmentForecastService(AppDbContext context, IGpaCalculationService gpaCalculationService)
    {
        _context = context;
        _gpaCalculationService = gpaCalculationService;
    }

    public async Task<CourseDemandForecastDto?> GetCourseDemandForecastAsync(int courseId)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course == null)
            return null;

        var forecastContext = await BuildForecastContextAsync();
        return BuildCourseDemandForecast(forecastContext, course);
    }

    public async Task<DepartmentForecastSummaryDto?> GetDepartmentSummaryAsync(int departmentId)
    {
        var department = await _context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == departmentId);

        if (department == null)
            return null;

        var forecastContext = await BuildForecastContextAsync();

        var relevantCourseIds = new HashSet<int>(
            forecastContext.Courses.Values
                .Where(course => course.DepartmentId == departmentId)
                .Where(course =>
                    forecastContext.ActiveCurriculumCourseIds.Contains(course.Id)
                    || forecastContext.ActiveTermSectionCourseIds.Contains(course.Id)
                    || forecastContext.PredictedDemandCourseIds.Contains(course.Id))
                .Select(course => course.Id));

        var courseSummaries = relevantCourseIds
            .Select(courseId => BuildCourseDemandForecast(forecastContext, forecastContext.Courses[courseId]))
            .OrderBy(summary => summary.CourseCode)
            .ToList();

        return new DepartmentForecastSummaryDto
        {
            DepartmentId = department.Id,
            DepartmentName = department.Name,
            TotalCoursesAnalyzed = courseSummaries.Count,
            TotalPredictedDemand = courseSummaries.Sum(summary => summary.TotalPredictedDemand),
            TotalRecommendedSections = courseSummaries.Sum(summary => summary.RecommendedSectionCount),
            CourseSummaries = courseSummaries,
            Notes = BuildDepartmentNotes(forecastContext, courseSummaries)
        };
    }

    private async Task<ForecastContextData> BuildForecastContextAsync()
    {
        var activeTerm = await _context.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(term => term.IsActive);

        var activeStudents = await _context.Students
            .AsNoTracking()
            .Include(student => student.StudentPrograms)
                .ThenInclude(program => program.Program)
            .Where(student => student.Status == StudentStatus.Active && !student.HasFinancialBlock)
            .ToListAsync();

        var activeMainPrograms = activeStudents
            .Select(student => new
            {
                Student = student,
                Program = student.StudentPrograms
                    .FirstOrDefault(program => program.Type == StudentProgramType.Main && program.IsActive)
            })
            .Where(item => item.Program != null)
            .ToDictionary(item => item.Student.Id, item => item.Program!);

        var activeStudentIds = activeMainPrograms.Keys.ToList();
        var activeCurriculumIds = activeMainPrograms.Values
            .Select(program => program.CurriculumId)
            .Distinct()
            .ToList();

        var courses = await _context.Courses
            .AsNoTracking()
            .ToDictionaryAsync(course => course.Id);

        var curriculumCourses = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(curriculumCourse => activeCurriculumIds.Contains(curriculumCourse.CurriculumId))
            .ToListAsync();

        var curriculumCoursesByCurriculumId = curriculumCourses
            .GroupBy(curriculumCourse => curriculumCourse.CurriculumId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(curriculumCourse => curriculumCourse.SuggestedSemester)
                    .ThenBy(curriculumCourse => courses.TryGetValue(curriculumCourse.CourseId, out var course)
                        ? course.Code
                        : curriculumCourse.CourseId.ToString())
                    .ToList());

        var prerequisites = await _context.CoursePrerequisites
            .AsNoTracking()
            .ToListAsync();

        var prerequisitesByCourseId = prerequisites
            .GroupBy(prerequisite => prerequisite.CourseId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(prerequisite => prerequisite.RequiredCourseId)
                    .ToHashSet());

        var equivalencyPairs = await _context.CourseEquivalencies
            .AsNoTracking()
            .ToListAsync();

        var equivalentCourseIdsByCourseId = BuildEquivalentCourseMap(equivalencyPairs);

        var allSections = await _context.CourseSections
            .AsNoTracking()
            .Include(section => section.Course)
            .Include(section => section.Classroom)
            .ToListAsync();

        var registrations = activeStudentIds.Count == 0
            ? new List<CourseRegistration>()
            : await _context.CourseRegistrations
                .AsNoTracking()
                .Include(registration => registration.CourseSection)
                    .ThenInclude(section => section.Course)
                .Where(registration => activeStudentIds.Contains(registration.StudentId))
                .ToListAsync();

        var registrationsByStudentId = registrations
            .GroupBy(registration => registration.StudentId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var activeTermRegistrationsByStudentId = activeTerm == null
            ? new Dictionary<int, List<CourseRegistration>>()
            : registrations
                .Where(registration =>
                    registration.CourseSection.TermId == activeTerm.Id
                    && RegistrationRulePolicy.IsDemandStatus(registration.Status))
                .GroupBy(registration => registration.StudentId)
                .ToDictionary(group => group.Key, group => group.ToList());

        var cgpaCache = new Dictionary<int, decimal>();
        var studentSnapshots = new List<StudentForecastSnapshot>();

        foreach (var student in activeStudents)
        {
            if (!activeMainPrograms.TryGetValue(student.Id, out var activeProgram))
                continue;

            var studentRegistrations = registrationsByStudentId.GetValueOrDefault(student.Id, new List<CourseRegistration>());
            var activeTermRegistrations = activeTermRegistrationsByStudentId.GetValueOrDefault(student.Id, new List<CourseRegistration>());

            var snapshot = await BuildStudentSnapshotAsync(
                student,
                activeProgram,
                studentRegistrations,
                activeTermRegistrations,
                courses,
                curriculumCoursesByCurriculumId,
                prerequisitesByCourseId,
                equivalentCourseIdsByCourseId,
                cgpaCache);

            studentSnapshots.Add(snapshot);
        }

        return new ForecastContextData
        {
            ActiveTerm = activeTerm,
            Courses = courses,
            AllSections = allSections,
            StudentSnapshots = studentSnapshots,
            ActiveCurriculumCourseIds = curriculumCourses.Select(curriculumCourse => curriculumCourse.CourseId).ToHashSet(),
            ActiveTermSectionCourseIds = activeTerm == null
                ? new HashSet<int>()
                : allSections
                    .Where(section => section.TermId == activeTerm.Id && section.IsActive)
                    .Select(section => section.CourseId)
                    .ToHashSet(),
            GlobalNotes = BuildGlobalNotes(activeTerm, activeStudents.Count, activeMainPrograms.Count)
        };
    }

    private async Task<StudentForecastSnapshot> BuildStudentSnapshotAsync(
        Student student,
        StudentProgram activeProgram,
        List<CourseRegistration> registrations,
        List<CourseRegistration> activeTermRegistrations,
        IReadOnlyDictionary<int, Course> courses,
        IReadOnlyDictionary<int, List<CurriculumCourse>> curriculumCoursesByCurriculumId,
        IReadOnlyDictionary<int, HashSet<int>> prerequisitesByCourseId,
        IReadOnlyDictionary<int, HashSet<int>> equivalentCourseIdsByCourseId,
        IDictionary<int, decimal> cgpaCache)
    {
        var cgpa = await GetEffectiveCgpaAsync(student, cgpaCache);
        var ectsLimit = RegistrationRulePolicy.GetEctsLimit(cgpa);
        var completedSemesters = Math.Max(0, student.CompletedSemesters);
        var currentSemester = completedSemesters + 1;

        var passedCourseIds = registrations
            .Where(registration => RegistrationRulePolicy.IsPassedGrade(registration.Grade))
            .Select(registration => registration.CourseSection.CourseId)
            .ToHashSet();

        var failedCourseIds = registrations
            .Where(registration => RegistrationRulePolicy.IsFailedGrade(registration.Grade))
            .Select(registration => registration.CourseSection.CourseId)
            .ToHashSet();

        failedCourseIds.ExceptWith(passedCourseIds);

        var currentTermSelectedCourseIds = activeTermRegistrations
            .Select(registration => registration.CourseSection.CourseId)
            .ToHashSet();

        var retakeCourseIds = new HashSet<int>(failedCourseIds);
        var regularCourseIds = new HashSet<int>();
        var upperCourseIds = new HashSet<int>();

        // Preserve already-selected demand first, then fit new courses into the remaining ECTS headroom.
        if (currentTermSelectedCourseIds.Count > 0)
        {
            foreach (var selectedCourseId in currentTermSelectedCourseIds)
            {
                if (retakeCourseIds.Contains(selectedCourseId))
                    continue;

                if (ShouldClassifyAsUpperCourse(selectedCourseId, currentSemester, activeProgram.CurriculumId, curriculumCoursesByCurriculumId))
                    upperCourseIds.Add(selectedCourseId);
                else
                    regularCourseIds.Add(selectedCourseId);
            }
        }

        var reservedEcts = SumCourseEcts(currentTermSelectedCourseIds, courses);
        reservedEcts += SumCourseEcts(retakeCourseIds.Except(currentTermSelectedCourseIds), courses);

        var remainingEcts = Math.Max(0, ectsLimit - reservedEcts);
        var regularCandidates = new List<CourseCandidate>();
        var upperCandidates = new List<CourseCandidate>();

        if (curriculumCoursesByCurriculumId.TryGetValue(activeProgram.CurriculumId, out var curriculumCourses))
        {
            foreach (var curriculumCourse in curriculumCourses)
            {
                if (curriculumCourse.SuggestedSemester <= 0)
                    continue;

                if (!courses.TryGetValue(curriculumCourse.CourseId, out var course))
                    continue;

                if (currentTermSelectedCourseIds.Contains(course.Id) || retakeCourseIds.Contains(course.Id))
                    continue;

                if (passedCourseIds.Contains(course.Id))
                    continue;

                if (!ArePrerequisitesMet(course.Id, passedCourseIds, prerequisitesByCourseId, equivalentCourseIdsByCourseId))
                    continue;

                if (RegistrationRulePolicy.HasLowCgpaRestriction(cgpa, completedSemesters)
                    && !RegistrationRulePolicy.IsAllowedUnderLowCgpaRestriction(
                        course,
                        curriculumCourse.SuggestedSemester,
                        completedSemesters,
                        activeProgram.Program?.DepartmentId,
                        false,
                        false))
                {
                    continue;
                }

                var candidate = new CourseCandidate(
                    course.Id,
                    course.Ects,
                    curriculumCourse.SuggestedSemester,
                    course.Code);

                if (curriculumCourse.SuggestedSemester > currentSemester)
                    upperCandidates.Add(candidate);
                else
                    regularCandidates.Add(candidate);
            }
        }

        SelectCandidatesWithinEctsLimit(regularCandidates, regularCourseIds, ref remainingEcts);
        SelectCandidatesWithinEctsLimit(upperCandidates, upperCourseIds, ref remainingEcts);

        regularCourseIds.ExceptWith(retakeCourseIds);
        upperCourseIds.ExceptWith(retakeCourseIds);
        upperCourseIds.ExceptWith(regularCourseIds);

        return new StudentForecastSnapshot
        {
            StudentId = student.Id,
            RetakeCourseIds = retakeCourseIds,
            RegularCourseIds = regularCourseIds,
            UpperCourseIds = upperCourseIds
        };
    }

    private async Task<decimal> GetEffectiveCgpaAsync(Student student, IDictionary<int, decimal> cgpaCache)
    {
        if (cgpaCache.TryGetValue(student.Id, out var cachedCgpa))
            return cachedCgpa;

        var effectiveCgpa = await _gpaCalculationService.CalculateCgpaAsync(student.Id);
        cgpaCache[student.Id] = effectiveCgpa;
        return effectiveCgpa;
    }

    private CourseDemandForecastDto BuildCourseDemandForecast(ForecastContextData context, Course course)
    {
        var retakeStudents = context.StudentSnapshots
            .Where(snapshot => snapshot.RetakeCourseIds.Contains(course.Id))
            .Select(snapshot => snapshot.StudentId)
            .ToHashSet();

        var regularStudents = context.StudentSnapshots
            .Where(snapshot => snapshot.RegularCourseIds.Contains(course.Id))
            .Select(snapshot => snapshot.StudentId)
            .ToHashSet();

        regularStudents.ExceptWith(retakeStudents);

        var upperStudents = context.StudentSnapshots
            .Where(snapshot => snapshot.UpperCourseIds.Contains(course.Id))
            .Select(snapshot => snapshot.StudentId)
            .ToHashSet();

        upperStudents.ExceptWith(retakeStudents);
        upperStudents.ExceptWith(regularStudents);

        var totalPredictedDemand = retakeStudents.Count + regularStudents.Count + upperStudents.Count;
        var capacityRecommendation = BuildCapacityRecommendation(context, course);

        var notes = new List<string>(context.GlobalNotes);

        if (!context.ActiveCurriculumCourseIds.Contains(course.Id))
            notes.Add("Bu ders aktif mufredatlarda yer almiyor; tahmin agirlikla tekrarlar ve mevcut aktif kayitlar uzerinden uretilir.");

        if (!context.ActiveTermSectionCourseIds.Contains(course.Id))
            notes.Add("Bu ders icin aktif donemde section bulunmuyor; talep latent ihtiyac olarak hesaplandi.");

        notes.AddRange(capacityRecommendation.Notes);

        return new CourseDemandForecastDto
        {
            CourseId = course.Id,
            CourseCode = course.Code,
            CourseName = course.Name,
            PredictedEligibleCount = regularStudents.Count,
            PredictedRetakeCount = retakeStudents.Count,
            PredictedOverloadOrUpperClassCount = upperStudents.Count,
            TotalPredictedDemand = totalPredictedDemand,
            RecommendedSectionCount = CalculateRecommendedSectionCount(totalPredictedDemand, capacityRecommendation.EffectiveCapacity),
            CapacityBasis = capacityRecommendation.CapacityBasis,
            Notes = notes.Distinct().ToList()
        };
    }

    private CapacityRecommendation BuildCapacityRecommendation(ForecastContextData context, Course course)
    {
        if (context.ActiveTerm != null)
        {
            var activeSections = context.AllSections
                .Where(section => section.CourseId == course.Id
                    && section.TermId == context.ActiveTerm.Id
                    && section.IsActive)
                .ToList();

            if (TryBuildCapacity(activeSections, "Aktif donem course section ortalamasi", out var activeCapacity))
                return activeCapacity;
        }

        var historicalSections = context.AllSections
            .Where(section => section.CourseId == course.Id)
            .ToList();

        if (TryBuildCapacity(historicalSections, "Gecmis course section ortalamasi", out var historicalCapacity))
            return historicalCapacity;

        if (course.DepartmentId != null && context.ActiveTerm != null)
        {
            var activeDepartmentSections = context.AllSections
                .Where(section => section.TermId == context.ActiveTerm.Id
                    && section.IsActive
                    && section.Course.DepartmentId == course.DepartmentId)
                .ToList();

            if (TryBuildCapacity(activeDepartmentSections, "Aktif donem department section ortalamasi", out var departmentActiveCapacity))
            {
                departmentActiveCapacity.Notes.Add("Bu ders icin dogrudan capacity verisi olmadigindan ayni bolumun aktif donem section kapasite ortalamasi kullanildi.");
                return departmentActiveCapacity;
            }
        }

        if (course.DepartmentId != null)
        {
            var departmentSections = context.AllSections
                .Where(section => section.Course.DepartmentId == course.DepartmentId)
                .ToList();

            if (TryBuildCapacity(departmentSections, "Bolum geneli tarihsel section ortalamasi", out var departmentHistoricalCapacity))
            {
                departmentHistoricalCapacity.Notes.Add("Bu ders icin dogrudan capacity verisi olmadigindan ayni bolumun tarihsel section kapasite ortalamasi kullanildi.");
                return departmentHistoricalCapacity;
            }
        }

        if (TryBuildCapacity(context.AllSections, "Sistem geneli tarihsel section ortalamasi", out var globalCapacity))
        {
            globalCapacity.Notes.Add("Bu ders ve bolum icin capacity verisi olmadigindan sistem geneli tarihsel section kapasite ortalamasi kullanildi.");
            return globalCapacity;
        }

        return new CapacityRecommendation
        {
            EffectiveCapacity = 0,
            CapacityBasis = "Capacity verisi bulunamadi",
            Notes =
            {
                "Mevcut veya gecmis section/capacity verisi bulunamadigi icin onerilen section sayisi 0 donduruldu."
            }
        };
    }

    private static bool TryBuildCapacity(
        IEnumerable<CourseSection> sections,
        string capacityBasis,
        out CapacityRecommendation recommendation)
    {
        var effectiveCapacities = sections
            .Select(section => ComputeEffectiveCapacity(section.Capacity, section.Classroom?.Capacity))
            .Where(capacity => capacity > 0)
            .ToList();

        if (effectiveCapacities.Count == 0)
        {
            recommendation = new CapacityRecommendation();
            return false;
        }

        recommendation = new CapacityRecommendation
        {
            EffectiveCapacity = Math.Max(1, (int)Math.Floor(effectiveCapacities.Average())),
            CapacityBasis = capacityBasis
        };

        return true;
    }

    private static int ComputeEffectiveCapacity(int sectionCapacity, int? classroomCapacity)
    {
        var normalizedSectionCapacity = Math.Max(0, sectionCapacity);
        if (classroomCapacity == null)
            return normalizedSectionCapacity;

        return Math.Min(normalizedSectionCapacity, Math.Max(0, classroomCapacity.Value));
    }

    private static int CalculateRecommendedSectionCount(int totalPredictedDemand, int effectiveCapacity)
    {
        if (totalPredictedDemand <= 0 || effectiveCapacity <= 0)
            return 0;

        return (int)Math.Ceiling(totalPredictedDemand / (decimal)effectiveCapacity);
    }

    private static Dictionary<int, HashSet<int>> BuildEquivalentCourseMap(IEnumerable<CourseEquivalency> equivalencyPairs)
    {
        var map = new Dictionary<int, HashSet<int>>();

        foreach (var equivalency in equivalencyPairs)
        {
            AddEquivalentCourse(map, equivalency.CourseId, equivalency.EquivalentCourseId);
            AddEquivalentCourse(map, equivalency.EquivalentCourseId, equivalency.CourseId);
        }

        return map;
    }

    private static void AddEquivalentCourse(Dictionary<int, HashSet<int>> map, int courseId, int equivalentCourseId)
    {
        if (!map.TryGetValue(courseId, out var equivalents))
        {
            equivalents = new HashSet<int>();
            map[courseId] = equivalents;
        }

        equivalents.Add(equivalentCourseId);
    }

    private static bool ArePrerequisitesMet(
        int courseId,
        IReadOnlySet<int> passedCourseIds,
        IReadOnlyDictionary<int, HashSet<int>> prerequisitesByCourseId,
        IReadOnlyDictionary<int, HashSet<int>> equivalentCourseIdsByCourseId)
    {
        if (!prerequisitesByCourseId.TryGetValue(courseId, out var prerequisiteIds) || prerequisiteIds.Count == 0)
            return true;

        foreach (var prerequisiteId in prerequisiteIds)
        {
            if (passedCourseIds.Contains(prerequisiteId))
                continue;

            if (equivalentCourseIdsByCourseId.TryGetValue(prerequisiteId, out var equivalentCourseIds)
                && equivalentCourseIds.Any(passedCourseIds.Contains))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static void SelectCandidatesWithinEctsLimit(
        IEnumerable<CourseCandidate> candidates,
        ISet<int> selectedCourseIds,
        ref int remainingEcts)
    {
        foreach (var candidate in candidates
                     .OrderBy(candidate => candidate.SuggestedSemester)
                     .ThenBy(candidate => candidate.CourseCode)
                     .ThenBy(candidate => candidate.CourseId))
        {
            if (candidate.Ects <= 0)
                continue;

            if (candidate.Ects > remainingEcts)
                continue;

            selectedCourseIds.Add(candidate.CourseId);
            remainingEcts -= candidate.Ects;
        }
    }

    private static bool ShouldClassifyAsUpperCourse(
        int courseId,
        int currentSemester,
        int curriculumId,
        IReadOnlyDictionary<int, List<CurriculumCourse>> curriculumCoursesByCurriculumId)
    {
        if (!curriculumCoursesByCurriculumId.TryGetValue(curriculumId, out var curriculumCourses))
            return false;

        var curriculumCourse = curriculumCourses.FirstOrDefault(item => item.CourseId == courseId);
        return curriculumCourse != null && curriculumCourse.SuggestedSemester > currentSemester;
    }

    private static int SumCourseEcts(IEnumerable<int> courseIds, IReadOnlyDictionary<int, Course> courses) =>
        courseIds
            .Distinct()
            .Sum(courseId => courses.TryGetValue(courseId, out var course) ? Math.Max(0, course.Ects) : 0);

    private static List<string> BuildGlobalNotes(AcademicTerm? activeTerm, int activeStudentCount, int eligibleProgramCount)
    {
        var notes = new List<string>
        {
            "Tahmin sadece aktif durumdaki, finansal blokesi olmayan ve aktif ana programi bulunan ogrenciler uzerinden hesaplandi.",
            "CGPA tabanli ECTS limitleri mevcut IGpaCalculationService uzerinden hesaplandi.",
            "Ayni ogrenci ayni ders icin yalnizca bir kez sayildi; kategori onceligi Retake > Regular Eligible > UpperClass/Overload olarak uygulandi.",
            "U notu, mevcut registration validation mantigiyla uyumlu olacak sekilde FF/NA ile ayni basarisiz kategoriye dahil edildi.",
            "Registration period ve section bazli schedule conflict kontrolleri talep tahminini yapisal ihtiyac bazli tutmak icin bilerek uygulanmadi.",
            "Aktif donemde Draft/PendingAdvisorApproval/Active durumundaki kayitlar mevcut talep olarak dahil edildi ve ogrencinin ECTS headroom'unu tuketti.",
            "Effective capacity classroom varsa min(section.Capacity, classroom.Capacity), yoksa section.Capacity olarak hesaplandi."
        };

        if (activeTerm == null)
            notes.Add("Aktif akademik donem bulunamadigi icin aktif section verisi capacity fallback'leriyle desteklendi.");

        if (activeStudentCount == 0 || eligibleProgramCount == 0)
            notes.Add("Tahmin havuzunda uygun aktif ogrenci bulunamadigi icin sonuc sifir talep olarak hesaplandi.");

        return notes;
    }

    private static List<string> BuildDepartmentNotes(
        ForecastContextData context,
        IReadOnlyCollection<CourseDemandForecastDto> courseSummaries)
    {
        var notes = new List<string>(context.GlobalNotes)
        {
            "Department toplam talep degeri, course bazli tahminlerin toplami olarak hesaplandi; benzersiz ogrenci sayisi degildir."
        };

        if (courseSummaries.Count == 0)
            notes.Add("Bolum icin aktif mufredat, aktif section veya tahminlenen ogrenci talebi bulunamadi.");

        return notes.Distinct().ToList();
    }

    private sealed class ForecastContextData
    {
        public AcademicTerm? ActiveTerm { get; init; }
        public required Dictionary<int, Course> Courses { get; init; }
        public required List<CourseSection> AllSections { get; init; }
        public required List<StudentForecastSnapshot> StudentSnapshots { get; init; }
        public required HashSet<int> ActiveCurriculumCourseIds { get; init; }
        public required HashSet<int> ActiveTermSectionCourseIds { get; init; }
        public required List<string> GlobalNotes { get; init; }

        public HashSet<int> PredictedDemandCourseIds =>
            StudentSnapshots
                .SelectMany(snapshot => snapshot.RetakeCourseIds
                    .Concat(snapshot.RegularCourseIds)
                    .Concat(snapshot.UpperCourseIds))
                .ToHashSet();
    }

    private sealed class StudentForecastSnapshot
    {
        public int StudentId { get; init; }
        public HashSet<int> RetakeCourseIds { get; init; } = new();
        public HashSet<int> RegularCourseIds { get; init; } = new();
        public HashSet<int> UpperCourseIds { get; init; } = new();
    }

    private sealed record CourseCandidate(
        int CourseId,
        int Ects,
        int SuggestedSemester,
        string CourseCode);

    private sealed class CapacityRecommendation
    {
        public int EffectiveCapacity { get; set; }
        public string CapacityBasis { get; set; } = string.Empty;
        public List<string> Notes { get; } = new();
    }
}
