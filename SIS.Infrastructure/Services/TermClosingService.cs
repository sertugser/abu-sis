using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Application.AcademicProgression.DTOs;
using SIS.Application.AcademicProgression.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Application.TermClosing.DTOs;
using SIS.Application.TermClosing.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class TermClosingService : ITermClosingService
{
    private readonly AppDbContext _context;
    private readonly IAcademicProgressionService _progressionService;
    private readonly IGpaCalculationService _gpaService;

    private static readonly Dictionary<TermClosingStep, string> StepLabels = new()
    {
        { TermClosingStep.GradeLock, "Not Girisi Kilitle" },
        { TermClosingStep.GpaCalculation, "GPA Hesapla" },
        { TermClosingStep.Promotion, "Sinif Atlama" },
        { TermClosingStep.NewTerm, "Yeni Donem Ac" },
    };

    public TermClosingService(
        AppDbContext context,
        IAcademicProgressionService progressionService,
        IGpaCalculationService gpaService)
    {
        _context = context;
        _progressionService = progressionService;
        _gpaService = gpaService;
    }

    public async Task<TermClosingStatusDto> GetStatusAsync(int departmentId, CancellationToken ct = default)
    {
        var term = await GetActiveTermAsync(ct);
        if (term == null)
            return new TermClosingStatusDto
            {
                DepartmentId = departmentId,
                Steps = BuildEmptySteps()
            };

        var progresses = await _context.TermClosingProgresses
            .AsNoTracking()
            .Where(p => p.DepartmentId == departmentId && p.TermId == term.Id)
            .ToListAsync(ct);

        var userIds = progresses
            .Where(p => p.CompletedByUserId.HasValue)
            .Select(p => p.CompletedByUserId!.Value)
            .Distinct()
            .ToList();

        var userNames = userIds.Count > 0
            ? await _context.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}", ct)
            : new Dictionary<int, string>();

        var steps = new List<TermClosingStepStatus>();
        foreach (var step in Enum.GetValues<TermClosingStep>())
        {
            var progress = progresses.FirstOrDefault(p => p.Step == step);
            steps.Add(new TermClosingStepStatus
            {
                Step = step,
                Label = StepLabels[step],
                IsCompleted = progress?.IsCompleted ?? false,
                CompletedAt = progress?.CompletedAt,
                CompletedByName = progress?.CompletedByUserId != null
                    ? userNames.GetValueOrDefault(progress.CompletedByUserId.Value)
                    : null,
                Notes = progress?.Notes
            });
        }

        return new TermClosingStatusDto
        {
            DepartmentId = departmentId,
            TermId = term.Id,
            TermName = term.Name,
            Steps = steps
        };
    }

    public async Task<TermClosingStepResultDto> LockGradesAsync(int departmentId, int userId, CancellationToken ct = default)
    {
        var term = await GetActiveTermAsync(ct);
        if (term == null)
            return new TermClosingStepResultDto { Success = false, Message = "Aktif donem bulunamadi." };

        if (term.Status != TermStatus.GradeEntry && term.Status != TermStatus.Closed)
            return new TermClosingStepResultDto { Success = false, Message = $"Not kilitleme sadece 'Not Girişi' veya 'Kapalı' aşamasında yapılabilir. Mevcut: {term.Status}" };

        // C10: Concurrent access protection
        await MarkInProgressAsync(departmentId, term.Id, TermClosingStep.GradeLock, userId, ct);

        var sections = await _context.CourseSections
            .Include(cs => cs.Course)
            .Where(cs => cs.TermId == term.Id
                && cs.IsActive
                && cs.Course.DepartmentId == departmentId)
            .ToListAsync(ct);

        if (sections.Count == 0)
            return new TermClosingStepResultDto { Success = false, Message = "Bu bolumde aktif section bulunamadi." };

        // C1: Transaction wrapper
        var useTransaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
        await using var transaction = useTransaction
            ? await _context.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            var allSectionIds = sections.Select(s => s.Id).ToList();
            var courseIds = sections.Select(s => s.CourseId).Distinct().ToList();

            // Toplu yükleme: tüm registration, component, criteria, grade tek seferde
            var allRegistrations = await _context.CourseRegistrations
                .Where(cr => allSectionIds.Contains(cr.CourseSectionId)
                    && cr.Status != RegistrationStatus.Dropped
                    && cr.Status != RegistrationStatus.Draft)
                .ToListAsync(ct);

            var allComponents = await _context.GradeComponents
                .Where(gc => courseIds.Contains(gc.CourseId) && gc.AcademicTermId == term.Id)
                .ToListAsync(ct);

            var allCriteria = await _context.GradingCriteria
                .Where(gc => courseIds.Contains(gc.CourseId) && gc.AcademicTermId == term.Id)
                .ToListAsync(ct);

            var allRegIds = allRegistrations.Select(r => r.Id).ToList();
            var allStudentGrades = await _context.StudentGrades
                .Where(sg => allRegIds.Contains(sg.CourseRegistrationId))
                .ToListAsync(ct);

            // Index yapıları
            var regsBySectionId = allRegistrations.GroupBy(r => r.CourseSectionId).ToDictionary(g => g.Key, g => g.ToList());
            var componentsByCourseId = allComponents.GroupBy(c => c.CourseId).ToDictionary(g => g.Key, g => g.ToList());
            var criteriaByKey = allCriteria.ToDictionary(c => c.CourseId);
            var gradesByRegId = allStudentGrades.GroupBy(g => g.CourseRegistrationId).ToDictionary(g => g.Key, g => g.ToList());

            var notes = new List<string>();
            var totalRegistrations = allRegistrations.Count;
            var missingGrades = allRegistrations.Count(r => r.Grade == null);
            if (missingGrades > 0 && totalRegistrations > 0)
            {
                var pct = Math.Round((double)missingGrades / totalRegistrations * 100, 1);
                notes.Add($"Uyari: {missingGrades} ogrencinin ({pct}%) notu eksik, NA olarak atanacak.");
            }

            var unfinalizedSections = sections.Where(s => !s.IsFinalized).ToList();
            int gradeLockCount = 0;

            foreach (var section in unfinalizedSections)
            {
                var components = componentsByCourseId.GetValueOrDefault(section.CourseId) ?? new List<GradeComponent>();
                var totalWeight = components.Sum(c => c.Weight);
                var registrations = regsBySectionId.GetValueOrDefault(section.Id) ?? new List<CourseRegistration>();

                if (registrations.Count == 0)
                {
                    section.IsFinalized = true;
                    gradeLockCount++;
                    continue;
                }

                if (components.Count > 0 && totalWeight == 100)
                {
                    // C5: Grading criteria must exist
                    if (!criteriaByKey.TryGetValue(section.CourseId, out var criteria))
                        throw new InvalidOperationException($"'{section.Course.Code}' dersi icin not kriterleri tanimlanmamis.");

                    foreach (var reg in registrations)
                    {
                        if (reg.Grade != null) continue;

                        var studentGrades = gradesByRegId.GetValueOrDefault(reg.Id) ?? new List<StudentGrade>();
                        decimal weightedScore = 0;
                        bool allComponentsHaveGrades = true;

                        foreach (var comp in components)
                        {
                            var sg = studentGrades.FirstOrDefault(g => g.GradeComponentId == comp.Id);
                            if (sg?.Score == null)
                            {
                                allComponentsHaveGrades = false;
                                break;
                            }
                            weightedScore += sg.Score.Value * comp.Weight / 100m;
                        }

                        reg.Grade = allComponentsHaveGrades
                            ? criteria.ResolveGrade((int)Math.Round(weightedScore, 0, MidpointRounding.AwayFromZero))
                            : GradeCode.NA;
                        reg.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    foreach (var reg in registrations.Where(r => r.Grade == null))
                    {
                        reg.Grade = GradeCode.NA;
                        reg.UpdatedAt = DateTime.UtcNow;
                    }
                }

                section.IsFinalized = true;
                gradeLockCount++;
            }

            // Grade assignments must be persisted BEFORE GPA recalculation
            await _context.SaveChangesAsync(ct);

            // Batch GPA hesaplama (N+1 yerine tek sorgu)
            var affectedStudentIds = allRegistrations
                .Select(cr => cr.StudentId)
                .Distinct()
                .ToList();

            foreach (var studentId in affectedStudentIds)
            {
                await _gpaService.RecalculateAndUpdateAsync(studentId);
            }

            notes.Add($"{gradeLockCount} section kilitlendi (toplam {sections.Count} section).");
            var notesStr = string.Join(" ", notes);
            await UpsertProgressAsync(departmentId, term.Id, TermClosingStep.GradeLock, userId, notesStr, ct);

            if (transaction != null)
                await transaction.CommitAsync(ct);

            return new TermClosingStepResultDto { Success = true, Message = notesStr };
        }
        catch
        {
            if (transaction != null)
                await transaction.RollbackAsync(ct);
            await ClearProgressAsync(departmentId, term.Id, TermClosingStep.GradeLock, ct);
            throw;
        }
    }

    public async Task<TermClosingStepResultDto> UnlockGradesAsync(int departmentId, int userId, CancellationToken ct = default)
    {
        var term = await GetActiveTermAsync(ct);
        if (term == null)
            return new TermClosingStepResultDto { Success = false, Message = "Aktif donem bulunamadi." };

        // Sonraki adımlar tamamlanmışsa önce onları engelleyelim
        var laterSteps = await _context.TermClosingProgresses
            .Where(p => p.DepartmentId == departmentId && p.TermId == term.Id
                && (p.Step == TermClosingStep.NewTerm || p.Step == TermClosingStep.Promotion || p.Step == TermClosingStep.GpaCalculation)
                && p.IsCompleted)
            .ToListAsync(ct);

        if (laterSteps.Any(s => s.Step == TermClosingStep.NewTerm))
            return new TermClosingStepResultDto { Success = false, Message = "Yeni donem acildigi icin not kilidi geri alinamaz. Once yeni donem adimini geri alin." };

        // Cascade rollback: Adim 3 ve 2'yi otomatik temizle
        var warnings = new List<string>();
        if (laterSteps.Any(s => s.Step == TermClosingStep.Promotion))
        {
            // Sinif atlama snapshot'u varsa rollback yap
            await RollbackPromotionAsync(departmentId, userId, ct);
            warnings.Add("Sinif atlama geri alindi.");
        }
        if (laterSteps.Any(s => s.Step == TermClosingStep.GpaCalculation))
        {
            await ClearProgressAsync(departmentId, term.Id, TermClosingStep.GpaCalculation, ct);
            warnings.Add("GPA hesaplama adimi sifirlandi.");
        }

        var sections = await _context.CourseSections
            .Include(cs => cs.Course)
            .Where(cs => cs.TermId == term.Id
                && cs.IsActive
                && cs.Course.DepartmentId == departmentId
                && cs.IsFinalized)
            .ToListAsync(ct);

        foreach (var section in sections)
        {
            section.IsFinalized = false;
        }

        await _context.SaveChangesAsync(ct);
        await ClearProgressAsync(departmentId, term.Id, TermClosingStep.GradeLock, ct);

        var msg = $"{sections.Count} section kilidi acildi.";
        if (warnings.Count > 0)
            msg += $" Ayrica: {string.Join(" ", warnings)}";

        return new TermClosingStepResultDto
        {
            Success = true,
            Message = msg
        };
    }

    public async Task<GpaRecalculationResultDto> CalculateGpaAsync(int departmentId, int userId, CancellationToken ct = default)
    {
        var term = await GetActiveTermAsync(ct);
        if (term == null)
            throw new InvalidOperationException("Aktif donem bulunamadi.");

        if (term.Status != TermStatus.GradeEntry && term.Status != TermStatus.Closed)
            throw new InvalidOperationException($"GPA hesaplama sadece 'Not Girisi' veya 'Kapali' asamasinda yapilabilir. Mevcut: {term.Status}");

        // C3: Step order enforcement — LockGrades must be completed first
        var progress = await _context.TermClosingProgresses
            .Where(p => p.DepartmentId == departmentId && p.TermId == term.Id)
            .ToListAsync(ct);
        if (!progress.Any(p => p.Step == TermClosingStep.GradeLock && p.IsCompleted))
            throw new InvalidOperationException("Once not girisi kilitlenmelidir.");

        // C10: Concurrent access protection
        await MarkInProgressAsync(departmentId, term.Id, TermClosingStep.GpaCalculation, userId, ct);

        // C1: Transaction wrapper
        var useTransaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
        await using var transaction = useTransaction
            ? await _context.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            var result = await _progressionService.RecalculateDepartmentGpaAsync(departmentId, ct);
            var notes = $"{result.RecalculatedCount} ogrenci hesaplandi, {result.ChangedCount} degisiklik.";
            await UpsertProgressAsync(departmentId, term.Id, TermClosingStep.GpaCalculation, userId, notes, ct);

            if (transaction != null)
                await transaction.CommitAsync(ct);

            return result;
        }
        catch
        {
            if (transaction != null)
                await transaction.RollbackAsync(ct);
            await ClearProgressAsync(departmentId, term.Id, TermClosingStep.GpaCalculation, ct);
            throw;
        }
    }

    public async Task<BatchProgressionResultDto> PreviewPromotionAsync(int departmentId, CancellationToken ct = default)
    {
        return await _progressionService.PromoteDepartmentStudentsAsync(departmentId, dryRun: true, ct);
    }

    public async Task<BatchProgressionResultDto> PromoteAsync(int departmentId, int userId, CancellationToken ct = default)
    {
        var promoteTerm = await GetActiveTermAsync(ct);
        if (promoteTerm == null)
            throw new InvalidOperationException("Aktif donem bulunamadi.");

        if (promoteTerm.Status != TermStatus.GradeEntry && promoteTerm.Status != TermStatus.Closed)
            throw new InvalidOperationException($"Sinif atlatma sadece 'Not Girisi' veya 'Kapali' asamasinda yapilabilir. Mevcut: {promoteTerm.Status}");

        // C3: Step order enforcement — CalculateGpa must be completed first
        var progress = await _context.TermClosingProgresses
            .Where(p => p.DepartmentId == departmentId && p.TermId == promoteTerm.Id)
            .ToListAsync(ct);
        if (!progress.Any(p => p.Step == TermClosingStep.GpaCalculation && p.IsCompleted))
            throw new InvalidOperationException("Once GPA hesaplanmalidir.");

        // C10: Concurrent access protection
        await MarkInProgressAsync(departmentId, promoteTerm.Id, TermClosingStep.Promotion, userId, ct);

        // C1: Transaction wrapper
        var useTransaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
        await using var transaction = useTransaction
            ? await _context.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            // C6: Snapshot BEFORE promotion (not after)
            var students = await _context.Students
                .Include(s => s.StudentPrograms)
                    .ThenInclude(sp => sp.Program)
                .Where(s => s.Status == StudentStatus.Active
                    && s.StudentPrograms.Any(sp =>
                        sp.Type == StudentProgramType.Main
                        && sp.IsActive
                        && sp.Program.DepartmentId == departmentId))
                .ToListAsync(ct);

            var snapshot = students.ToDictionary(s => s.Id, s => new { s.CompletedSemesters, s.Cgpa, s.Sgpa });
            var snapshotJson = JsonSerializer.Serialize(snapshot);

            var result = await _progressionService.PromoteDepartmentStudentsAsync(departmentId, dryRun: false, ct);

            await UpsertProgressAsync(departmentId, promoteTerm.Id, TermClosingStep.Promotion, userId, snapshotJson, ct);

            if (transaction != null)
                await transaction.CommitAsync(ct);

            return result;
        }
        catch
        {
            if (transaction != null)
                await transaction.RollbackAsync(ct);
            await ClearProgressAsync(departmentId, promoteTerm.Id, TermClosingStep.Promotion, ct);
            throw;
        }
    }

    public async Task<TermClosingStepResultDto> RollbackPromotionAsync(int departmentId, int userId, CancellationToken ct = default)
    {
        var term = await GetActiveTermAsync(ct);
        if (term == null)
            return new TermClosingStepResultDto { Success = false, Message = "Aktif donem bulunamadi." };

        var progress = await _context.TermClosingProgresses
            .FirstOrDefaultAsync(p => p.DepartmentId == departmentId
                && p.TermId == term.Id
                && p.Step == TermClosingStep.Promotion
                && p.IsCompleted, ct);

        if (progress?.Notes == null)
            return new TermClosingStepResultDto { Success = false, Message = "Geri alinacak sinif atlama kaydi bulunamadi." };

        Dictionary<int, StudentAcademicSnapshot>? snapshotData;
        try
        {
            snapshotData = JsonSerializer.Deserialize<Dictionary<int, StudentAcademicSnapshot>>(progress.Notes);
            // Eski format desteği (sadece CompletedSemesters int olarak saklanmış olabilir)
            if (snapshotData == null)
            {
                var legacySnapshot = JsonSerializer.Deserialize<Dictionary<int, int>>(progress.Notes);
                if (legacySnapshot != null)
                    snapshotData = legacySnapshot.ToDictionary(kv => kv.Key, kv => new StudentAcademicSnapshot { CompletedSemesters = kv.Value });
            }
        }
        catch
        {
            // Eski format fallback
            try
            {
                var legacySnapshot = JsonSerializer.Deserialize<Dictionary<int, int>>(progress.Notes);
                snapshotData = legacySnapshot?.ToDictionary(kv => kv.Key, kv => new StudentAcademicSnapshot { CompletedSemesters = kv.Value });
            }
            catch
            {
                return new TermClosingStepResultDto { Success = false, Message = "Snapshot verisi okunamadi." };
            }
        }

        if (snapshotData == null || snapshotData.Count == 0)
            return new TermClosingStepResultDto { Success = false, Message = "Snapshot bos." };

        var studentIds = snapshotData.Keys.ToList();
        var students = await _context.Students
            .Where(s => studentIds.Contains(s.Id))
            .ToListAsync(ct);

        int restoredCount = 0;
        foreach (var student in students)
        {
            if (snapshotData.TryGetValue(student.Id, out var snap))
            {
                bool changed = false;
                if (student.CompletedSemesters != snap.CompletedSemesters)
                {
                    student.CompletedSemesters = snap.CompletedSemesters;
                    changed = true;
                }
                if (snap.Cgpa.HasValue && Math.Abs(student.Cgpa - snap.Cgpa.Value) > 0.0001m)
                {
                    student.Cgpa = snap.Cgpa.Value;
                    changed = true;
                }
                if (snap.Sgpa.HasValue && Math.Abs(student.Sgpa - snap.Sgpa.Value) > 0.0001m)
                {
                    student.Sgpa = snap.Sgpa.Value;
                    changed = true;
                }
                if (changed) restoredCount++;
            }
        }

        await _context.SaveChangesAsync(ct);
        await ClearProgressAsync(departmentId, term.Id, TermClosingStep.Promotion, ct);

        return new TermClosingStepResultDto
        {
            Success = true,
            Message = $"{restoredCount} ogrencinin sinif seviyesi eski haline donduruldu."
        };
    }

    public async Task<NewTermResultDto> OpenNewTermAsync(int departmentId, int userId, NewTermRequestDto request, CancellationToken ct = default)
    {
        var currentTerm = await GetActiveTermAsync(ct);
        if (currentTerm == null)
            return new NewTermResultDto { Success = false, Message = "Aktif donem bulunamadi." };

        // C3: Step order enforcement — Promote must be completed first
        var progressList = await _context.TermClosingProgresses
            .Where(p => p.DepartmentId == departmentId && p.TermId == currentTerm.Id)
            .ToListAsync(ct);
        if (!progressList.Any(p => p.Step == TermClosingStep.Promotion && p.IsCompleted))
            throw new InvalidOperationException("Once sinif atlatma tamamlanmalidir.");

        // C10: Concurrent access protection
        await MarkInProgressAsync(departmentId, currentTerm.Id, TermClosingStep.NewTerm, userId, ct);

        // C4: Date validation
        if (request.RegistrationEnd <= request.RegistrationStart)
            throw new InvalidOperationException("Kayit bitis tarihi baslangic tarihinden sonra olmalidir.");
        if (request.TermEnd <= request.TermStart)
            throw new InvalidOperationException("Donem bitis tarihi baslangic tarihinden sonra olmalidir.");

        // C1: Transaction wrapper
        var useTransaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
        await using var transaction = useTransaction
            ? await _context.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            var existingTerm = await _context.AcademicTerms
                .FirstOrDefaultAsync(t => t.Year == request.Year && t.Semester == request.Semester, ct);

            int newTermId;

            if (existingTerm != null)
            {
                newTermId = existingTerm.Id;
                existingTerm.IsActive = true;
                existingTerm.Name = request.Name;
                existingTerm.RegistrationStart = request.RegistrationStart;
                existingTerm.RegistrationEnd = request.RegistrationEnd;
                existingTerm.AddDropStart = request.RegistrationStart;
                existingTerm.AddDropEnd = request.RegistrationEnd;
                existingTerm.TermStart = request.TermStart;
                existingTerm.TermEnd = request.TermEnd;
                existingTerm.WithdrawalDeadline = request.TermStart.AddDays(request.Semester == SemesterType.Summer ? 28 : 49);
            }
            else
            {
                var newTerm = new AcademicTerm
                {
                    Name = request.Name,
                    Year = request.Year,
                    Semester = request.Semester,
                    RegistrationStart = request.RegistrationStart,
                    RegistrationEnd = request.RegistrationEnd,
                    AddDropStart = request.RegistrationStart,
                    AddDropEnd = request.RegistrationEnd,
                    WithdrawalDeadline = request.TermStart.AddDays(request.Semester == SemesterType.Summer ? 28 : 49),
                    TermStart = request.TermStart,
                    TermEnd = request.TermEnd,
                    IsActive = true
                };
                _context.AcademicTerms.Add(newTerm);
                await _context.SaveChangesAsync(ct);
                newTermId = newTerm.Id;
            }

            // NOT: currentTerm.IsActive = false yapmiyoruz -- bu donem gecisi merkezi
            // TransitionStatusAsync uzerinden yapilmali. Bolum bazli kapatma mevcut
            // donemin aktifligini degistirmemeli (diger bolumler hala kullaniyor olabilir).
            await _context.SaveChangesAsync(ct);

            await UpsertProgressAsync(departmentId, currentTerm.Id, TermClosingStep.NewTerm, userId,
                $"Yeni donem: {request.Name} (ID: {newTermId})", ct);

            if (transaction != null)
                await transaction.CommitAsync(ct);

            return new NewTermResultDto
            {
                Success = true,
                Message = $"Yeni donem '{request.Name}' olusturuldu ve aktif edildi.",
                NewTermId = newTermId
            };
        }
        catch
        {
            if (transaction != null)
                await transaction.RollbackAsync(ct);
            await ClearProgressAsync(departmentId, currentTerm.Id, TermClosingStep.NewTerm, ct);
            throw;
        }
    }

    // ===================================================================
    //  HELPERS
    // ===================================================================

    private async Task<AcademicTerm?> GetActiveTermAsync(CancellationToken ct)
    {
        return await _context.AcademicTerms.FirstOrDefaultAsync(t => t.IsActive, ct);
    }

    private async Task UpsertProgressAsync(int departmentId, int termId, TermClosingStep step, int userId, string notes, CancellationToken ct)
    {
        var progress = await _context.TermClosingProgresses
            .FirstOrDefaultAsync(p => p.DepartmentId == departmentId && p.TermId == termId && p.Step == step, ct);

        if (progress == null)
        {
            progress = new TermClosingProgress
            {
                DepartmentId = departmentId,
                TermId = termId,
                Step = step
            };
            _context.TermClosingProgresses.Add(progress);
        }

        progress.IsCompleted = true;
        progress.CompletedAt = DateTime.UtcNow;
        progress.CompletedByUserId = userId;
        progress.Notes = notes;

        await _context.SaveChangesAsync(ct);
    }

    private async Task ClearProgressAsync(int departmentId, int termId, TermClosingStep step, CancellationToken ct)
    {
        var progress = await _context.TermClosingProgresses
            .FirstOrDefaultAsync(p => p.DepartmentId == departmentId && p.TermId == termId && p.Step == step, ct);

        if (progress != null)
        {
            _context.TermClosingProgresses.Remove(progress);
            await _context.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Concurrent access koruması: adım başlangıcında "in progress" kaydı oluşturur.
    /// Aynı adım zaten çalışıyorsa veya tamamlanmışsa exception fırlatır.
    /// </summary>
    private async Task MarkInProgressAsync(int departmentId, int termId, TermClosingStep step, int userId, CancellationToken ct)
    {
        var existing = await _context.TermClosingProgresses
            .FirstOrDefaultAsync(p => p.DepartmentId == departmentId && p.TermId == termId && p.Step == step, ct);

        if (existing != null)
        {
            if (existing.IsCompleted)
                throw new InvalidOperationException($"Bu adim zaten tamamlanmis: {StepLabels[step]}");
            throw new InvalidOperationException("Bu adim baska bir kullanici tarafindan isleniyor.");
        }

        _context.TermClosingProgresses.Add(new TermClosingProgress
        {
            DepartmentId = departmentId,
            TermId = termId,
            Step = step,
            IsCompleted = false,
            StartedAt = DateTime.UtcNow,
            StartedByUserId = userId
        });
        await _context.SaveChangesAsync(ct);
    }

    private static List<TermClosingStepStatus> BuildEmptySteps()
    {
        return Enum.GetValues<TermClosingStep>()
            .Select(step => new TermClosingStepStatus
            {
                Step = step,
                Label = StepLabels[step],
                IsCompleted = false
            })
            .ToList();
    }
}

internal sealed class StudentAcademicSnapshot
{
    public int CompletedSemesters { get; set; }
    public decimal? Cgpa { get; set; }
    public decimal? Sgpa { get; set; }
}
