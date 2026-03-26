# Term Closing Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement department-scoped, 4-step term closing backend with progress tracking and rollback support.

**Architecture:** New `TermClosingService` orchestrates existing services (`AcademicProgressionService`, `GpaCalculationService`) behind a `TermClosingController`. A `TermClosingProgress` entity tracks per-department, per-term, per-step completion state with audit info. Steps are flexible — each can be rolled back independently.

**Tech Stack:** ASP.NET Core 10, EF Core 10 (PostgreSQL + InMemory for tests), xUnit, FluentAssertions

---

## File Structure

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `SIS.Domain/Entities/TermClosingProgress.cs` | Progress tracking entity |
| Create | `SIS.Domain/Enums/TermClosingStep.cs` | Step enum (GradeLock, GpaCalculation, Promotion, NewTerm) |
| Create | `SIS.Application/TermClosing/DTOs/TermClosingDtos.cs` | All DTOs for the feature |
| Create | `SIS.Application/TermClosing/Services/ITermClosingService.cs` | Service interface |
| Create | `SIS.Infrastructure/Services/TermClosingService.cs` | Orchestration logic |
| Create | `SIS.API/Controllers/TermClosingController.cs` | REST endpoints |
| Modify | `SIS.Infrastructure/Persistence/AppDbContext.cs` | Add DbSet + OnModelCreating config |
| Modify | `SIS.API/Program.cs` | Register DI |
| Create | `SIS.Tests/Application/TermClosingServiceTests.cs` | Service-level tests |
| Create | `SIS.Tests/API/TermClosingControllerTests.cs` | Controller attribute tests |

---

### Task 1: Domain Layer — Entity and Enum

**Files:**
- Create: `SIS.Domain/Enums/TermClosingStep.cs`
- Create: `SIS.Domain/Entities/TermClosingProgress.cs`

- [ ] **Step 1: Create the TermClosingStep enum**

```csharp
// SIS.Domain/Enums/TermClosingStep.cs
namespace SIS.Domain.Enums;

public enum TermClosingStep
{
    GradeLock = 1,
    GpaCalculation = 2,
    Promotion = 3,
    NewTerm = 4
}
```

- [ ] **Step 2: Create the TermClosingProgress entity**

```csharp
// SIS.Domain/Entities/TermClosingProgress.cs
using SIS.Domain.Enums;

namespace SIS.Domain.Entities;

public class TermClosingProgress
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;
    public TermClosingStep Step { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? CompletedByUserId { get; set; }
    public string? Notes { get; set; }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build`
Expected: Build succeeded. 0 Errors.

- [ ] **Step 4: Commit**

```bash
git add SIS.Domain/Enums/TermClosingStep.cs SIS.Domain/Entities/TermClosingProgress.cs
git commit -m "feat: add TermClosingProgress entity and TermClosingStep enum"
```

---

### Task 2: EF Core Configuration — DbSet and OnModelCreating

**Files:**
- Modify: `SIS.Infrastructure/Persistence/AppDbContext.cs`

- [ ] **Step 1: Add DbSet declaration**

Add after the existing `SchedulingAuditLogs` DbSet (around line 33):

```csharp
public DbSet<TermClosingProgress> TermClosingProgresses => Set<TermClosingProgress>();
```

Add the required using at the top if not present:

```csharp
using SIS.Domain.Enums;
```

- [ ] **Step 2: Add OnModelCreating configuration**

Add at the end of the `OnModelCreating` method, before the closing brace:

```csharp
// ── TermClosingProgress ────────────────────────────────────
builder.Entity<TermClosingProgress>(e =>
{
    e.HasIndex(p => new { p.DepartmentId, p.TermId, p.Step }).IsUnique();

    e.HasOne(p => p.Department)
     .WithMany()
     .HasForeignKey(p => p.DepartmentId)
     .OnDelete(DeleteBehavior.Restrict);

    e.HasOne(p => p.Term)
     .WithMany()
     .HasForeignKey(p => p.TermId)
     .OnDelete(DeleteBehavior.Restrict);
});
```

