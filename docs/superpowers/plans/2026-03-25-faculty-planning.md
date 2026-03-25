# Faculty Planning (Fakülte Planlama) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a faculty-wide classroom/schedule assignment page (Phase 2) that works with the existing department-level section approval (Phase 1), enabling cross-department conflict detection and hybrid automatic+manual scheduling.

**Architecture:** New `FacultyPlanningService` in Infrastructure layer handles all business logic. New `FacultyPlanningController` exposes 8 REST endpoints. New React page with 4 tabs (Overview, Section List, Auto-Schedule, Conflict Analysis). Reuses existing `SchedulingService` for conflict detection and suggestion logic. New `SchedulingAuditLog` entity tracks assignment history.

**Tech Stack:** ASP.NET Core 10, EF Core 10, PostgreSQL, React 18, TanStack Query, shadcn/ui, Tailwind CSS

**Spec:** `docs/superpowers/specs/2026-03-25-faculty-planning-design.md`

---

## File Structure

### New Files — Backend

| File | Responsibility |
|------|---------------|
| `SIS.Domain/Entities/SchedulingAuditLog.cs` | Audit trail entity for scheduling assignments |
| `SIS.Application/FacultyPlanning/DTOs/FacultyPlanningOverviewDto.cs` | Overview tab DTOs (progress, warnings, recent logs) |
| `SIS.Application/FacultyPlanning/DTOs/FacultyPlanningSectionDto.cs` | Section list DTO with status |
| `SIS.Application/FacultyPlanning/DTOs/AssignSectionDto.cs` | Assign/unassign request+result DTOs |
| `SIS.Application/FacultyPlanning/DTOs/AutoScheduleDto.cs` | Auto-schedule preview+apply DTOs |
| `SIS.Application/FacultyPlanning/DTOs/AvailableClassroomDto.cs` | Available classroom query DTO |
| `SIS.Application/FacultyPlanning/Services/IFacultyPlanningService.cs` | Service interface |
| `SIS.Infrastructure/Services/FacultyPlanningService.cs` | Service implementation |
| `SIS.API/Controllers/FacultyPlanningController.cs` | 8 API endpoints |
| `SIS.Tests/Application/FacultyPlanningServiceTests.cs` | Unit tests |

### New Files — Frontend

| File | Responsibility |
|------|---------------|

| File | Responsibility |
|------|---------------|
| `SIS.Web/src/types/facultyPlanning.ts` | TypeScript types matching backend DTOs |
| `SIS.Web/src/api/facultyPlanning.api.ts` | API client functions |
| `SIS.Web/src/hooks/useFacultyPlanning.ts` | React Query hooks |
| `SIS.Web/src/pages/FacultyPlanningPage.tsx` | Main page with 4 tabs |

### Modified Files

| File | Change |
|------|--------|
| `SIS.Domain/ValueObjects/ScheduleSlot.cs` | Add nullable `ClassroomId` field |
| `SIS.Application/Scheduling/DTOs/ScheduleSlotDto.cs` | Add `ClassroomId` + `ClassroomName` |
| `SIS.Domain/Entities/CourseSection.cs` | Add `RowVersion` concurrency token |
| `SIS.Infrastructure/Persistence/AppDbContext.cs` | Register `SchedulingAuditLog` DbSet, configure `CourseSection.RowVersion` |
| `SIS.Web/src/lib/constants.ts` | Add `FACULTY_PLANNING` route, remove `MANAGEMENT_BATCH_SCHEDULING`, add query keys |
| `SIS.Web/src/App.tsx` | Add FacultyPlanningPage route, remove SchedulingDashboardPage route |
| `SIS.Web/src/components/layout/SidebarMenuGroups.ts` | Add "Fakülte Planlama" menu item, remove "Toplu Programlama" |
| `SIS.Web/src/pages/InstructorSchedulingPage.tsx` | Remove classroom assignment UI (derslik ataması Fakülte Planlama'ya taşındı) |

---

## Task 1: Domain Layer — New Entity + Model Changes

**Files:**
- Create: `SIS.Domain/Entities/SchedulingAuditLog.cs`
- Modify: `SIS.Domain/ValueObjects/ScheduleSlot.cs`
- Modify: `SIS.Domain/Entities/CourseSection.cs`

- [ ] **Step 1: Create SchedulingAuditLog entity**

```csharp
// SIS.Domain/Entities/SchedulingAuditLog.cs
namespace SIS.Domain.Entities;

public class SchedulingAuditLog
{
    public int Id { get; set; }
    public int SectionId { get; set; }
    public CourseSection Section { get; set; } = null!;
    public string Action { get; set; } = string.Empty; // "Assign", "Unassign", "AutoSchedule"
    public string UserId { get; set; } = string.Empty;
    public int? ClassroomId { get; set; }
    public string? ScheduleJson { get; set; }
    public int? InstructorId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Add ClassroomId to ScheduleSlot**

In `SIS.Domain/ValueObjects/ScheduleSlot.cs`, add after `EndTime`:

```csharp
public int? ClassroomId { get; set; }
```

- [ ] **Step 3: Add RowVersion to CourseSection**

In `SIS.Domain/Entities/CourseSection.cs`, add:

```csharp
public uint RowVersion { get; set; }
```

Note: PostgreSQL uses `xmin` system column, EF Core maps this via `uint` with `IsRowVersion()`.

- [ ] **Step 4: Add ClassroomId to ScheduleSlotDto**

In `SIS.Application/Scheduling/DTOs/ScheduleSlotDto.cs`, add:

```csharp
public int? ClassroomId { get; set; }
public string? ClassroomName { get; set; }
```

- [ ] **Step 5: Commit**

```bash
git add SIS.Domain/Entities/SchedulingAuditLog.cs SIS.Domain/ValueObjects/ScheduleSlot.cs SIS.Domain/Entities/CourseSection.cs SIS.Application/Scheduling/DTOs/ScheduleSlotDto.cs
git commit -m "feat: add SchedulingAuditLog entity, RowVersion on CourseSection, ClassroomId on ScheduleSlot"
```

---

## Task 2: DbContext Configuration + Migration

**Files:**
- Modify: `SIS.Infrastructure/Persistence/AppDbContext.cs`

- [ ] **Step 1: Register SchedulingAuditLog and configure CourseSection RowVersion**

In `AppDbContext.cs`:

1. Add DbSet:
```csharp
public DbSet<SchedulingAuditLog> SchedulingAuditLogs { get; set; }
```

2. In `OnModelCreating`, add SchedulingAuditLog configuration:
```csharp
modelBuilder.Entity<SchedulingAuditLog>(entity =>
{
    entity.HasOne(e => e.Section)
        .WithMany()
        .HasForeignKey(e => e.SectionId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasIndex(e => new { e.SectionId, e.CreatedAt });
});
```

3. In the existing `CourseSection` configuration, add:
```csharp
entity.Property(e => e.RowVersion)
    .IsRowVersion();
```

- [ ] **Step 2: Create EF Core migration**

Run:
```bash
cd "c:/Users/Sertuğ/Desktop/Student Information System"
dotnet ef migrations add AddFacultyPlanningSupport -p SIS.Infrastructure -s SIS.API
```

- [ ] **Step 3: Apply migration**

Run:
```bash
dotnet ef database update -p SIS.Infrastructure -s SIS.API
```

- [ ] **Step 4: Commit**

```bash
git add SIS.Infrastructure/Persistence/AppDbContext.cs SIS.Infrastructure/Migrations/
git commit -m "feat: add SchedulingAuditLog table and RowVersion on CourseSection migration"
```

---

## Task 3: Application Layer — DTOs

**Files:**
- Create: `SIS.Application/FacultyPlanning/DTOs/FacultyPlanningOverviewDto.cs`
- Create: `SIS.Application/FacultyPlanning/DTOs/FacultyPlanningSectionDto.cs`
- Create: `SIS.Application/FacultyPlanning/DTOs/AssignSectionDto.cs`
- Create: `SIS.Application/FacultyPlanning/DTOs/AutoScheduleDto.cs`
- Create: `SIS.Application/FacultyPlanning/DTOs/AvailableClassroomDto.cs`

- [ ] **Step 1: Create FacultyPlanningOverviewDto.cs**

```csharp
namespace SIS.Application.FacultyPlanning.DTOs;

public class FacultyPlanningOverviewDto
{
    public int FacultyId { get; set; }
    public string FacultyName { get; set; } = string.Empty;
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public int TotalSections { get; set; }
    public int ScheduledSections { get; set; }
    public int UnscheduledSections { get; set; }
    public int ConflictCount { get; set; }
    public Dictionary<string, int> ConflictCountByType { get; set; } = new();
    public List<DepartmentProgressDto> DepartmentProgress { get; set; } = new();
    public List<PendingApprovalWarningDto> PendingApprovalWarnings { get; set; } = new();
    public List<RecentAssignmentDto> RecentAssignments { get; set; } = new();
}

public class DepartmentProgressDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public int TotalSections { get; set; }
    public int ScheduledSections { get; set; }
    public int PendingApprovals { get; set; }
}

public class PendingApprovalWarningDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int PendingCount { get; set; }
}

public class RecentAssignmentDto
{
    public int SectionId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string? ClassroomName { get; set; }
    public string? ScheduleSummary { get; set; }
    public string AssignedByUserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
}
```

- [ ] **Step 2: Create FacultyPlanningSectionDto.cs**

```csharp
namespace SIS.Application.FacultyPlanning.DTOs;

