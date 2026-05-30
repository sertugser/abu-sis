using SIS.Application.StudentCard.DTOs;
using SIS.Application.StudentCard.Services;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Services.Gpa;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Transkript Senaryosu (what-if) hesabının TEK uygulaması. Harf→puan / geçti / GPA'ya girer
/// kuralları <see cref="GradePointMap"/> + <see cref="GpaMath"/>'tan okunur — transkript ile
/// AYNI kaynak, ikibaşlılık yok. Saf/stateless: DB'ye dokunmaz, yalnızca öğrencinin ekranda
/// simüle ettiği ders listesiyle çalışır (sistemde kalıcı değişiklik yapılmaz).
///
/// Eski frontend kopyasındaki üç gizli hata burada kökten kapanır:
///  - NI artık "geçti" sayılır (GpaMath.IsPassedGrade) → Toplam Geçilen AKTS doğru.
///  - Bilinmeyen harf güvenli "GPA-dışı"a düşer (eski kod sessizce FF=0 yapıyordu).
///  - GPA'ya giren ders yoksa ortalama null döner ("veri yok" → UI "—"; sahte 0,00 değil).
/// </summary>
public sealed class TranscriptScenarioService : ITranscriptScenarioService
{
    private readonly record struct GradeMeta(decimal? Point, bool Passed, bool CountsTowardsGpa);

    public ScenarioReportDto Calculate(TranscriptScenarioRequestDto request)
    {
        var sortedTerms = request.Terms.OrderBy(term => term.Order).ToList();
        var sortedCourses = request.Courses.OrderBy(course => course.SortOrder).ToList();
        var coursesById = sortedCourses.ToDictionary(course => course.ScenarioId);

        var chainIdByScenarioId = sortedCourses.ToDictionary(
            course => course.ScenarioId,
            course => ResolveChainId(course, coursesById, new HashSet<string>()));

        var repeatedChainCounts = new Dictionary<string, int>();
        var failedAttemptRepairByChainId = new Dictionary<string, int>();

        foreach (var course in sortedCourses)
        {
            var chainId = chainIdByScenarioId.GetValueOrDefault(course.ScenarioId, course.ScenarioId);
            repeatedChainCounts[chainId] = repeatedChainCounts.GetValueOrDefault(chainId) + 1;

            var originalMeta = course.OriginalGrade is { } original ? GetGradeMeta(original) : (GradeMeta?)null;
            var currentMeta = GetGradeMeta(course.Grade);
            if (course.SourceRegistrationId != null
                && originalMeta is { Passed: false }
                && currentMeta is { Passed: true, CountsTowardsGpa: true }
                && !failedAttemptRepairByChainId.ContainsKey(chainId))
            {
                failedAttemptRepairByChainId[chainId] = course.SortOrder;
            }
        }

        var latestByChainId = new Dictionary<string, ScenarioComputedCourseDto>();
        var termReports = new List<ScenarioTermReportDto>(sortedTerms.Count);

        foreach (var term in sortedTerms)
        {
            var termCourses = sortedCourses
                .Where(course => course.TermId == term.TermId)
                .Select(course => BuildComputedCourse(course, chainIdByScenarioId, failedAttemptRepairByChainId, repeatedChainCounts))
                .ToList();

            foreach (var course in termCourses)
            {
                // Notu HENÜZ GİRİLMEMİŞ ders (projeksiyon/yeni — kullanıcı harf seçmedi) zinciri
                // ETKİLEMEZ: daha önce alınmış gerçek not korunur. Aksi halde boş notlu "son deneme"
                // eski geçerli notu ortalamadan düşürürdü (örn. tekrar alınan dersin eski notu silinip
                // AGNO sapardı). Kullanıcı harf seçer seçmez "son deneme geçerli" kuralı uygulanır.
                if (string.IsNullOrWhiteSpace(course.Grade))
                    continue;

                if (!course.CountsTowardsGpa && WasSupersededByRepairedFailure(course, failedAttemptRepairByChainId))
                    continue;

                latestByChainId[course.ChainId] = course;
            }

            var cumulativeCourses = latestByChainId.Values.ToList();

            termReports.Add(new ScenarioTermReportDto
            {
                TermId = term.TermId,
                TermName = term.TermName,
                Year = term.Year,
                Semester = term.Semester,
                AttemptedCredits = termCourses.Sum(course => course.Credits),
                AttemptedEcts = termCourses.Sum(course => course.Ects),
                EarnedCredits = termCourses.Where(course => course.IsPassed).Sum(course => course.Credits),
                EarnedEcts = termCourses.Where(course => course.IsPassed).Sum(course => course.Ects),
                GpaEcts = termCourses.Where(course => course.CountsTowardsGpa).Sum(course => course.Ects),
                GpaPoints = Round2(termCourses.Sum(course => course.WeightedEcts ?? 0m)),
                CumulativeGpaEcts = cumulativeCourses.Where(course => course.CountsTowardsGpa).Sum(course => course.Ects),
                CumulativeGpaPoints = Round2(cumulativeCourses.Sum(course => course.WeightedEcts ?? 0m)),
                Sgpa = CalculateAverage(termCourses),
                Cgpa = CalculateAverage(cumulativeCourses),
                Courses = termCourses,
            });
        }

        var latestCourses = latestByChainId.Values.ToList();

        return new ScenarioReportDto
        {
            CurrentCgpa = CalculateAverage(latestCourses),
            TotalAttemptedCredits = latestCourses.Sum(course => course.Credits),
            TotalAttemptedEcts = latestCourses.Sum(course => course.Ects),
            TotalEarnedCredits = latestCourses.Where(course => course.IsPassed).Sum(course => course.Credits),
            TotalEarnedEcts = latestCourses.Where(course => course.IsPassed).Sum(course => course.Ects),
            TotalGpaEcts = latestCourses.Where(course => course.CountsTowardsGpa).Sum(course => course.Ects),
            TotalGpaPoints = Round2(latestCourses.Sum(course => course.WeightedEcts ?? 0m)),
            Terms = termReports,
        };
    }

