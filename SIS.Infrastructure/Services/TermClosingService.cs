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

        var sections = await _context.CourseSections
            .Include(cs => cs.Course)
            .Where(cs => cs.TermId == term.Id
                && cs.IsActive
                && cs.Course.DepartmentId == departmentId)
            .ToListAsync(ct);

        if (sections.Count == 0)
            return new TermClosingStepResultDto { Success = false, Message = "Bu bolumde aktif section bulunamadi." };

        var unfinalizedSections = sections.Where(s => !s.IsFinalized).ToList();
        int gradeLockCount = 0;

        foreach (var section in unfinalizedSections)
        {
            var components = await _context.GradeComponents
                .Where(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId)
                .ToListAsync(ct);

            var totalWeight = components.Sum(c => c.Weight);

            var registrations = await _context.CourseRegistrations
                .Where(cr => cr.CourseSectionId == section.Id
                    && cr.Status != RegistrationStatus.Dropped
                    && cr.Status != RegistrationStatus.Draft)
                .ToListAsync(ct);

            if (registrations.Count == 0)
            {
                section.IsFinalized = true;
                gradeLockCount++;
                continue;
            }

            if (components.Count > 0 && totalWeight == 100)
            {
                var criteria = await _context.GradingCriteria
                    .FirstOrDefaultAsync(gc => gc.CourseId == section.CourseId
                        && gc.AcademicTermId == section.TermId, ct);
                criteria ??= new GradingCriteria();

                var registrationIds = registrations.Select(r => r.Id).ToList();
                var allGrades = await _context.StudentGrades
                    .Where(sg => registrationIds.Contains(sg.CourseRegistrationId))
                    .ToListAsync(ct);

                foreach (var reg in registrations)
                {
                    if (reg.Grade != null) continue;

                    var studentGrades = allGrades.Where(sg => sg.CourseRegistrationId == reg.Id).ToList();
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

        // Grade assignments (IsFinalized, reg.Grade) must be persisted BEFORE GPA recalculation,
        // otherwise RecalculateAndUpdateAsync reads stale data from DB.
        await _context.SaveChangesAsync(ct);

        var affectedStudentIds = await _context.CourseRegistrations
            .Where(cr => sections.Select(s => s.Id).Contains(cr.CourseSectionId)
                && cr.Status != RegistrationStatus.Dropped
                && cr.Status != RegistrationStatus.Draft)
            .Select(cr => cr.StudentId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var studentId in affectedStudentIds)
        {
            await _gpaService.RecalculateAndUpdateAsync(studentId);
        }

        var notes = $"{gradeLockCount} section kilitlendi (toplam {sections.Count} section).";
        await UpsertProgressAsync(departmentId, term.Id, TermClosingStep.GradeLock, userId, notes, ct);

        return new TermClosingStepResultDto { Success = true, Message = notes };
    }

    public async Task<TermClosingStepResultDto> UnlockGradesAsync(int departmentId, int userId, CancellationToken ct = default)
    {
        var term = await GetActiveTermAsync(ct);
        if (term == null)
            return new TermClosingStepResultDto { Success = false, Message = "Aktif donem bulunamadi." };

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

        return new TermClosingStepResultDto
        {
            Success = true,
            Message = $"{sections.Count} section kilidi acildi."
        };
    }

    public async Task<GpaRecalculationResultDto> CalculateGpaAsync(int departmentId, int userId, CancellationToken ct = default)
    {
        var term = await GetActiveTermAsync(ct);
        if (term != null && term.Status != TermStatus.GradeEntry && term.Status != TermStatus.Closed)
            return new GpaRecalculationResultDto { Notes = { $"GPA hesaplama sadece 'Not Girişi' veya 'Kapalı' aşamasında yapılabilir. Mevcut: {term.Status}" } };

        var result = await _progressionService.RecalculateDepartmentGpaAsync(departmentId, ct);
        if (term != null)
        {
            var notes = $"{result.RecalculatedCount} ogrenci hesaplandi, {result.ChangedCount} degisiklik.";
            await UpsertProgressAsync(departmentId, term.Id, TermClosingStep.GpaCalculation, userId, notes, ct);
        }

        return result;
    }

    public async Task<BatchProgressionResultDto> PreviewPromotionAsync(int departmentId, CancellationToken ct = default)
    {
        return await _progressionService.PromoteDepartmentStudentsAsync(departmentId, dryRun: true, ct);
    }

    public async Task<BatchProgressionResultDto> PromoteAsync(int departmentId, int userId, CancellationToken ct = default)
    {
        var promoteTerm = await GetActiveTermAsync(ct);
        if (promoteTerm != null && promoteTerm.Status != TermStatus.GradeEntry && promoteTerm.Status != TermStatus.Closed)
            return new BatchProgressionResultDto { Notes = { $"Sınıf atlatma sadece 'Not Girişi' veya 'Kapalı' aşamasında yapılabilir. Mevcut: {promoteTerm.Status}" } };

        var students = await _context.Students
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .Where(s => s.Status == StudentStatus.Active
                && s.StudentPrograms.Any(sp =>
                    sp.Type == StudentProgramType.Main
                    && sp.IsActive
                    && sp.Program.DepartmentId == departmentId))
            .ToListAsync(ct);

        var snapshot = students.ToDictionary(s => s.Id, s => s.CompletedSemesters);
        var snapshotJson = JsonSerializer.Serialize(snapshot);

        var result = await _progressionService.PromoteDepartmentStudentsAsync(departmentId, dryRun: false, ct);

        var term = await GetActiveTermAsync(ct);
        if (term != null)
        {
            await UpsertProgressAsync(departmentId, term.Id, TermClosingStep.Promotion, userId, snapshotJson, ct);
        }

        return result;
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

        Dictionary<int, int>? snapshotData;
        try
        {
            snapshotData = JsonSerializer.Deserialize<Dictionary<int, int>>(progress.Notes);
        }
        catch
        {
            return new TermClosingStepResultDto { Success = false, Message = "Snapshot verisi okunamadi." };
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
            if (snapshotData.TryGetValue(student.Id, out var oldSemesters) && student.CompletedSemesters != oldSemesters)
            {
                student.CompletedSemesters = oldSemesters;
                restoredCount++;
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

        var existingTerm = await _context.AcademicTerms
            .FirstOrDefaultAsync(t => t.Year == request.Year && t.Semester == request.Semester, ct);

        int newTermId;

        if (existingTerm != null)
        {
            newTermId = existingTerm.Id;
            if (!existingTerm.IsActive)
                existingTerm.IsActive = true;
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

        // NOT: currentTerm.IsActive = false yapmıyoruz — bu dönem geçişi merkezi
        // TransitionStatusAsync üzerinden yapılmalı. Bölüm bazlı kapatma mevcut
        // dönemin aktifliğini değiştirmemeli (diğer bölümler hâlâ kullanıyor olabilir).
        await _context.SaveChangesAsync(ct);

        await UpsertProgressAsync(departmentId, currentTerm.Id, TermClosingStep.NewTerm, userId,
            $"Yeni donem: {request.Name} (ID: {newTermId})", ct);

        return new NewTermResultDto
        {
            Success = true,
            Message = $"Yeni donem '{request.Name}' olusturuldu ve aktif edildi.",
            NewTermId = newTermId
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════

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
            progress.IsCompleted = false;
            progress.CompletedAt = null;
            progress.CompletedByUserId = null;
            progress.Notes = null;
            await _context.SaveChangesAsync(ct);
        }
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
