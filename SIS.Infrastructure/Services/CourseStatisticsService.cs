using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.DTOs;
using SIS.Application.Curriculum.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Ders bazlı öğrenci istatistikleri. Pass/fail sınıflandırması merkezi
/// <see cref="RegistrationRulePolicy"/> üzerinden yapılır (drift önlemi).
/// "Toplam" tüm dönemleri kapsar; "Aktif" yalnızca aktif (Kullanıma Açık) dönemi.
/// </summary>
public sealed class CourseStatisticsService(AppDbContext context) : ICourseStatisticsService
{
    // "Dersi alan" sayılan kayıt durumları: koltuğun fiilen oluştuğu durumlar.
    // Draft / PendingAdvisorApproval / Rejected / Dropped sayılmaz (gerçekleşmemiş ya da iptal).
    private static readonly RegistrationStatus[] TookStatuses =
    {
        RegistrationStatus.Active,
        RegistrationStatus.Completed,
        RegistrationStatus.Withdrawn,
    };

    public async Task<CourseIntibakStatisticsDto> GetCourseIntibakStatisticsAsync(int courseId, CancellationToken ct = default)
    {
        // Aktif (Kullanıma Açık) dönem — tek kaynak. Yoksa aktif metrikleri 0 olur.
        var activeTermId = await context.AcademicTerms
            .ActiveTerm()
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(ct);

        // Tek dersin tüm şubelerindeki kayıtları minimal alanlarla çek (öğrenci + not + dönem).
        var rows = await context.CourseRegistrations
            .AsNoTracking()
            .Where(r => r.CourseSection.CourseId == courseId)
            .Where(r => TookStatuses.Contains(r.Status))
            .Select(r => new RegRow(r.StudentId, r.Grade, r.CourseSection.TermId))
            .ToListAsync(ct);

        var allTime = Aggregate(rows);
        var activeTerm = activeTermId is null
            ? (Total: 0, Passed: 0, Failed: 0)
            : Aggregate(rows.Where(r => r.TermId == activeTermId.Value));

        return new CourseIntibakStatisticsDto(
            CourseId: courseId,
            TotalStudents: allTime.Total,
            PassedStudents: allTime.Passed,
            FailedStudents: allTime.Failed,
            ActiveStudents: activeTerm.Total,
            ActivePassedStudents: activeTerm.Passed,
            ActiveFailedStudents: activeTerm.Failed);
    }

    private readonly record struct RegRow(int StudentId, GradeCode? Grade, int TermId);

    /// <summary>
    /// Verilen kayıtları öğrenci bazında (distinct) gruplar; başarı/başarısızlık
    /// öğrencinin en iyi denemesine göre belirlenir (geçeri varsa başarılı; yoksa
    /// ve başarısızı varsa başarısız). Withdrawn/Incomplete ne geçer ne kalır sayılır.
    /// </summary>
    private static (int Total, int Passed, int Failed) Aggregate(IEnumerable<RegRow> regs)
    {
        var perStudent = regs
            .GroupBy(r => r.StudentId)
            .Select(g => new
            {
                HasPass = g.Any(x => RegistrationRulePolicy.IsPassedGrade(x.Grade)),
                HasFail = g.Any(x => RegistrationRulePolicy.IsFailedGrade(x.Grade)),
            })
            .ToList();

        var total = perStudent.Count;
        var passed = perStudent.Count(s => s.HasPass);
        var failed = perStudent.Count(s => !s.HasPass && s.HasFail);
        return (total, passed, failed);
    }
}