public class FacultyPlanningSectionDto
{
    public int SectionId { get; set; }
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string DepartmentCode { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int? ClassroomId { get; set; }
    public string? ClassroomName { get; set; }
    public string? ClassroomType { get; set; }
    public int? InstructorId { get; set; }
    public string? InstructorName { get; set; }
    public string ScheduleJson { get; set; } = "[]";
    public string ScheduleSummary { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Unscheduled, Scheduled, Conflicted, Partial
    public int TheoryHours { get; set; }
    public int LabHours { get; set; }
    public int SectionIndex { get; set; }
}

public class FacultyPlanningSectionPageDto
{
    public List<FacultyPlanningSectionDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
```

- [ ] **Step 3: Create AssignSectionDto.cs**

```csharp
using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.FacultyPlanning.DTOs;

public class AssignSectionRequest
{
    public int SectionId { get; set; }
    public int? ClassroomId { get; set; }
    public List<ScheduleSlotDto>? ScheduleSlots { get; set; }
    public int? InstructorId { get; set; }
}

public class AssignmentResultDto
{
    public bool Success { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<SchedulingConflictDto> Conflicts { get; set; } = new();
}
```

- [ ] **Step 4: Create AutoScheduleDto.cs**

```csharp
using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.FacultyPlanning.DTOs;

public class AutoScheduleRequest
{
    public int FacultyId { get; set; }
    public int TermId { get; set; }
    public int? DepartmentId { get; set; }
    public List<int>? SectionIds { get; set; }
}

public class AutoSchedulePreviewDto
{
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<AutoSchedulePreviewItemDto> Assignments { get; set; } = new();
    public List<AutoScheduleFailureDto> Failures { get; set; } = new();
}

public class AutoSchedulePreviewItemDto
{
    public int SectionId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public int ClassroomId { get; set; }
    public string ClassroomName { get; set; } = string.Empty;
    public List<ScheduleSlotDto> ScheduleSlots { get; set; } = new();
    public string ScheduleSummary { get; set; } = string.Empty;
    public int? InstructorId { get; set; }
    public string? InstructorName { get; set; }
    public decimal FitScore { get; set; }
}

public class AutoScheduleFailureDto
{
    public int SectionId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class ApplyScheduleRequest
{
    public List<ApplyScheduleItemRequest> Assignments { get; set; } = new();
}

public class ApplyScheduleItemRequest
{
    public int SectionId { get; set; }
    public int ClassroomId { get; set; }
    public List<ScheduleSlotDto> ScheduleSlots { get; set; } = new();
    public int? InstructorId { get; set; }
}

public class ApplyScheduleResultDto
{
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
```

- [ ] **Step 5: Create AvailableClassroomDto.cs**

```csharp
namespace SIS.Application.FacultyPlanning.DTOs;

public class AvailableClassroomDto
{
    public int ClassroomId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal FitScore { get; set; }
}
```

- [ ] **Step 6: Commit**

```bash
git add SIS.Application/FacultyPlanning/
git commit -m "feat: add Faculty Planning DTOs"
```

---

## Task 4: Application Layer — Service Interface

**Files:**
- Create: `SIS.Application/FacultyPlanning/Services/IFacultyPlanningService.cs`

- [ ] **Step 1: Create IFacultyPlanningService**

```csharp
using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.Scheduling.DTOs;

namespace SIS.Application.FacultyPlanning.Services;

public interface IFacultyPlanningService
{
    Task<FacultyPlanningOverviewDto> GetOverviewAsync(int facultyId, int termId, CancellationToken ct = default);

    Task<FacultyPlanningSectionPageDto> GetSectionsAsync(
        int facultyId, int termId, int? departmentId = null, string? status = null,
        int page = 1, int pageSize = 50, CancellationToken ct = default);

    Task<AssignmentResultDto> AssignSectionAsync(AssignSectionRequest request, string userId, CancellationToken ct = default);

    Task UnassignSectionAsync(int sectionId, string userId, CancellationToken ct = default);

    Task<AutoSchedulePreviewDto> AutoSchedulePreviewAsync(AutoScheduleRequest request, CancellationToken ct = default);

    Task<ApplyScheduleResultDto> ApplyScheduleAsync(ApplyScheduleRequest request, string userId, CancellationToken ct = default);

    Task<List<AvailableClassroomDto>> GetAvailableClassroomsAsync(
        int termId, DayOfWeek? day = null, TimeOnly? startTime = null, TimeOnly? endTime = null,
        int? minCapacity = null, string? type = null, CancellationToken ct = default);

    Task<List<SchedulingConflictDto>> GetConflictsAsync(int facultyId, int termId, CancellationToken ct = default);

    Task<int> ResolveFacultyForUserAsync(string userId, CancellationToken ct = default);
}
```

- [ ] **Step 2: Commit**

```bash
git add SIS.Application/FacultyPlanning/
git commit -m "feat: add IFacultyPlanningService interface"
```

---

## Task 5: Infrastructure — FacultyPlanningService (Core Logic)

**Files:**
- Create: `SIS.Infrastructure/Services/FacultyPlanningService.cs`

This is the largest task. The service implements all 9 methods. Key logic:

- [ ] **Step 1: Create FacultyPlanningService with constructor and ResolveFacultyForUserAsync**

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.FacultyPlanning.Services;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class FacultyPlanningService : IFacultyPlanningService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppDbContext _context;
    private readonly ISchedulingService _schedulingService;

    public FacultyPlanningService(AppDbContext context, ISchedulingService schedulingService)
    {
        _context = context;
        _schedulingService = schedulingService;
    }

    public async Task<int> ResolveFacultyForUserAsync(string userId, CancellationToken ct = default)
    {
        if (!int.TryParse(userId, out var userIdInt))
            throw new UnauthorizedAccessException("Gecersiz kullanici kimlik bilgisi.");

        var facultyId = await _context.Instructors
            .AsNoTracking()
            .Where(i => i.UserId == userIdInt)
            .Select(i => i.Department.FacultyId)
            .FirstOrDefaultAsync(ct);

        if (facultyId == 0)
            throw new UnauthorizedAccessException("Kullanici icin fakulte bilgisi bulunamadi.");

        return facultyId;
    }

    /// Resolves the department ID for a DepartmentManager user. Returns null for Admin roles.
    private async Task<int?> ResolveDepartmentForUserAsync(string userId, CancellationToken ct)
    {
        if (!int.TryParse(userId, out var userIdInt)) return null;

        return await _context.Instructors
            .AsNoTracking()
            .Where(i => i.UserId == userIdInt)
            .Select(i => (int?)i.DepartmentId)
            .FirstOrDefaultAsync(ct);
    }
}
```

- [ ] **Step 2: Implement GetOverviewAsync**

Add to `FacultyPlanningService`:

```csharp
public async Task<FacultyPlanningOverviewDto> GetOverviewAsync(int facultyId, int termId, CancellationToken ct = default)
{
    var faculty = await _context.Faculties
        .AsNoTracking()
        .Include(f => f.Departments)
        .FirstOrDefaultAsync(f => f.Id == facultyId, ct)
        ?? throw new KeyNotFoundException("Fakulte bulunamadi.");

    var term = await _context.AcademicTerms
        .AsNoTracking()
        .FirstOrDefaultAsync(t => t.Id == termId, ct)
        ?? throw new KeyNotFoundException("Donem bulunamadi.");

    var departmentIds = faculty.Departments.Select(d => d.Id).ToList();

    var sections = await _context.CourseSections
        .AsNoTracking()
        .Include(cs => cs.Course)
        .Include(cs => cs.Classroom)
        .Where(cs => cs.TermId == termId && cs.IsActive && departmentIds.Contains(cs.Course.DepartmentId ?? 0))
        .ToListAsync(ct);

    var scheduled = sections.Count(s => s.ClassroomId.HasValue && s.ScheduleJson != "[]");
    var unscheduled = sections.Count - scheduled;

    // Conflict detection — full term, no department filter
    var conflicts = await _schedulingService.GetConflictsAsync(termId, ct);
    var facultyConflicts = conflicts.Where(c =>
        sections.Any(s => s.Id == c.SectionId || s.Id == c.RelatedSectionId)).ToList();

    var conflictByType = facultyConflicts
        .GroupBy(c => c.ConflictType)
        .ToDictionary(g => g.Key, g => g.Count());

    // Department progress
    var departmentProgress = new List<DepartmentProgressDto>();
    foreach (var dept in faculty.Departments)
    {
        var deptSections = sections.Where(s => s.Course.DepartmentId == dept.Id).ToList();
        var pendingApprovals = await _context.CourseSectionApprovals
            .CountAsync(a => a.DepartmentId == dept.Id && a.TermId == termId && a.Status == SectionApprovalStatus.Pending, ct);

        departmentProgress.Add(new DepartmentProgressDto
        {
            DepartmentId = dept.Id,
            DepartmentName = dept.Name,
            DepartmentCode = dept.Code,
            TotalSections = deptSections.Count,
            ScheduledSections = deptSections.Count(s => s.ClassroomId.HasValue && s.ScheduleJson != "[]"),
            PendingApprovals = pendingApprovals
        });
    }

    var pendingWarnings = departmentProgress
        .Where(d => d.PendingApprovals > 0)
        .Select(d => new PendingApprovalWarningDto
        {
            DepartmentId = d.DepartmentId,
            DepartmentName = d.DepartmentName,
            PendingCount = d.PendingApprovals
        }).ToList();

    // Recent audit logs
    var recentLogs = await _context.SchedulingAuditLogs
        .AsNoTracking()
        .Include(l => l.Section).ThenInclude(s => s.Course)
        .Where(l => sections.Select(s => s.Id).Contains(l.SectionId))
        .OrderByDescending(l => l.CreatedAt)
        .Take(10)
        .ToListAsync(ct);

    var userIdInts = recentLogs
        .Select(l => int.TryParse(l.UserId, out var uid) ? uid : 0)
        .Where(id => id > 0).Distinct().ToList();
    var userNameMap = await _context.Users
        .AsNoTracking()
        .Where(u => userIdInts.Contains(u.Id))
        .ToDictionaryAsync(u => u.Id, u => u.UserName ?? "Bilinmeyen", ct);

    var classroomIds = recentLogs.Where(l => l.ClassroomId.HasValue).Select(l => l.ClassroomId!.Value).Distinct().ToList();
    var classroomNameMap = await _context.Classrooms
        .AsNoTracking()
        .Where(c => classroomIds.Contains(c.Id))
        .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

    return new FacultyPlanningOverviewDto
    {
        FacultyId = faculty.Id,
        FacultyName = faculty.Name,
        TermId = term.Id,
        TermName = term.Name,
        TotalSections = sections.Count,
        ScheduledSections = scheduled,
        UnscheduledSections = unscheduled,
        ConflictCount = facultyConflicts.Count,
        ConflictCountByType = conflictByType,
        DepartmentProgress = departmentProgress,
        PendingApprovalWarnings = pendingWarnings,
        RecentAssignments = recentLogs.Select(l => new RecentAssignmentDto
        {
            SectionId = l.SectionId,
            CourseCode = l.Section.Course.Code,
            CourseName = l.Section.Course.Name,
            ClassroomName = l.ClassroomId.HasValue
                ? classroomNameMap.GetValueOrDefault(l.ClassroomId.Value)
                : null,
            ScheduleSummary = l.ScheduleJson,
            AssignedByUserName = int.TryParse(l.UserId, out var uid) ? userNameMap.GetValueOrDefault(uid, "Bilinmeyen") : "Bilinmeyen",
            Action = l.Action,
            AssignedAt = l.CreatedAt
        }).ToList()
    };
}
```

- [ ] **Step 3: Implement GetSectionsAsync**

```csharp
public async Task<FacultyPlanningSectionPageDto> GetSectionsAsync(
    int facultyId, int termId, int? departmentId = null, string? status = null,
    int page = 1, int pageSize = 50, CancellationToken ct = default)
{
    var departmentIds = await _context.Departments
        .AsNoTracking()
        .Where(d => d.FacultyId == facultyId)
        .Select(d => d.Id)
        .ToListAsync(ct);

    if (departmentId.HasValue && !departmentIds.Contains(departmentId.Value))
        throw new UnauthorizedAccessException("Bu bolum belirtilen fakulteye ait degil.");

    var query = _context.CourseSections
        .AsNoTracking()
        .Include(cs => cs.Course).ThenInclude(c => c.Department)
        .Include(cs => cs.Classroom)
        .Include(cs => cs.Instructor)
        .Where(cs => cs.TermId == termId && cs.IsActive)
        .Where(cs => departmentIds.Contains(cs.Course.DepartmentId ?? 0));

    if (departmentId.HasValue)
        query = query.Where(cs => cs.Course.DepartmentId == departmentId.Value);

    var allSections = await query.OrderBy(cs => cs.Course.Code).ThenBy(cs => cs.Id).ToListAsync(ct);

    // Compute status and section index
    var grouped = allSections.GroupBy(s => new { s.CourseId, s.TermId });
    var items = new List<FacultyPlanningSectionDto>();

    // Get conflicts for status calculation
    var conflicts = await _schedulingService.GetConflictsAsync(termId, ct);
    var conflictSectionIds = conflicts.SelectMany(c => new[] { c.SectionId, c.RelatedSectionId ?? 0 }).ToHashSet();

    // Pre-load instructor names (batch, no N+1)
    var instructorIds = allSections.Where(s => s.InstructorId.HasValue).Select(s => s.InstructorId!.Value).Distinct();
    var instructorNameMap = await LoadInstructorNameMapAsync(instructorIds, ct);

    foreach (var group in grouped)
    {
        var index = 0;
        foreach (var s in group.OrderBy(x => x.Id))
        {
            index++;
            var hasClassroom = s.ClassroomId.HasValue;
            var hasSchedule = s.ScheduleJson != "[]";
            var hasInstructor = s.InstructorId.HasValue;
            var hasConflict = conflictSectionIds.Contains(s.Id);

            string sectionStatus;
            if (hasConflict) sectionStatus = "Conflicted";
            else if (hasClassroom && hasSchedule) sectionStatus = "Scheduled";
            else if (hasClassroom || hasInstructor) sectionStatus = "Partial";
            else sectionStatus = "Unscheduled";

            if (status != null && !string.Equals(sectionStatus, status, StringComparison.OrdinalIgnoreCase))
                continue;

            items.Add(new FacultyPlanningSectionDto
            {
                SectionId = s.Id,
                CourseId = s.CourseId,
                CourseCode = s.Course.Code,
                CourseName = s.Course.Name,
                DepartmentId = s.Course.DepartmentId ?? 0,
                DepartmentCode = s.Course.Department?.Code ?? "",
                Capacity = s.Capacity,
                ClassroomId = s.ClassroomId,
                ClassroomName = s.Classroom?.Name,
                ClassroomType = s.Classroom?.Type.ToString(),
                InstructorId = s.InstructorId,
                InstructorName = s.InstructorId.HasValue
                    ? instructorNameMap.GetValueOrDefault(s.InstructorId.Value)
                    : null,
                ScheduleJson = s.ScheduleJson,
                ScheduleSummary = BuildScheduleSummary(s.ScheduleJson),
                Status = sectionStatus,
                TheoryHours = s.Course.TheoryHours,
                LabHours = s.Course.LabHours,
                SectionIndex = index
            });
        }
    }

    var totalCount = items.Count;
    var paged = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();

    return new FacultyPlanningSectionPageDto
    {
        Items = paged,
        TotalCount = totalCount,
        Page = page,
        PageSize = pageSize
    };
}

/// Pre-loads instructor display names for given instructor IDs (batch, no N+1).
private async Task<Dictionary<int, string>> LoadInstructorNameMapAsync(IEnumerable<int> instructorIds, CancellationToken ct)
{
    var ids = instructorIds.Distinct().ToList();
    if (ids.Count == 0) return new();

    var instructors = await _context.Instructors
        .AsNoTracking()
        .Where(i => ids.Contains(i.Id))
        .Select(i => new { i.Id, i.Title, i.UserId })
        .ToListAsync(ct);

    var userIds = instructors.Select(i => i.UserId).Distinct().ToList();
    var userNameMap = await _context.Users
        .AsNoTracking()
        .Where(u => userIds.Contains(u.Id))
        .ToDictionaryAsync(u => u.Id, u => u.UserName ?? "", ct);

    return instructors.ToDictionary(
        i => i.Id,
        i => userNameMap.TryGetValue(i.UserId, out var name) ? $"{i.Title} {name}" : $"{i.Title} (#{i.Id})");
}

private static string BuildScheduleSummary(string scheduleJson)
{
    if (string.IsNullOrEmpty(scheduleJson) || scheduleJson == "[]") return "Zamanlanmamis";
    try
    {
        var slots = JsonSerializer.Deserialize<List<ScheduleSlotSimple>>(scheduleJson, JsonOptions);
        if (slots == null || slots.Count == 0) return "Zamanlanmamis";

        var dayLabels = new Dictionary<int, string>
        {
            [0] = "Paz", [1] = "Pzt", [2] = "Sal", [3] = "Car", [4] = "Per", [5] = "Cum", [6] = "Cmt"
        };

        return string.Join(", ", slots.Select(s =>
            $"{dayLabels.GetValueOrDefault((int)s.Day, "?")} {s.StartTime:HH:mm}-{s.EndTime:HH:mm}"));
    }
    catch { return "Gecersiz"; }
}

private record ScheduleSlotSimple(DayOfWeek Day, TimeOnly StartTime, TimeOnly EndTime);
```

- [ ] **Step 4: Implement AssignSectionAsync and UnassignSectionAsync**

```csharp
public async Task<AssignmentResultDto> AssignSectionAsync(AssignSectionRequest request, string userId, CancellationToken ct = default)
{
    var section = await _context.CourseSections
        .Include(cs => cs.Course)
        .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, ct)
        ?? throw new KeyNotFoundException("Sube bulunamadi.");

    if (section.IsFinalized)
        throw new InvalidOperationException("Bu sube kesinlesmis, degisiklik yapilamaz.");

    // DepartmentManager authorization: can only assign their own department's sections
    var userDeptId = await ResolveDepartmentForUserAsync(userId, ct);
    if (userDeptId.HasValue && section.Course.DepartmentId != userDeptId.Value)
        throw new UnauthorizedAccessException("Sadece kendi bolumunuzun subelerine atama yapabilirsiniz.");

    var result = new AssignmentResultDto { Success = true };

    // Validate classroom if provided
    if (request.ClassroomId.HasValue)
    {
        if (request.ScheduleSlots == null || request.ScheduleSlots.Count == 0)
            throw new ArgumentException("Derslik atanirken zaman slotlari da belirtilmelidir.");

        var classroom = await _context.Classrooms
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ClassroomId.Value, ct)
            ?? throw new KeyNotFoundException("Derslik bulunamadi.");

        if (classroom.Capacity < section.Capacity)
            result.Warnings.Add($"Derslik kapasitesi ({classroom.Capacity}) sube kapasitesinden ({section.Capacity}) dusuk.");

        var expectedType = section.Course.LabHours > 0 && section.Course.TheoryHours == 0
            ? ClassroomType.Lab : ClassroomType.Lecture;
        if (classroom.Type != expectedType)
            result.Warnings.Add($"Derslik tipi uyumsuz: beklenen {expectedType}, atanan {classroom.Type}.");

        section.ClassroomId = request.ClassroomId.Value;
        section.ScheduleJson = JsonSerializer.Serialize(
            request.ScheduleSlots.Select(s => new { s.Day, s.StartTime, s.EndTime, s.ClassroomId }),
            JsonOptions);
    }

    if (request.InstructorId.HasValue)
    {
        var instructorExists = await _context.Instructors.AnyAsync(i => i.Id == request.InstructorId.Value, ct);
        if (!instructorExists)
            throw new KeyNotFoundException("Ogretim uyesi bulunamadi.");
        section.InstructorId = request.InstructorId.Value;
    }

    // Check conflicts after assignment
    try
    {
        await _context.SaveChangesAsync(ct);
    }
    catch (DbUpdateConcurrencyException)
    {
        throw new InvalidOperationException("Bu sube baska bir kullanici tarafindan guncellendi. Lutfen yenileyip tekrar deneyin.");
    }

    // Run conflict check
    var conflicts = await _schedulingService.GetConflictsAsync(section.TermId, ct);
    var sectionConflicts = conflicts.Where(c => c.SectionId == section.Id || c.RelatedSectionId == section.Id).ToList();
    result.Conflicts = sectionConflicts;

    // Audit log
    _context.SchedulingAuditLogs.Add(new SchedulingAuditLog
    {
        SectionId = section.Id,
        Action = "Assign",
        UserId = userId,
        ClassroomId = request.ClassroomId,
        ScheduleJson = section.ScheduleJson,
        InstructorId = request.InstructorId
    });
    await _context.SaveChangesAsync(ct);

    return result;
}

public async Task UnassignSectionAsync(int sectionId, string userId, CancellationToken ct = default)
{
    var section = await _context.CourseSections
        .Include(cs => cs.Course)
        .FirstOrDefaultAsync(cs => cs.Id == sectionId, ct)
        ?? throw new KeyNotFoundException("Sube bulunamadi.");

    if (section.IsFinalized)
        throw new InvalidOperationException("Bu sube kesinlesmis, degisiklik yapilamaz.");

    var userDeptId = await ResolveDepartmentForUserAsync(userId, ct);
    if (userDeptId.HasValue && section.Course.DepartmentId != userDeptId.Value)
        throw new UnauthorizedAccessException("Sadece kendi bolumunuzun subelerinin atamasini kaldirabilirsiniz.");

    section.ClassroomId = null;
    section.ScheduleJson = "[]";
    section.InstructorId = null;

    _context.SchedulingAuditLogs.Add(new SchedulingAuditLog
    {
        SectionId = sectionId,
        Action = "Unassign",
        UserId = userId
    });

    try
    {
        await _context.SaveChangesAsync(ct);
    }
    catch (DbUpdateConcurrencyException)
    {
        throw new InvalidOperationException("Bu sube baska bir kullanici tarafindan guncellendi. Lutfen yenileyip tekrar deneyin.");
    }
}
```

- [ ] **Step 5: Implement GetAvailableClassroomsAsync**

```csharp
public async Task<List<AvailableClassroomDto>> GetAvailableClassroomsAsync(
    int termId, DayOfWeek? day = null, TimeOnly? startTime = null, TimeOnly? endTime = null,
    int? minCapacity = null, string? type = null, CancellationToken ct = default)
{
    var classrooms = await _context.Classrooms.AsNoTracking().ToListAsync(ct);

    if (minCapacity.HasValue)
        classrooms = classrooms.Where(c => c.Capacity >= minCapacity.Value).ToList();

    if (!string.IsNullOrEmpty(type) && Enum.TryParse<ClassroomType>(type, true, out var parsedType))
        classrooms = classrooms.Where(c => c.Type == parsedType).ToList();

    // If time slot specified, filter out occupied classrooms
    if (day.HasValue && startTime.HasValue && endTime.HasValue)
    {
        var occupiedClassroomIds = await GetOccupiedClassroomIdsAsync(termId, day.Value, startTime.Value, endTime.Value, ct);
        classrooms = classrooms.Where(c => !occupiedClassroomIds.Contains(c.Id)).ToList();
    }

    return classrooms
        .OrderBy(c => c.Capacity)
        .Select(c => new AvailableClassroomDto
        {
            ClassroomId = c.Id,
            Name = c.Name,
            Capacity = c.Capacity,
            Type = c.Type.ToString(),
            FitScore = minCapacity.HasValue
                ? Math.Max(0, 1m - Math.Abs(c.Capacity - minCapacity.Value) / (decimal)Math.Max(c.Capacity, 1))
                : 1m
        })
        .OrderByDescending(c => c.FitScore)
        .ToList();
}

private async Task<HashSet<int>> GetOccupiedClassroomIdsAsync(
    int termId, DayOfWeek day, TimeOnly startTime, TimeOnly endTime, CancellationToken ct)
{
    var sections = await _context.CourseSections
        .AsNoTracking()
        .Where(cs => cs.TermId == termId && cs.IsActive && cs.ClassroomId.HasValue && cs.ScheduleJson != "[]")
        .Select(cs => new { cs.ClassroomId, cs.ScheduleJson })
        .ToListAsync(ct);

    var occupied = new HashSet<int>();
    foreach (var s in sections)
    {
        try
        {
            var slots = JsonSerializer.Deserialize<List<ScheduleSlotSimple>>(s.ScheduleJson!, JsonOptions);
            if (slots?.Any(slot => slot.Day == day && slot.StartTime < endTime && slot.EndTime > startTime) == true)
                occupied.Add(s.ClassroomId!.Value);
        }
        catch { /* skip invalid JSON */ }
    }
    return occupied;
}
```

- [ ] **Step 6: Implement AutoSchedulePreviewAsync and ApplyScheduleAsync**

```csharp
public async Task<AutoSchedulePreviewDto> AutoSchedulePreviewAsync(AutoScheduleRequest request, CancellationToken ct = default)
{
    var result = new AutoSchedulePreviewDto();

    var departmentIds = await _context.Departments
        .AsNoTracking()
        .Where(d => d.FacultyId == request.FacultyId)
        .Select(d => d.Id)
        .ToListAsync(ct);

    // Get unscheduled sections
    var query = _context.CourseSections
        .AsNoTracking()
        .Include(cs => cs.Course).ThenInclude(c => c.Department)
        .Include(cs => cs.Instructor)
        .Where(cs => cs.TermId == request.TermId && cs.IsActive && !cs.IsFinalized)
        .Where(cs => cs.ScheduleJson == "[]" || cs.ClassroomId == null)
        .Where(cs => departmentIds.Contains(cs.Course.DepartmentId ?? 0));

    if (request.DepartmentId.HasValue)
        query = query.Where(cs => cs.Course.DepartmentId == request.DepartmentId.Value);

    if (request.SectionIds is { Count: > 0 })
        query = query.Where(cs => request.SectionIds.Contains(cs.Id));

    var sections = await query.ToListAsync(ct);

    // Determine compulsory courses
    var courseIds = sections.Select(s => s.CourseId).Distinct().ToList();
    var compulsoryCourseIds = await _context.CurriculumCourses
        .AsNoTracking()
        .Where(cc => courseIds.Contains(cc.CourseId) && cc.IsCompulsory)
        .Where(cc => cc.Curriculum.IsActive)
        .Select(cc => cc.CourseId)
        .Distinct()
        .ToListAsync(ct);

    // Priority sort: Lab first, then compulsory, then large capacity
    sections = sections
        .OrderByDescending(s => s.Course.LabHours > 0 ? 1 : 0)
        .ThenByDescending(s => compulsoryCourseIds.Contains(s.CourseId) ? 1 : 0)
        .ThenByDescending(s => s.Capacity)
        .ToList();

    var classrooms = await _context.Classrooms.AsNoTracking().ToListAsync(ct);
    var allScheduled = await _context.CourseSections
        .AsNoTracking()
        .Where(cs => cs.TermId == request.TermId && cs.IsActive && cs.ClassroomId.HasValue && cs.ScheduleJson != "[]")
        .Select(cs => new { cs.Id, cs.ClassroomId, cs.InstructorId, cs.ScheduleJson })
        .ToListAsync(ct);

    // Pre-load instructor names (batch, no N+1)
    var sectionInstructorIds = sections.Where(s => s.InstructorId.HasValue).Select(s => s.InstructorId!.Value).Distinct();
    var instructorNameMap = await LoadInstructorNameMapAsync(sectionInstructorIds, ct);

    // Track assignments made during this preview for conflict avoidance
    var previewAssignments = new List<(int classroomId, DayOfWeek day, TimeOnly start, TimeOnly end)>();

    foreach (var section in sections)
    {
        result.TotalProcessed++;

        var requiredType = section.Course.LabHours > 0 && section.Course.TheoryHours == 0
            ? ClassroomType.Lab : ClassroomType.Lecture;

        // Sort classrooms: type match first, then capacity fit
        var candidateClassrooms = classrooms
            .Where(c => c.Capacity >= section.Capacity)
            .OrderByDescending(c => c.Type == requiredType ? 1 : 0)
            .ThenBy(c => c.Capacity - section.Capacity)
            .ToList();

        if (candidateClassrooms.Count == 0)
        {
            result.Failures.Add(new AutoScheduleFailureDto
            {
                SectionId = section.Id,
                CourseCode = section.Course.Code,
                CourseName = section.Course.Name,
                Reason = $"Uygun kapasitede derslik bulunamadi (gerekli: {section.Capacity})."
            });
            result.FailedCount++;
            continue;
        }

        var totalHours = section.Course.TheoryHours + section.Course.LabHours;
        if (totalHours <= 0) totalHours = 3; // fallback

        var found = false;

        // Try each classroom with each time slot
        foreach (var classroom in candidateClassrooms)
        {
            for (var dayInt = 1; dayInt <= 5; dayInt++) // Mon-Fri
            {
                var day = (DayOfWeek)dayInt;
                for (var hour = 8; hour <= 17 - totalHours; hour++)
                {
                    var start = new TimeOnly(hour, 0);
                    var end = new TimeOnly(hour + totalHours, 0);

                    // Check against existing schedules
                    var conflict = allScheduled.Any(s =>
                    {
                        if (s.ClassroomId != classroom.Id) return false;
                        try
                        {
                            var slots = JsonSerializer.Deserialize<List<ScheduleSlotSimple>>(s.ScheduleJson!, JsonOptions);
                            return slots?.Any(slot => slot.Day == day && slot.StartTime < end && slot.EndTime > start) == true;
                        }
                        catch { return false; }
                    });

                    // Check against preview assignments
                    var previewConflict = previewAssignments.Any(p =>
                        p.classroomId == classroom.Id && p.day == day && p.start < end && p.end > start);

                    if (!conflict && !previewConflict)
                    {
                        var fitScore = (classroom.Type == requiredType ? 0.5m : 0m)
                            + Math.Max(0, 0.5m - Math.Abs(classroom.Capacity - section.Capacity) / (decimal)Math.Max(classroom.Capacity, 1));

                        result.Assignments.Add(new AutoSchedulePreviewItemDto
                        {
                            SectionId = section.Id,
                            CourseCode = section.Course.Code,
                            CourseName = section.Course.Name,
                            DepartmentCode = section.Course.Department?.Code ?? "",
                            ClassroomId = classroom.Id,
                            ClassroomName = classroom.Name,
                            ScheduleSlots = new List<ScheduleSlotDto>
                            {
                                new() { Day = day, StartTime = start, EndTime = end }
                            },
                            ScheduleSummary = $"{DayLabel(day)} {start:HH:mm}-{end:HH:mm}",
                            InstructorId = section.InstructorId,
                            InstructorName = section.InstructorId.HasValue
                                ? instructorNameMap.GetValueOrDefault(section.InstructorId.Value) : null,
                            FitScore = fitScore
                        });

                        previewAssignments.Add((classroom.Id, day, start, end));
                        result.SuccessCount++;
                        found = true;
                        break;
                    }
                }
                if (found) break;
            }
            if (found) break;
        }

        if (!found)
        {
            result.Failures.Add(new AutoScheduleFailureDto
            {
                SectionId = section.Id,
                CourseCode = section.Course.Code,
                CourseName = section.Course.Name,
                Reason = "Uygun zaman slotu bulunamadi (tum derslikler dolu)."
            });
            result.FailedCount++;
        }
    }

    return result;
}

private static string DayLabel(DayOfWeek day) => day switch
{
    DayOfWeek.Monday => "Pzt",
    DayOfWeek.Tuesday => "Sal",
    DayOfWeek.Wednesday => "Car",
    DayOfWeek.Thursday => "Per",
    DayOfWeek.Friday => "Cum",
    _ => day.ToString()
};

public async Task<ApplyScheduleResultDto> ApplyScheduleAsync(ApplyScheduleRequest request, string userId, CancellationToken ct = default)
{
    var result = new ApplyScheduleResultDto();

    foreach (var item in request.Assignments)
    {
        try
        {
            // AssignSectionAsync already checks DepartmentManager ownership per-item
            // Unauthorized items throw UnauthorizedAccessException → caught below → skipped with error
            var assignResult = await AssignSectionAsync(new AssignSectionRequest
            {
                SectionId = item.SectionId,
                ClassroomId = item.ClassroomId,
                ScheduleSlots = item.ScheduleSlots,
                InstructorId = item.InstructorId
            }, userId, ct);

            if (assignResult.Success) result.SuccessCount++;
            else
            {
                result.FailedCount++;
                result.Errors.Add($"Sube {item.SectionId}: Atama basarisiz.");
            }
        }
        catch (UnauthorizedAccessException)
        {
            result.FailedCount++;
            result.Errors.Add($"Sube {item.SectionId}: Yetkiniz bulunmuyor.");
        }
        catch (Exception ex)
        {
            result.FailedCount++;
            result.Errors.Add($"Sube {item.SectionId}: {ex.Message}");
        }
    }

    return result;
}
```

- [ ] **Step 7: Implement GetConflictsAsync**

```csharp
public async Task<List<SchedulingConflictDto>> GetConflictsAsync(int facultyId, int termId, CancellationToken ct = default)
{
    var departmentIds = await _context.Departments
        .AsNoTracking()
        .Where(d => d.FacultyId == facultyId)
        .Select(d => d.Id)
        .ToListAsync(ct);

    // Get ALL conflicts for the term (no department filter — cross-department visibility)
    var allConflicts = await _schedulingService.GetConflictsAsync(termId, ct);

    // Filter to faculty scope
    var facultySectionIds = (await _context.CourseSections
        .AsNoTracking()
        .Where(cs => cs.TermId == termId && cs.IsActive && departmentIds.Contains(cs.Course.DepartmentId ?? 0))
        .Select(cs => cs.Id)
        .ToListAsync(ct)).ToHashSet();

    return allConflicts
        .Where(c => facultySectionIds.Contains(c.SectionId) || (c.RelatedSectionId.HasValue && facultySectionIds.Contains(c.RelatedSectionId.Value)))
        .ToList();
}
```

- [ ] **Step 8: Commit**

```bash
git add SIS.Infrastructure/Services/FacultyPlanningService.cs
git commit -m "feat: implement FacultyPlanningService with all methods"
```

---

## Task 6: DI Registration

**Files:**
- Modify: `SIS.API/Program.cs` or DI configuration file

- [ ] **Step 1: Find and update DI registration**

Search for where services are registered (e.g., `builder.Services.AddScoped<ISchedulingService, SchedulingService>()`). Add:

```csharp
builder.Services.AddScoped<IFacultyPlanningService, FacultyPlanningService>();
```

Note: `FacultyPlanningService` depends on `ISchedulingService` (interface), which is already registered. No additional registration needed.

- [ ] **Step 2: Commit**

```bash
git add SIS.API/Program.cs
git commit -m "feat: register FacultyPlanningService in DI container"
```

---

## Task 7: API Controller

**Files:**
- Create: `SIS.API/Controllers/FacultyPlanningController.cs`

- [ ] **Step 1: Create FacultyPlanningController**

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.FacultyPlanning.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/faculty-planning")]
[Authorize(Roles = "DepartmentManager,AdministrativeStaff,SystemAdministrator")]
public class FacultyPlanningController : ControllerBase
{
    private readonly IFacultyPlanningService _service;

    public FacultyPlanningController(IFacultyPlanningService service)
    {
        _service = service;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException();

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] int facultyId, [FromQuery] int termId, CancellationToken ct)
    {
        var result = await _service.GetOverviewAsync(facultyId, termId, ct);
        return Ok(result);
    }

    [HttpGet("sections")]
    public async Task<IActionResult> GetSections(
        [FromQuery] int facultyId, [FromQuery] int termId,
        [FromQuery] int? departmentId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        var result = await _service.GetSectionsAsync(facultyId, termId, departmentId, status, page, pageSize, ct);
        return Ok(result);
    }

    [HttpPost("assign")]
    public async Task<IActionResult> AssignSection([FromBody] AssignSectionRequest request, CancellationToken ct)
    {
        var result = await _service.AssignSectionAsync(request, GetUserId(), ct);
        return Ok(result);
    }

    [HttpDelete("unassign/{sectionId}")]
    public async Task<IActionResult> UnassignSection(int sectionId, CancellationToken ct)
    {
        await _service.UnassignSectionAsync(sectionId, GetUserId(), ct);
        return NoContent();
    }

    [HttpPost("auto-schedule")]
    public async Task<IActionResult> AutoSchedule([FromBody] AutoScheduleRequest request, CancellationToken ct)
    {
        var result = await _service.AutoSchedulePreviewAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("apply-schedule")]
    public async Task<IActionResult> ApplySchedule([FromBody] ApplyScheduleRequest request, CancellationToken ct)
    {
        var result = await _service.ApplyScheduleAsync(request, GetUserId(), ct);
        return Ok(result);
    }

    [HttpGet("available-classrooms")]
    public async Task<IActionResult> GetAvailableClassrooms(
        [FromQuery] int termId, [FromQuery] DayOfWeek? day,
        [FromQuery] TimeOnly? startTime, [FromQuery] TimeOnly? endTime,
        [FromQuery] int? minCapacity, [FromQuery] string? type,
        CancellationToken ct)
    {
        var result = await _service.GetAvailableClassroomsAsync(termId, day, startTime, endTime, minCapacity, type, ct);
        return Ok(result);
    }

    [HttpGet("conflicts")]
    public async Task<IActionResult> GetConflicts([FromQuery] int facultyId, [FromQuery] int termId, CancellationToken ct)
    {
        var result = await _service.GetConflictsAsync(facultyId, termId, ct);
        return Ok(result);
    }
}
```

- [ ] **Step 2: Build and verify**

Run:
```bash
cd "c:/Users/Sertuğ/Desktop/Student Information System"
dotnet build
```
Expected: Build succeeds with no errors.

- [ ] **Step 3: Commit**

```bash
git add SIS.API/Controllers/FacultyPlanningController.cs
git commit -m "feat: add FacultyPlanningController with 8 endpoints"
```

---

## Task 8: Unit Tests

**Files:**
- Create: `SIS.Tests/Application/FacultyPlanningServiceTests.cs`

- [ ] **Step 1: Write core tests**

Create tests covering:
1. `ResolveFacultyForUserAsync` — returns correct faculty ID
2. `GetOverviewAsync` — returns correct counts
3. `GetSectionsAsync` — status filtering works
4. `AssignSectionAsync` — assigns classroom and creates audit log
5. `AssignSectionAsync` — rejects finalized sections
6. `AssignSectionAsync` — DepartmentManager rejected for wrong department's section
7. `UnassignSectionAsync` — clears assignment
8. `UnassignSectionAsync` — DepartmentManager rejected for wrong department's section
9. `GetAvailableClassroomsAsync` — filters by capacity and type
10. `AutoSchedulePreviewAsync` — prioritizes lab sections first
11. `ApplyScheduleAsync` — partial success (some items unauthorized, reports per-item errors)
12. `GetConflictsAsync` — filters to faculty scope only

Use the same in-memory test patterns as existing tests in the project (check `SIS.Tests/Application/SchedulingServiceTests.cs` for reference).

- [ ] **Step 2: Run tests**

```bash
dotnet test SIS.Tests/
```
Expected: All tests pass.

- [ ] **Step 3: Commit**

```bash
git add SIS.Tests/Application/FacultyPlanningServiceTests.cs
git commit -m "test: add FacultyPlanningService unit tests"
```

---

## Task 9: Frontend — Types + API + Hooks

**Files:**
- Create: `SIS.Web/src/types/facultyPlanning.ts`
- Create: `SIS.Web/src/api/facultyPlanning.api.ts`
- Create: `SIS.Web/src/hooks/useFacultyPlanning.ts`
- Modify: `SIS.Web/src/lib/constants.ts`

- [ ] **Step 1: Create types**

```typescript
// SIS.Web/src/types/facultyPlanning.ts
import type { ScheduleSlot, SchedulingConflict } from './scheduling';

export interface FacultyPlanningOverview {
  facultyId: number;
  facultyName: string;
  termId: number;
  termName: string;
  totalSections: number;
  scheduledSections: number;
  unscheduledSections: number;
  conflictCount: number;
  conflictCountByType: Record<string, number>;
  departmentProgress: DepartmentProgress[];
  pendingApprovalWarnings: PendingApprovalWarning[];
  recentAssignments: RecentAssignment[];
}

export interface DepartmentProgress {
  departmentId: number;
  departmentName: string;
  departmentCode: string;
  totalSections: number;
  scheduledSections: number;
  pendingApprovals: number;
}

export interface PendingApprovalWarning {
  departmentId: number;
  departmentName: string;
  pendingCount: number;
}

export interface RecentAssignment {
  sectionId: number;
  courseCode: string;
  courseName: string;
  classroomName: string | null;
  scheduleSummary: string | null;
  assignedByUserName: string;
  action: string;
  assignedAt: string;
}

export type SectionStatus = 'Unscheduled' | 'Scheduled' | 'Conflicted' | 'Partial';

export interface FacultyPlanningSection {
  sectionId: number;
  courseId: number;
  courseCode: string;
  courseName: string;
  departmentId: number;
  departmentCode: string;
  capacity: number;
  classroomId: number | null;
  classroomName: string | null;
  classroomType: string | null;
  instructorId: number | null;
  instructorName: string | null;
  scheduleJson: string;
  scheduleSummary: string;
  status: SectionStatus;
  theoryHours: number;
  labHours: number;
  sectionIndex: number;
}

export interface FacultyPlanningSectionPage {
  items: FacultyPlanningSection[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AssignSectionRequest {
  sectionId: number;
  classroomId?: number;
  scheduleSlots?: ScheduleSlot[];
  instructorId?: number;
}

export interface AssignmentResult {
  success: boolean;
  warnings: string[];
  conflicts: SchedulingConflict[];
}

export interface AutoScheduleRequest {
  facultyId: number;
  termId: number;
  departmentId?: number;
  sectionIds?: number[];
}

export interface AutoSchedulePreview {
  totalProcessed: number;
  successCount: number;
  failedCount: number;
  assignments: AutoSchedulePreviewItem[];
  failures: AutoScheduleFailure[];
}

export interface AutoSchedulePreviewItem {
  sectionId: number;
  courseCode: string;
  courseName: string;
  departmentCode: string;
  classroomId: number;
  classroomName: string;
  scheduleSlots: ScheduleSlot[];
  scheduleSummary: string;
  instructorId: number | null;
  instructorName: string | null;
  fitScore: number;
}

export interface AutoScheduleFailure {
  sectionId: number;
  courseCode: string;
  courseName: string;
  reason: string;
}

export interface ApplyScheduleRequest {
  assignments: ApplyScheduleItem[];
}

export interface ApplyScheduleItem {
  sectionId: number;
  classroomId: number;
  scheduleSlots: ScheduleSlot[];
  instructorId?: number;
}

export interface ApplyScheduleResult {
  successCount: number;
  failedCount: number;
  errors: string[];
}

export interface AvailableClassroom {
  classroomId: number;
  name: string;
  capacity: number;
  type: string;
  fitScore: number;
}
```

- [ ] **Step 2: Create API functions**

```typescript
// SIS.Web/src/api/facultyPlanning.api.ts
import apiClient from './axios';
import type {
  ApplyScheduleRequest, ApplyScheduleResult, AssignmentResult, AssignSectionRequest,
  AutoSchedulePreview, AutoScheduleRequest, AvailableClassroom,
  FacultyPlanningOverview, FacultyPlanningSectionPage,
} from '../types/facultyPlanning';
import type { SchedulingConflict } from '../types/scheduling';

export async function getFacultyPlanningOverview(facultyId: number, termId: number): Promise<FacultyPlanningOverview> {
  const { data } = await apiClient.get<FacultyPlanningOverview>('/faculty-planning/overview', {
    params: { facultyId, termId },
  });
  return data;
}

export async function getFacultyPlanningSections(
  facultyId: number, termId: number, departmentId?: number, status?: string, page = 1, pageSize = 50,
): Promise<FacultyPlanningSectionPage> {
  const { data } = await apiClient.get<FacultyPlanningSectionPage>('/faculty-planning/sections', {
    params: { facultyId, termId, departmentId, status, page, pageSize },
  });
  return data;
}

export async function assignSection(request: AssignSectionRequest): Promise<AssignmentResult> {
  const { data } = await apiClient.post<AssignmentResult>('/faculty-planning/assign', request);
  return data;
}

export async function unassignSection(sectionId: number): Promise<void> {
  await apiClient.delete(`/faculty-planning/unassign/${sectionId}`);
}

export async function autoSchedulePreview(request: AutoScheduleRequest): Promise<AutoSchedulePreview> {
  const { data } = await apiClient.post<AutoSchedulePreview>('/faculty-planning/auto-schedule', request);
  return data;
}

export async function applySchedule(request: ApplyScheduleRequest): Promise<ApplyScheduleResult> {
  const { data } = await apiClient.post<ApplyScheduleResult>('/faculty-planning/apply-schedule', request);
  return data;
}

export async function getAvailableClassrooms(
  termId: number, day?: number, startTime?: string, endTime?: string, minCapacity?: number, type?: string,
): Promise<AvailableClassroom[]> {
  const { data } = await apiClient.get<AvailableClassroom[]>('/faculty-planning/available-classrooms', {
    params: { termId, day, startTime, endTime, minCapacity, type },
  });
  return data;
}

export async function getFacultyPlanningConflicts(facultyId: number, termId: number): Promise<SchedulingConflict[]> {
  const { data } = await apiClient.get<SchedulingConflict[]>('/faculty-planning/conflicts', {
    params: { facultyId, termId },
  });
  return data;
}
```

- [ ] **Step 3: Create hooks**

```typescript
// SIS.Web/src/hooks/useFacultyPlanning.ts
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  applySchedule, assignSection, autoSchedulePreview, getAvailableClassrooms,
  getFacultyPlanningConflicts, getFacultyPlanningOverview, getFacultyPlanningSections, unassignSection,
} from '../api/facultyPlanning.api';
import type {
  ApplyScheduleRequest, AssignSectionRequest, AutoScheduleRequest,
} from '../types/facultyPlanning';
import { QUERY_KEYS } from '../lib/constants';

export function useFacultyPlanningOverview(facultyId: number | null, termId: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.facultyPlanningOverview(facultyId ?? 0, termId ?? 0),
    queryFn: () => getFacultyPlanningOverview(facultyId!, termId!),
    enabled: facultyId != null && termId != null,
    staleTime: 30 * 1000,
  });
}

export function useFacultyPlanningSections(
  facultyId: number | null, termId: number | null,
  departmentId?: number, status?: string, page = 1, pageSize = 50,
) {
  return useQuery({
    queryKey: QUERY_KEYS.facultyPlanningSections(facultyId ?? 0, termId ?? 0, departmentId, status, page),
    queryFn: () => getFacultyPlanningSections(facultyId!, termId!, departmentId, status, page, pageSize),
    enabled: facultyId != null && termId != null,
    staleTime: 30 * 1000,
  });
}

export function useAssignSection(facultyId: number, termId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: AssignSectionRequest) => assignSection(req),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['facultyPlanning'] });
    },
  });
}