- [ ] **Step 3: Verify build**

Run: `dotnet build`
Expected: Build succeeded. 0 Errors.

- [ ] **Step 4: Commit**

```bash
git add SIS.Infrastructure/Persistence/AppDbContext.cs
git commit -m "feat: add TermClosingProgress DbSet and EF Core configuration"
```

---

### Task 3: Application Layer — DTOs and Interface

**Files:**
- Create: `SIS.Application/TermClosing/DTOs/TermClosingDtos.cs`
- Create: `SIS.Application/TermClosing/Services/ITermClosingService.cs`

- [ ] **Step 1: Create DTOs**

```csharp
// SIS.Application/TermClosing/DTOs/TermClosingDtos.cs
using SIS.Domain.Enums;

namespace SIS.Application.TermClosing.DTOs;

public class TermClosingStatusDto
{
    public int DepartmentId { get; set; }
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public List<TermClosingStepStatus> Steps { get; set; } = new();
}

public class TermClosingStepStatus
{
    public TermClosingStep Step { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedByName { get; set; }
    public string? Notes { get; set; }
}

public class TermClosingStepResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class NewTermRequestDto
{
    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }
    public SemesterType Semester { get; set; }
    public DateTime RegistrationStart { get; set; }
    public DateTime RegistrationEnd { get; set; }
    public DateTime TermStart { get; set; }
    public DateTime TermEnd { get; set; }
}

public class NewTermResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? NewTermId { get; set; }
}
```

- [ ] **Step 2: Create service interface**

```csharp
// SIS.Application/TermClosing/Services/ITermClosingService.cs
using SIS.Application.AcademicProgression.DTOs;
using SIS.Application.TermClosing.DTOs;

namespace SIS.Application.TermClosing.Services;

public interface ITermClosingService
{
    Task<TermClosingStatusDto> GetStatusAsync(int departmentId, CancellationToken ct = default);
    Task<TermClosingStepResultDto> LockGradesAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<TermClosingStepResultDto> UnlockGradesAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<GpaRecalculationResultDto> CalculateGpaAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<BatchProgressionResultDto> PreviewPromotionAsync(int departmentId, CancellationToken ct = default);
    Task<BatchProgressionResultDto> PromoteAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<TermClosingStepResultDto> RollbackPromotionAsync(int departmentId, int userId, CancellationToken ct = default);
    Task<NewTermResultDto> OpenNewTermAsync(int departmentId, int userId, NewTermRequestDto request, CancellationToken ct = default);
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build`
Expected: Build succeeded. 0 Errors.

- [ ] **Step 4: Commit**

```bash
git add SIS.Application/TermClosing/
git commit -m "feat: add term closing DTOs and ITermClosingService interface"
```

---

### Task 4: Service Implementation — TermClosingService (GetStatus + LockGrades + UnlockGrades)

**Files:**
- Create: `SIS.Infrastructure/Services/TermClosingService.cs`

- [ ] **Step 1: Create the service with helper methods, GetStatus, LockGrades, and UnlockGrades**

