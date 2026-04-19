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
    //  NEW: GRADE READINESS DETAIL (section-level breakdown)
    // ===================================================================

    public async Task<GradeLockDetailResultDto> GetGradeReadinessDetailAsync(int departmentId, CancellationToken ct = default)
    {
        var term = await GetActiveTermAsync(ct);
        if (term == null)
            return new GradeLockDetailResultDto();

        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Where(cs => cs.TermId == term.Id
                && cs.IsActive
                && cs.Course.DepartmentId == departmentId)
            .ToListAsync(ct);

        var sectionIds = sections.Select(s => s.Id).ToList();

        // Instructor userId -> ad soyad
        var instructorUserIds = sections
            .Where(s => s.Instructor != null)
            .Select(s => s.Instructor!.UserId)
            .Distinct()
            .ToList();

        var userNames = instructorUserIds.Count > 0
            ? await _context.Users
                .AsNoTracking()
                .Where(u => instructorUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}", ct)
            : new Dictionary<int, string>();

        // Registration sayıları tek sorguda
        var regStats = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(cr => sectionIds.Contains(cr.CourseSectionId)
                && cr.Status != RegistrationStatus.Dropped
                && cr.Status != RegistrationStatus.Draft)
            .GroupBy(cr => cr.CourseSectionId)
            .Select(g => new
            {
                SectionId = g.Key,
                Total = g.Count(),
                Entered = g.Count(cr => cr.Grade != null),
                Missing = g.Count(cr => cr.Grade == null)
            })
            .ToListAsync(ct);

        var regStatsDict = regStats.ToDictionary(r => r.SectionId);

        var sectionDtos = sections
            .Select(s =>
            {
                var stats = regStatsDict.GetValueOrDefault(s.Id);
                var total = stats?.Total ?? 0;
                var entered = stats?.Entered ?? 0;
                var missing = stats?.Missing ?? 0;

                return new SectionGradeDetailDto
                {
                    SectionId = s.Id,
                    CourseCode = s.Course.Code,
                    CourseName = s.Course.Name,
                    SectionCode = $"{s.Course.Code}.{s.SectionNumber:D2}",
                    InstructorName = s.Instructor != null
                        ? userNames.GetValueOrDefault(s.Instructor.UserId, "—")
                        : "Atanmamış",
                    TotalRegistrations = total,
                    EnteredGradeCount = entered,
                    MissingGradeCount = missing,
                    IsFinalized = s.IsFinalized,
                    EntryPercentage = total > 0 ? Math.Round((decimal)entered / total * 100, 1) : 100m
                };
            })
            .OrderByDescending(s => s.MissingGradeCount)
            .ThenBy(s => s.CourseCode)
            .ToList();

        var totalActive = sectionDtos.Sum(s => s.TotalRegistrations);
        var totalMissing = sectionDtos.Sum(s => s.MissingGradeCount);

        return new GradeLockDetailResultDto
        {
            TotalSections = sections.Count,
            TotalActiveRegistrations = totalActive,
            MissingGradeCount = totalMissing,
            AllGradesEntered = totalMissing == 0,
            HasActiveRegistrations = totalActive > 0,
            FinalizedSectionCount = sections.Count(s => s.IsFinalized),
            OverallEntryPercentage = totalActive > 0 ? Math.Round((decimal)(totalActive - totalMissing) / totalActive * 100, 1) : 100m,
            Sections = sectionDtos
        };
    }

    // ===================================================================
    //  NEW: GRADE DISTRIBUTION (after grade lock)
    // ===================================================================

    public async Task<GradeDistributionResultDto> GetGradeDistributionAsync(int departmentId, CancellationToken ct = default)
    {
        var term = await GetActiveTermAsync(ct);
        if (term == null)
            return new GradeDistributionResultDto();

        var sectionIds = await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.TermId == term.Id
                && cs.IsActive
                && cs.Course.DepartmentId == departmentId)
            .Select(cs => cs.Id)
            .ToListAsync(ct);

        var grades = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(cr => sectionIds.Contains(cr.CourseSectionId)
                && cr.Status != RegistrationStatus.Dropped
                && cr.Status != RegistrationStatus.Draft
                && cr.Grade != null)
            .Select(cr => cr.Grade!.Value)
            .ToListAsync(ct);

        if (grades.Count == 0)
            return new GradeDistributionResultDto();

        var passingGrades = new HashSet<GradeCode>
        {
            GradeCode.AA, GradeCode.BA, GradeCode.BB, GradeCode.CB,
            GradeCode.CC, GradeCode.DC, GradeCode.DD, GradeCode.S
        };

        // Harf notu sıralama (AA'dan FF'e)
        var orderedGrades = new[]
        {
            GradeCode.AA, GradeCode.BA, GradeCode.BB, GradeCode.CB,
            GradeCode.CC, GradeCode.DC, GradeCode.DD, GradeCode.FF,
            GradeCode.NA, GradeCode.W, GradeCode.I, GradeCode.NI,
            GradeCode.S, GradeCode.U
        };

        var groupedGrades = grades.GroupBy(g => g).ToDictionary(g => g.Key, g => g.Count());
        var total = grades.Count;

        var distribution = orderedGrades
            .Where(g => groupedGrades.ContainsKey(g))
            .Select(g => new GradeDistributionItemDto
            {
                GradeCode = g.ToString(),
                Count = groupedGrades[g],
                Percentage = Math.Round((decimal)groupedGrades[g] / total * 100, 1)
            })
            .ToList();

        var passCount = grades.Count(g => passingGrades.Contains(g));

        // Ortalama puan hesapla (sadece GPA dahil notlar)
        decimal totalPoints = 0;
        int gpaIncludedCount = 0;
        foreach (var grade in grades)
        {
            var points = Domain.ValueObjects.GradePointMap.GetPoint(grade);
            if (points.HasValue)
            {
                totalPoints += points.Value;
                gpaIncludedCount++;
            }
        }

        return new GradeDistributionResultDto
        {
            TotalGraded = total,
            AverageScore = gpaIncludedCount > 0 ? Math.Round(totalPoints / gpaIncludedCount, 2) : 0m,
            PassRate = total > 0 ? Math.Round((decimal)passCount / total * 100, 1) : 0m,
            Grades = distribution
        };
    }

    // ===================================================================
    //  NEW: DEPARTMENT OVERVIEW (dean/admin multi-dept view)
    // ===================================================================

    public async Task<List<DepartmentClosingOverviewDto>> GetDepartmentOverviewAsync(int? facultyId, CancellationToken ct = default)
    {
        var term = await GetActiveTermAsync(ct);
        if (term == null)
            return new List<DepartmentClosingOverviewDto>();

        var departmentsQuery = _context.Departments.AsNoTracking().AsQueryable();
        if (facultyId.HasValue)
            departmentsQuery = departmentsQuery.Where(d => d.FacultyId == facultyId.Value);

        var departments = await departmentsQuery
            .Select(d => new { d.Id, d.Code, d.Name })
            .ToListAsync(ct);

        var deptIds = departments.Select(d => d.Id).ToList();

        // Tüm progress kayıtları tek sorguda
        var progresses = await _context.TermClosingProgresses
            .AsNoTracking()
            .Where(p => deptIds.Contains(p.DepartmentId) && p.TermId == term.Id && p.IsCompleted)
            .ToListAsync(ct);

        var completedByUser = progresses
            .Where(p => p.CompletedByUserId.HasValue)
            .Select(p => p.CompletedByUserId!.Value)
            .Distinct()
            .ToList();

        var userNames = completedByUser.Count > 0
            ? await _context.Users.AsNoTracking()
                .Where(u => completedByUser.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}", ct)
            : new Dictionary<int, string>();

        // Section + missing grade counts per dept (tek sorgu)
        var sectionStats = await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.TermId == term.Id && cs.IsActive && cs.Course.DepartmentId.HasValue && deptIds.Contains(cs.Course.DepartmentId.Value))
            .GroupBy(cs => cs.Course.DepartmentId)
            .Select(g => new { DeptId = g.Key, SectionCount = g.Count() })
            .ToListAsync(ct);

        var sectionStatsDict = sectionStats.ToDictionary(s => s.DeptId);

        // Eksik not sayıları
        var missingStats = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(cr => cr.CourseSection.TermId == term.Id
                && cr.CourseSection.IsActive
                && cr.CourseSection.Course.DepartmentId.HasValue && deptIds.Contains(cr.CourseSection.Course.DepartmentId.Value)
                && cr.Status != RegistrationStatus.Dropped
                && cr.Status != RegistrationStatus.Draft
                && cr.Grade == null)
            .GroupBy(cr => cr.CourseSection.Course.DepartmentId)
            .Select(g => new { DeptId = g.Key, MissingCount = g.Count() })
            .ToListAsync(ct);

        var missingStatsDict = missingStats.ToDictionary(m => m.DeptId);

        var progressByDept = progresses.GroupBy(p => p.DepartmentId).ToDictionary(g => g.Key, g => g.ToList());

        var stepLabels = new Dictionary<int, string>
        {
            { 0, "Başlanmadı" }, { 1, "Not Kilitleme" }, { 2, "GPA Hesaplama" },
            { 3, "Sınıf Atlatma" }, { 4, "Tamamlandı" }
        };

        return departments.Select(d =>
        {
            var deptProgress = progressByDept.GetValueOrDefault(d.Id) ?? new List<TermClosingProgress>();
            var completedCount = deptProgress.Count;
            var lastActivity = deptProgress.OrderByDescending(p => p.CompletedAt).FirstOrDefault();

            return new DepartmentClosingOverviewDto
            {
                DepartmentId = d.Id,
                DepartmentCode = d.Code,
                DepartmentName = d.Name,
                CompletedStepCount = completedCount,
                CurrentStepLabel = stepLabels.GetValueOrDefault(completedCount, "—"),
                TotalSections = sectionStatsDict.GetValueOrDefault(d.Id)?.SectionCount ?? 0,
                MissingGradeCount = missingStatsDict.GetValueOrDefault(d.Id)?.MissingCount ?? 0,
                LastActivityAt = lastActivity?.CompletedAt,
                LastActivityByName = lastActivity?.CompletedByUserId != null
                    ? userNames.GetValueOrDefault(lastActivity.CompletedByUserId.Value)
                    : null
            };
        })
        .OrderBy(d => d.CompletedStepCount)
        .ThenBy(d => d.DepartmentCode)
        .ToList();
    }

    // ===================================================================
    //  NEW: EXPORT PROMOTION REPORT (Excel)
    // ===================================================================

    public async Task<byte[]> ExportPromotionReportAsync(int departmentId, CancellationToken ct = default)
    {
        var preview = await _progressionService.PromoteDepartmentStudentsAsync(departmentId, dryRun: true, ct);

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("Sınıf Atlatma");

        // Header
        var headers = new[] { "Öğrenci No", "Ad Soyad", "Eski Sınıf", "Yeni Sınıf", "AKTS", "ANO", "AGNO", "Durum", "Uyarılar" };
        for (var i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#E2E8F0");
        }

        var row = 2;
        foreach (var student in preview.Results.OrderBy(s => s.StudentNumber))
        {
            ws.Cell(row, 1).Value = student.StudentNumber;
            ws.Cell(row, 2).Value = student.FullName;
            ws.Cell(row, 3).Value = student.OldLevelLabel;
            ws.Cell(row, 4).Value = student.NewLevelLabel;
            ws.Cell(row, 5).Value = student.CompletedEcts;
            ws.Cell(row, 6).Value = (double)student.Sgpa;
            ws.Cell(row, 7).Value = (double)student.Cgpa;
            ws.Cell(row, 8).Value = student.WasPromoted ? "Terfi" : student.Status.StartsWith("Warning") ? "Uyarı" : "Sabit";
            ws.Cell(row, 9).Value = string.Join("; ", student.Warnings);

            if (student.Status.StartsWith("Warning"))
            {
                ws.Row(row).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#FEF3C7");
            }
            row++;
        }

        ws.Columns().AdjustToContents();

        // Özet satırı
        row += 1;
        ws.Cell(row, 1).Value = "ÖZET";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row + 1, 1).Value = $"Toplam: {preview.TotalStudentsAnalyzed}";
        ws.Cell(row + 2, 1).Value = $"Terfi: {preview.PromotedCount}";
        ws.Cell(row + 3, 1).Value = $"Sabit: {preview.StayedCount}";
        ws.Cell(row + 4, 1).Value = $"Uyarı: {preview.WarningCount}";

        using var ms = new System.IO.MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
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