export function useUnassignSection(facultyId: number, termId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (sectionId: number) => unassignSection(sectionId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['facultyPlanning'] });
    },
  });
}

export function useAutoSchedulePreview() {
  return useMutation({
    mutationFn: (req: AutoScheduleRequest) => autoSchedulePreview(req),
  });
}

export function useApplySchedule(facultyId: number, termId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: ApplyScheduleRequest) => applySchedule(req),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['facultyPlanning'] });
    },
  });
}

export function useAvailableClassrooms(
  termId: number | null, day?: number, startTime?: string, endTime?: string,
  minCapacity?: number, type?: string,
) {
  return useQuery({
    queryKey: ['availableClassrooms', termId, day, startTime, endTime, minCapacity, type],
    queryFn: () => getAvailableClassrooms(termId!, day, startTime, endTime, minCapacity, type),
    enabled: termId != null,
    staleTime: 15 * 1000,
  });
}

export function useFacultyPlanningConflicts(facultyId: number | null, termId: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.facultyPlanningConflicts(facultyId ?? 0, termId ?? 0),
    queryFn: () => getFacultyPlanningConflicts(facultyId!, termId!),
    enabled: facultyId != null && termId != null,
    staleTime: 30 * 1000,
  });
}
```

- [ ] **Step 4: Update constants.ts**

Add to `ROUTES`:
```typescript
FACULTY_PLANNING: '/fakulte-planlama',
```

Remove: `MANAGEMENT_BATCH_SCHEDULING`

Add to `QUERY_KEYS`:
```typescript
facultyPlanningOverview: (facultyId: number, termId: number) => ['facultyPlanning', 'overview', facultyId, termId] as const,
facultyPlanningSections: (facultyId: number, termId: number, departmentId?: number, status?: string, page?: number) =>
  ['facultyPlanning', 'sections', facultyId, termId, departmentId, status, page] as const,
