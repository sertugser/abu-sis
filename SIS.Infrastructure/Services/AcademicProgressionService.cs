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

        // γ hibrit: CurriculumId set olanlar müfredat-spesifik override, null olanlar program-bazlı default.
        // Her öğrenci değerlendirilirken önce müfredat-spesifik kural aranır, yoksa program-bazlıya düşer.
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

            var cgpa = cgpaMap.GetValueOrDefault(student.Id, 0m);

            // Mevcut seviye: CompletedSemesters bazlı
            var oldSemesters = student.CompletedSemesters;
            var oldLevel = (oldSemesters / 2) + 1;

            // Yeni seviye: PromotionRule varsa kural bazlı, yoksa AKTS bazlı fallback
            var programId = mainProgram.ProgramId;
            var curriculumId = mainProgram.CurriculumId;
            var rules = rulesByProgram.GetValueOrDefault(programId) ?? new List<PromotionRule>();

            // Toplu işlemle aynı per-öğrenci metrikleri hesapla, sonra ortak değerlendiriciye geç.
            var metrics = await BuildPromotionMetricsAsync(studentRegs, curriculumId, cgpa, cancellationToken);
            var passedCourseEcts = metrics.PassedEcts;

            var eval = EvaluatePromotion(metrics, Math.Max(1, oldLevel), normalDurationYears, ectsPerYear, rules, curriculumId);
            var promotionDetails = eval.Details;
            foreach (var note in eval.FallbackNotes)
                result.Notes.Add(note.Replace("{program}", program?.Name ?? "Bilinmeyen program"));
            int newLevel = Math.Clamp(eval.TargetLevel, 1, normalDurationYears);

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
                .ActiveTerm()
                .FirstOrDefaultAsync(cancellationToken);

        if (resolvedTerm == null)
        {
            result.Notes.Add("Aktif akademik donem bulunamadi.");
            return result;
        }

        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Where(cs => cs.TermId == resolvedTerm.Id
                && cs.Status == CourseSectionStatus.Active
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
    //  TEK ÖĞRENCİ SINIF ATLATMA (PER-STUDENT PROMOTION)
    // ═══════════════════════════════════════════════════════════════

    public async Task<StudentPromotionStatusDto?> EvaluateStudentPromotionAsync(int studentId, CancellationToken ct = default)
    {
        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student == null) return null;

        var mainProgram = student.StudentPrograms
            .FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive);

        var program = mainProgram?.Program;
        var normalDurationYears = GetNormalDurationYears(program);
        var totalRequiredEcts = program?.TotalRequiredEcts ?? 240;
        var ectsPerYear = totalRequiredEcts > 0 && normalDurationYears > 0
            ? (decimal)totalRequiredEcts / normalDurationYears
            : 60m;

        var currentLevel = (student.CompletedSemesters / 2) + 1;

        // mainProgram yoksa kural değerlendirilemez — sadece mevcut seviyeyi bildir.
        if (mainProgram == null)
        {
            return new StudentPromotionStatusDto(
                currentLevel, null, false,
                "Öğrencinin aktif ana programı bulunamadı.",
                normalDurationYears,
                Array.Empty<PromotionCriterionDto>());
        }

        var curriculumId = mainProgram.CurriculumId;

        var studentRegs = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(r => r.CourseSection)
                .ThenInclude(s => s.Course)
            .Where(r => r.StudentId == studentId)
            .ToListAsync(ct);

        var cgpa = await _gpaCalculationService.CalculateCgpaAsync(studentId);

        var rules = await _context.PromotionRules
            .AsNoTracking()
            .Where(pr => pr.ProgramId == mainProgram.ProgramId)
            .ToListAsync(ct);

        var metrics = await BuildPromotionMetricsAsync(studentRegs, curriculumId, cgpa, ct);

        var eval = EvaluatePromotion(metrics, Math.Max(1, currentLevel), normalDurationYears, ectsPerYear, rules, curriculumId);
        var targetLevel = Math.Clamp(eval.TargetLevel, 1, normalDurationYears);

        var canPromote = targetLevel > currentLevel;
        int? eligibleTargetLevel = canPromote ? targetLevel : null;
        var reason = canPromote ? null : eval.BlockReason ?? "Öğrenci bir sonraki sınıfa geçiş kurallarını sağlamıyor.";

        return new StudentPromotionStatusDto(
            currentLevel,
            eligibleTargetLevel,
            canPromote,
            reason,
            normalDurationYears,
            eval.Criteria);
    }

    public async Task SetStudentLevelAsync(int studentId, int targetLevel, CancellationToken ct = default)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct)
            ?? throw new KeyNotFoundException($"Öğrenci bulunamadı: {studentId}");
        if (targetLevel < 1) throw new InvalidOperationException("Sınıf seviyesi 1'den küçük olamaz.");
        // Clean inverse of level = CompletedSemesters/2 + 1
        student.CompletedSemesters = (targetLevel - 1) * 2;
        student.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════
    //  ORTAK SINIF ATLATMA DEĞERLENDİRİCİSİ (batch + per-student)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Per-öğrenci sınıf atlatma metrikleri (toplu ve tekil yolun ortak girdisi).</summary>
    private sealed record PromotionMetrics(
        decimal Cgpa,
        int PassedEcts,
        int PassedCourseCount,
        int FailedCourseCount,
        int CompulsoryPassedCount);

    /// <summary>Ortak değerlendirici çıktısı.</summary>
    private sealed record PromotionEvaluation(
        int TargetLevel,
        List<string> Details,
        List<string> FallbackNotes,
        List<PromotionCriterionDto> Criteria,
        string? BlockReason);

    /// <summary>
    /// Öğrencinin ders kayıtlarından sınıf atlatma metriklerini hesaplar
    /// (toplu işlemle birebir aynı mantık; zorunlu ders sayısı tek DB sorgusuyla).
    /// </summary>
    private async Task<PromotionMetrics> BuildPromotionMetricsAsync(
        List<CourseRegistration> studentRegs,
        int? curriculumId,
        decimal cgpa,
        CancellationToken ct)
    {
        var passedEcts = studentRegs
            .Where(r => RegistrationRulePolicy.IsPassedGrade(r.Grade))
            .GroupBy(r => r.CourseSection.CourseId)
            .Sum(g => g.First().CourseSection.Course?.Ects ?? 0);

        var passedCourseIds = studentRegs
            .Where(r => RegistrationRulePolicy.IsPassedGrade(r.Grade))
            .Select(r => r.CourseSection.CourseId)
            .Distinct()
            .ToHashSet();

        var passedCourseCount = passedCourseIds.Count;

        var failedCourseCount = studentRegs
            .Where(r => r.Grade == GradeCode.FF || r.Grade == GradeCode.NA || r.Grade == GradeCode.U)
            .Select(r => r.CourseSection.CourseId)
            .Distinct()
            .Count();

        var compulsoryPassedCount = await _context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => cc.CurriculumId == curriculumId
                && cc.IsCompulsory
                && passedCourseIds.Contains(cc.CourseId))
            .CountAsync(ct);

        return new PromotionMetrics(cgpa, passedEcts, passedCourseCount, failedCourseCount, compulsoryPassedCount);
    }

    /// <summary>
    /// γ-hibrit kural lookup + kriter kontrolleri + AKTS fallback. Toplu işlem (PromoteDepartmentStudentsAsync)
    /// ve tekil işlem (EvaluateStudentPromotionAsync) bu metodu paylaşır — kural mantığı tek noktada.
    /// Davranış toplu işlemden ÇIKARILDI, değiştirilmedi.
    /// </summary>
    private static PromotionEvaluation EvaluatePromotion(
        PromotionMetrics m,
        int startLevel,
        int normalDurationYears,
        decimal ectsPerYear,
        List<PromotionRule> rules,
        int? curriculumId)
    {
        var details = new List<string>();
        var fallbackNotes = new List<string>();
        var criteria = new List<PromotionCriterionDto>();
        string? blockReason = null;

        int newLevel = startLevel;
        for (int fromYear = startLevel; fromYear < normalDurationYears; fromYear++)
        {
            // γ hibrit lookup: önce müfredat-özel kural, yoksa program-bazlı (CurriculumId == null)
            var rule = rules.FirstOrDefault(r => r.FromYear == fromYear && r.ToYear == fromYear + 1 && r.CurriculumId == curriculumId)
                    ?? rules.FirstOrDefault(r => r.FromYear == fromYear && r.ToYear == fromYear + 1 && r.CurriculumId == null);

            if (rule != null)
            {
                bool canPromote = true;
                var reasons = new List<string>();

                if (rule.MinCgpa.HasValue)
                {
                    var passed = m.Cgpa >= rule.MinCgpa.Value;
                    if (!passed)
                    {
                        canPromote = false;
                        reasons.Add($"CGPA {m.Cgpa:F2} < {rule.MinCgpa.Value:F2} gerekli");
                    }
                    if (fromYear == startLevel)
                        criteria.Add(new PromotionCriterionDto("CGPA", passed, $"{m.Cgpa:F2} / {rule.MinCgpa.Value:F2}"));
                }
                if (rule.MinEcts.HasValue)
                {
                    var passed = m.PassedEcts >= rule.MinEcts.Value;
                    if (!passed)
                    {
                        canPromote = false;
                        reasons.Add($"AKTS {m.PassedEcts} < {rule.MinEcts.Value} gerekli");
                    }
                    if (fromYear == startLevel)
                        criteria.Add(new PromotionCriterionDto("AKTS", passed, $"{m.PassedEcts} / {rule.MinEcts.Value}"));
                }
                if (rule.MinCourseCount.HasValue)
                {
                    var passed = m.PassedCourseCount >= rule.MinCourseCount.Value;
                    if (!passed)
                    {
                        canPromote = false;
                        reasons.Add($"Ders sayisi {m.PassedCourseCount} < {rule.MinCourseCount.Value} gerekli");
                    }
                    if (fromYear == startLevel)
                        criteria.Add(new PromotionCriterionDto("Geçilen Ders Sayısı", passed, $"{m.PassedCourseCount} / {rule.MinCourseCount.Value}"));
                }
                if (rule.MaxFailedCourses.HasValue)
                {
                    var passed = m.FailedCourseCount <= rule.MaxFailedCourses.Value;
                    if (!passed)
                    {
                        canPromote = false;
                        reasons.Add($"Basarisiz ders {m.FailedCourseCount} > max {rule.MaxFailedCourses.Value}");
                    }
                    if (fromYear == startLevel)
                        criteria.Add(new PromotionCriterionDto("Başarısız Ders Sayısı", passed, $"{m.FailedCourseCount} / max {rule.MaxFailedCourses.Value}"));
                }
                if (rule.MinCompulsoryCoursePassed.HasValue)
                {
                    var passed = m.CompulsoryPassedCount >= rule.MinCompulsoryCoursePassed.Value;
                    if (!passed)
                    {
                        canPromote = false;
                        reasons.Add($"Zorunlu ders {m.CompulsoryPassedCount} < {rule.MinCompulsoryCoursePassed.Value} gerekli");
                    }
                    if (fromYear == startLevel)
                        criteria.Add(new PromotionCriterionDto("Geçilen Zorunlu Ders", passed, $"{m.CompulsoryPassedCount} / {rule.MinCompulsoryCoursePassed.Value}"));
                }

                if (canPromote)
                {
                    newLevel = fromYear + 1;
                    details.Add($"{fromYear}→{fromYear + 1}: Gecti (kural bazli)");
                }
                else
                {
                    details.Add($"{fromYear}→{fromYear + 1}: Engellendi — {string.Join(", ", reasons)}");
                    blockReason ??= string.Join(", ", reasons);
                    break; // Seviye atlanamaz
                }
            }
            else
            {
                // C9: Kural tanımlı değil — AKTS bazlı fallback + uyarı
                fallbackNotes.Add($"Uyari: {{program}} icin {fromYear}->{fromYear + 1} gecis kurali tanimlanmamis, AKTS bazli fallback kullanildi.");
                var ectsForLevel = ectsPerYear * fromYear;
                var passed = m.PassedEcts >= ectsForLevel;
                if (fromYear == startLevel)
                    criteria.Add(new PromotionCriterionDto("AKTS (kuralsız fallback)", passed, $"{m.PassedEcts} / {ectsForLevel:F0}"));
                if (passed)
                {
                    newLevel = fromYear + 1;
                    details.Add($"{fromYear}→{fromYear + 1}: Gecti (AKTS bazli fallback, {m.PassedEcts}/{ectsForLevel:F0})");
                }
                else
                {
                    details.Add($"{fromYear}→{fromYear + 1}: Engellendi (AKTS {m.PassedEcts} < {ectsForLevel:F0})");
                    blockReason ??= $"AKTS {m.PassedEcts} < {ectsForLevel:F0} gerekli";
                    break;
                }
            }
        }

        return new PromotionEvaluation(newLevel, details, fallbackNotes, criteria, blockReason);
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
