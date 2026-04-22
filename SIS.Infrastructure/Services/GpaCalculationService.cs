using Microsoft.EntityFrameworkCore;
using SIS.Application.GpaCalculation.DTOs;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class GpaCalculationService : IGpaCalculationService
{
    private readonly AppDbContext _context;

    public GpaCalculationService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Belirli bir dönemin ANO'sunu hesaplar.
    /// Tekrar alınan dersler dahil — o dönemde alınan her ders sayılır.
    /// </summary>
    public async Task<decimal> CalculateSgpaAsync(int studentId, int termId)
    {
        var registrations = await GetGradedRegistrationsQuery()
            .Where(cr => cr.StudentId == studentId && cr.CourseSection.TermId == termId)
            .ToListAsync();

        return CalculateAverage(registrations);
    }

    /// <summary>
    /// AGNO hesaplar. Aynı ders veya eşdeğer ders zinciri birden fazla alınmışsa sadece SON not geçerli.
    /// </summary>
    public async Task<decimal> CalculateCgpaAsync(int studentId)
    {
        var registrations = await GetGradedRegistrationsQuery()
            .Where(cr => cr.StudentId == studentId)
            .ToListAsync();

        var (totalAkts, totalPoints) = await GetCgpaComponentsAsync(registrations);
        if (totalAkts == 0)
            return 0m;

        return Math.Round(totalPoints / totalAkts, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<List<TermGpaHistoryItemDto>> GetTermGpaHistoryAsync(int studentId)
    {
        var registrations = await GetGradedRegistrationsQuery()
            .Where(cr => cr.StudentId == studentId)
            .ToListAsync();

        if (registrations.Count == 0)
            return new List<TermGpaHistoryItemDto>();

        var courseChainIdByCourseId = await BuildCourseChainIdMapAsync(
            registrations.Select(registration => registration.CourseSection.CourseId));

        var orderedTermGroups = registrations
            .GroupBy(cr => cr.CourseSection.TermId)
            .Select(group => new
            {
                Term = group.First().CourseSection.Term,
                Registrations = group
                    .OrderBy(cr => cr.CourseSection.Course.Code)
                    .ThenBy(cr => cr.Id)
                    .ToList()
            })
            .OrderBy(entry => entry.Term.TermStart)
            .ThenBy(entry => entry.Term.Id)
            .ToList();

        var latestGradesByChain = new Dictionary<int, CourseRegistration>();
        var history = new List<TermGpaHistoryItemDto>(orderedTermGroups.Count);

        foreach (var termGroup in orderedTermGroups)
        {
            foreach (var registration in termGroup.Registrations.OrderBy(registration => registration.Id))
            {
                var chainId = courseChainIdByCourseId.GetValueOrDefault(
                    registration.CourseSection.CourseId,
                    registration.CourseSection.CourseId);
                latestGradesByChain[chainId] = registration;
            }

            history.Add(new TermGpaHistoryItemDto
            {
                TermId = termGroup.Term.Id,
                TermName = termGroup.Term.Name,
                Year = termGroup.Term.Year,
                Semester = termGroup.Term.TermType.ToString(),
                CourseCount = termGroup.Registrations.Count,
                TotalCredits = termGroup.Registrations.Sum(registration =>
                    registration.CourseSection.Course.TheoryHours + registration.CourseSection.Course.LabHours),
                TotalEcts = termGroup.Registrations.Sum(registration =>
                    registration.EctsSnapshot ?? registration.CourseSection.Course.Ects),
                Sgpa = CalculateAverage(termGroup.Registrations),
                Cgpa = CalculateAverage(latestGradesByChain.Values)
            });
        }

        return history;
    }

    /// <summary>
    /// Hem ANO hem AGNO hesaplar ve Student tablosunu günceller.
    /// </summary>
    public async Task<GpaResultDto> RecalculateAndUpdateAsync(int studentId)
    {
        var student = await _context.Students.FindAsync(studentId)
            ?? throw new InvalidOperationException($"Student {studentId} bulunamadi.");

        var registrations = await GetGradedRegistrationsQuery()
            .Where(cr => cr.StudentId == studentId)
            .ToListAsync();

        var latestTermRegistrations = registrations
            .GroupBy(registration => registration.CourseSection.TermId)
            .Select(group => new
            {
                TermStart = group.First().CourseSection.Term.TermStart,
                TermId = group.Key,
                Registrations = group.ToList()
            })
            .OrderByDescending(group => group.TermStart)
            .ThenByDescending(group => group.TermId)
            .FirstOrDefault();

        var sgpa = latestTermRegistrations == null
            ? 0m
            : CalculateAverage(latestTermRegistrations.Registrations);

        var (totalAkts, totalPoints) = await GetCgpaComponentsAsync(registrations);
        var cgpa = totalAkts > 0
            ? Math.Round(totalPoints / totalAkts, 2, MidpointRounding.AwayFromZero)
            : 0m;

        student.Sgpa = sgpa;
        student.Cgpa = cgpa;
        await _context.SaveChangesAsync();

        return new GpaResultDto
        {
            Sgpa = sgpa,
            Cgpa = cgpa,
            TotalAkts = totalAkts,
            TotalPoints = totalPoints
        };
    }

    /// <summary>
    /// AGNO bileşenlerini hesaplar: her ders/eşdeğer zincir için sadece son notu alır.
    /// </summary>
    private async Task<(int totalAkts, decimal totalPoints)> GetCgpaComponentsAsync(
        IReadOnlyCollection<CourseRegistration> registrations)
    {
        var latestRegistrations = await GetLatestRegistrationsByChainAsync(registrations);
        return GetAverageComponents(latestRegistrations);
    }

    /// <summary>
    /// Batch CGPA hesaplama: Tüm öğrencilerin kayıtlarını TEK sorguda yükler,
    /// her öğrenci için in-memory CGPA hesaplar. N+1 sorununu ortadan kaldırır.
    /// </summary>
    public async Task<Dictionary<int, decimal>> BatchCalculateCgpaAsync(List<int> studentIds)
    {
        if (studentIds.Count == 0)
            return new Dictionary<int, decimal>();

        var distinctStudentIds = studentIds.Distinct().ToList();
        var allRegistrations = await GetGradedRegistrationsQuery()
            .Where(cr => distinctStudentIds.Contains(cr.StudentId))
            .ToListAsync();
        var courseChainIdByCourseId = await BuildCourseChainIdMapAsync(
            allRegistrations.Select(registration => registration.CourseSection.CourseId));
        var registrationsByStudent = allRegistrations
            .GroupBy(registration => registration.StudentId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var result = new Dictionary<int, decimal>(distinctStudentIds.Count);

        foreach (var studentId in distinctStudentIds)
        {
            if (!registrationsByStudent.TryGetValue(studentId, out var studentRegs) || studentRegs.Count == 0)
            {
                result[studentId] = 0m;
                continue;
            }

            var latestRegistrations = GetLatestRegistrationsByChain(studentRegs, courseChainIdByCourseId);
            result[studentId] = CalculateAverage(latestRegistrations);
        }

        return result;
    }

    private IQueryable<CourseRegistration> GetGradedRegistrationsQuery() =>
        _context.CourseRegistrations
            .AsNoTracking()
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
            .Where(cr => cr.Grade != null
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Dropped);

    private async Task<List<CourseRegistration>> GetLatestRegistrationsByChainAsync(
        IReadOnlyCollection<CourseRegistration> registrations)
    {
        if (registrations.Count == 0)
            return new List<CourseRegistration>();

        var courseChainIdByCourseId = await BuildCourseChainIdMapAsync(
            registrations.Select(registration => registration.CourseSection.CourseId));
        return GetLatestRegistrationsByChain(registrations, courseChainIdByCourseId);
    }

    private static List<CourseRegistration> GetLatestRegistrationsByChain(
        IEnumerable<CourseRegistration> registrations,
        IReadOnlyDictionary<int, int> courseChainIdByCourseId)
    {
        return registrations
            .GroupBy(registration => courseChainIdByCourseId.GetValueOrDefault(
                registration.CourseSection.CourseId,
                registration.CourseSection.CourseId))
            .Select(group => group
                .OrderByDescending(registration => registration.CourseSection.Term.TermStart)
                .ThenByDescending(registration => registration.CourseSection.Term.Id)
                .ThenByDescending(registration => registration.Id)
                .First())
            .ToList();
    }

    private async Task<Dictionary<int, int>> BuildCourseChainIdMapAsync(IEnumerable<int> courseIds)
    {
        var distinctCourseIds = courseIds.Distinct().ToList();
        if (distinctCourseIds.Count == 0)
            return new Dictionary<int, int>();

        var relevantCourseIds = distinctCourseIds.ToHashSet();
        var equivalentCourseIdsByCourseId = distinctCourseIds.ToDictionary(
            courseId => courseId,
            _ => new HashSet<int>());

        var equivalencies = await _context.CourseEquivalencies
            .AsNoTracking()
            .Where(equivalency =>
                relevantCourseIds.Contains(equivalency.CourseId)
                || relevantCourseIds.Contains(equivalency.EquivalentCourseId))
            .ToListAsync();

        foreach (var equivalency in equivalencies)
        {
            if (!relevantCourseIds.Contains(equivalency.CourseId)
                || !relevantCourseIds.Contains(equivalency.EquivalentCourseId))
            {
                continue;
            }

            equivalentCourseIdsByCourseId[equivalency.CourseId].Add(equivalency.EquivalentCourseId);
            equivalentCourseIdsByCourseId[equivalency.EquivalentCourseId].Add(equivalency.CourseId);
        }

        return BuildCourseChainIdMap(distinctCourseIds, equivalentCourseIdsByCourseId);
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

    private static (int totalAkts, decimal totalPoints) GetAverageComponents(IEnumerable<CourseRegistration> registrations)
    {
        var totalAkts = 0;
        decimal totalPoints = 0;

        foreach (var registration in registrations)
        {
            if (registration.Grade is not { } grade)
                continue;

            var point = GradePointMap.GetPoint(grade);
            if (point == null)
                continue;

            // Madde 7: Kayıt anında donan AKTS değeri önceliklidir; yoksa dersin katalog değeri
            // (geriye uyumlu — eski kayıtlar için snapshot henüz yok).
            var akts = registration.EctsSnapshot ?? registration.CourseSection.Course.Ects;
            if (akts <= 0)
                continue;

            totalAkts += akts;
            totalPoints += akts * point.Value;
        }

        return (totalAkts, totalPoints);
    }

    private static decimal CalculateAverage(IEnumerable<CourseRegistration> registrations)
    {
        var (totalAkts, totalPoints) = GetAverageComponents(registrations);
        if (totalAkts == 0)
            return 0m;

        return Math.Round(totalPoints / totalAkts, 2, MidpointRounding.AwayFromZero);
    }
}
