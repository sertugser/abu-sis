using Microsoft.EntityFrameworkCore;
using SIS.Application.GpaCalculation.DTOs;
using SIS.Application.GpaCalculation.Services;
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
        var registrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Where(cr => cr.StudentId == studentId
                && cr.CourseSection.TermId == termId
                && (cr.Status == RegistrationStatus.Active || cr.Status == RegistrationStatus.Completed)
                && cr.Grade != null)
            .ToListAsync();

        int totalAkts = 0;
        decimal totalPoints = 0;

        foreach (var reg in registrations)
        {
            var point = GradePointMap.GetPoint(reg.Grade!.Value);
            if (point == null) continue; // S, U, W, I, NI → GPA dışı

            var akts = reg.CourseSection.Course.Ects;
            if (akts == 0) continue;

            totalAkts += akts;
            totalPoints += akts * point.Value;
        }

        if (totalAkts == 0) return 0;
        return Math.Round(totalPoints / totalAkts, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// AGNO hesaplar. Aynı ders birden fazla alınmışsa sadece SON not geçerli.
    /// Eski notun AKTS'si toplam AKTS'den çıkar, yeni not yerine geçer.
    /// </summary>
    public async Task<decimal> CalculateCgpaAsync(int studentId)
    {
        var (totalAkts, totalPoints) = await GetCgpaComponentsAsync(studentId);
        if (totalAkts == 0) return 0;
        return Math.Round(totalPoints / totalAkts, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<List<TermGpaHistoryItemDto>> GetTermGpaHistoryAsync(int studentId)
    {
        var registrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
            .Where(cr => cr.StudentId == studentId
                && (cr.Status == RegistrationStatus.Active || cr.Status == RegistrationStatus.Completed)
                && cr.Grade != null)
            .ToListAsync();

        if (registrations.Count == 0)
        {
            return new List<TermGpaHistoryItemDto>();
        }

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
            .OrderBy(entry => entry.Term.Year)
            .ThenBy(entry => entry.Term.Semester)
            .ToList();

        var latestGradesByCourse = new Dictionary<int, Domain.Entities.CourseRegistration>();
        var history = new List<TermGpaHistoryItemDto>(orderedTermGroups.Count);

        foreach (var termGroup in orderedTermGroups)
        {
            foreach (var registration in termGroup.Registrations
                .OrderBy(registration => registration.CourseSection.CourseId)
                .ThenBy(registration => registration.Id))
            {
                latestGradesByCourse[registration.CourseSection.CourseId] = registration;
            }

            var sgpa = CalculateAverage(termGroup.Registrations);
            var cgpa = CalculateAverage(latestGradesByCourse.Values);

            history.Add(new TermGpaHistoryItemDto
            {
                TermId = termGroup.Term.Id,
                TermName = termGroup.Term.Name,
                Year = termGroup.Term.Year,
                Semester = termGroup.Term.Semester.ToString(),
                CourseCount = termGroup.Registrations.Count,
                TotalCredits = termGroup.Registrations.Sum(registration =>
                    registration.CourseSection.Course.TheoryHours + registration.CourseSection.Course.LabHours),
                TotalEcts = termGroup.Registrations.Sum(registration => registration.CourseSection.Course.Ects),
                Sgpa = sgpa,
                Cgpa = cgpa
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

        // Son dönemin term ID'sini bul
        var latestTermId = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
            .Where(cr => cr.StudentId == studentId
                && (cr.Status == RegistrationStatus.Active || cr.Status == RegistrationStatus.Completed)
                && cr.Grade != null)
            .Select(cr => cr.CourseSection.TermId)
            .Distinct()
            .OrderByDescending(termId => termId)
            .FirstOrDefaultAsync();

        var sgpa = latestTermId > 0
            ? await CalculateSgpaAsync(studentId, latestTermId)
            : 0;

        var (totalAkts, totalPoints) = await GetCgpaComponentsAsync(studentId);
        var cgpa = totalAkts > 0
            ? Math.Round(totalPoints / totalAkts, 2, MidpointRounding.AwayFromZero)
            : 0;

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
    /// AGNO bileşenlerini hesaplar: her dersin sadece son notunu alır.
    /// </summary>
    private async Task<(int totalAkts, decimal totalPoints)> GetCgpaComponentsAsync(int studentId)
    {
        var allRegistrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
            .Where(cr => cr.StudentId == studentId
                && (cr.Status == RegistrationStatus.Active || cr.Status == RegistrationStatus.Completed)
                && cr.Grade != null)
            .ToListAsync();

        // Her ders için sadece son notu al (en son dönem, en son kayıt)
        var latestPerCourse = allRegistrations
            .GroupBy(cr => cr.CourseSection.CourseId)
            .Select(g => g
                .OrderByDescending(cr => cr.CourseSection.Term.Year)
                .ThenByDescending(cr => cr.CourseSection.Term.Semester)
                .ThenByDescending(cr => cr.Id)
                .First())
            .ToList();

        int totalAkts = 0;
        decimal totalPoints = 0;

        foreach (var reg in latestPerCourse)
        {
            var point = GradePointMap.GetPoint(reg.Grade!.Value);
            if (point == null) continue; // S, U, W, I, NI → GPA dışı

            var akts = reg.CourseSection.Course.Ects;
            if (akts == 0) continue;

            totalAkts += akts;
            totalPoints += akts * point.Value;
        }

        return (totalAkts, totalPoints);
    }

    private static decimal CalculateAverage(IEnumerable<Domain.Entities.CourseRegistration> registrations)
    {
        var totalAkts = 0;
        decimal totalPoints = 0;

        foreach (var registration in registrations)
        {
            if (registration.Grade is not { } grade)
            {
                continue;
            }

            var point = GradePointMap.GetPoint(grade);
            if (point == null)
            {
                continue;
            }

            var akts = registration.CourseSection.Course.Ects;
            if (akts <= 0)
            {
                continue;
            }

            totalAkts += akts;
            totalPoints += akts * point.Value;
        }

        if (totalAkts == 0)
        {
            return 0;
        }

        return Math.Round(totalPoints / totalAkts, 2, MidpointRounding.AwayFromZero);
    }
}