    private ScenarioComputedCourseDto BuildComputedCourse(
        ScenarioCourseInputDto course,
        IReadOnlyDictionary<string, string> chainIdByScenarioId,
        IReadOnlyDictionary<string, int> failedAttemptRepairByChainId,
        IReadOnlyDictionary<string, int> repeatedChainCounts)
    {
        var meta = GetGradeMeta(course.Grade);
        var chainId = chainIdByScenarioId.GetValueOrDefault(course.ScenarioId, course.ScenarioId);

        var isSupersededOriginalRetake = course.SourceRegistrationId != null
            && failedAttemptRepairByChainId.TryGetValue(chainId, out var repairedOrder)
            && course.SortOrder > repairedOrder;

        var countsTowardsGpa = !isSupersededOriginalRetake && !course.ExcludeFromAverage && meta.CountsTowardsGpa;
        var weightedEcts = countsTowardsGpa && meta.Point is { } point
            ? Round2(course.Ects * point)
            : (decimal?)null;

        return new ScenarioComputedCourseDto
        {
            ScenarioId = course.ScenarioId,
            SourceRegistrationId = course.SourceRegistrationId,
            CourseId = course.CourseId,
            TermId = course.TermId,
            CourseCode = course.CourseCode,
            CourseName = course.CourseName,
            Credits = course.Credits,
            Ects = course.Ects,
            AverageScore = course.AverageScore,
            Grade = course.Grade,
            OriginalGrade = course.OriginalGrade,
            ExcludeFromAverage = course.ExcludeFromAverage,
            ContinuationCourseId = course.ContinuationCourseId,
            IsAdded = course.IsAdded,
            IsPlannedFromCurriculum = course.IsPlannedFromCurriculum,
            SortOrder = course.SortOrder,
            GradePoint = countsTowardsGpa ? meta.Point : null,
            CountsTowardsGpa = countsTowardsGpa,
            IsPassed = meta.Passed,
            WeightedEcts = weightedEcts,
            ChainId = chainId,
            IsRepeated = course.WasRepeated || repeatedChainCounts.GetValueOrDefault(chainId) > 1,
        };
    }

    /// <summary>
    /// Harf notu meta verisi TEK kaynaktan: katsayı GradePointMap, geçti mi GpaMath.IsPassedGrade,
    /// GPA'ya girer mi = katsayısı var mı. Geçersiz/bilinmeyen harf → güvenli "GPA-dışı, geçmedi".
    /// </summary>
    private static GradeMeta GetGradeMeta(string grade)
    {
        if (!Enum.TryParse<GradeCode>(grade?.Trim(), ignoreCase: true, out var code))
            return new GradeMeta(Point: null, Passed: false, CountsTowardsGpa: false);

        var point = GradePointMap.GetPoint(code);
        return new GradeMeta(Point: point, Passed: GpaMath.IsPassedGrade(code), CountsTowardsGpa: point != null);
    }

    /// <summary>
    /// Ortalama; GPA'ya giren ders yoksa null döner ("veri yok"). Yalnızca S/U/NI gibi
    /// notlardan oluşan dönem gerçek 0 değil "—" gösterilir (GpaMath.CalculateAverageOrNull ile aynı).
    /// </summary>
    private static decimal? CalculateAverage(IReadOnlyCollection<ScenarioComputedCourseDto> courses)
    {
        var gpaEcts = courses.Where(course => course.CountsTowardsGpa).Sum(course => course.Ects);
        if (gpaEcts == 0)
            return null;

        var gpaPoints = courses.Sum(course => course.WeightedEcts ?? 0m);
        return Round2(gpaPoints / gpaEcts);
    }

    private static bool WasSupersededByRepairedFailure(
        ScenarioComputedCourseDto course,
        IReadOnlyDictionary<string, int> failedAttemptRepairByChainId)
        => course.SourceRegistrationId != null
            && failedAttemptRepairByChainId.TryGetValue(course.ChainId, out var repairedOrder)
            && course.SortOrder > repairedOrder;

    private static string ResolveChainId(
        ScenarioCourseInputDto course,
        IReadOnlyDictionary<string, ScenarioCourseInputDto> coursesById,
        HashSet<string> visited)
    {
        if (string.IsNullOrEmpty(course.ContinuationCourseId) || course.ContinuationCourseId == course.ScenarioId)
            return course.ScenarioId;

        if (!visited.Add(course.ScenarioId))
            return course.ScenarioId;

        return coursesById.TryGetValue(course.ContinuationCourseId, out var target)
            ? ResolveChainId(target, coursesById, visited)
            : course.ScenarioId;
    }

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
