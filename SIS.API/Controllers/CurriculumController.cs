using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Curriculum.Services;
using SIS.Application.ElectiveGroups.Services;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/curriculum")]
[Authorize]
public class CurriculumController(
    AppDbContext context,
    IElectiveGroupScopeValidator scopeValidator,
    ICourseStatisticsService courseStatistics) : ControllerBase
{
    // ── DTOs ────────────────────────────────────────────────────────
    public record CurriculumListDto(
        int Id, int ProgramId, string ProgramName, string ProgramCode,
        string FacultyName, string DepartmentName,
        string Version, string? Name, DateTime? StartDate, DateTime? EndDate,
        bool IsActive, CurriculumStatus Status,
        int CourseCount, int TotalEcts, int TotalCredit,
        int CompulsoryCount, int ElectiveCount,
        int ActiveStudentCount, int TotalStudentCount,
        DateTime CreatedAt);

    public record CurriculumDetailDto(
        int Id, int ProgramId, string ProgramName, string Version, bool IsActive,
        string? Name, string? ShortName, string? EnglishName,
        DateTime? StartDate, DateTime? EndDate,
        string? DecisionDate, string? DecisionNo, string? DecisionNote,
        string? GradingSystem, int? InternshipDuration, string? Description,
        // Müfredat-özel dönem AKTS tavanı override'ları (null = global varsayılan kullanılıyor)
        int? EctsLimitBelow200, int? EctsLimit200To249, int? EctsLimitAbove250,
        List<CurriculumCourseItemDto> Courses);

    public record CurriculumCourseItemDto(
        int CourseId, string CourseCode, string CourseName,
        int Ects, int TheoryHours, int LabHours,
        int SuggestedSemester, bool IsCompulsory,
        bool IncludeInGpa, bool ShowOnTranscript,
        int? CategoryId, string? CategoryName,
        int? OpeningReasonId, string? OpeningReasonCode, string? OpeningReasonName,
        int NationalCredit,
        // Phase 2 — flags
        bool IsMainGroupCourse, bool IsFormationCourse,
        bool ExcludedFromCreditTotal, bool ExcludedFromTranscriptTotal,
        bool AutoAddOnNewRegistration, bool ShowAtTakenCurriculum,
        bool ShowInstructorAtRegistration,
        // Phase 2 — decision metadata
        DateTime? DecisionDate, string? DecisionNo, string? DecisionNote,
        // Phase 2 — curriculum-specific description
        string? CurriculumDescription,
        // Phase 3 — İntibak (önceki ders, müfredata özel + tek yönlü)
        int? PreviousCourseId, string? PreviousCourseCode, string? PreviousCourseName);

    public record CreateCurriculumRequest(
        int ProgramId, string Version,
        string? Name = null, string? ShortName = null, string? EnglishName = null,
        DateTime? StartDate = null, DateTime? EndDate = null,
        string? DecisionDate = null, string? DecisionNo = null, string? DecisionNote = null,
        string? GradingSystem = null, int? InternshipDuration = null, string? Description = null);

    public record UpdateCurriculumRequest(
        string? Version = null, bool? IsActive = null,
        string? Name = null, string? ShortName = null, string? EnglishName = null,
        DateTime? StartDate = null, DateTime? EndDate = null,
        string? DecisionDate = null, string? DecisionNo = null, string? DecisionNote = null,
        string? GradingSystem = null, int? InternshipDuration = null, string? Description = null,
        // Dönem AKTS tavanı override'ları; uygulanması için EctsLimitsProvided=true gönderilmelidir.
        // EctsLimitsProvided=true iken üçü de null geçilirse override'lar temizlenir (global fallback aktif olur).
        int? EctsLimitBelow200 = null, int? EctsLimit200To249 = null, int? EctsLimitAbove250 = null,
        bool EctsLimitsProvided = false);

    public record AddCourseRequest(
        int CourseId, int SuggestedSemester, bool IsCompulsory,
        bool IncludeInGpa = true, bool ShowOnTranscript = true,
        int? CategoryId = null, int? OpeningReasonId = null,
        // Phase 2 flags
        bool IsMainGroupCourse = false, bool IsFormationCourse = false,
        bool ExcludedFromCreditTotal = false, bool ExcludedFromTranscriptTotal = false,
        bool AutoAddOnNewRegistration = false, bool ShowAtTakenCurriculum = false,
        bool ShowInstructorAtRegistration = true,
        // Phase 2 decision metadata
        DateTime? DecisionDate = null, string? DecisionNo = null, string? DecisionNote = null,
        // Phase 2 description
        string? CurriculumDescription = null,
        // Phase 3 — İntibak
        int? PreviousCourseId = null);

    public record UpdateCourseRequest(
        int? SuggestedSemester = null, bool? IsCompulsory = null,
        bool? IncludeInGpa = null, bool? ShowOnTranscript = null,
        int? CategoryId = null, int? OpeningReasonId = null,
        bool ClearCategory = false, bool ClearOpeningReason = false,
        // Phase 2 flags (all bool? — null means "no change")
        bool? IsMainGroupCourse = null, bool? IsFormationCourse = null,
        bool? ExcludedFromCreditTotal = null, bool? ExcludedFromTranscriptTotal = null,
        bool? AutoAddOnNewRegistration = null, bool? ShowAtTakenCurriculum = null,
        bool? ShowInstructorAtRegistration = null,
        // Phase 2 decision metadata (string? where null = no change; ClearXxx flags allow setting to null)
        DateTime? DecisionDate = null, string? DecisionNo = null, string? DecisionNote = null,
        bool ClearDecisionDate = false,
        // Phase 2 description
        string? CurriculumDescription = null,
        bool ClearCurriculumDescription = false,
        // Phase 3 — İntibak (null = no change; ClearPreviousCourse=true → null)
        int? PreviousCourseId = null,
        bool ClearPreviousCourse = false);

    public record CopyCurriculumRequest(string NewVersion);

    // ── İntibak (Müfredat Ders İntibak İşlemleri — toplu PreviousCourse eşleştirmesi) ──
    public record IntibakCurriculumDto(
        int Id, int ProgramId, string ProgramName, string ProgramCode,
        string FacultyName, string DepartmentName, string Version, string? Name,
        bool IsActive, CurriculumStatus Status,
        int CourseCount, int MappedCount);

    public record IntibakCourseDto(
        int CourseId, string CourseCode, string CourseName,
        int SuggestedSemester, bool IsCompulsory,
        int NationalCredit, int Ects,
        int? PreviousCourseId, string? PreviousCourseCode, string? PreviousCourseName);

    public record IntibakBulkItem(int CourseId, int? PreviousCourseId);
    public record IntibakBulkRequest(List<IntibakBulkItem> Items);

    public record CurriculumElectivePoolDto(
        int Id, int ElectiveGroupId, string GroupName, string GroupCode,
        int ElectiveSubtypeId,
        string SlotName,
        int SuggestedSemesterStart, int SuggestedSemesterEnd,
        int RequiredCourseCount, int RequiredEcts,
        int? EctsPerCourse,
        int PoolCourseCount);

    public record CreatePoolRequest(
        int ElectiveGroupId,
        string SlotName,
        int SuggestedSemesterStart, int SuggestedSemesterEnd,
        int RequiredCourseCount, int RequiredEcts,
        int? EctsPerCourse = null);

    public record UpdatePoolRequest(
        string? SlotName = null,
        int? SuggestedSemesterStart = null, int? SuggestedSemesterEnd = null,
        int? RequiredCourseCount = null, int? RequiredEcts = null,
        int? EctsPerCourse = null,
        bool ClearEctsPerCourse = false);

    // ── GET /api/curriculum?programId=X&departmentId=Y&facultyId=Z ──
    // Filtre önceliği: programId > departmentId > facultyId (en dar olan uygulanır).
    // Hiçbiri verilmezse tüm müfredatlar döner.
    [HttpGet]
    [RequirePermission("menu.mufredat-islemleri.view")]
    public async Task<IActionResult> List(
        [FromQuery] int? programId,
        [FromQuery] int? departmentId,
        [FromQuery] int? facultyId,
        CancellationToken ct)
    {
        var query = context.Curricula
            .AsNoTracking()
            .Include(c => c.Program).ThenInclude(p => p.Department).ThenInclude(d => d.Faculty)
            .Include(c => c.CurriculumCourses).ThenInclude(cc => cc.Course)
            .AsQueryable();

        if (programId.HasValue)
            query = query.Where(c => c.ProgramId == programId.Value);
        else if (departmentId.HasValue)
            query = query.Where(c => c.Program.DepartmentId == departmentId.Value);
        else if (facultyId.HasValue)
            query = query.Where(c => c.Program.Department.FacultyId == facultyId.Value);

        var list = await query
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.Version)
            .Select(c => new CurriculumListDto(
                c.Id,
                c.ProgramId,
                c.Program.Name,
                c.Program.Code,
                c.Program.Department.Faculty.Name,
                c.Program.Department.Name,
                c.Version,
                c.Name,
                c.StartDate,
                c.EndDate,
                c.IsActive,
                c.Status,
                c.CurriculumCourses.Count,
                c.CurriculumCourses.Sum(cc => cc.Course.Ects),
                c.CurriculumCourses.Sum(cc => cc.Course.NationalCredit),
                c.CurriculumCourses.Count(cc => cc.IsCompulsory),
                c.CurriculumCourses.Count(cc => !cc.IsCompulsory),
                context.StudentPrograms.Count(sp => sp.CurriculumId == c.Id && sp.IsActive),
                context.StudentPrograms.Count(sp => sp.CurriculumId == c.Id),
                c.CreatedAt))
            .ToListAsync(ct);

        return Ok(list);
    }

    // ── GET /api/curriculum/{id} ────────────────────────────────────
    [HttpGet("{id:int}")]
    [RequirePermission("menu.mufredat-islemleri.view")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var curriculum = await context.Curricula
            .AsNoTracking()
            .Include(c => c.Program)
            .Include(c => c.CurriculumCourses).ThenInclude(cc => cc.Course)
            .Include(c => c.CurriculumCourses).ThenInclude(cc => cc.Category)
            .Include(c => c.CurriculumCourses).ThenInclude(cc => cc.OpeningReason)
            .Include(c => c.CurriculumCourses).ThenInclude(cc => cc.PreviousCourse)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (curriculum is null)
            return NotFound(new { message = "Müfredat bulunamadı." });

        var dto = new CurriculumDetailDto(
            curriculum.Id,
            curriculum.ProgramId,
            curriculum.Program.Name,
            curriculum.Version,
            curriculum.IsActive,
            curriculum.Name,
            curriculum.ShortName,
            curriculum.EnglishName,
            curriculum.StartDate,
            curriculum.EndDate,
            curriculum.DecisionDate,
            curriculum.DecisionNo,
            curriculum.DecisionNote,
            curriculum.GradingSystem,
            curriculum.InternshipDuration,
            curriculum.Description,
            curriculum.EctsLimitBelow200,
            curriculum.EctsLimit200To249,
            curriculum.EctsLimitAbove250,
            curriculum.CurriculumCourses
                .OrderBy(cc => cc.SuggestedSemester)
                .ThenBy(cc => cc.Course.Code)
                .Select(cc => new CurriculumCourseItemDto(
                    cc.CourseId, cc.Course.Code, cc.Course.Name,
                    cc.Course.Ects, cc.Course.TheoryHours, cc.Course.LabHours,
                    cc.SuggestedSemester, cc.IsCompulsory,
                    cc.IncludeInGpa, cc.ShowOnTranscript,
                    cc.CategoryId, cc.Category != null ? cc.Category.Name : null,
                    cc.OpeningReasonId,
                    cc.OpeningReason != null ? cc.OpeningReason.Code : null,
                    cc.OpeningReason != null ? cc.OpeningReason.Name : null,
                    cc.Course.NationalCredit,
                    // Phase 2 — flags
                    cc.IsMainGroupCourse, cc.IsFormationCourse,
                    cc.ExcludedFromCreditTotal, cc.ExcludedFromTranscriptTotal,
                    cc.AutoAddOnNewRegistration, cc.ShowAtTakenCurriculum,
                    cc.ShowInstructorAtRegistration,
                    // Phase 2 — decision metadata
                    cc.DecisionDate, cc.DecisionNo, cc.DecisionNote,
                    // Phase 2 — curriculum-specific description
                    cc.CurriculumDescription,
                    // Phase 3 — İntibak
                    cc.PreviousCourseId,
                    cc.PreviousCourse != null ? cc.PreviousCourse.Code : null,
                    cc.PreviousCourse != null ? cc.PreviousCourse.Name : null))
                .ToList());

        return Ok(dto);
    }

    // ── POST /api/curriculum ────────────────────────────────────────
    [HttpPost]
    [RequirePermission("menu.mufredat-islemleri.create")]
    public async Task<IActionResult> Create([FromBody] CreateCurriculumRequest req, CancellationToken ct)
    {
        var program = await context.Set<Domain.Entities.AcademicProgram>()
            .Include(p => p.Department).ThenInclude(d => d.Faculty)
            .FirstOrDefaultAsync(p => p.Id == req.ProgramId, ct);
        if (program is null)
            return NotFound(new { message = "Program bulunamadı." });

        var versionExists = await context.Curricula
            .AnyAsync(c => c.ProgramId == req.ProgramId && c.Version == req.Version, ct);
        if (versionExists)
            return BadRequest(new { message = $"Bu program için '{req.Version}' versiyonu zaten mevcut." });

        var entity = new Domain.Entities.Curriculum
        {
            ProgramId = req.ProgramId,
            Version = req.Version,
            Name = req.Name,
            ShortName = req.ShortName,
            EnglishName = req.EnglishName,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            DecisionDate = req.DecisionDate,
            DecisionNo = req.DecisionNo,
            DecisionNote = req.DecisionNote,
            GradingSystem = req.GradingSystem,
            InternshipDuration = req.InternshipDuration,
            Description = req.Description,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };

        context.Curricula.Add(entity);
        await context.SaveChangesAsync(ct);

        return Ok(new CurriculumListDto(
            entity.Id, entity.ProgramId, program.Name, program.Code,
            program.Department.Faculty.Name, program.Department.Name,
            entity.Version, entity.Name, entity.StartDate, entity.EndDate,
            entity.IsActive, entity.Status,
            0, 0, program.TotalRequiredEcts,
            0, 0, 0, 0,
            entity.CreatedAt));
    }

    // ── PUT /api/curriculum/{id} ────────────────────────────────────
    [HttpPut("{id:int}")]
    [RequirePermission("menu.mufredat-islemleri.edit")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCurriculumRequest req, CancellationToken ct)
    {
        var curriculum = await context.Curricula
            .Include(c => c.Program)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (curriculum is null)
            return NotFound(new { message = "Müfredat bulunamadı." });

        if (req.Version is not null && req.Version != curriculum.Version)
        {
            var versionExists = await context.Curricula
                .AnyAsync(c => c.ProgramId == curriculum.ProgramId && c.Version == req.Version && c.Id != id, ct);
            if (versionExists)
                return BadRequest(new { message = $"Bu program için '{req.Version}' versiyonu zaten mevcut." });
            curriculum.Version = req.Version;
        }

        if (req.IsActive.HasValue)
            curriculum.IsActive = req.IsActive.Value;

        if (req.Name is not null) curriculum.Name = req.Name;
        if (req.ShortName is not null) curriculum.ShortName = req.ShortName;
        if (req.EnglishName is not null) curriculum.EnglishName = req.EnglishName;
        if (req.StartDate.HasValue) curriculum.StartDate = req.StartDate;
        if (req.EndDate.HasValue) curriculum.EndDate = req.EndDate;
        if (req.DecisionDate is not null) curriculum.DecisionDate = req.DecisionDate;
        if (req.DecisionNo is not null) curriculum.DecisionNo = req.DecisionNo;
        if (req.DecisionNote is not null) curriculum.DecisionNote = req.DecisionNote;
        if (req.GradingSystem is not null) curriculum.GradingSystem = req.GradingSystem;
        if (req.InternshipDuration.HasValue) curriculum.InternshipDuration = req.InternshipDuration;
        if (req.Description is not null) curriculum.Description = req.Description;

        // Dönem AKTS tavanı override'ları: sadece istemci açıkça "uygulamak istiyorum" derse işle.
        // Üçü birlikte yönetilir; biri verilip diğerleri verilmezse de gönderilenler yazılır.
        if (req.EctsLimitsProvided)
        {
            curriculum.EctsLimitBelow200 = req.EctsLimitBelow200;
            curriculum.EctsLimit200To249 = req.EctsLimit200To249;
            curriculum.EctsLimitAbove250 = req.EctsLimitAbove250;
        }

        curriculum.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        return Ok(new { message = "Müfredat güncellendi." });
    }

    // ── DELETE /api/curriculum/{id} ─────────────────────────────────
    [HttpDelete("{id:int}")]
    [RequirePermission("menu.mufredat-islemleri.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var curriculum = await context.Curricula
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (curriculum is null)
            return NotFound(new { message = "Müfredat bulunamadı." });

        var hasStudents = await context.StudentPrograms
            .AnyAsync(sp => sp.CurriculumId == id, ct);
        if (hasStudents)
            return BadRequest(new { message = "Bu müfredata kayıtlı öğrenciler var, silinemez." });

        context.Curricula.Remove(curriculum);
        await context.SaveChangesAsync(ct);

        return NoContent();
    }

    // ── GET /api/curriculum/{id}/courses ─────────────────────────────
    [HttpGet("{id:int}/courses")]
    [RequirePermission("menu.mufredat-islemleri.view")]
    public async Task<IActionResult> ListCourses(int id, CancellationToken ct)
    {
        var exists = await context.Curricula.AnyAsync(c => c.Id == id, ct);
        if (!exists)
            return NotFound(new { message = "Müfredat bulunamadı." });

        var courses = await context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => cc.CurriculumId == id)
            .Include(cc => cc.Course)
            .Include(cc => cc.Category)
            .Include(cc => cc.OpeningReason)
            .Include(cc => cc.PreviousCourse)
            .OrderBy(cc => cc.SuggestedSemester)
            .ThenBy(cc => cc.Course.Code)
            .Select(cc => new CurriculumCourseItemDto(
                cc.CourseId, cc.Course.Code, cc.Course.Name,
                cc.Course.Ects, cc.Course.TheoryHours, cc.Course.LabHours,
                cc.SuggestedSemester, cc.IsCompulsory,
                cc.IncludeInGpa, cc.ShowOnTranscript,
                cc.CategoryId, cc.Category != null ? cc.Category.Name : null,
                cc.OpeningReasonId,
                cc.OpeningReason != null ? cc.OpeningReason.Code : null,
                cc.OpeningReason != null ? cc.OpeningReason.Name : null,
                cc.Course.NationalCredit,
                // Phase 2
                cc.IsMainGroupCourse, cc.IsFormationCourse,
                cc.ExcludedFromCreditTotal, cc.ExcludedFromTranscriptTotal,
                cc.AutoAddOnNewRegistration, cc.ShowAtTakenCurriculum,
                cc.ShowInstructorAtRegistration,
                cc.DecisionDate, cc.DecisionNo, cc.DecisionNote,
                cc.CurriculumDescription,
                // Phase 3
                cc.PreviousCourseId,
                cc.PreviousCourse != null ? cc.PreviousCourse.Code : null,
                cc.PreviousCourse != null ? cc.PreviousCourse.Name : null))
            .ToListAsync(ct);

        return Ok(courses);
    }

    // ── POST /api/curriculum/{id}/courses ────────────────────────────
    [HttpPost("{id:int}/courses")]
    [RequirePermission("menu.mufredat-islemleri.add-course")]
    public async Task<IActionResult> AddCourse(int id, [FromBody] AddCourseRequest req, CancellationToken ct)
    {
        // Madde 8: Published müfredat kilit — ders eklenip çıkarılamaz
        var curriculum = await context.Curricula.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (curriculum is null)
            return NotFound(new { message = "Müfredat bulunamadı." });
        if (curriculum.Status == Domain.Enums.CurriculumStatus.Published)
            return BadRequest(new { message = "Yayındaki müfredata ders eklenemez. Yeni sürüm oluşturun." });
        if (curriculum.Status == Domain.Enums.CurriculumStatus.Archived)
            return BadRequest(new { message = "Arşiv durumundaki müfredat değiştirilemez." });

        var course = await context.Set<Domain.Entities.Course>()
            .FirstOrDefaultAsync(c => c.Id == req.CourseId, ct);
        if (course is null)
            return NotFound(new { message = "Ders bulunamadı." });

        var alreadyExists = await context.CurriculumCourses
            .AnyAsync(cc => cc.CurriculumId == id && cc.CourseId == req.CourseId, ct);
        if (alreadyExists)
            return BadRequest(new { message = "Bu ders müfredatta zaten mevcut." });

        // Phase 3 — İntibak validation
        if (req.PreviousCourseId.HasValue)
        {
            if (req.PreviousCourseId.Value == req.CourseId)
                return BadRequest(new { message = "Bir ders kendisinin önceki dersi olamaz." });
            var prevExists = await context.Set<Domain.Entities.Course>()
                .AnyAsync(c => c.Id == req.PreviousCourseId.Value, ct);
            if (!prevExists)
                return NotFound(new { message = "İntibak için seçilen önceki ders bulunamadı." });
        }

        var entity = new Domain.Entities.CurriculumCourse
        {
            CurriculumId = id,
            CourseId = req.CourseId,
            SuggestedSemester = req.SuggestedSemester,
            IsCompulsory = req.IsCompulsory,
            IncludeInGpa = req.IncludeInGpa,
            ShowOnTranscript = req.ShowOnTranscript,
            CategoryId = req.CategoryId,
            OpeningReasonId = req.OpeningReasonId,
            // Phase 2 flags
            IsMainGroupCourse = req.IsMainGroupCourse,
            IsFormationCourse = req.IsFormationCourse,
            ExcludedFromCreditTotal = req.ExcludedFromCreditTotal,
            ExcludedFromTranscriptTotal = req.ExcludedFromTranscriptTotal,
            AutoAddOnNewRegistration = req.AutoAddOnNewRegistration,
            ShowAtTakenCurriculum = req.ShowAtTakenCurriculum,
            ShowInstructorAtRegistration = req.ShowInstructorAtRegistration,
            // Phase 2 decision metadata
            DecisionDate = req.DecisionDate,
            DecisionNo = req.DecisionNo,
            DecisionNote = req.DecisionNote,
            // Phase 2 description
            CurriculumDescription = req.CurriculumDescription,
            // Phase 3 — İntibak
            PreviousCourseId = req.PreviousCourseId,
        };

        context.CurriculumCourses.Add(entity);
        await context.SaveChangesAsync(ct);

        // Re-fetch to populate navigation names for the response
        var saved = await context.CurriculumCourses
            .AsNoTracking()
            .Include(cc => cc.Course)
            .Include(cc => cc.Category)
            .Include(cc => cc.OpeningReason)
            .Include(cc => cc.PreviousCourse)
            .FirstAsync(cc => cc.CurriculumId == id && cc.CourseId == req.CourseId, ct);

        return Ok(new CurriculumCourseItemDto(
            saved.CourseId, saved.Course.Code, saved.Course.Name,
            saved.Course.Ects, saved.Course.TheoryHours, saved.Course.LabHours,
            saved.SuggestedSemester, saved.IsCompulsory,
            saved.IncludeInGpa, saved.ShowOnTranscript,
            saved.CategoryId, saved.Category != null ? saved.Category.Name : null,
            saved.OpeningReasonId,
            saved.OpeningReason != null ? saved.OpeningReason.Code : null,
            saved.OpeningReason != null ? saved.OpeningReason.Name : null,
            saved.Course.NationalCredit,
            // Phase 2
            saved.IsMainGroupCourse, saved.IsFormationCourse,
            saved.ExcludedFromCreditTotal, saved.ExcludedFromTranscriptTotal,
            saved.AutoAddOnNewRegistration, saved.ShowAtTakenCurriculum,
            saved.ShowInstructorAtRegistration,
            saved.DecisionDate, saved.DecisionNo, saved.DecisionNote,
            saved.CurriculumDescription,
            // Phase 3
            saved.PreviousCourseId,
            saved.PreviousCourse != null ? saved.PreviousCourse.Code : null,
            saved.PreviousCourse != null ? saved.PreviousCourse.Name : null));
    }

    // ── PUT /api/curriculum/{id}/courses/{courseId} ──────────────────
    [HttpPut("{id:int}/courses/{courseId:int}")]
    [RequirePermission("menu.mufredat-islemleri.edit-course")]
    public async Task<IActionResult> UpdateCourse(int id, int courseId, [FromBody] UpdateCourseRequest req, CancellationToken ct)
    {
        // Madde 8: Published guard
        var curriculum = await context.Curricula.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (curriculum is not null && curriculum.Status == Domain.Enums.CurriculumStatus.Published)
            return BadRequest(new { message = "Yayındaki müfredatın dersi düzenlenemez. Yeni sürüm oluşturun." });

        var cc = await context.CurriculumCourses
            .FindAsync([id, courseId], ct);
        if (cc is null)
            return NotFound(new { message = "Müfredat dersi bulunamadı." });

        if (req.SuggestedSemester.HasValue)
            cc.SuggestedSemester = req.SuggestedSemester.Value;
        if (req.IsCompulsory.HasValue)
            cc.IsCompulsory = req.IsCompulsory.Value;
        if (req.IncludeInGpa.HasValue)
            cc.IncludeInGpa = req.IncludeInGpa.Value;
        if (req.ShowOnTranscript.HasValue)
            cc.ShowOnTranscript = req.ShowOnTranscript.Value;

        if (req.ClearCategory) cc.CategoryId = null;
        else if (req.CategoryId.HasValue) cc.CategoryId = req.CategoryId;

        if (req.ClearOpeningReason) cc.OpeningReasonId = null;
        else if (req.OpeningReasonId.HasValue) cc.OpeningReasonId = req.OpeningReasonId;

        // Phase 2 — flags
        if (req.IsMainGroupCourse.HasValue) cc.IsMainGroupCourse = req.IsMainGroupCourse.Value;
        if (req.IsFormationCourse.HasValue) cc.IsFormationCourse = req.IsFormationCourse.Value;
        if (req.ExcludedFromCreditTotal.HasValue) cc.ExcludedFromCreditTotal = req.ExcludedFromCreditTotal.Value;
        if (req.ExcludedFromTranscriptTotal.HasValue) cc.ExcludedFromTranscriptTotal = req.ExcludedFromTranscriptTotal.Value;
        if (req.AutoAddOnNewRegistration.HasValue) cc.AutoAddOnNewRegistration = req.AutoAddOnNewRegistration.Value;
        if (req.ShowAtTakenCurriculum.HasValue) cc.ShowAtTakenCurriculum = req.ShowAtTakenCurriculum.Value;
        if (req.ShowInstructorAtRegistration.HasValue) cc.ShowInstructorAtRegistration = req.ShowInstructorAtRegistration.Value;

        // Phase 2 — decision metadata
        if (req.ClearDecisionDate) cc.DecisionDate = null;
        else if (req.DecisionDate.HasValue) cc.DecisionDate = req.DecisionDate;
        if (req.DecisionNo is not null) cc.DecisionNo = req.DecisionNo;
        if (req.DecisionNote is not null) cc.DecisionNote = req.DecisionNote;

        // Phase 2 — curriculum-specific description
        if (req.ClearCurriculumDescription) cc.CurriculumDescription = null;
        else if (req.CurriculumDescription is not null) cc.CurriculumDescription = req.CurriculumDescription;

        // Phase 3 — İntibak (önceki ders, müfredata özel)
        if (req.ClearPreviousCourse)
        {
            cc.PreviousCourseId = null;
        }
        else if (req.PreviousCourseId.HasValue)
        {
            if (req.PreviousCourseId.Value == cc.CourseId)
                return BadRequest(new { message = "Bir ders kendisinin önceki dersi olamaz." });
            var prevExists = await context.Set<Domain.Entities.Course>()
                .AnyAsync(c => c.Id == req.PreviousCourseId.Value, ct);
            if (!prevExists)
                return NotFound(new { message = "İntibak için seçilen önceki ders bulunamadı." });
            cc.PreviousCourseId = req.PreviousCourseId;
        }

        await context.SaveChangesAsync(ct);

        return Ok(new { message = "Müfredat dersi güncellendi." });
    }

    // ── DELETE /api/curriculum/{id}/courses/{courseId} ───────────────
    [HttpDelete("{id:int}/courses/{courseId:int}")]
    [RequirePermission("menu.mufredat-islemleri.remove-course")]
    public async Task<IActionResult> RemoveCourse(int id, int courseId, CancellationToken ct)
    {
        // Madde 8: Published guard
        var curriculum = await context.Curricula.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (curriculum is not null && curriculum.Status == Domain.Enums.CurriculumStatus.Published)
            return BadRequest(new { message = "Yayındaki müfredattan ders silinemez. Yeni sürüm oluşturun." });

        var cc = await context.CurriculumCourses
            .FindAsync([id, courseId], ct);
        if (cc is null)
            return NotFound(new { message = "Müfredat dersi bulunamadı." });

        context.CurriculumCourses.Remove(cc);
        await context.SaveChangesAsync(ct);

        return NoContent();
    }

    // ════════════════════════════════════════════════════════════════
    // İntibak (Müfredat Ders İntibak İşlemleri) — standalone toplu yönetim
    // PreviousCourse (müfredata özel, tek yönlü yeni→eski) eşleştirmesi.
    // CourseEquivalency ile KARIŞTIRILMAZ (o ayrı: katalog, simetrik).
    // ════════════════════════════════════════════════════════════════

    // ── GET /api/curriculum/intibak/curricula ── (intibak sayfası müfredat seçici) ──
    [HttpGet("intibak/curricula")]
    [RequirePermission("menu.mufredat-ders-intibak.view")]
    public async Task<IActionResult> ListIntibakCurricula(CancellationToken ct)
    {
        var list = await context.Curricula
            .AsNoTracking()
            .Include(c => c.Program).ThenInclude(p => p.Department).ThenInclude(d => d.Faculty)
            .OrderBy(c => c.Program.Name).ThenByDescending(c => c.Version)
            .Select(c => new IntibakCurriculumDto(
                c.Id, c.ProgramId, c.Program.Name, c.Program.Code,
                c.Program.Department.Faculty.Name, c.Program.Department.Name, c.Version, c.Name,
                c.IsActive, c.Status,
                c.CurriculumCourses.Count,
                c.CurriculumCourses.Count(cc => cc.PreviousCourseId != null)))
            .ToListAsync(ct);

        return Ok(list);
    }

    // ── GET /api/curriculum/{id}/intibak ── (seçili müfredatın ders+intibak listesi) ──
    [HttpGet("{id:int}/intibak")]
    [RequirePermission("menu.mufredat-ders-intibak.view")]
    public async Task<IActionResult> GetIntibak(int id, CancellationToken ct)
    {
        var exists = await context.Curricula.AnyAsync(c => c.Id == id, ct);
        if (!exists)
            return NotFound(new { message = "Müfredat bulunamadı." });

        var courses = await context.CurriculumCourses
            .AsNoTracking()
            .Where(cc => cc.CurriculumId == id)
            .Include(cc => cc.Course)
            .Include(cc => cc.PreviousCourse)
            .OrderBy(cc => cc.SuggestedSemester)
            .ThenBy(cc => cc.Course.Code)
            .Select(cc => new IntibakCourseDto(
                cc.CourseId, cc.Course.Code, cc.Course.Name,
                cc.SuggestedSemester, cc.IsCompulsory,
                cc.Course.NationalCredit, cc.Course.Ects,
                cc.PreviousCourseId,
                cc.PreviousCourse != null ? cc.PreviousCourse.Code : null,
                cc.PreviousCourse != null ? cc.PreviousCourse.Name : null))
            .ToListAsync(ct);

        return Ok(courses);
    }

    // ── GET /api/curriculum/course/{courseId}/intibak-statistics ── (detay paneli öğrenci sayıları) ──
    [HttpGet("course/{courseId:int}/intibak-statistics")]
    [RequirePermission("menu.mufredat-ders-intibak.view")]
    public async Task<IActionResult> GetCourseIntibakStatistics(int courseId, CancellationToken ct)
    {
        var exists = await context.Set<Domain.Entities.Course>().AnyAsync(c => c.Id == courseId, ct);
        if (!exists)
            return NotFound(new { message = "Ders bulunamadı." });

        var stats = await courseStatistics.GetCourseIntibakStatisticsAsync(courseId, ct);
        return Ok(stats);
    }

    // ── PUT /api/curriculum/{id}/intibak ── (toplu intibak kaydet) ──
    [HttpPut("{id:int}/intibak")]
    [RequirePermission("menu.mufredat-ders-intibak.edit")]
    public async Task<IActionResult> SaveIntibak(int id, [FromBody] IntibakBulkRequest req, CancellationToken ct)
    {
        var curriculum = await context.Curricula.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (curriculum is null)
            return NotFound(new { message = "Müfredat bulunamadı." });
        if (curriculum.Status == Domain.Enums.CurriculumStatus.Published)
            return BadRequest(new { message = "Yayındaki müfredatın intibakı düzenlenemez. Yeni sürüm oluşturun." });
        if (curriculum.Status == Domain.Enums.CurriculumStatus.Archived)
            return BadRequest(new { message = "Arşiv durumundaki müfredat değiştirilemez." });

        var items = req.Items ?? new List<IntibakBulkItem>();
        if (items.Count == 0)
            return Ok(new { updated = 0, message = "Değişiklik yok." });

        // Self-reference guard
        var selfRef = items.FirstOrDefault(i => i.PreviousCourseId.HasValue && i.PreviousCourseId.Value == i.CourseId);
        if (selfRef is not null)
            return BadRequest(new { message = "Bir ders kendisinin önceki dersi olamaz." });

        // Curriculum'daki dersler
        var ccByCourse = await context.CurriculumCourses
            .Where(cc => cc.CurriculumId == id)
            .ToDictionaryAsync(cc => cc.CourseId, ct);

        var unknownInCurriculum = items.Where(i => !ccByCourse.ContainsKey(i.CourseId)).Select(i => i.CourseId).ToList();
        if (unknownInCurriculum.Count > 0)
            return BadRequest(new { message = $"Şu ders(ler) bu müfredatta yok: {string.Join(", ", unknownInCurriculum)}" });

        // Seçilen önceki derslerin varlığını tek sorguda doğrula
        var previousIds = items.Where(i => i.PreviousCourseId.HasValue).Select(i => i.PreviousCourseId!.Value).Distinct().ToList();
        if (previousIds.Count > 0)
        {
            var existingIds = await context.Set<Domain.Entities.Course>()
                .Where(c => previousIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync(ct);
            var missing = previousIds.Except(existingIds).ToList();
            if (missing.Count > 0)
                return BadRequest(new { message = $"İntibak için seçilen önceki ders(ler) bulunamadı: {string.Join(", ", missing)}" });
        }

        var updated = 0;
        foreach (var item in items)
        {
            var cc = ccByCourse[item.CourseId];
            if (cc.PreviousCourseId == item.PreviousCourseId) continue;
            cc.PreviousCourseId = item.PreviousCourseId;
            updated++;
        }

        if (updated > 0)
            await context.SaveChangesAsync(ct);

        return Ok(new { updated, message = $"{updated} ders için intibak güncellendi." });
    }

    // ── POST /api/curriculum/{id}/copy ────────────────────────────
    [HttpPost("{id:int}/copy")]
    [RequirePermission("menu.mufredat-islemleri.copy")]
    public async Task<IActionResult> Copy(int id, [FromBody] CopyCurriculumRequest req, CancellationToken ct)
    {
        var source = await context.Curricula
            .Include(c => c.Program)
            .Include(c => c.CurriculumCourses)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (source is null)
            return NotFound(new { message = "Kaynak müfredat bulunamadı." });

        var versionExists = await context.Curricula
            .AnyAsync(c => c.ProgramId == source.ProgramId && c.Version == req.NewVersion, ct);
        if (versionExists)
            return BadRequest(new { message = $"Bu program için '{req.NewVersion}' versiyonu zaten mevcut." });

        var newCurriculum = new Domain.Entities.Curriculum
        {
            ProgramId = source.ProgramId,
            Version = req.NewVersion,
            Name = source.Name,
            ShortName = source.ShortName,
            EnglishName = source.EnglishName,
            StartDate = source.StartDate,
            EndDate = source.EndDate,
            DecisionDate = source.DecisionDate,
            DecisionNo = source.DecisionNo,
            DecisionNote = source.DecisionNote,
            GradingSystem = source.GradingSystem,
            InternshipDuration = source.InternshipDuration,
            Description = source.Description,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };

        context.Curricula.Add(newCurriculum);
        await context.SaveChangesAsync(ct);

        // Copy all courses from source
        foreach (var cc in source.CurriculumCourses)
        {
            context.CurriculumCourses.Add(new Domain.Entities.CurriculumCourse
            {
                CurriculumId = newCurriculum.Id,
                CourseId = cc.CourseId,
                SuggestedSemester = cc.SuggestedSemester,
                IsCompulsory = cc.IsCompulsory,
                IncludeInGpa = cc.IncludeInGpa,
                ShowOnTranscript = cc.ShowOnTranscript,
                CategoryId = cc.CategoryId,
                OpeningReasonId = cc.OpeningReasonId,
                // Phase 2 — flags
                IsMainGroupCourse = cc.IsMainGroupCourse,
                IsFormationCourse = cc.IsFormationCourse,
                ExcludedFromCreditTotal = cc.ExcludedFromCreditTotal,
                ExcludedFromTranscriptTotal = cc.ExcludedFromTranscriptTotal,
                AutoAddOnNewRegistration = cc.AutoAddOnNewRegistration,
                ShowAtTakenCurriculum = cc.ShowAtTakenCurriculum,
                ShowInstructorAtRegistration = cc.ShowInstructorAtRegistration,
                // Phase 2 — decision metadata
                DecisionDate = cc.DecisionDate,
                DecisionNo = cc.DecisionNo,
                DecisionNote = cc.DecisionNote,
                // Phase 2 — curriculum-specific description
                CurriculumDescription = cc.CurriculumDescription,
                // Phase 3 — İntibak (preserve predecessor link in copied curriculum)
                PreviousCourseId = cc.PreviousCourseId,
            });
        }

        await context.SaveChangesAsync(ct);

        // Return the full detail of the new curriculum
        var detail = await context.Curricula
            .AsNoTracking()
            .Include(c => c.Program)
            .Include(c => c.CurriculumCourses).ThenInclude(cc => cc.Course)
            .Include(c => c.CurriculumCourses).ThenInclude(cc => cc.Category)
            .Include(c => c.CurriculumCourses).ThenInclude(cc => cc.OpeningReason)
            .Include(c => c.CurriculumCourses).ThenInclude(cc => cc.PreviousCourse)
            .FirstAsync(c => c.Id == newCurriculum.Id, ct);

        return Ok(new CurriculumDetailDto(
            detail.Id,
            detail.ProgramId,
            detail.Program.Name,
            detail.Version,
            detail.IsActive,
            detail.Name,
            detail.ShortName,
            detail.EnglishName,
            detail.StartDate,
            detail.EndDate,
            detail.DecisionDate,
            detail.DecisionNo,
            detail.DecisionNote,
            detail.GradingSystem,
            detail.InternshipDuration,
            detail.Description,
            detail.EctsLimitBelow200,
            detail.EctsLimit200To249,
            detail.EctsLimitAbove250,
            detail.CurriculumCourses
                .OrderBy(cc => cc.SuggestedSemester)
                .ThenBy(cc => cc.Course.Code)
                .Select(cc => new CurriculumCourseItemDto(
                    cc.CourseId, cc.Course.Code, cc.Course.Name,
                    cc.Course.Ects, cc.Course.TheoryHours, cc.Course.LabHours,
                    cc.SuggestedSemester, cc.IsCompulsory,
                    cc.IncludeInGpa, cc.ShowOnTranscript,
                    cc.CategoryId, cc.Category != null ? cc.Category.Name : null,
                    cc.OpeningReasonId,
                    cc.OpeningReason != null ? cc.OpeningReason.Code : null,
                    cc.OpeningReason != null ? cc.OpeningReason.Name : null,
                    cc.Course.NationalCredit,
                    // Phase 2 — flags
                    cc.IsMainGroupCourse, cc.IsFormationCourse,
                    cc.ExcludedFromCreditTotal, cc.ExcludedFromTranscriptTotal,
                    cc.AutoAddOnNewRegistration, cc.ShowAtTakenCurriculum,
                    cc.ShowInstructorAtRegistration,
                    // Phase 2 — decision metadata
                    cc.DecisionDate, cc.DecisionNo, cc.DecisionNote,
                    // Phase 2 — curriculum-specific description
                    cc.CurriculumDescription,
                    // Phase 3 — İntibak
                    cc.PreviousCourseId,
                    cc.PreviousCourse != null ? cc.PreviousCourse.Code : null,
                    cc.PreviousCourse != null ? cc.PreviousCourse.Name : null))
                .ToList()));
    }

    // ── POST /api/curriculum/{id}/activate ──────────────────────────
    [HttpPost("{id:int}/activate")]
    [RequirePermission("menu.mufredat-islemleri.activate")]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
    {
        var curriculum = await context.Curricula
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (curriculum is null)
            return NotFound(new { message = "Müfredat bulunamadı." });

        // Aynı programa ait diğer müfredatları deaktif et
        var siblings = await context.Curricula
            .Where(c => c.ProgramId == curriculum.ProgramId && c.Id != id && c.IsActive)
            .ToListAsync(ct);

        foreach (var s in siblings)
        {
            s.IsActive = false;
            s.UpdatedAt = DateTime.UtcNow;
        }

        curriculum.IsActive = true;
        curriculum.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        return Ok(new { message = "Müfredat aktif olarak ayarlandı." });
    }

    // ── GET /api/curriculum/{id}/elective-pools ─────────────────────
    [HttpGet("{id:int}/elective-pools")]
    [RequirePermission("menu.mufredat-islemleri.view")]
    public async Task<IActionResult> ListPools(int id, CancellationToken ct)
    {
        var exists = await context.Curricula.AnyAsync(c => c.Id == id, ct);
        if (!exists)
            return NotFound(new { message = "Müfredat bulunamadı." });

        var pools = await context.CurriculumElectivePools
            .AsNoTracking()
            .Where(p => p.CurriculumId == id)
            .Include(p => p.ElectiveGroup).ThenInclude(eg => eg.Courses)
            .OrderBy(p => p.SuggestedSemesterStart)
            .Select(p => new CurriculumElectivePoolDto(
                p.Id, p.ElectiveGroupId, p.ElectiveGroup.Name, p.ElectiveGroup.Code,
                p.ElectiveGroup.ElectiveSubtypeId,
                p.SlotName,
                p.SuggestedSemesterStart, p.SuggestedSemesterEnd,
                p.RequiredCourseCount, p.RequiredEcts,
                p.EctsPerCourse,
                p.ElectiveGroup.Courses.Count))
            .ToListAsync(ct);

        return Ok(pools);
    }

    // ── POST /api/curriculum/{id}/elective-pools ────────────────────
    [HttpPost("{id:int}/elective-pools")]
    [RequirePermission("menu.mufredat-islemleri.create-pool")]
    public async Task<IActionResult> CreatePool(int id, [FromBody] CreatePoolRequest req, CancellationToken ct)
    {
        // Madde 8: Published müfredat kilit + scope ladder validation için Program/Department include'u
        var curr = await context.Curricula
            .Include(c => c.Program)
                .ThenInclude(p => p.Department)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (curr is null)
            return NotFound(new { message = "Müfredat bulunamadı." });
        if (curr.Status == Domain.Enums.CurriculumStatus.Published)
            return BadRequest(new { message = "Yayındaki müfredata havuz eklenemez. Yeni sürüm oluşturun." });
        if (curr.Status == Domain.Enums.CurriculumStatus.Archived)
            return BadRequest(new { message = "Arşiv durumundaki müfredat değiştirilemez." });
        if (curr.Program?.Department is null)
            return BadRequest(new { message = "Müfredata bağlı program/bölüm bilgisi yüklenemedi." });

        var group = await context.ElectiveGroups
            .Include(eg => eg.Courses)
            .FirstOrDefaultAsync(eg => eg.Id == req.ElectiveGroupId, ct);
        if (group is null)
            return NotFound(new { message = "Seçmeli ders grubu bulunamadı." });

        // Scope ladder validation: havuz scope'u müfredatın hiyerarşisinde eşit veya daha geniş olmalı
        var facultyId = curr.Program.Department.FacultyId;
        var departmentId = curr.Program.DepartmentId;
        var scopeResult = scopeValidator.CanBind(group, facultyId, departmentId);
        if (!scopeResult.IsValid)
            return BadRequest(new { message = scopeResult.ErrorMessage });

        if (req.RequiredCourseCount <= 0 && req.RequiredEcts <= 0)
            return BadRequest(new { message = "Gerekli ders sayısı veya AKTS en az biri 0'dan büyük olmalıdır." });

        // SlotName validation + uniqueness via shared helper
        var (trimmedSlot, slotError) = await ValidateAndResolveSlotNameAsync(
            curriculumId: id,
            electiveGroupId: req.ElectiveGroupId,
            slotName: req.SlotName,
            excludePoolId: null,
            ct: ct);
        if (slotError != null) return slotError;

        // EctsPerCourse non-negative
        if (req.EctsPerCourse.HasValue && req.EctsPerCourse.Value < 0)
            return BadRequest(new { message = "AKTS değeri negatif olamaz." });

        var entity = new Domain.Entities.CurriculumElectivePool
        {
            CurriculumId = id,
            ElectiveGroupId = req.ElectiveGroupId,
            SlotName = trimmedSlot!,
            SuggestedSemesterStart = req.SuggestedSemesterStart,
            SuggestedSemesterEnd = req.SuggestedSemesterEnd,
            RequiredCourseCount = req.RequiredCourseCount,
            RequiredEcts = req.RequiredEcts,
            EctsPerCourse = req.EctsPerCourse,
            CreatedAt = DateTime.UtcNow
        };

        context.CurriculumElectivePools.Add(entity);
        await context.SaveChangesAsync(ct);

        return Ok(new CurriculumElectivePoolDto(
            entity.Id, entity.ElectiveGroupId, group.Name, group.Code,
            group.ElectiveSubtypeId,
            entity.SlotName,
            entity.SuggestedSemesterStart, entity.SuggestedSemesterEnd,
            entity.RequiredCourseCount, entity.RequiredEcts,
            entity.EctsPerCourse,
            group.Courses.Count));
    }

    // ── PUT /api/curriculum/{id}/elective-pools/{poolId} ────────────
    [HttpPut("{id:int}/elective-pools/{poolId:int}")]
    [RequirePermission("menu.mufredat-islemleri.edit-pool")]
    public async Task<IActionResult> UpdatePool(int id, int poolId, [FromBody] UpdatePoolRequest req, CancellationToken ct)
    {
        // NOT: UpdatePool'da scope ladder validation YOK çünkü UpdatePoolRequest ElectiveGroupId
        // alanı içermiyor — havuz değişmez, sadece SuggestedSemester/RequiredCourseCount/Ects
        // güncellenebilir. Eğer ileride ElectiveGroupId değiştirilebilir hale gelirse,
        // CreatePool'daki scope check buraya da eklenmelidir.

        // Madde 8: Published müfredat kilit
        var curr = await context.Curricula.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (curr is not null && curr.Status == Domain.Enums.CurriculumStatus.Published)
            return BadRequest(new { message = "Yayındaki müfredatın havuzu düzenlenemez. Yeni sürüm oluşturun." });

        var pool = await context.CurriculumElectivePools
            .FirstOrDefaultAsync(p => p.Id == poolId && p.CurriculumId == id, ct);
        if (pool is null)
            return NotFound(new { message = "Seçmeli havuz bulunamadı." });

        // SlotName değişiyorsa validation + uniqueness check
        if (req.SlotName != null && req.SlotName.Trim() != pool.SlotName)
        {
            var (newSlot, slotError) = await ValidateAndResolveSlotNameAsync(
                curriculumId: pool.CurriculumId,
                electiveGroupId: pool.ElectiveGroupId,
                slotName: req.SlotName,
                excludePoolId: poolId,
                ct: ct);
            if (slotError != null) return slotError;
            pool.SlotName = newSlot!;
        }

        if (req.SuggestedSemesterStart.HasValue) pool.SuggestedSemesterStart = req.SuggestedSemesterStart.Value;
        if (req.SuggestedSemesterEnd.HasValue) pool.SuggestedSemesterEnd = req.SuggestedSemesterEnd.Value;
        if (req.RequiredCourseCount.HasValue) pool.RequiredCourseCount = req.RequiredCourseCount.Value;
        if (req.RequiredEcts.HasValue) pool.RequiredEcts = req.RequiredEcts.Value;

        // EctsPerCourse: ClearEctsPerCourse=true → null; aksi halde value varsa atanır
        if (req.ClearEctsPerCourse)
        {
            pool.EctsPerCourse = null;
        }
        else if (req.EctsPerCourse.HasValue)
        {
            if (req.EctsPerCourse.Value < 0)
                return BadRequest(new { message = "AKTS değeri negatif olamaz." });
            pool.EctsPerCourse = req.EctsPerCourse;
        }

        pool.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        return Ok(new { message = "Seçmeli havuz güncellendi." });
    }

    // ── DELETE /api/curriculum/{id}/elective-pools/{poolId} ─────────
    [HttpDelete("{id:int}/elective-pools/{poolId:int}")]
    [RequirePermission("menu.mufredat-islemleri.delete-pool")]
    public async Task<IActionResult> DeletePool(int id, int poolId, CancellationToken ct)
    {
        // Madde 8: Published müfredat kilit
        var curr = await context.Curricula.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (curr is not null && curr.Status == Domain.Enums.CurriculumStatus.Published)
            return BadRequest(new { message = "Yayındaki müfredattan havuz silinemez. Yeni sürüm oluşturun." });

        var pool = await context.CurriculumElectivePools
            .FirstOrDefaultAsync(p => p.Id == poolId && p.CurriculumId == id, ct);
        if (pool is null)
            return NotFound(new { message = "Seçmeli havuz bulunamadı." });

        context.CurriculumElectivePools.Remove(pool);
        await context.SaveChangesAsync(ct);

        return NoContent();
    }

    // ── Madde 8: Müfredat yayınlama akışı ─────────────────────────

    /// <summary>Draft → Published geçişi. Yayınlandıktan sonra ders/havuz değişikliği engellenir.</summary>
    [HttpPost("{id:int}/publish")]
    [RequirePermission("menu.mufredat-islemleri.publish")]
    public async Task<IActionResult> PublishCurriculum(int id, CancellationToken ct)
    {
        var curr = await context.Curricula.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (curr is null)
            return NotFound(new { message = "Müfredat bulunamadı." });
        if (curr.Status == Domain.Enums.CurriculumStatus.Published)
            return BadRequest(new { message = "Müfredat zaten yayında." });
        if (curr.Status == Domain.Enums.CurriculumStatus.Archived)
            return BadRequest(new { message = "Arşiv müfredat yeniden yayınlanamaz. Yeni sürüm oluşturun." });

        curr.Status = Domain.Enums.CurriculumStatus.Published;
        curr.PublishedAt = DateTime.UtcNow;
        curr.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return Ok(new { message = "Müfredat yayınlandı.", publishedAt = curr.PublishedAt });
    }

    /// <summary>Published → Archived geçişi. Eski müfredat kapatılır; yeni öğrenci bağlanamaz.</summary>
    [HttpPost("{id:int}/archive")]
    [RequirePermission("menu.mufredat-islemleri.archive")]
    public async Task<IActionResult> ArchiveCurriculum(int id, CancellationToken ct)
    {
        var curr = await context.Curricula.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (curr is null)
            return NotFound(new { message = "Müfredat bulunamadı." });
        if (curr.Status != Domain.Enums.CurriculumStatus.Published)
            return BadRequest(new { message = "Sadece yayında olan müfredatlar arşive alınabilir." });

        curr.Status = Domain.Enums.CurriculumStatus.Archived;
        curr.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return Ok(new { message = "Müfredat arşive alındı." });
    }

    // ── Müfredat Dönem Kuralları (Proliz "Müfredat Dönem Kuralları" muadili) ─────────────

    public record CurriculumTermRuleDto(
        int Id,
        int CurriculumId,
        CurriculumClassYear ClassYear,
        CurriculumTermHalf TermHalf,
        CurriculumTermRuleType RuleType,
        decimal Value,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    public record CreateTermRuleRequest(
        CurriculumClassYear ClassYear,
        CurriculumTermHalf TermHalf,
        CurriculumTermRuleType RuleType,
        decimal Value);

    public record UpdateTermRuleRequest(decimal Value);

    [HttpGet("{id:int}/term-rules")]
    [RequirePermission("menu.mufredat-sinif-donem.view")]
    public async Task<IActionResult> ListTermRules(int id, CancellationToken ct)
    {
        var exists = await context.Curricula.AnyAsync(c => c.Id == id, ct);
        if (!exists)
            return NotFound(new { message = "Müfredat bulunamadı." });

        var rules = await context.CurriculumTermRules
            .AsNoTracking()
            .Where(r => r.CurriculumId == id)
            .OrderBy(r => r.ClassYear)
            .ThenBy(r => r.TermHalf)
            .ThenBy(r => r.RuleType)
            .Select(r => new CurriculumTermRuleDto(
                r.Id, r.CurriculumId, r.ClassYear, r.TermHalf, r.RuleType, r.Value, r.CreatedAt, r.UpdatedAt))
            .ToListAsync(ct);

        return Ok(rules);
    }

    [HttpPost("{id:int}/term-rules")]
    [RequirePermission("menu.mufredat-sinif-donem.create")]
    public async Task<IActionResult> CreateTermRule(int id, [FromBody] CreateTermRuleRequest req, CancellationToken ct)
    {
        var curriculum = await context.Curricula.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (curriculum is null)
            return NotFound(new { message = "Müfredat bulunamadı." });

        var duplicate = await context.CurriculumTermRules.AnyAsync(
            r => r.CurriculumId == id
              && r.ClassYear == req.ClassYear
              && r.TermHalf == req.TermHalf
              && r.RuleType == req.RuleType,
            ct);
        if (duplicate)
            return BadRequest(new { message = "Bu sınıf/dönem/kural kombinasyonu zaten tanımlı. Var olan kuralı düzenleyin." });

        if (req.Value < 0)
            return BadRequest(new { message = "Değer negatif olamaz." });

        var entity = new Domain.Entities.CurriculumTermRule
        {
            CurriculumId = id,
            ClassYear = req.ClassYear,
            TermHalf = req.TermHalf,
            RuleType = req.RuleType,
            Value = req.Value,
            CreatedAt = DateTime.UtcNow,
        };

        context.CurriculumTermRules.Add(entity);
        await context.SaveChangesAsync(ct);

        return Ok(new CurriculumTermRuleDto(
            entity.Id, entity.CurriculumId, entity.ClassYear, entity.TermHalf,
            entity.RuleType, entity.Value, entity.CreatedAt, entity.UpdatedAt));
    }

    [HttpPut("{id:int}/term-rules/{ruleId:int}")]
    [RequirePermission("menu.mufredat-sinif-donem.edit")]
    public async Task<IActionResult> UpdateTermRule(int id, int ruleId, [FromBody] UpdateTermRuleRequest req, CancellationToken ct)
    {
        var rule = await context.CurriculumTermRules
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.CurriculumId == id, ct);
        if (rule is null)
            return NotFound(new { message = "Kural bulunamadı." });

        if (req.Value < 0)
            return BadRequest(new { message = "Değer negatif olamaz." });

        rule.Value = req.Value;
        rule.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        return Ok(new { message = "Kural güncellendi." });
    }

    [HttpDelete("{id:int}/term-rules/{ruleId:int}")]
    [RequirePermission("menu.mufredat-sinif-donem.delete")]
    public async Task<IActionResult> DeleteTermRule(int id, int ruleId, CancellationToken ct)
    {
        var rule = await context.CurriculumTermRules
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.CurriculumId == id, ct);
        if (rule is null)
            return NotFound(new { message = "Kural bulunamadı." });

        context.CurriculumTermRules.Remove(rule);
        await context.SaveChangesAsync(ct);

        return NoContent();
    }

    // ── Madde 17: Öğrencinin havuz tamamlama durumu ──────────────
    // Faz C C5: SlotName + Faz C C4: SuggestedSemesterStart/End eklendi.
    // Faz E: deprecated Subtype string field kaldırıldı (frontend zaten kullanmıyordu).
    public record PoolProgressDto(
        int PoolId, string PoolCode, string PoolName,
        string SlotName,
        int SuggestedSemesterStart, int SuggestedSemesterEnd,
        int RequiredCourseCount, int CompletedCourseCount,
        int RequiredEcts, int EarnedEcts,
        int? EctsPerCourse,
        bool IsFulfilled);

    [HttpGet("student/{studentId:int}/pool-status")]
    [RequireAnyPermission("menu.ders-kayit.view", "menu.mufredat-islemleri.view")]
    // Patch 2A: Permission kaldırıldı; data multi-role (sadece authenticated).
    public async Task<IActionResult> GetPoolStatus(int studentId, CancellationToken ct)
    {
        // Öğrencinin aktif ana müfredatı
        if (User.IsInRole("Student"))
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim is null || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var ownStudentId = await context.Students
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .Select(s => s.Id)
                .FirstOrDefaultAsync(ct);

            if (ownStudentId == 0 || ownStudentId != studentId)
                return Forbid();
        }

        var studentProgram = await context.StudentPrograms
            .AsNoTracking()
            .Where(sp => sp.StudentId == studentId && sp.Type == StudentProgramType.Main && sp.IsActive)
            .FirstOrDefaultAsync(ct);
        if (studentProgram is null)
            return NotFound(new { message = "Öğrencinin aktif müfredatı bulunamadı." });

        var pools = await context.CurriculumElectivePools
            .AsNoTracking()
            .Include(p => p.ElectiveGroup)
            .Where(p => p.CurriculumId == studentProgram.CurriculumId)
            .ToListAsync(ct);

        // Öğrencinin AppliedElectivePoolId üzerinden havuzlara sayılan kayıtları
        var poolRegs = await context.CourseRegistrations
            .AsNoTracking()
            .Include(cr => cr.CourseSection).ThenInclude(cs => cs.Course)
            .Where(cr => cr.StudentId == studentId
                && cr.AppliedElectivePoolId != null
                && (cr.Status == RegistrationStatus.Active
                    || cr.Status == RegistrationStatus.PendingAdvisorApproval
                    || cr.Status == RegistrationStatus.Draft
                    || cr.Grade != null))
            .ToListAsync(ct);

        var result = pools.Select(p =>
        {
            var regs = poolRegs.Where(r => r.AppliedElectivePoolId == p.Id).ToList();
            var completedCount = regs.Count;
            var earnedEcts = regs.Sum(r => r.EctsSnapshot ?? r.CourseSection.Course.Ects);
            var isFulfilled = (p.RequiredCourseCount == 0 || completedCount >= p.RequiredCourseCount)
                           && (p.RequiredEcts == 0 || earnedEcts >= p.RequiredEcts);
            return new PoolProgressDto(
                p.Id,
                p.ElectiveGroup.Code,
                p.ElectiveGroup.Name,
                p.SlotName,
                p.SuggestedSemesterStart,
                p.SuggestedSemesterEnd,
                p.RequiredCourseCount,
                completedCount,
                p.RequiredEcts,
                earnedEcts,
                p.EctsPerCourse,
                isFulfilled);
        }).ToList();

        return Ok(result);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Slot adı doğrulaması + (curriculumId, electiveGroupId, slotName) uniqueness kontrolü.
    /// Geçerliyse trimmed slot adını döner. Geçersizse BadRequestObjectResult döner.
    /// </summary>
    /// <param name="excludePoolId">Update senaryosunda kendi kaydını dışla (Create için null).</param>
    private async Task<(string? Trimmed, IActionResult? Error)> ValidateAndResolveSlotNameAsync(
        int curriculumId,
        int electiveGroupId,
        string? slotName,
        int? excludePoolId,
        CancellationToken ct)
    {
        var trimmed = slotName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
            return (null, new BadRequestObjectResult(new { message = "Slot adı boş olamaz." }));
        if (trimmed.Length > 50)
            return (null, new BadRequestObjectResult(new { message = "Slot adı 50 karakterden uzun olamaz." }));

        var conflict = await context.CurriculumElectivePools
            .AnyAsync(p => p.CurriculumId == curriculumId
                      && p.ElectiveGroupId == electiveGroupId
                      && p.SlotName == trimmed
                      && (excludePoolId == null || p.Id != excludePoolId.Value), ct);
        if (conflict)
            return (null, new BadRequestObjectResult(new { message = $"'{trimmed}' adlı slot bu havuza zaten bağlı." }));

        return (trimmed, null);
    }
}
