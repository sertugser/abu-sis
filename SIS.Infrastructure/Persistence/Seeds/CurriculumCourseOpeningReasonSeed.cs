using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence.Seeds;

/// <summary>
/// Seeds the 21 standard "Açma Nedeni" values from Eski Proliz.
/// Idempotent — only inserts missing rows by Code.
/// "Dönemsel (Teorik)" carries the special bypass flags that match Proliz behavior.
/// </summary>
public static class CurriculumCourseOpeningReasonSeed
{
    public static readonly IReadOnlyList<CurriculumCourseOpeningReason> All = new List<CurriculumCourseOpeningReason>
    {
        New("Periodic", "Dönemsel", null, 10),
        New("Erasmus", "Erasmus", null, 20),
        New("OutOfField", "Alan Dışı", null, 30),
        New("Internship", "Staj", null, 40),
        New("LangSupport", "Y.Dil Destek", null, 50),
        New("Hobby", "Hobi", null, 60),
        New("DistanceEducation", "Uzaktan Eğitim", null, 70),
        New(
            "PeriodicTheory",
            "Dönemsel (Teorik)",
            "Alttan uyg. ders devam zorunluluğu çakışma kontrolü bu ders için geçersiz",
            80,
            bypassScheduleConflict: true,
            bypassMandatoryAttendance: true),
        New("Preparatory", "Hazırlık", null, 90),
        New("Thesis", "Tez", null, 100),
        New("ExpertiseField", "Uzmanlık Alan", null, 110),
        New("Seminar", "Seminer", null, 120),
        New("Counseling", "Danışmanlık", null, 130),
        New("MidtermBarrier", "Vize Barajlı", null, 140),
        New("MedServiceCourses", "Tıp Servis Dersleri", null, 150),
        New("MedCommittee", "Tıp Kurul", null, 160),
        New("MedInternship", "Tıp Staj", null, 170),
        New("MedInternIntern", "Tıp Staj(İntörn)", null, 180),
        New("MedCommitteeBarrier", "Tıp Kurul Vize Barajlı", null, 190),
        New("DentalApplication", "Diş Uygulama", null, 200),
        New("Other", "Diğer", null, 210),
    };

    public static async Task SeedAsync(AppDbContext context)
    {
        var existing = await context.CurriculumCourseOpeningReasons
            .Select(r => r.Code)
            .ToListAsync();
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toInsert = All.Where(r => !existingSet.Contains(r.Code)).ToList();
        if (toInsert.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var r in toInsert) r.CreatedAt = now;

        context.CurriculumCourseOpeningReasons.AddRange(toInsert);
        await context.SaveChangesAsync();
    }

    private static CurriculumCourseOpeningReason New(
        string code, string name, string? description, int sortOrder,
        bool allowsAdvisorGrading = false,
        bool bypassScheduleConflict = false,
        bool bypassMandatoryAttendance = false,
        bool isExcludedFromGpa = false,
        bool isCreditFree = false,
        bool requiresApproval = false,
        bool allowsCustomGradingScale = false) => new()
    {
        Code = code,
        Name = name,
        Description = description,
        SortOrder = sortOrder,
        IsActive = true,
        AllowsAdvisorGrading = allowsAdvisorGrading,
        BypassScheduleConflict = bypassScheduleConflict,
        BypassMandatoryAttendance = bypassMandatoryAttendance,
        IsExcludedFromGpa = isExcludedFromGpa,
        IsCreditFree = isCreditFree,
        RequiresApproval = requiresApproval,
        AllowsCustomGradingScale = allowsCustomGradingScale,
    };
}