facultyPlanningConflicts: (facultyId: number, termId: number) => ['facultyPlanning', 'conflicts', facultyId, termId] as const,
```

Update `IMPLEMENTED_ROUTES`: add `/fakulte-planlama`, remove `/toplu-programlama`.

- [ ] **Step 5: Commit**

```bash
git add SIS.Web/src/types/facultyPlanning.ts SIS.Web/src/api/facultyPlanning.api.ts SIS.Web/src/hooks/useFacultyPlanning.ts SIS.Web/src/lib/constants.ts
git commit -m "feat: add Faculty Planning frontend types, API, hooks, and constants"
```

---

## Task 10: Frontend — FacultyPlanningPage (4 Tabs)

**Files:**
- Create: `SIS.Web/src/pages/FacultyPlanningPage.tsx`

This is the largest frontend task. The page has 4 tabs built as internal components (following the AcademicOperationsPage pattern). Split into 5 steps.

- [ ] **Step 1: Create FacultyPlanningPage main shell with tab navigation**

```tsx
// SIS.Web/src/pages/FacultyPlanningPage.tsx
import { useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  AlertCircle, AlertTriangle, Building2, Calendar, Check, CheckCircle2,
  ChevronDown, Minus, Plus, RefreshCcw, Search, X, Zap,
} from 'lucide-react';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import {
  useApplySchedule, useAssignSection, useAutoSchedulePreview,
  useFacultyPlanningConflicts, useFacultyPlanningOverview,
  useFacultyPlanningSections, useUnassignSection,
} from '../hooks/useFacultyPlanning';
import type {
  AutoSchedulePreviewItem, FacultyPlanningSection,
} from '../types/facultyPlanning';
import type { SchedulingConflict } from '../types/scheduling';