```csharp
// SIS.Infrastructure/Services/TermClosingService.cs
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

    // ═══════════════════════════════════════════════════════════════
    //  GET STATUS
    // ═══════════════════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════════════════
    //  STEP 1: LOCK GRADES
    // ═══════════════════════════════════════════════════════════════

    public async Task<TermClosingStepResultDto> LockGradesAsync(int departmentId, int userId, CancellationToken ct = default)
    {
        var term = await GetActiveTermAsync(ct);
        if (term == null)
            return new TermClosingStepResultDto { Success = false, Message = "Aktif donem bulunamadi." };

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
            // Get components for this course+term
            var components = await _context.GradeComponents
                .Where(gc => gc.CourseId == section.CourseId && gc.AcademicTermId == section.TermId)
                .ToListAsync(ct);

            var totalWeight = components.Sum(c => c.Weight);

            // Get active registrations
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

            // If components exist and weights sum to 100, calculate grades
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
                    if (reg.Grade != null) continue; // Already has a grade

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
                // No valid components — mark all ungraded as NA
                foreach (var reg in registrations.Where(r => r.Grade == null))
                {
                    reg.Grade = GradeCode.NA;
                    reg.UpdatedAt = DateTime.UtcNow;
                }
            }

            section.IsFinalized = true;
            gradeLockCount++;
        }

        // Recalculate GPA for affected students
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

        await _context.SaveChangesAsync(ct);

        var notes = $"{gradeLockCount} section kilitlendi (toplam {sections.Count} section).";
        await UpsertProgressAsync(departmentId, term.Id, TermClosingStep.GradeLock, userId, notes, ct);

        return new TermClosingStepResultDto { Success = true, Message = notes };
    }

    // ═══════════════════════════════════════════════════════════════
    //  STEP 1 ROLLBACK: UNLOCK GRADES
    // ═══════════════════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════════════════
    //  STEP 2: CALCULATE GPA
    // ═══════════════════════════════════════════════════════════════

    public async Task<GpaRecalculationResultDto> CalculateGpaAsync(int departmentId, int userId, CancellationToken ct = default)
    {
        var result = await _progressionService.RecalculateDepartmentGpaAsync(departmentId, ct);

        var term = await GetActiveTermAsync(ct);
        if (term != null)
        {
            var notes = $"{result.RecalculatedCount} ogrenci hesaplandi, {result.ChangedCount} degisiklik.";
            await UpsertProgressAsync(departmentId, term.Id, TermClosingStep.GpaCalculation, userId, notes, ct);
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════════
    //  STEP 3: PREVIEW PROMOTION (DRY RUN)
    // ═══════════════════════════════════════════════════════════════

    public async Task<BatchProgressionResultDto> PreviewPromotionAsync(int departmentId, CancellationToken ct = default)
    {
        return await _progressionService.PromoteDepartmentStudentsAsync(departmentId, dryRun: true, ct);
    }

    // ═══════════════════════════════════════════════════════════════
    //  STEP 3: PROMOTE
    // ═══════════════════════════════════════════════════════════════

    public async Task<BatchProgressionResultDto> PromoteAsync(int departmentId, int userId, CancellationToken ct = default)
    {
        // Take snapshot of current CompletedSemesters for rollback
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
            var notes = snapshotJson;
            await UpsertProgressAsync(departmentId, term.Id, TermClosingStep.Promotion, userId, notes, ct);
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════════
    //  STEP 3 ROLLBACK
    // ═══════════════════════════════════════════════════════════════

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

        Dictionary<int, int>? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<Dictionary<int, int>>(progress.Notes);
        }
        catch
        {
            return new TermClosingStepResultDto { Success = false, Message = "Snapshot verisi okunamadi." };
        }

        if (snapshot == null || snapshot.Count == 0)
            return new TermClosingStepResultDto { Success = false, Message = "Snapshot bos." };

        var studentIds = snapshot.Keys.ToList();
        var students = await _context.Students
            .Where(s => studentIds.Contains(s.Id))
            .ToListAsync(ct);

        int restoredCount = 0;
        foreach (var student in students)
        {
            if (snapshot.TryGetValue(student.Id, out var oldSemesters) && student.CompletedSemesters != oldSemesters)
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

    // ═══════════════════════════════════════════════════════════════
    //  STEP 4: OPEN NEW TERM
    // ═══════════════════════════════════════════════════════════════

    public async Task<NewTermResultDto> OpenNewTermAsync(int departmentId, int userId, NewTermRequestDto request, CancellationToken ct = default)
    {
        var currentTerm = await GetActiveTermAsync(ct);
        if (currentTerm == null)
            return new NewTermResultDto { Success = false, Message = "Aktif donem bulunamadi." };

        // Check if a term with the same Year+Semester already exists
        var existingTerm = await _context.AcademicTerms
            .FirstOrDefaultAsync(t => t.Year == request.Year && t.Semester == request.Semester, ct);

        int newTermId;

        if (existingTerm != null)
        {
            // Another department already created this term — reuse it
            newTermId = existingTerm.Id;
            if (!existingTerm.IsActive)
            {
                existingTerm.IsActive = true;
            }
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
                WithdrawalDeadlineWeek = request.Semester == SemesterType.Summer ? 4 : 7,
                TermStart = request.TermStart,
                TermEnd = request.TermEnd,
                IsActive = true
            };
            _context.AcademicTerms.Add(newTerm);
            await _context.SaveChangesAsync(ct);
            newTermId = newTerm.Id;
        }

        // Deactivate old term
        currentTerm.IsActive = false;
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
        return await _context.AcademicTerms
            .FirstOrDefaultAsync(t => t.IsActive, ct);
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
```

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: Build succeeded. 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add SIS.Infrastructure/Services/TermClosingService.cs
git commit -m "feat: implement TermClosingService with all 4 steps and rollback"
```

---

### Task 5: DI Registration

**Files:**
- Modify: `SIS.API/Program.cs`

- [ ] **Step 1: Add using statement at top of Program.cs**

Add after the existing `using SIS.Application.CourseOpening.Services;` line:

```csharp
using SIS.Application.TermClosing.Services;
```

- [ ] **Step 2: Register the service**

Add after the `builder.Services.AddScoped<ICourseOpeningService, CourseOpeningService>();` line:

```csharp
builder.Services.AddScoped<ITermClosingService, TermClosingService>();
```

- [ ] **Step 3: Verify build**

Run: `dotnet build`
Expected: Build succeeded. 0 Errors.

- [ ] **Step 4: Commit**

```bash
git add SIS.API/Program.cs
git commit -m "feat: register ITermClosingService in DI container"
```

---

### Task 6: Controller — TermClosingController

**Files:**
- Create: `SIS.API/Controllers/TermClosingController.cs`

- [ ] **Step 1: Create the controller**

```csharp
// SIS.API/Controllers/TermClosingController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.TermClosing.DTOs;
using SIS.Application.TermClosing.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/term-closing")]
[Authorize(Roles = "DepartmentManager,SystemAdministrator")]
public class TermClosingController : ControllerBase
{
    private readonly ITermClosingService _service;

