using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.GpaCalculation.DTOs;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Gpa;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Tek GPA motoru. Tüm ANO/AGNO, kredi/AKTS ve eşdeğer ders zinciri hesabı buradan ve
/// <see cref="GpaMath"/>'tan okur (transkript de aynı çekirdeği kullanır → ikibaşlılık yok).
///
/// Not ilan görünürlüğü YALNIZCA öğrenci/yönetici görüntüleme metodunda
/// (<see cref="GetTermGpaHistoryAsync"/>) uygulanır. Saklanan/karar veren hesaplar
/// (Recalculate, Batch, CalculateCgpa/Sgpa) gerçek notlarla çalışır — yoksa not ilan
/// edilmeden student.Cgpa düşer, ilanla zıplardı.
/// </summary>
public class GpaCalculationService : IGpaCalculationService
{
    private readonly AppDbContext _context;
    private readonly IGradeVisibilityService _gradeVisibility;

    public GpaCalculationService(AppDbContext context, IGradeVisibilityService gradeVisibility)
    {
        _context = context;
        _gradeVisibility = gradeVisibility;
    }

    /// <summary>
    /// Belirli bir dönemin ANO'sunu hesaplar. Tekrar alınan dersler dahil — o dönemde
    /// alınan her ders sayılır. (Karar hesabı — görünürlük uygulanmaz.)
    /// </summary>
    public async Task<decimal> CalculateSgpaAsync(int studentId, int termId)
    {
        var registrations = await GetGradedRegistrationsQuery()
            .Where(cr => cr.StudentId == studentId && cr.CourseSection.TermId == termId)
            .ToListAsync();

        return GpaMath.CalculateAverage(registrations);
    }

    /// <summary>
    /// AGNO hesaplar. Aynı ders veya eşdeğer ders zinciri birden fazla alınmışsa sadece
    /// SON not geçerli. (Karar hesabı — görünürlük uygulanmaz.)
    /// </summary>
    public async Task<decimal> CalculateCgpaAsync(int studentId)
    {
        var registrations = await GetGradedRegistrationsQuery()
            .Where(cr => cr.StudentId == studentId)
            .ToListAsync();

        var latest = await GetLatestRegistrationsByChainAsync(registrations);
        return GpaMath.CalculateAverage(latest);
    }

    public async Task<TermGpaHistoryDto> GetTermGpaHistoryAsync(int studentId)
    {
        var registrations = await GetGradedRegistrationsQuery()
            .Where(cr => cr.StudentId == studentId)
            .ToListAsync();

        if (registrations.Count == 0)
            return new TermGpaHistoryDto();

        // Görüntüleme: aktif dönemin henüz ilan edilmemiş notlarını gizle (yalnızca aktif
        // dönem gizlenebilir; o da en yeni dönem olduğundan satır + toplam tutarlı kalır).
        var visibility = await _gradeVisibility.BuildTermGradeVisibilityAsync(
            registrations.Select(cr => cr.CourseSection.TermId));
        registrations = registrations
            .Where(cr => visibility.GetValueOrDefault(cr.CourseSection.TermId, true))
            .ToList();

        if (registrations.Count == 0)
            return new TermGpaHistoryDto();

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
        var items = new List<TermGpaHistoryItemDto>(orderedTermGroups.Count);

        foreach (var termGroup in orderedTermGroups)
        {
            foreach (var registration in termGroup.Registrations.OrderBy(registration => registration.Id))
            {
                var chainId = courseChainIdByCourseId.GetValueOrDefault(
                    registration.CourseSection.CourseId,
                    registration.CourseSection.CourseId);
                latestGradesByChain[chainId] = registration;
            }

            items.Add(new TermGpaHistoryItemDto
            {
                TermId = termGroup.Term.Id,
                TermName = termGroup.Term.Name,
                Year = termGroup.Term.Year,
                Semester = termGroup.Term.TermType.ToString(),
                CourseCount = termGroup.Registrations.Count,
                TotalCredits = termGroup.Registrations.Sum(GpaMath.GetCredit),
                TotalEcts = termGroup.Registrations.Sum(GpaMath.GetEcts),
                Sgpa = GpaMath.CalculateAverageOrNull(termGroup.Registrations),
                Cgpa = GpaMath.CalculateAverageOrNull(latestGradesByChain.Values)
            });
        }

        // Toplam (denenmiş) kredi/AKTS — ders tekrarlarında son alınan ders sayılır.
        var latestRegistrations = GpaMath.GetLatestRegistrationsByChain(registrations, courseChainIdByCourseId);

        return new TermGpaHistoryDto
        {
            Terms = items,
            TotalAttemptedCredits = latestRegistrations.Sum(GpaMath.GetCredit),
            TotalAttemptedEcts = latestRegistrations.Sum(GpaMath.GetEcts)
        };
    }

    /// <summary>
    /// Hem ANO hem AGNO hesaplar ve Student tablosunu günceller. (Karar/saklama hesabı —
    /// görünürlük uygulanmaz; gerçek notlarla.)
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
            : GpaMath.CalculateAverage(latestTermRegistrations.Registrations);

        var latest = await GetLatestRegistrationsByChainAsync(registrations);
        var (totalAkts, totalPoints) = GpaMath.GetAverageComponents(latest);
        var cgpa = GpaMath.CalculateAverage(latest);

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
    /// Batch CGPA hesaplama: Tüm öğrencilerin kayıtlarını TEK sorguda yükler,
    /// her öğrenci için in-memory CGPA hesaplar. N+1 sorununu ortadan kaldırır.
    /// (Karar hesabı — görünürlük uygulanmaz.)
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

            var latestRegistrations = GpaMath.GetLatestRegistrationsByChain(studentRegs, courseChainIdByCourseId);
            result[studentId] = GpaMath.CalculateAverage(latestRegistrations);
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
            .Include(cr => cr.OpeningReasonSnapshot) // Faz 4 — IsExcludedFromGpa flag için
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
        return GpaMath.GetLatestRegistrationsByChain(registrations, courseChainIdByCourseId);
    }

    private async Task<Dictionary<int, int>> BuildCourseChainIdMapAsync(IEnumerable<int> courseIds)
    {
        var distinctCourseIds = courseIds.Distinct().ToList();
        if (distinctCourseIds.Count == 0)
            return new Dictionary<int, int>();

        var equivalencies = await _context.CourseEquivalencies
            .AsNoTracking()
            .Where(equivalency =>
                distinctCourseIds.Contains(equivalency.CourseId)
                || distinctCourseIds.Contains(equivalency.EquivalentCourseId))
            .ToListAsync();

        return GpaMath.BuildCourseChainIdMap(distinctCourseIds, equivalencies);
    }
}