type TabKey = 'overview' | 'sections' | 'auto-schedule' | 'conflicts';

const TABS: { key: TabKey; label: string }[] = [
  { key: 'overview', label: 'Genel Bakış' },
  { key: 'sections', label: 'Şube Listesi' },
  { key: 'auto-schedule', label: 'Otomatik Zamanlama' },
  { key: 'conflicts', label: 'Çakışma Analizi' },
];

export default function FacultyPlanningPage() {
  const [activeTab, setActiveTab] = useState<TabKey>('overview');
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();

  // Resolve facultyId from user's department context
  // For now, derive from first accessible department; backend resolves faculty from department
  const departments = contextQuery.data?.accessibleDepartments ?? [];
  const defaultDeptId = contextQuery.data?.defaultDepartmentId ?? null;

  // Faculty ID will come from a faculty-level API or be derived
  // Placeholder: use 1 until faculty selector is built from /api/faculty-planning/overview
  const [facultyId] = useState<number>(1); // TODO: resolve from context
  const termId = activeTerm?.id ?? null;

  if (contextQuery.isLoading) return <Skeleton className="h-96 rounded-xl" />;

  if (contextQuery.isError) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Fakülte planlama sayfası yüklenemedi</AlertTitle>
        <AlertDescription>Yetkili kapsam bilgileri alınamadı.</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap gap-2">
        {TABS.map(tab => (
          <Button
            key={tab.key}
            variant={activeTab === tab.key ? 'default' : 'outline'}
            size="sm"
            onClick={() => setActiveTab(tab.key)}
          >
            {tab.label}
          </Button>
        ))}
      </div>

      {activeTab === 'overview' && <OverviewTab facultyId={facultyId} termId={termId} />}
      {activeTab === 'sections' && (
        <SectionListTab facultyId={facultyId} termId={termId} departments={departments} />
      )}
      {activeTab === 'auto-schedule' && <AutoScheduleTab facultyId={facultyId} termId={termId} />}
      {activeTab === 'conflicts' && <ConflictAnalysisTab facultyId={facultyId} termId={termId} />}
    </div>
  );
}
```

- [ ] **Step 2: Build OverviewTab**

```tsx
function OverviewTab({ facultyId, termId }: { facultyId: number; termId: number | null }) {
  const overviewQuery = useFacultyPlanningOverview(facultyId, termId);

  if (!termId || overviewQuery.isLoading) return <Skeleton className="h-64 rounded-xl" />;
  if (overviewQuery.isError) return <Alert variant="destructive"><AlertTitle>Genel bakış yüklenemedi</AlertTitle></Alert>;

  const ov = overviewQuery.data;
  if (!ov) return null;

  const progressPercent = ov.totalSections > 0
    ? Math.round((ov.scheduledSections / ov.totalSections) * 100) : 0;

  return (
    <div className="space-y-6">
      {/* Progress bar */}
      <Card>
        <CardHeader>
          <CardTitle>{ov.facultyName} — {ov.termName}</CardTitle>
          <CardDescription>{ov.scheduledSections}/{ov.totalSections} şube zamanlandı (%{progressPercent})</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="h-3 bg-muted rounded-full overflow-hidden">
            <div className="h-full bg-primary rounded-full transition-all" style={{ width: `${progressPercent}%` }} />
          </div>
        </CardContent>
      </Card>

      {/* Pending approval warnings */}
      {ov.pendingApprovalWarnings.length > 0 && (
        <Alert>
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Onay Bekleyen Bölümler</AlertTitle>
          <AlertDescription className="space-y-1">
            {ov.pendingApprovalWarnings.map(w => (
              <p key={w.departmentId}>{w.departmentName}: <strong>{w.pendingCount}</strong> ders onay bekliyor</p>
            ))}
          </AlertDescription>
        </Alert>
      )}

      {/* Department progress cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
        {ov.departmentProgress.map(dept => (
          <Card key={dept.departmentId}>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm">{dept.departmentCode} — {dept.departmentName}</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="flex justify-between text-sm">
                <span>Zamanlanmış: <strong>{dept.scheduledSections}/{dept.totalSections}</strong></span>
                {dept.pendingApprovals > 0 && (
                  <Badge variant="warning">{dept.pendingApprovals} onay bekliyor</Badge>
                )}
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Conflict summary */}
      {ov.conflictCount > 0 && (
        <Card>
          <CardHeader><CardTitle className="text-sm">Çakışma Özeti</CardTitle></CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {Object.entries(ov.conflictCountByType).map(([type, count]) => (
              <Badge key={type} variant="destructive">{type}: {count}</Badge>
            ))}
          </CardContent>
        </Card>
      )}

      {/* Recent assignments */}
      {ov.recentAssignments.length > 0 && (
        <Card>
          <CardHeader><CardTitle className="text-sm">Son İşlemler</CardTitle></CardHeader>
          <CardContent>
            <div className="space-y-2 text-sm">
              {ov.recentAssignments.map((log, i) => (
                <div key={i} className="flex justify-between text-muted-foreground">
                  <span>
                    <strong>{log.assignedByUserName}</strong> — {log.courseCode} →{' '}
                    {log.classroomName ?? 'Derslik yok'} {log.scheduleSummary ?? ''}
                  </span>
                  <span>{new Date(log.assignedAt).toLocaleString('tr-TR')}</span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
```

- [ ] **Step 3: Build SectionListTab with assignment form**

```tsx
const STATUS_CONFIG: Record<string, { label: string; variant: 'default' | 'success' | 'destructive' | 'warning' | 'outline' }> = {
  Unscheduled: { label: 'Zamanlanmamış', variant: 'outline' },
  Scheduled: { label: 'Zamanlanmış', variant: 'success' },
  Conflicted: { label: 'Çakışmalı', variant: 'destructive' },
  Partial: { label: 'Kısmi', variant: 'warning' },
};

function SectionListTab({
  facultyId, termId, departments,
}: {
  facultyId: number;
  termId: number | null;
  departments: { departmentId: number; departmentCode: string; departmentName: string }[];
}) {
  const [filterDept, setFilterDept] = useState<number | undefined>();
  const [filterStatus, setFilterStatus] = useState<string | undefined>();
  const [page, setPage] = useState(1);
  const [expandedId, setExpandedId] = useState<number | null>(null);

  const sectionsQuery = useFacultyPlanningSections(facultyId, termId, filterDept, filterStatus, page);
  const assignMutation = useAssignSection(facultyId, termId ?? 0);
  const unassignMutation = useUnassignSection(facultyId, termId ?? 0);

  if (!termId) return <Card><CardContent className="py-8 text-center text-muted-foreground">Aktif dönem bulunamadı.</CardContent></Card>;

  return (
    <div className="space-y-4">
      {/* Filters */}
      <div className="flex flex-wrap gap-3">
        <select
          className="h-9 rounded-md border border-input bg-background px-3 text-sm"
          value={filterDept ?? ''}
          onChange={e => { setFilterDept(e.target.value ? Number(e.target.value) : undefined); setPage(1); }}
        >
          <option value="">Tüm Bölümler</option>
          {departments.map(d => <option key={d.departmentId} value={d.departmentId}>{d.departmentCode}</option>)}
        </select>
        <select
          className="h-9 rounded-md border border-input bg-background px-3 text-sm"
          value={filterStatus ?? ''}
          onChange={e => { setFilterStatus(e.target.value || undefined); setPage(1); }}
        >
          <option value="">Tüm Durumlar</option>
          {Object.entries(STATUS_CONFIG).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
        </select>
      </div>

      {/* Table */}
      {sectionsQuery.isLoading ? <Skeleton className="h-64" /> : (
        <Card>
          <CardContent className="pt-4">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Ders</TableHead>
                  <TableHead>Bölüm</TableHead>
                  <TableHead>Kapasite</TableHead>
                  <TableHead>Derslik</TableHead>
                  <TableHead>Saat</TableHead>
                  <TableHead>Hoca</TableHead>
                  <TableHead>Durum</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(sectionsQuery.data?.items ?? []).map(s => (
                  <SectionRow
                    key={s.sectionId}
                    section={s}
                    expanded={expandedId === s.sectionId}
                    onToggle={() => setExpandedId(prev => prev === s.sectionId ? null : s.sectionId)}
                    onAssign={(classroomId, slots, instructorId) =>
                      assignMutation.mutate({ sectionId: s.sectionId, classroomId, scheduleSlots: slots, instructorId })
                    }
                    onUnassign={() => unassignMutation.mutate(s.sectionId)}
                    isAssigning={assignMutation.isPending}
                  />
                ))}
              </TableBody>
            </Table>

            {/* Pagination */}
            {sectionsQuery.data && sectionsQuery.data.totalCount > 50 && (
              <div className="flex justify-center gap-2 mt-4">
                <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Önceki</Button>
                <span className="text-sm py-1">Sayfa {page} / {Math.ceil(sectionsQuery.data.totalCount / 50)}</span>
                <Button size="sm" variant="outline" disabled={page * 50 >= sectionsQuery.data.totalCount} onClick={() => setPage(p => p + 1)}>Sonraki</Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function SectionRow({ section: s, expanded, onToggle, onAssign, onUnassign, isAssigning }: {
  section: FacultyPlanningSection;
  expanded: boolean;
  onToggle: () => void;
  onAssign: (classroomId?: number, slots?: any[], instructorId?: number) => void;
  onUnassign: () => void;
  isAssigning: boolean;
}) {
  const [classroomId, setClassroomId] = useState<string>(s.classroomId?.toString() ?? '');
  const [day, setDay] = useState('1');
  const [startHour, setStartHour] = useState('9');
  const totalHours = s.theoryHours + s.labHours || 3;

  const statusCfg = STATUS_CONFIG[s.status] ?? STATUS_CONFIG.Unscheduled;

  return (
    <>
      <TableRow className="cursor-pointer hover:bg-muted/30" onClick={onToggle}>
        <TableCell>
          <div>
            <span className="font-mono text-xs font-semibold text-primary">{s.courseCode}</span>
            <span className="text-sm ml-2">{s.courseName}</span>
            <span className="text-xs text-muted-foreground ml-1">Şube {s.sectionIndex}</span>
          </div>
        </TableCell>
        <TableCell>{s.departmentCode}</TableCell>
        <TableCell>{s.capacity}</TableCell>
        <TableCell>{s.classroomName ?? '—'}</TableCell>
        <TableCell>{s.scheduleSummary || '—'}</TableCell>
        <TableCell>{s.instructorName ?? '—'}</TableCell>
        <TableCell><Badge variant={statusCfg.variant}>{statusCfg.label}</Badge></TableCell>
      </TableRow>
      {expanded && (
        <TableRow className="bg-muted/20">
          <TableCell colSpan={7} className="p-4">
            <div className="flex flex-wrap items-end gap-4">
              <div className="space-y-1">
                <label className="text-xs font-medium">Derslik ID</label>
                <Input className="h-8 w-24" value={classroomId} onChange={e => setClassroomId(e.target.value)} placeholder="Derslik ID" />
              </div>
              <div className="space-y-1">
                <label className="text-xs font-medium">Gün</label>
                <select className="h-8 rounded-md border px-2 text-sm" value={day} onChange={e => setDay(e.target.value)}>
                  <option value="1">Pzt</option><option value="2">Sal</option><option value="3">Çar</option>
                  <option value="4">Per</option><option value="5">Cum</option>
                </select>
              </div>
              <div className="space-y-1">
                <label className="text-xs font-medium">Saat</label>
                <select className="h-8 rounded-md border px-2 text-sm" value={startHour} onChange={e => setStartHour(e.target.value)}>
                  {Array.from({ length: 10 }, (_, i) => i + 8).map(h => (
                    <option key={h} value={h}>{`${h}:00-${h + totalHours}:00`}</option>
                  ))}
                </select>
              </div>
              <div className="flex gap-2">
                <Button size="sm" disabled={isAssigning} onClick={() => {
                  const slots = [{ day: Number(day), startTime: `${startHour.padStart(2, '0')}:00`, endTime: `${(Number(startHour) + totalHours).toString().padStart(2, '0')}:00` }];
                  onAssign(classroomId ? Number(classroomId) : undefined, slots, undefined);
                }}>
                  <Check className="h-4 w-4 mr-1" /> Ata
                </Button>
                {s.status !== 'Unscheduled' && (
                  <Button size="sm" variant="destructive" onClick={onUnassign}>
                    <X className="h-4 w-4 mr-1" /> Kaldır
                  </Button>
                )}
              </div>
            </div>
          </TableCell>
        </TableRow>
      )}
    </>
  );
}
```

- [ ] **Step 4: Build AutoScheduleTab and ConflictAnalysisTab**

```tsx
function AutoScheduleTab({ facultyId, termId }: { facultyId: number; termId: number | null }) {
  const previewMutation = useAutoSchedulePreview();
  const applyMutation = useApplySchedule(facultyId, termId ?? 0);
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());

  if (!termId) return null;

  const handlePreview = () => {
    previewMutation.mutate({ facultyId, termId });
  };

  const handleApplySelected = () => {
    const items = (previewMutation.data?.assignments ?? [])
      .filter(a => selectedIds.has(a.sectionId))
      .map(a => ({ sectionId: a.sectionId, classroomId: a.classroomId, scheduleSlots: a.scheduleSlots, instructorId: a.instructorId ?? undefined }));
    if (items.length > 0) applyMutation.mutate({ assignments: items });
  };

  const handleApplyAll = () => {
    const items = (previewMutation.data?.assignments ?? [])
      .map(a => ({ sectionId: a.sectionId, classroomId: a.classroomId, scheduleSlots: a.scheduleSlots, instructorId: a.instructorId ?? undefined }));
    if (items.length > 0) applyMutation.mutate({ assignments: items });
  };

  const toggleSelect = (id: number) => setSelectedIds(prev => {
    const next = new Set(prev);
    next.has(id) ? next.delete(id) : next.add(id);
    return next;
  });

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <div>
            <CardTitle>Otomatik Zamanlama</CardTitle>
            <CardDescription>Zamanlanmamış şubeler için derslik ve saat önerisi üretir.</CardDescription>
          </div>
          <Button onClick={handlePreview} disabled={previewMutation.isPending}>
            <Zap className="h-4 w-4 mr-1" />
            {previewMutation.isPending ? 'Hesaplanıyor...' : 'Otomatik Ata'}
          </Button>
        </CardHeader>
      </Card>

      {previewMutation.data && (
        <>
          <div className="flex gap-2">
            <Badge variant="success">{previewMutation.data.successCount} başarılı</Badge>
            <Badge variant="destructive">{previewMutation.data.failedCount} başarısız</Badge>
          </div>

          {previewMutation.data.assignments.length > 0 && (
            <Card>
              <CardHeader className="flex flex-row items-center justify-between">
                <CardTitle className="text-sm">Önizleme</CardTitle>
                <div className="flex gap-2">
                  <Button size="sm" onClick={handleApplySelected} disabled={selectedIds.size === 0 || applyMutation.isPending}>
                    Seçilenleri Uygula ({selectedIds.size})
                  </Button>
                  <Button size="sm" onClick={handleApplyAll} disabled={applyMutation.isPending}>
                    Tümünü Uygula
                  </Button>
                </div>
              </CardHeader>
              <CardContent>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-8" />
                      <TableHead>Ders</TableHead>
                      <TableHead>Bölüm</TableHead>
                      <TableHead>Derslik</TableHead>
                      <TableHead>Saat</TableHead>
                      <TableHead>Uyum</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {previewMutation.data.assignments.map(a => (
                      <TableRow key={a.sectionId}>
                        <TableCell>
                          <input type="checkbox" checked={selectedIds.has(a.sectionId)} onChange={() => toggleSelect(a.sectionId)} />
                        </TableCell>
                        <TableCell><span className="font-mono text-xs">{a.courseCode}</span> {a.courseName}</TableCell>
                        <TableCell>{a.departmentCode}</TableCell>
                        <TableCell>{a.classroomName}</TableCell>
                        <TableCell>{a.scheduleSummary}</TableCell>
                        <TableCell>%{(a.fitScore * 100).toFixed(0)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}

          {previewMutation.data.failures.length > 0 && (
            <Card>
              <CardHeader><CardTitle className="text-sm text-destructive">Atanamayan Şubeler</CardTitle></CardHeader>
              <CardContent>
                {previewMutation.data.failures.map(f => (
                  <div key={f.sectionId} className="text-sm py-1">
                    <span className="font-mono">{f.courseCode}</span> — {f.reason}
                  </div>
                ))}
              </CardContent>
            </Card>
          )}

          {applyMutation.data && (
            <Alert variant={applyMutation.data.failedCount > 0 ? 'destructive' : 'default'}>
              <AlertTitle>Uygulama Sonucu</AlertTitle>
              <AlertDescription>
                {applyMutation.data.successCount} başarılı, {applyMutation.data.failedCount} başarısız
                {applyMutation.data.errors.map((e, i) => <p key={i} className="text-xs">{e}</p>)}
              </AlertDescription>
            </Alert>
          )}
        </>
      )}
    </div>
  );
}

function ConflictAnalysisTab({ facultyId, termId }: { facultyId: number; termId: number | null }) {
  const conflictsQuery = useFacultyPlanningConflicts(facultyId, termId);

  if (!termId || conflictsQuery.isLoading) return <Skeleton className="h-64" />;

  const conflicts = conflictsQuery.data ?? [];

  if (conflicts.length === 0) {
    return (
      <Card>
        <CardContent className="py-16 text-center text-muted-foreground">
          <CheckCircle2 className="h-8 w-8 mx-auto mb-2 text-green-500" />
          Çakışma bulunamadı
        </CardContent>
      </Card>
    );
  }

  const grouped = conflicts.reduce<Record<string, SchedulingConflict[]>>((acc, c) => {
    (acc[c.conflictType] ??= []).push(c);
    return acc;
  }, {});

  return (
    <div className="space-y-4">
      {Object.entries(grouped).map(([type, items]) => (
        <Card key={type}>
          <CardHeader>
            <CardTitle className="text-sm flex items-center gap-2">
              <Badge variant="destructive">{items.length}</Badge>
              {type}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {items.map((c, i) => (
              <div key={i} className="text-sm p-3 bg-muted/30 rounded-md">
                <p className="font-medium">{c.courseCode} — {c.courseName}</p>
                <p className="text-muted-foreground">{c.message}</p>
                {c.classroomName && <p className="text-xs">Derslik: {c.classroomName}</p>}
                {c.instructorName && <p className="text-xs">Hoca: {c.instructorName}</p>}
              </div>
            ))}
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
```

- [ ] **Step 5: Commit**

```bash
git add SIS.Web/src/pages/FacultyPlanningPage.tsx
git commit -m "feat: add FacultyPlanningPage with 4 tabs"
```

---

## Task 11: Routing + Sidebar + Cleanup

**Files:**
- Modify: `SIS.Web/src/App.tsx`
- Modify: `SIS.Web/src/components/layout/SidebarMenuGroups.ts`
- Delete: `SIS.Web/src/pages/SchedulingDashboardPage.tsx`

- [ ] **Step 1: Update App.tsx**

Add FacultyPlanningPage import and route:
```tsx
import FacultyPlanningPage from './pages/FacultyPlanningPage';
// In MANAGEMENT_ROLES routes:
<Route path="/fakulte-planlama" element={<FacultyPlanningPage />} />
```

Remove SchedulingDashboardPage import and `/toplu-programlama` route.

- [ ] **Step 2: Update SidebarMenuGroups.ts**

In "Bolum Islemleri" group:
- Add: `{ to: '/fakulte-planlama', label: 'Fakulte Planlama' }`
- Remove: the "Toplu Programlama" entry (path `/toplu-programlama`)

- [ ] **Step 3: Delete SchedulingDashboardPage.tsx**

```bash
rm "SIS.Web/src/pages/SchedulingDashboardPage.tsx"
```

- [ ] **Step 4: Update InstructorSchedulingPage.tsx**

Remove the classroom assignment UI from InstructorSchedulingPage (derslik ataması artık Fakülte Planlama'da). Find and remove any sections that allow instructors to pick classrooms or assign classrooms to their sections. Keep the instructor availability and schedule view functionality intact. Add a note/badge in the UI: "Derslik ataması Fakülte Planlama sayfasından yapılmaktadır."

- [ ] **Step 5: Verify frontend builds**

```bash
cd "c:/Users/Sertuğ/Desktop/Student Information System/SIS.Web"
npm run build
```
Expected: Build succeeds.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add Faculty Planning route, remove Batch Scheduling page"
```

---

## Task 12: Full Integration Test

- [ ] **Step 1: Build entire solution**

```bash
cd "c:/Users/Sertuğ/Desktop/Student Information System"
dotnet build
```

- [ ] **Step 2: Run all backend tests**

```bash
dotnet test SIS.Tests/
```

- [ ] **Step 3: Run frontend build**

```bash
cd SIS.Web && npm run build
```

- [ ] **Step 4: Start API and verify endpoints**

```bash
cd SIS.API && dotnet run
```

Test endpoints with curl:
```bash
curl http://localhost:5000/api/faculty-planning/overview?facultyId=1&termId=1
curl http://localhost:5000/api/faculty-planning/sections?facultyId=1&termId=1
curl http://localhost:5000/api/faculty-planning/available-classrooms?termId=1
```

- [ ] **Step 5: Final commit if any fixes needed**