    public TermClosingController(ITermClosingService service)
    {
        _service = service;
    }

    [HttpGet("{departmentId}/status")]
    public async Task<IActionResult> GetStatus(int departmentId, CancellationToken ct)
    {
        var result = await _service.GetStatusAsync(departmentId, ct);
        return Ok(result);
    }

    [HttpPost("{departmentId}/lock-grades")]
    public async Task<IActionResult> LockGrades(int departmentId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _service.LockGradesAsync(departmentId, userId.Value, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{departmentId}/unlock-grades")]
    public async Task<IActionResult> UnlockGrades(int departmentId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _service.UnlockGradesAsync(departmentId, userId.Value, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{departmentId}/calculate-gpa")]
    public async Task<IActionResult> CalculateGpa(int departmentId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _service.CalculateGpaAsync(departmentId, userId.Value, ct);
        return Ok(result);
    }

    [HttpGet("{departmentId}/preview-promote")]
    public async Task<IActionResult> PreviewPromotion(int departmentId, CancellationToken ct)
    {
        var result = await _service.PreviewPromotionAsync(departmentId, ct);
        return Ok(result);
    }

    [HttpPost("{departmentId}/promote")]
    public async Task<IActionResult> Promote(int departmentId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _service.PromoteAsync(departmentId, userId.Value, ct);
        return Ok(result);
    }

    [HttpPost("{departmentId}/rollback-promote")]
    public async Task<IActionResult> RollbackPromotion(int departmentId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _service.RollbackPromotionAsync(departmentId, userId.Value, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{departmentId}/open-new-term")]
    public async Task<IActionResult> OpenNewTerm(int departmentId, [FromBody] NewTermRequestDto request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _service.OpenNewTermAsync(departmentId, userId.Value, request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim, out var id) ? id : null;
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: Build succeeded. 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add SIS.API/Controllers/TermClosingController.cs
git commit -m "feat: add TermClosingController with 8 endpoints"
```

---

### Task 7: Service Tests — TermClosingServiceTests

**Files:**
- Create: `SIS.Tests/Application/TermClosingServiceTests.cs`

- [ ] **Step 1: Create the test class with seed data and all tests**

```csharp
// SIS.Tests/Application/TermClosingServiceTests.cs
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.TermClosing.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class TermClosingServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly TermClosingService _service;

    private const int FacultyId = 1;
    private const int DepartmentId = 1;
    private const int ProgramId = 1;
    private const int CurriculumId = 1;
    private const int TermId = 1;
    private const int UserId = 100;
    private const int StudentId = 1;
    private const int StudentUserId = 200;
    private const int CourseId = 1;
    private const int SectionId = 1;

    public TermClosingServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);

        var gpaService = new GpaCalculationService(_context);
        var progressionService = new AcademicProgressionService(_context, gpaService);
        _service = new TermClosingService(_context, progressionService, gpaService);

        SeedBaseData();
    }

    private void SeedBaseData()
    {
        _context.Faculties.Add(new Faculty { Id = FacultyId, Name = "Muhendislik", Code = "MUH" });
        _context.Departments.Add(new Department { Id = DepartmentId, Name = "Bilgisayar", Code = "BM", FacultyId = FacultyId });

        _context.AcademicTerms.Add(new AcademicTerm
        {
            Id = TermId,
            Name = "2025-2026 Guz",
            Year = 2025,
            Semester = SemesterType.Fall,
            IsActive = true,
            RegistrationStart = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            RegistrationEnd = new DateTime(2025, 9, 15, 0, 0, 0, DateTimeKind.Utc),
            AddDropStart = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            AddDropEnd = new DateTime(2025, 9, 15, 0, 0, 0, DateTimeKind.Utc),
            TermStart = new DateTime(2025, 9, 15, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
        });

        _context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId,
            Name = "Bilgisayar Muhendisligi",
            Code = "BM",
            DepartmentId = DepartmentId,
            DegreeLevel = DegreeLevel.Bachelor,
            TotalRequiredEcts = 240
        });

        _context.Set<Curriculum>().Add(new Curriculum
        {
            Id = CurriculumId,
            ProgramId = ProgramId,
            Year = 2024,
            IsActive = true
        });

        // User for the student
        _context.Users.Add(new ApplicationUser
        {
            Id = StudentUserId,
            UserName = "student1",
            NormalizedUserName = "STUDENT1",
            FirstName = "Ali",
            LastName = "Yilmaz",
            Email = "ali@test.com",
            NormalizedEmail = "ALI@TEST.COM"
        });

        _context.Students.Add(new Student
        {
            Id = StudentId,
            UserId = StudentUserId,
            StudentNumber = "2024BM0001",
            NationalId = "11111111111",
            Status = StudentStatus.Active,
            Cgpa = 2.50m,
            Sgpa = 2.50m,
            CompletedSemesters = 2
        });

        _context.StudentPrograms.Add(new StudentProgram
        {
            Id = 1,
            StudentId = StudentId,
            ProgramId = ProgramId,
            Type = StudentProgramType.Main,
            IsActive = true,
            EnrollmentDate = new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        // Course and Section
        _context.Courses.Add(new Course
        {
            Id = CourseId,
            Code = "BM101",
            Name = "Programlama",
            DepartmentId = DepartmentId,
            Ects = 6,
            TheoryHours = 3,
            LabHours = 2
        });

        _context.CourseSections.Add(new CourseSection
        {
            Id = SectionId,
            CourseId = CourseId,
            TermId = TermId,
            Capacity = 40,
            EnrolledCount = 1,
            IsActive = true,
            IsFinalized = false
        });

        // Registration
        _context.CourseRegistrations.Add(new CourseRegistration
        {
            Id = 1,
            StudentId = StudentId,
            CourseSectionId = SectionId,
            Status = RegistrationStatus.Active,
            CreatedAt = DateTime.UtcNow
        });

        // Grade components (Vize %40, Final %60)
        _context.GradeComponents.Add(new GradeComponent
        {
            Id = 1,
            CourseId = CourseId,
            AcademicTermId = TermId,
            Name = "Vize",
            Weight = 40,
            Order = 1
        });
        _context.GradeComponents.Add(new GradeComponent
        {
            Id = 2,
            CourseId = CourseId,
            AcademicTermId = TermId,
            Name = "Final",
            Weight = 60,
            Order = 2
        });

        // Student grades (Vize: 70, Final: 80 → weighted: 76 → BB)
        _context.StudentGrades.Add(new StudentGrade { Id = 1, CourseRegistrationId = 1, GradeComponentId = 1, Score = 70 });
        _context.StudentGrades.Add(new StudentGrade { Id = 2, CourseRegistrationId = 1, GradeComponentId = 2, Score = 80 });

        // User for the manager (for CompletedByUserId tracking)
        _context.Users.Add(new ApplicationUser
        {
            Id = UserId,
            UserName = "manager1",
            NormalizedUserName = "MANAGER1",
            FirstName = "Mehmet",
            LastName = "Ozturk",
            Email = "mehmet@test.com",
            NormalizedEmail = "MEHMET@TEST.COM"
        });

        _context.SaveChanges();
    }

    // ── GET STATUS ──

    [Fact]
    public async Task GetStatus_ReturnsAllFourSteps()
    {
        var result = await _service.GetStatusAsync(DepartmentId);

        result.Steps.Should().HaveCount(4);
        result.Steps.Should().AllSatisfy(s => s.IsCompleted.Should().BeFalse());
        result.TermId.Should().Be(TermId);
    }

    // ── LOCK GRADES ──

    [Fact]
    public async Task LockGrades_FinalizesAllSections()
    {
        var result = await _service.LockGradesAsync(DepartmentId, UserId);

        result.Success.Should().BeTrue();

        var section = await _context.CourseSections.FindAsync(SectionId);
        section!.IsFinalized.Should().BeTrue();

        // Student should have a grade calculated
        var reg = await _context.CourseRegistrations.FindAsync(1);
        reg!.Grade.Should().NotBeNull();
    }

    [Fact]
    public async Task LockGrades_CalculatesCorrectGrade()
    {
        await _service.LockGradesAsync(DepartmentId, UserId);

        // Vize: 70 * 0.4 = 28, Final: 80 * 0.6 = 48, Total = 76 → BB (71-80)
        var reg = await _context.CourseRegistrations.FindAsync(1);
        reg!.Grade.Should().Be(GradeCode.BB);
    }

    [Fact]
    public async Task LockGrades_RecordsProgress()
    {
        await _service.LockGradesAsync(DepartmentId, UserId);

        var progress = await _context.TermClosingProgresses
            .FirstOrDefaultAsync(p => p.DepartmentId == DepartmentId && p.Step == TermClosingStep.GradeLock);

        progress.Should().NotBeNull();
        progress!.IsCompleted.Should().BeTrue();
        progress.CompletedByUserId.Should().Be(UserId);
    }

    // ── UNLOCK GRADES ──

    [Fact]
    public async Task UnlockGrades_ClearsFinalized()
    {
        await _service.LockGradesAsync(DepartmentId, UserId);
        var result = await _service.UnlockGradesAsync(DepartmentId, UserId);

        result.Success.Should().BeTrue();

        var section = await _context.CourseSections.FindAsync(SectionId);
        section!.IsFinalized.Should().BeFalse();

        var progress = await _context.TermClosingProgresses
            .FirstOrDefaultAsync(p => p.DepartmentId == DepartmentId && p.Step == TermClosingStep.GradeLock);
        progress!.IsCompleted.Should().BeFalse();
    }

    // ── CALCULATE GPA ──

    [Fact]
    public async Task CalculateGpa_RecalculatesAndRecordsProgress()
    {
        var result = await _service.CalculateGpaAsync(DepartmentId, UserId);

        result.TotalStudents.Should().BeGreaterThan(0);

        var progress = await _context.TermClosingProgresses
            .FirstOrDefaultAsync(p => p.DepartmentId == DepartmentId && p.Step == TermClosingStep.GpaCalculation);
        progress.Should().NotBeNull();
        progress!.IsCompleted.Should().BeTrue();
    }

    // ── PROMOTE ──

    [Fact]
    public async Task Promote_SavesSnapshotAndRecordsProgress()
    {
        var result = await _service.PromoteAsync(DepartmentId, UserId);

        result.TotalStudentsAnalyzed.Should().BeGreaterThan(0);

        var progress = await _context.TermClosingProgresses
            .FirstOrDefaultAsync(p => p.DepartmentId == DepartmentId && p.Step == TermClosingStep.Promotion);
        progress.Should().NotBeNull();
        progress!.IsCompleted.Should().BeTrue();

        // Notes should contain a valid JSON snapshot
        var snapshot = JsonSerializer.Deserialize<Dictionary<int, int>>(progress.Notes!);
        snapshot.Should().NotBeNull();
        snapshot.Should().ContainKey(StudentId);
    }

    // ── ROLLBACK PROMOTION ──

    [Fact]
    public async Task RollbackPromotion_RestoresOldValues()
    {
        var originalSemesters = (await _context.Students.FindAsync(StudentId))!.CompletedSemesters;

        await _service.PromoteAsync(DepartmentId, UserId);
        var result = await _service.RollbackPromotionAsync(DepartmentId, UserId);

        result.Success.Should().BeTrue();

        var student = await _context.Students.FindAsync(StudentId);
        student!.CompletedSemesters.Should().Be(originalSemesters);

        var progress = await _context.TermClosingProgresses
            .FirstOrDefaultAsync(p => p.DepartmentId == DepartmentId && p.Step == TermClosingStep.Promotion);
        progress!.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task RollbackPromotion_FailsWhenNoSnapshot()
    {
        var result = await _service.RollbackPromotionAsync(DepartmentId, UserId);

        result.Success.Should().BeFalse();
    }

    // ── OPEN NEW TERM ──

    [Fact]
    public async Task OpenNewTerm_CreatesTermAndDeactivatesOld()
    {
        var request = new NewTermRequestDto
        {
            Name = "2025-2026 Bahar",
            Year = 2025,
            Semester = SemesterType.Spring,
            RegistrationStart = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            RegistrationEnd = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            TermStart = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        var result = await _service.OpenNewTermAsync(DepartmentId, UserId, request);

        result.Success.Should().BeTrue();
        result.NewTermId.Should().NotBeNull();

        // Old term deactivated
        var oldTerm = await _context.AcademicTerms.FindAsync(TermId);
        oldTerm!.IsActive.Should().BeFalse();

        // New term active
        var newTerm = await _context.AcademicTerms.FindAsync(result.NewTermId);
        newTerm!.IsActive.Should().BeTrue();
        newTerm.Name.Should().Be("2025-2026 Bahar");
    }

    [Fact]
    public async Task OpenNewTerm_ReusesExistingTerm()
    {
        // Create a term with same Year+Semester
        _context.AcademicTerms.Add(new AcademicTerm
        {
            Id = 99,
            Name = "2025-2026 Bahar",
            Year = 2025,
            Semester = SemesterType.Spring,
            IsActive = false,
            RegistrationStart = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            RegistrationEnd = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            AddDropStart = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            AddDropEnd = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            TermStart = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)
        });
        await _context.SaveChangesAsync();

        var request = new NewTermRequestDto
        {
            Name = "2025-2026 Bahar",
            Year = 2025,
            Semester = SemesterType.Spring,
            RegistrationStart = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            RegistrationEnd = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            TermStart = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        var result = await _service.OpenNewTermAsync(DepartmentId, UserId, request);

        result.Success.Should().BeTrue();
        result.NewTermId.Should().Be(99);

        // Should not create a new term, total should be 2
        var termCount = await _context.AcademicTerms.CountAsync();
        termCount.Should().Be(2);
    }

    // ── NO ACTIVE TERM ──

    [Fact]
    public async Task LockGrades_FailsWhenNoActiveTerm()
    {
        var term = await _context.AcademicTerms.FindAsync(TermId);
        term!.IsActive = false;
        await _context.SaveChangesAsync();

        var result = await _service.LockGradesAsync(DepartmentId, UserId);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Aktif donem bulunamadi");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test --filter "TermClosingServiceTests" --nologo`
Expected: All 12 tests pass.

- [ ] **Step 3: Commit**

```bash
git add SIS.Tests/Application/TermClosingServiceTests.cs
git commit -m "test: add TermClosingService tests covering all 4 steps and rollback"
```

---

### Task 8: Controller Tests — TermClosingControllerTests

**Files:**
- Create: `SIS.Tests/API/TermClosingControllerTests.cs`

- [ ] **Step 1: Create the test class**

```csharp
// SIS.Tests/API/TermClosingControllerTests.cs
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Controllers;

namespace SIS.Tests.API;

public class TermClosingControllerTests
{
    [Fact]
    public void Controller_HasCorrectRouteAndAuthorization()
    {
        var controllerType = typeof(TermClosingController);

        var routeAttribute = controllerType.GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Single();

        var authorizeAttribute = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        routeAttribute.Template.Should().Be("api/term-closing");
        authorizeAttribute.Roles.Should().Be("DepartmentManager,SystemAdministrator");
    }

    [Theory]
    [InlineData("GetStatus", "GET")]
    [InlineData("LockGrades", "POST")]
    [InlineData("UnlockGrades", "POST")]
    [InlineData("CalculateGpa", "POST")]
    [InlineData("PreviewPromotion", "GET")]
    [InlineData("Promote", "POST")]
    [InlineData("RollbackPromotion", "POST")]
    [InlineData("OpenNewTerm", "POST")]
    public void Controller_HasExpectedHttpMethods(string methodName, string expectedMethod)
    {
        var method = typeof(TermClosingController).GetMethod(methodName);
        method.Should().NotBeNull($"method {methodName} should exist");

        var hasCorrectAttribute = expectedMethod switch
        {
            "GET" => method!.GetCustomAttributes(typeof(HttpGetAttribute), false).Length > 0,
            "POST" => method!.GetCustomAttributes(typeof(HttpPostAttribute), false).Length > 0,
            _ => false
        };

        hasCorrectAttribute.Should().BeTrue($"{methodName} should have Http{expectedMethod} attribute");
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test --filter "TermClosingControllerTests" --nologo`
Expected: All 9 tests pass (1 Fact + 8 Theory).

- [ ] **Step 3: Commit**

```bash
git add SIS.Tests/API/TermClosingControllerTests.cs
git commit -m "test: add TermClosingController route and authorization tests"
```

---

### Task 9: Final Integration Test

**Files:** None (verification only)

- [ ] **Step 1: Run full backend test suite**

Run: `dotnet test --nologo`
Expected: All tests pass (155 existing + 12 service + 9 controller = ~176 tests).

- [ ] **Step 2: Verify backend build**

Run: `dotnet build --nologo`
Expected: Build succeeded. 0 Warnings. 0 Errors.

- [ ] **Step 3: Verify frontend build (no regression)**

Run: `cd SIS.Web && npm run build`
Expected: Build succeeds.
