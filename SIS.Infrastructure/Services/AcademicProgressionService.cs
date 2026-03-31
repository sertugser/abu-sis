using Microsoft.EntityFrameworkCore;
using SIS.Application.AcademicProgression.DTOs;
using SIS.Application.AcademicProgression.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AcademicProgressionService : IAcademicProgressionService
{
    private readonly AppDbContext _context;
    private readonly IGpaCalculationService _gpaCalculationService;

    private static readonly string[] ClassLabels =
        { "Hazirlik", "1. Sinif", "2. Sinif", "3. Sinif", "4. Sinif", "5. Sinif", "6. Sinif" };

    public AcademicProgressionService(AppDbContext context, IGpaCalculationService gpaCalculationService)
    {
        _context = context;
        _gpaCalculationService = gpaCalculationService;
    }

    // ═══════════════════════════════════════════════════════════════
    //  TOPLU SINIF ATLATMA (BATCH PROMOTION)
    // ═══════════════════════════════════════════════════════════════

    public async Task<BatchProgressionResultDto> PromoteDepartmentStudentsAsync(
        int departmentId,
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        var result = new BatchProgressionResultDto();

        var students = await _context.Students
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .Where(s => s.Status == StudentStatus.Active
                && s.StudentPrograms.Any(sp =>
                    sp.Type == StudentProgramType.Main
                    && sp.IsActive
                    && sp.Program.DepartmentId == departmentId))
            .ToListAsync(cancellationToken);

        if (students.Count == 0)
        {
            result.Notes.Add("Bu bolumde aktif ogrenci bulunamadi.");
            return result;
        }

        var studentIds = students.Select(s => s.Id).ToList();
        var userIds = students.Select(s => s.UserId).Distinct().ToList();

        var users = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var registrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(r => r.CourseSection)
                .ThenInclude(s => s.Course)
            .Where(r => studentIds.Contains(r.StudentId))
            .ToListAsync(cancellationToken);

        var registrationsByStudent = registrations
            .GroupBy(r => r.StudentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        result.TotalStudentsAnalyzed = students.Count;
        result.Notes.Add($"{students.Count} aktif ogrenci analiz edildi.");
        result.Notes.Add(dryRun
            ? "DRY RUN — degisiklikler uygulanmadi, sadece simulasyon."
            : "GERCEK CALISTIRMA — degisiklikler veritabanina yazildi.");

        foreach (var student in students)
        {
            var mainProgram = student.StudentPrograms
                .FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive);

            if (mainProgram == null) continue;

            var program = mainProgram.Program;
            var normalDurationYears = GetNormalDurationYears(program);
            var totalRequiredEcts = program?.TotalRequiredEcts ?? 240;
            var ectsPerYear = totalRequiredEcts > 0 && normalDurationYears > 0
                ? (decimal)totalRequiredEcts / normalDurationYears
                : 60m;

            var studentRegs = registrationsByStudent
                .GetValueOrDefault(student.Id, new List<CourseRegistration>());

            var passedCourseEcts = studentRegs
                .Where(r => RegistrationRulePolicy.IsPassedGrade(r.Grade))
                .GroupBy(r => r.CourseSection.CourseId)
                .Sum(g => g.First().CourseSection.Course?.Ects ?? 0);

            var cgpa = await _gpaCalculationService.CalculateCgpaAsync(student.Id);

            // Mevcut seviye: CompletedSemesters bazlı
            var oldSemesters = student.CompletedSemesters;
            var oldLevel = (oldSemesters / 2) + 1;

            // Yeni seviye: AKTS bazlı
            var ectsBasedLevel = ectsPerYear > 0
                ? (int)Math.Floor(passedCourseEcts / ectsPerYear) + 1
                : 1;
            var newLevel = Math.Clamp(ectsBasedLevel, 1, normalDurationYears);

            // CompletedSemesters: en az mevcut değeri koru, seviye yükseldiyse güncelle
            // Tek sayı kaybını önle: sadece gerçekten seviye değiştiyse güncelle
            var newSemesters = newLevel > oldLevel
                ? oldSemesters + 1  // Bir dönem daha tamamlandı
                : oldSemesters;     // Seviye değişmediyse mevcut değeri koru

            var user = users.GetValueOrDefault(student.UserId);
            var fullName = user != null ? $"{user.FirstName} {user.LastName}" : student.StudentNumber;

            var dto = new StudentProgressionDto
            {
                StudentId = student.Id,
                StudentNumber = student.StudentNumber,
                FullName = fullName,
                OldLevel = oldLevel,
                OldLevelLabel = GetClassLabel(oldLevel),
                NewLevel = newLevel,
                NewLevelLabel = GetClassLabel(newLevel),
                CompletedEcts = passedCourseEcts,
                Cgpa = Math.Round(cgpa, 2),
                Sgpa = Math.Round(student.Sgpa, 2),
                WasPromoted = newLevel > oldLevel
            };

            // Uyarılar
            if (cgpa < 1.70m && oldSemesters >= 2)
            {
                dto.Status = "Warning-LowCGPA";
                dto.Warnings.Add($"CGPA kritik seviyede: {cgpa:F2} (< 1.70)");
                result.WarningCount++;
            }
            else if (newLevel < oldLevel)
            {
                dto.Status = "Warning-BehindSchedule";
                dto.Warnings.Add($"Ogrenci AKTS bazli {GetClassLabel(newLevel)} seviyesindeyken kayitlar {GetClassLabel(oldLevel)} gosteriyor.");
                result.WarningCount++;
            }
            else
            {
                dto.Status = "Normal";
            }

            if (dto.WasPromoted)
                result.PromotedCount++;
            else
                result.StayedCount++;

            result.Results.Add(dto);

            // Gerçek güncelleme
            if (!dryRun && newSemesters != oldSemesters)
            {
                student.CompletedSemesters = newSemesters;
                student.Cgpa = Math.Round(cgpa, 4);

                // SGPA güncelle: son dönemin ANO'sunu hesapla
                var latestTermId = await _context.CourseRegistrations
                    .Include(cr => cr.CourseSection)
                    .Where(cr => cr.StudentId == student.Id
                        && (cr.Status == RegistrationStatus.Active || cr.Status == RegistrationStatus.Completed)
                        && cr.Grade != null)
                    .Select(cr => cr.CourseSection.TermId)
                    .Distinct()
                    .OrderByDescending(tid => tid)
                    .FirstOrDefaultAsync(cancellationToken);

                if (latestTermId > 0)
                {
                    var sgpa = await _gpaCalculationService.CalculateSgpaAsync(student.Id, latestTermId);
                    student.Sgpa = Math.Round(sgpa, 4);
                }
            }
        }

        if (!dryRun)
        {
            await _context.SaveChangesAsync(cancellationToken);
            result.Notes.Add("Tum degisiklikler veritabanina yazildi.");
        }

        result.Results = result.Results
            .OrderBy(r => r.StudentNumber)
            .ToList();

        return result;
    }

    // ═══════════════════════════════════════════════════════════════
    //  TOPLU GPA YENİDEN HESAPLAMA
    // ═══════════════════════════════════════════════════════════════

    public async Task<GpaRecalculationResultDto> RecalculateDepartmentGpaAsync(
        int departmentId,
        CancellationToken cancellationToken = default)
    {
        var result = new GpaRecalculationResultDto();

        var students = await _context.Students
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .Where(s => s.Status == StudentStatus.Active
                && s.StudentPrograms.Any(sp =>
                    sp.Type == StudentProgramType.Main
                    && sp.IsActive
                    && sp.Program.DepartmentId == departmentId))
            .ToListAsync(cancellationToken);

        result.TotalStudents = students.Count;

        if (students.Count == 0)
        {
            result.Notes.Add("Bolumde aktif ogrenci bulunamadi.");
            return result;
        }

        foreach (var student in students)
        {
            var newCgpa = await _gpaCalculationService.CalculateCgpaAsync(student.Id);
            var cgpaChanged = Math.Abs(newCgpa - student.Cgpa) > 0.001m;

            if (cgpaChanged)
            {
                student.Cgpa = Math.Round(newCgpa, 4);
                result.ChangedCount++;
            }

            result.RecalculatedCount++;
        }

        await _context.SaveChangesAsync(cancellationToken);

        result.Notes.Add($"{result.RecalculatedCount} ogrenci icin GPA yeniden hesaplandi.");
        if (result.ChangedCount > 0)
            result.Notes.Add($"{result.ChangedCount} ogrencide GPA degisiklik tespit edildi ve guncellendi.");
        else
            result.Notes.Add("Hicbir ogrencide GPA degisikligi tespit edilmedi.");

        return result;
    }

    // ═══════════════════════════════════════════════════════════════
    //  NOT GİRİŞİ EKSİKLİK KONTROLÜ
    // ═══════════════════════════════════════════════════════════════

    public async Task<GradeLockResultDto> CheckGradeLockReadinessAsync(
        int departmentId,
        int? termId = null,
        CancellationToken cancellationToken = default)
    {
        var result = new GradeLockResultDto();

        var resolvedTerm = termId.HasValue
            ? await _context.AcademicTerms.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == termId.Value, cancellationToken)
            : await _context.AcademicTerms.AsNoTracking()
                .FirstOrDefaultAsync(t => t.IsActive, cancellationToken);

        if (resolvedTerm == null)
        {
            result.Notes.Add("Aktif akademik donem bulunamadi.");
            return result;
        }

        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Where(cs => cs.TermId == resolvedTerm.Id
                && cs.IsActive
                && cs.Course.DepartmentId == departmentId)
            .ToListAsync(cancellationToken);

        result.TotalSections = sections.Count;

        if (sections.Count == 0)
        {
            result.AllGradesEntered = false;
            result.HasActiveRegistrations = false;
            result.Notes.Add("Bu bölümde aktif şube bulunamadı.");
            return result;
        }

        var sectionIds = sections.Select(s => s.Id).ToList();

        // Toplam aktif ders kaydı sayısı
        var totalActiveRegistrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(r => sectionIds.Contains(r.CourseSectionId)
                && r.Status == RegistrationStatus.Active)
            .CountAsync(cancellationToken);

        result.TotalActiveRegistrations = totalActiveRegistrations;
        result.HasActiveRegistrations = totalActiveRegistrations > 0;

        if (totalActiveRegistrations == 0)
        {
            result.AllGradesEntered = false;
            result.Notes.Add($"Dönem: {resolvedTerm.Name}");
            result.Notes.Add($"Toplam şube: {sections.Count}");
            result.Notes.Add("Henüz aktif ders kaydı bulunmuyor.");
            return result;
        }

        // Aktif ders kaydı olan ama henüz not girilmemiş öğrenci sayısı
        var registrationsWithoutGrade = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(r => sectionIds.Contains(r.CourseSectionId)
                && r.Status == RegistrationStatus.Active
                && r.Grade == null)
            .CountAsync(cancellationToken);

        result.MissingGradeCount = registrationsWithoutGrade;
        result.AllGradesEntered = registrationsWithoutGrade == 0;

        result.Notes.Add($"Dönem: {resolvedTerm.Name}");
        result.Notes.Add($"Toplam şube: {sections.Count}");
        result.Notes.Add(registrationsWithoutGrade == 0
            ? "Tüm notlar girilmiş, dönem kapatmaya hazır."
            : $"{registrationsWithoutGrade} kayıt için not girişi eksik.");

        return result;
    }

    // ═══════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static int GetNormalDurationYears(AcademicProgram? program)
    {
        if (program == null) return 4;
        return program.DegreeLevel switch
        {
            DegreeLevel.Associate => 2,
            DegreeLevel.Bachelor => 4,
            DegreeLevel.Master => 2,
            DegreeLevel.Doctorate => 4,
            _ => 4
        };
    }

    private static string GetClassLabel(int level)
    {
        if (level <= 0) return "Belirsiz";
        if (level < ClassLabels.Length) return ClassLabels[level];
        return $"{level}. Sinif";
    }
}
