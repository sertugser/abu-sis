using Microsoft.EntityFrameworkCore;
using SIS.Application.AcademicProgression.DTOs;
using SIS.Application.AcademicProgression.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Application.Common.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AcademicProgressionService : IAcademicProgressionService
{
    private readonly AppDbContext _context;
    private readonly IGpaCalculationService _gpaCalculationService;
    private readonly ISystemConfigProvider _config;

    private static readonly string[] ClassLabels =
        { "Hazirlik", "1. Sinif", "2. Sinif", "3. Sinif", "4. Sinif", "5. Sinif", "6. Sinif" };

    public AcademicProgressionService(AppDbContext context, IGpaCalculationService gpaCalculationService, ISystemConfigProvider? config = null)
    {
        _context = context;
        _gpaCalculationService = gpaCalculationService;
        _config = config ?? new NullSystemConfigProvider();
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

        // PromotionRule'ları yükle
        var programIds = students
            .SelectMany(s => s.StudentPrograms.Where(sp => sp.IsActive && sp.Type == StudentProgramType.Main))
            .Select(sp => sp.ProgramId)
            .Distinct()
            .ToList();

        var promotionRules = await _context.PromotionRules
            .AsNoTracking()
            .Where(pr => programIds.Contains(pr.ProgramId))
            .ToListAsync(cancellationToken);

        var rulesByProgram = promotionRules
            .GroupBy(pr => pr.ProgramId)
            .ToDictionary(g => g.Key, g => g.ToList());

        result.TotalStudentsAnalyzed = students.Count;
        result.Notes.Add($"{students.Count} aktif ogrenci analiz edildi.");
        result.Notes.Add(dryRun
            ? "DRY RUN — degisiklikler uygulanmadi, sadece simulasyon."
            : "GERCEK CALISTIRMA — degisiklikler veritabanina yazildi.");

        var retentionThreshold = await _config.GetDecimalAsync("GPA.RetentionThreshold", 1.70m, cancellationToken);

        // Batch CGPA hesaplama — N+1 query yerine tek sorgu
        var cgpaMap = await _gpaCalculationService.BatchCalculateCgpaAsync(studentIds);

        // Batch latestTermId hesaplama — dryRun olmayan durumda N+1 önlemi
        var latestTermIdByStudent = !dryRun
            ? await _context.CourseRegistrations
                .Include(cr => cr.CourseSection)
                .Where(cr => studentIds.Contains(cr.StudentId)
                    && (cr.Status == RegistrationStatus.Active || cr.Status == RegistrationStatus.Completed)
                    && cr.Grade != null)
                .GroupBy(cr => cr.StudentId)
                .Select(g => new { StudentId = g.Key, LatestTermId = g.Max(cr => cr.CourseSection.TermId) })
                .ToDictionaryAsync(x => x.StudentId, x => x.LatestTermId, cancellationToken)
            : new Dictionary<int, int>();

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

            var cgpa = cgpaMap.GetValueOrDefault(student.Id, 0m);

            // Mevcut seviye: CompletedSemesters bazlı
            var oldSemesters = student.CompletedSemesters;
            var oldLevel = (oldSemesters / 2) + 1;

            // Yeni seviye: PromotionRule varsa kural bazlı, yoksa AKTS bazlı fallback
            var programId = mainProgram.ProgramId;
            var rules = rulesByProgram.GetValueOrDefault(programId) ?? new List<PromotionRule>();
            var promotionDetails = new List<string>();

            // Mevcut seviyeden başla — geçmişte kazanılmış seviyeyi koruyoruz.
            // Sadece mevcut seviyeden ileriye doğru terfi değerlendirilir.
            int newLevel = Math.Max(1, oldLevel);
            for (int fromYear = newLevel; fromYear < normalDurationYears; fromYear++)
            {
                var rule = rules.FirstOrDefault(r => r.FromYear == fromYear && r.ToYear == fromYear + 1);

                if (rule != null)
                {
                    bool canPromote = true;
                    var reasons = new List<string>();

                    if (rule.MinCgpa.HasValue && cgpa < rule.MinCgpa.Value)
                    {
                        canPromote = false;
                        reasons.Add($"CGPA {cgpa:F2} < {rule.MinCgpa.Value:F2} gerekli");
                    }
                    if (rule.MinEcts.HasValue && passedCourseEcts < rule.MinEcts.Value)
                    {
                        canPromote = false;
                        reasons.Add($"AKTS {passedCourseEcts} < {rule.MinEcts.Value} gerekli");
                    }
                    if (rule.MinCourseCount.HasValue)
                    {
                        var passedCourseCount = studentRegs
                            .Where(r => RegistrationRulePolicy.IsPassedGrade(r.Grade))
                            .Select(r => r.CourseSection.CourseId)
                            .Distinct()
                            .Count();
                        if (passedCourseCount < rule.MinCourseCount.Value)
                        {
                            canPromote = false;
                            reasons.Add($"Ders sayisi {passedCourseCount} < {rule.MinCourseCount.Value} gerekli");
                        }
                    }
                    if (rule.MaxFailedCourses.HasValue)
                    {
                        var failedCourseCount = studentRegs
                            .Where(r => r.Grade == GradeCode.FF || r.Grade == GradeCode.NA || r.Grade == GradeCode.U)
                            .Select(r => r.CourseSection.CourseId)
                            .Distinct()
                            .Count();
                        if (failedCourseCount > rule.MaxFailedCourses.Value)
                        {
                            canPromote = false;
                            reasons.Add($"Basarisiz ders {failedCourseCount} > max {rule.MaxFailedCourses.Value}");
                        }
                    }
                    if (rule.MinCompulsoryCoursePassed.HasValue)
                    {
                        // Öğrencinin müfredatındaki zorunlu derslerden geçtiği sayı
                        var passedCompulsoryCourseIds = studentRegs
                            .Where(r => RegistrationRulePolicy.IsPassedGrade(r.Grade))
                            .Select(r => r.CourseSection.CourseId)
                            .Distinct()
                            .ToHashSet();

                        var compulsoryPassedCount = await _context.CurriculumCourses
                            .AsNoTracking()
                            .Where(cc => cc.CurriculumId == mainProgram.CurriculumId
                                && cc.IsCompulsory
                                && passedCompulsoryCourseIds.Contains(cc.CourseId))
                            .CountAsync(cancellationToken);

                        if (compulsoryPassedCount < rule.MinCompulsoryCoursePassed.Value)
                        {
                            canPromote = false;
                            reasons.Add($"Zorunlu ders {compulsoryPassedCount} < {rule.MinCompulsoryCoursePassed.Value} gerekli");
                        }
                    }

                    if (canPromote)
                    {
                        newLevel = fromYear + 1;
                        promotionDetails.Add($"{fromYear}→{fromYear + 1}: Gecti (kural bazli)");
                    }
                    else
                    {
                        promotionDetails.Add($"{fromYear}→{fromYear + 1}: Engellendi — {string.Join(", ", reasons)}");
                        break; // Seviye atlanamaz
                    }
                }
                else
                {
                    // C9: Kural tanımlı değil — AKTS bazlı fallback + uyarı
                    result.Notes.Add($"Uyari: {program?.Name ?? "Bilinmeyen program"} icin {fromYear}->{fromYear + 1} gecis kurali tanimlanmamis, AKTS bazli fallback kullanildi.");
                    var ectsForLevel = ectsPerYear * fromYear;
                    if (passedCourseEcts >= ectsForLevel)
                    {
                        newLevel = fromYear + 1;
                        promotionDetails.Add($"{fromYear}→{fromYear + 1}: Gecti (AKTS bazli fallback, {passedCourseEcts}/{ectsForLevel:F0})");
                    }
                    else
                    {
                        promotionDetails.Add($"{fromYear}→{fromYear + 1}: Engellendi (AKTS {passedCourseEcts} < {ectsForLevel:F0})");
                        break;
                    }
                }
            }
            newLevel = Math.Clamp(newLevel, 1, normalDurationYears);

            // C8: CompletedSemesters: seviye farkı kadar artır (birden fazla sınıf atlayabilir)
            var newSemesters = newLevel > oldLevel
                ? oldSemesters + (newLevel - oldLevel)
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
                WasPromoted = newLevel > oldLevel,
                PromotionRuleDetails = promotionDetails
            };

            // PromotionRule engel uyarıları
            foreach (var detail in promotionDetails.Where(d => d.Contains("Engellendi")))
            {
                var dashIndex = detail.IndexOf("—");
                if (dashIndex >= 0)
                {
                    var prefix = detail[..dashIndex].Trim();
                    var reason = detail[(dashIndex + 1)..].Trim();
                    dto.Warnings.Add($"Sinif gecis engeli ({prefix}): {reason}");
                }
            }

            // Uyarılar
            if (cgpa < retentionThreshold && oldSemesters >= 2)
            {
                dto.Status = "Warning-LowCGPA";
                dto.Warnings.Add($"CGPA kritik seviyede: {cgpa:F2} (< {retentionThreshold:F2})");
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

                // SGPA güncelle: batch'te hesaplanan latestTermId kullan
                var latestTermId = latestTermIdByStudent.GetValueOrDefault(student.Id, 0);

                if (latestTermId > 0)
                {
                    var sgpa = await _gpaCalculationService.CalculateSgpaAsync(student.Id, latestTermId);
                    student.Sgpa = Math.Round(sgpa, 4);
                }
                else
                {
                    // C7: SGPA stale — no graded term found, reset to 0
                    student.Sgpa = 0m;
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
                .FirstOrDefaultAsync(t => t.Status == TermStatus.Ready, cancellationToken);

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
