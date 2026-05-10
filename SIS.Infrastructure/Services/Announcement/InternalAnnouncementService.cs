using Microsoft.EntityFrameworkCore;
using SIS.Application.Announcement.DTOs;
using SIS.Application.Announcement.Services;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.Announcement;

public class InternalAnnouncementService : IInternalAnnouncementService
{
    private readonly AppDbContext _context;
    private readonly IWebModuleSettingsProvider _webSettings;

    public InternalAnnouncementService(AppDbContext context, IWebModuleSettingsProvider? webSettings = null)
    {
        _context = context;
        _webSettings = webSettings ?? new NullWebModuleSettingsProvider();
    }

    // ── Active feed (kullanıcının görebileceği duyurular) ─────────────────────

    public async Task<List<AnnouncementListDto>> GetActiveForUserAsync(
        string userId,
        IEnumerable<string> roles,
        int? userDepartmentId,
        int? userFacultyId,
        IEnumerable<int> userEnrolledSectionIds,
        string? advisorUserId,
        int? maxResults,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var roleSet = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var isStudent         = roleSet.Contains("Student");
        var isInstructor      = roleSet.Contains("Instructor");
        var isAcademicAdvisor = roleSet.Contains("AcademicAdvisor");
        var isDeptMgr         = roleSet.Contains("DepartmentManager");
        var isDean            = roleSet.Contains("Dean");
        var isAdminStaff      = roleSet.Contains("AdministrativeStaff");
        var isManagement      = isDeptMgr
            || isDean
            || roleSet.Contains("SystemAdministrator")
            || isAdminStaff;

        var sectionIds = userEnrolledSectionIds.ToList();

        var query = _context.InternalAnnouncements
            .AsNoTracking()
            .Include(a => a.TargetDepartment)
            .Include(a => a.TargetFaculty)
            .Include(a => a.TargetSection).ThenInclude(s => s!.Course)
            .Where(a => a.IsActive
                && a.StartsAt <= now
                && (a.ExpiresAt == null || a.ExpiresAt > now));

        query = query.Where(a =>
            // ── System-wide ──────────────────────────────────────────────────
            a.TargetAudience == AnnouncementAudience.All
            || (a.TargetAudience == AnnouncementAudience.Students   && isStudent)
            || (a.TargetAudience == AnnouncementAudience.Instructors && (isInstructor || isAcademicAdvisor))
            || (a.TargetAudience == AnnouncementAudience.Management  && isManagement)

            // ── Department-scoped (geriye dönük uyumluluk) ───────────────────
            || (a.TargetAudience == AnnouncementAudience.Department
                && userDepartmentId.HasValue && a.TargetDepartmentId == userDepartmentId)
            || (a.TargetAudience == AnnouncementAudience.DepartmentStudents
                && isStudent && userDepartmentId.HasValue && a.TargetDepartmentId == userDepartmentId)
            || (a.TargetAudience == AnnouncementAudience.DepartmentInstructors
                && (isInstructor || isAcademicAdvisor) && userDepartmentId.HasValue && a.TargetDepartmentId == userDepartmentId)

            // ── Faculty-scoped (geriye dönük uyumluluk) ──────────────────────
            || (a.TargetAudience == AnnouncementAudience.Faculty
                && userFacultyId.HasValue && a.TargetFacultyId == userFacultyId)
            || (a.TargetAudience == AnnouncementAudience.FacultyStudents
                && isStudent && userFacultyId.HasValue && a.TargetFacultyId == userFacultyId)
            || (a.TargetAudience == AnnouncementAudience.FacultyInstructors
                && (isInstructor || isAcademicAdvisor) && userFacultyId.HasValue && a.TargetFacultyId == userFacultyId)
            || (a.TargetAudience == AnnouncementAudience.FacultyManagement
                && isDeptMgr && userFacultyId.HasValue && a.TargetFacultyId == userFacultyId)

            // ── Course-section-scoped ─────────────────────────────────────────
            || (a.TargetAudience == AnnouncementAudience.CourseSection
                && isStudent
                && a.TargetSectionId != null
                && sectionIds.Contains(a.TargetSectionId.Value))

            // ── Advisor-scoped (danışmanın kendi öğrencileri) ─────────────────
            || (a.TargetAudience == AnnouncementAudience.MyAdvisees
                && isStudent
                && advisorUserId != null
                && a.CreatedBy == advisorUserId)

            // ── Flat rol bazlı (yeni sistem) ──────────────────────────────────
            || (a.TargetAudience == AnnouncementAudience.AcademicAdvisors        && isAcademicAdvisor)
            || (a.TargetAudience == AnnouncementAudience.DepartmentManagers      && isDeptMgr)
            || (a.TargetAudience == AnnouncementAudience.Deans                   && isDean)
            || (a.TargetAudience == AnnouncementAudience.AdministrativeStaffRole && isAdminStaff)

            // ── Rol kapsamı "Hepsi" değerleri ────────────────────────────────
            || (a.TargetAudience == AnnouncementAudience.AllForSysAdmin
                && (isAdminStaff || isDean || isDeptMgr || isAcademicAdvisor || isInstructor || isStudent))
            || (a.TargetAudience == AnnouncementAudience.AllForAdmin
                && (isDean || isDeptMgr || isAcademicAdvisor || isInstructor || isStudent))
            || (a.TargetAudience == AnnouncementAudience.AllForDean
                && (isDeptMgr || isAcademicAdvisor || isInstructor || isStudent))
            || (a.TargetAudience == AnnouncementAudience.AllForDeptMgr
                && (isAcademicAdvisor || isInstructor || isStudent))
        );

        var pinnedFirst = await _webSettings.GetBoolAsync("Announcements", "PinnedAnnouncementsFirst", true, ct);
        query = pinnedFirst
            ? query.OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.CreatedAt)
            : query.OrderByDescending(a => a.CreatedAt);

        var effectiveMaxResults = maxResults;
        if (isStudent && maxResults.HasValue)
        {
            effectiveMaxResults = await _webSettings.GetIntAsync(
                "Announcements", "StudentTopCount", maxResults.Value, ct);
        }

        if (effectiveMaxResults is > 0)
            query = query.Take(effectiveMaxResults.Value);

        var items = await query.ToListAsync(ct);
        return items.Select(MapToList).ToList();
    }

    // ── Management page feeds ─────────────────────────────────────────────────

    public async Task<AnnouncementDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var a = await _context.InternalAnnouncements
            .AsNoTracking()
            .Include(x => x.TargetDepartment)
            .Include(x => x.TargetFaculty)
            .Include(x => x.TargetSection).ThenInclude(s => s!.Course)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return a is null ? null : MapToDetail(a);
    }

    public async Task<List<AnnouncementListDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await _context.InternalAnnouncements
            .AsNoTracking()
            .Include(a => a.TargetDepartment)
            .Include(a => a.TargetFaculty)
            .Include(a => a.TargetSection).ThenInclude(s => s!.Course)
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
        return items.Select(MapToList).ToList();
    }

    public async Task<List<AnnouncementListDto>> GetMyCreatedAsync(string userId, CancellationToken ct = default)
    {
        var items = await _context.InternalAnnouncements
            .AsNoTracking()
            .Include(a => a.TargetDepartment)
            .Include(a => a.TargetFaculty)
            .Include(a => a.TargetSection).ThenInclude(s => s!.Course)
            .Where(a => a.CreatedBy == userId)
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
        return items.Select(MapToList).ToList();
    }

    // ── Create (hiyerarşik kural doğrulaması dahil) ───────────────────────────

    public async Task<AnnouncementDetailDto> CreateAsync(
        CreateAnnouncementDto dto,
        string createdBy,
        string createdByName,
        IEnumerable<string> senderRoles,
        int? senderDepartmentId,
        int? senderFacultyId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new InvalidOperationException("Duyuru başlığı boş olamaz.");
        if (string.IsNullOrWhiteSpace(dto.Content))
            throw new InvalidOperationException("Duyuru içeriği boş olamaz.");

        var roleSet = senderRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Hangi rolün hangi hedef kitleyi seçebileceğini doğrula
        await ValidateHierarchicalRulesAsync(dto, roleSet, senderDepartmentId, senderFacultyId, createdBy, ct);

        var announcement = new InternalAnnouncement
        {
            Title            = dto.Title.Trim(),
            Content          = dto.Content.Trim(),
            TargetAudience   = dto.TargetAudience,
            TargetDepartmentId = GetTargetDepartmentId(dto),
            TargetFacultyId   = GetTargetFacultyId(dto),
            TargetSectionId   = GetTargetSectionId(dto),
            IsPinned         = dto.IsPinned,
            StartsAt         = dto.StartsAt ?? DateTime.UtcNow,
            ExpiresAt        = dto.ExpiresAt,
            CreatedBy        = createdBy,
            CreatedByName    = createdByName,
            CreatedAt        = DateTime.UtcNow,
            IsActive         = true,
        };

        _context.InternalAnnouncements.Add(announcement);
        await _context.SaveChangesAsync(ct);

        await _context.Entry(announcement).Reference(a => a.TargetDepartment).LoadAsync(ct);
        await _context.Entry(announcement).Reference(a => a.TargetFaculty).LoadAsync(ct);
        if (announcement.TargetSectionId.HasValue)
            await _context.Entry(announcement).Reference(a => a.TargetSection).LoadAsync(ct);
        return MapToDetail(announcement);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task<AnnouncementDetailDto?> UpdateAsync(
        int id,
        UpdateAnnouncementDto dto,
        string requestingUserId,
        bool isAdminOverride,
        CancellationToken ct = default)
    {
        var announcement = await _context.InternalAnnouncements
            .Include(a => a.TargetDepartment)
            .Include(a => a.TargetFaculty)
            .Include(a => a.TargetSection)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (announcement is null) return null;

        if (!isAdminOverride && announcement.CreatedBy != requestingUserId)
            throw new UnauthorizedAccessException("Yalnızca kendi oluşturduğunuz duyuruları düzenleyebilirsiniz.");

        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new InvalidOperationException("Duyuru başlığı boş olamaz.");
        if (string.IsNullOrWhiteSpace(dto.Content))
            throw new InvalidOperationException("Duyuru içeriği boş olamaz.");

        announcement.Title            = dto.Title.Trim();
        announcement.Content          = dto.Content.Trim();
        announcement.TargetAudience   = dto.TargetAudience;
        announcement.TargetDepartmentId = GetTargetDepartmentId(dto);
        announcement.TargetFacultyId   = GetTargetFacultyId(dto);
        announcement.TargetSectionId   = GetTargetSectionId(dto);
        announcement.IsPinned         = dto.IsPinned;
        announcement.StartsAt         = dto.StartsAt;
        announcement.ExpiresAt        = dto.ExpiresAt;
        announcement.UpdatedAt        = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        await _context.Entry(announcement).Reference(a => a.TargetDepartment).LoadAsync(ct);
        await _context.Entry(announcement).Reference(a => a.TargetFaculty).LoadAsync(ct);
        if (announcement.TargetSectionId.HasValue)
            await _context.Entry(announcement).Reference(a => a.TargetSection).LoadAsync(ct);
        return MapToDetail(announcement);
    }

    // ── Delete (soft) ─────────────────────────────────────────────────────────

    public async Task<bool> DeleteAsync(int id, string requestingUserId, bool isAdminOverride, CancellationToken ct = default)
    {
        var announcement = await _context.InternalAnnouncements.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (announcement is null) return false;

        if (!isAdminOverride && announcement.CreatedBy != requestingUserId)
            throw new UnauthorizedAccessException("Yalnızca kendi oluşturduğunuz duyuruları pasife alabilirsiniz.");

        announcement.IsActive  = false;
        announcement.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    // ── Permanent delete ──────────────────────────────────────────────────────

    public async Task<bool> PermanentDeleteAsync(int id, string requestingUserId, bool isAdminOverride, CancellationToken ct = default)
    {
        var announcement = await _context.InternalAnnouncements.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (announcement is null) return false;

        if (!isAdminOverride && announcement.CreatedBy != requestingUserId)
            throw new UnauthorizedAccessException("Yalnızca kendi oluşturduğunuz duyuruları kalıcı olarak silebilirsiniz.");

        _context.InternalAnnouncements.Remove(announcement);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    // ── Reactivate (geri çekilmiş duyuruyu yeniden yayına al) ────────────────

    public async Task<bool> ReactivateAsync(int id, string requestingUserId, bool isAdminOverride, CancellationToken ct = default)
    {
        var announcement = await _context.InternalAnnouncements.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (announcement is null) return false;

        if (!isAdminOverride && announcement.CreatedBy != requestingUserId)
            throw new UnauthorizedAccessException("Yalnızca kendi oluşturduğunuz duyuruları yeniden yayına alabilirsiniz.");

        announcement.IsActive  = true;
        announcement.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    // ── Create context ────────────────────────────────────────────────────────

    public async Task<AnnouncementCreateContextDto> GetCreateContextAsync(
        string userId,
        IEnumerable<string> roles,
        CancellationToken ct = default)
    {
        var roleSet = senderRoleSet(roles);

        // ── SystemAdministrator → tüm roller (kendisi hariç) ─────────────────
        if (roleSet.Contains("systemadministrator"))
        {
            return new AnnouncementCreateContextDto(
                "Admin",
                new[]
                {
                    AnnouncementAudience.AllForSysAdmin,
                    AnnouncementAudience.AdministrativeStaffRole,
                    AnnouncementAudience.Deans,
                    AnnouncementAudience.DepartmentManagers,
                    AnnouncementAudience.AcademicAdvisors,
                    AnnouncementAudience.Instructors,
                    AnnouncementAudience.Students,
                },
                null, null, null, null,
                Array.Empty<AnnouncementSectionOptionDto>()
            );
        }

        // ── AdministrativeStaff → Dean ve aşağısı ────────────────────────────
        if (roleSet.Contains("administrativestaff"))
        {
            return new AnnouncementCreateContextDto(
                "Admin",
                new[]
                {
                    AnnouncementAudience.AllForAdmin,
                    AnnouncementAudience.Deans,
                    AnnouncementAudience.DepartmentManagers,
                    AnnouncementAudience.AcademicAdvisors,
                    AnnouncementAudience.Instructors,
                    AnnouncementAudience.Students,
                },
                null, null, null, null,
                Array.Empty<AnnouncementSectionOptionDto>()
            );
        }

        // ── Dean → DepartmentManager ve aşağısı ──────────────────────────────
        if (roleSet.Contains("dean"))
        {
            return new AnnouncementCreateContextDto(
                "Dean",
                new[]
                {
                    AnnouncementAudience.AllForDean,
                    AnnouncementAudience.DepartmentManagers,
                    AnnouncementAudience.AcademicAdvisors,
                    AnnouncementAudience.Instructors,
                    AnnouncementAudience.Students,
                },
                null, null, null, null,
                Array.Empty<AnnouncementSectionOptionDto>()
            );
        }

        // ── DepartmentManager → AcademicAdvisor ve aşağısı ───────────────────
        if (roleSet.Contains("departmentmanager"))
        {
            return new AnnouncementCreateContextDto(
                "DepartmentManager",
                new[]
                {
                    AnnouncementAudience.AllForDeptMgr,
                    AnnouncementAudience.AcademicAdvisors,
                    AnnouncementAudience.Instructors,
                    AnnouncementAudience.Students,
                },
                null, null, null, null,
                Array.Empty<AnnouncementSectionOptionDto>()
            );
        }

        // ── AcademicAdvisor → yalnızca danışmanı olduğu öğrenciler ──────────
        if (roleSet.Contains("academicadvisor"))
        {
            return new AnnouncementCreateContextDto(
                "Instructor",
                new[] { AnnouncementAudience.MyAdvisees },
                null, null, null, null,
                Array.Empty<AnnouncementSectionOptionDto>()
            );
        }

        // ── Instructor → yalnızca kendi ders şubesinin öğrencileri ───────────
        if (roleSet.Contains("instructor"))
        {
            Instructor? instructor = null;
            if (int.TryParse(userId, out var uid))
            {
                instructor = await _context.Instructors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.UserId == uid, ct);
            }

            var sections = instructor is null
                ? new List<CourseSection>()
                : await _context.CourseSections
                    .AsNoTracking()
                    .Include(cs => cs.Course)
                    .Include(cs => cs.Term)
                    .Where(cs => cs.InstructorId == instructor.Id
                              && cs.Status == CourseSectionStatus.Active)
                    .OrderBy(cs => cs.Course.Name)
                    .ThenBy(cs => cs.SectionNumber)
                    .ToListAsync(ct);

            return new AnnouncementCreateContextDto(
                "Instructor",
                new[] { AnnouncementAudience.CourseSection },
                null, null, null, null,
                sections.Select(s => new AnnouncementSectionOptionDto(
                    s.Id, s.Course.Name, s.Course.Code, s.SectionNumber, s.Term.ShortEnglishName
                )).ToList()
            );
        }

        throw new InvalidOperationException("Duyuru oluşturma yetkiniz bulunmuyor.");
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static HashSet<string> senderRoleSet(IEnumerable<string> roles) =>
        roles.Select(r => r.ToLowerInvariant()).ToHashSet();

    private static readonly HashSet<AnnouncementAudience> SysAdminAudiences =
    [
        AnnouncementAudience.AllForSysAdmin,
        AnnouncementAudience.AdministrativeStaffRole,
        AnnouncementAudience.Deans,
        AnnouncementAudience.DepartmentManagers,
        AnnouncementAudience.AcademicAdvisors,
        AnnouncementAudience.Instructors,
        AnnouncementAudience.Students,
    ];

    private static readonly HashSet<AnnouncementAudience> AdminStaffAudiences =
    [
        AnnouncementAudience.AllForAdmin,
        AnnouncementAudience.Deans,
        AnnouncementAudience.DepartmentManagers,
        AnnouncementAudience.AcademicAdvisors,
        AnnouncementAudience.Instructors,
        AnnouncementAudience.Students,
    ];

    private static readonly HashSet<AnnouncementAudience> DeanAudiences =
    [
        AnnouncementAudience.AllForDean,
        AnnouncementAudience.DepartmentManagers,
        AnnouncementAudience.AcademicAdvisors,
        AnnouncementAudience.Instructors,
        AnnouncementAudience.Students,
    ];

    private static readonly HashSet<AnnouncementAudience> DeptMgrAudiences =
    [
        AnnouncementAudience.AllForDeptMgr,
        AnnouncementAudience.AcademicAdvisors,
        AnnouncementAudience.Instructors,
        AnnouncementAudience.Students,
    ];

    private static readonly HashSet<AnnouncementAudience> InstructorAudiences =
    [
        AnnouncementAudience.CourseSection,
    ];

    private async Task ValidateHierarchicalRulesAsync(
        CreateAnnouncementDto dto,
        HashSet<string> roleSet,
        int? senderDepartmentId,
        int? senderFacultyId,
        string createdBy,
        CancellationToken ct)
    {
        var audience = dto.TargetAudience;

        // ── SystemAdministrator: her izin verilen hedef kitle ─────────────────
        if (roleSet.Contains("systemadministrator"))
        {
            if (!SysAdminAudiences.Contains(audience))
                throw new InvalidOperationException("Geçersiz hedef kitle.");
            return;
        }

        // ── AdministrativeStaff → Deans ve aşağısı ───────────────────────────
        if (roleSet.Contains("administrativestaff"))
        {
            if (!AdminStaffAudiences.Contains(audience))
                throw new InvalidOperationException("İdari personel yalnızca Dekanlar, Bölüm Başkanları, Akademik Danışmanlar, Öğretim Üyeleri veya Öğrencilere duyuru yayınlayabilir.");
            return;
        }

        // ── Dean → DepartmentManagers ve aşağısı ─────────────────────────────
        if (roleSet.Contains("dean"))
        {
            if (!DeanAudiences.Contains(audience))
                throw new InvalidOperationException("Dekan yalnızca Bölüm Başkanları, Akademik Danışmanlar, Öğretim Üyeleri veya Öğrencilere duyuru yayınlayabilir.");
            return;
        }

        // ── DepartmentManager → AcademicAdvisors ve aşağısı ──────────────────
        if (roleSet.Contains("departmentmanager"))
        {
            if (!DeptMgrAudiences.Contains(audience))
                throw new InvalidOperationException("Bölüm başkanı yalnızca Akademik Danışmanlar, Öğretim Üyeleri veya Öğrencilere duyuru yayınlayabilir.");
            return;
        }

        // ── AcademicAdvisor → yalnızca danışmanı olduğu öğrenciler ──────────
        if (roleSet.Contains("academicadvisor"))
        {
            if (audience != AnnouncementAudience.MyAdvisees)
                throw new InvalidOperationException("Akademik danışman yalnızca danışmanı olduğu öğrencilere duyuru yayınlayabilir.");
            return;
        }

        // ── Instructor → öğrenciler veya kendi ders şubesi ───────────────────
        if (roleSet.Contains("instructor"))
        {
            if (!InstructorAudiences.Contains(audience))
                throw new InvalidOperationException("Öğretim üyesi yalnızca öğrencilere veya kendi ders şubesine duyuru yayınlayabilir.");

            if (audience == AnnouncementAudience.CourseSection)
            {
                if (!dto.TargetSectionId.HasValue)
                    throw new InvalidOperationException("Hedef ders şubesi belirtilmelidir.");

                if (!int.TryParse(createdBy, out var uid))
                    throw new InvalidOperationException("Kullanıcı kimliği geçersiz.");

                var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.UserId == uid, ct);
                if (instructor is null)
                    throw new InvalidOperationException("Öğretim elemanı kaydı bulunamadı.");

                var sectionBelongs = await _context.CourseSections.AnyAsync(
                    s => s.Id == dto.TargetSectionId.Value && s.InstructorId == instructor.Id, ct);
                if (!sectionBelongs)
                    throw new InvalidOperationException("Belirtilen ders şubesi size ait değil.");
            }
            return;
        }

        throw new InvalidOperationException("Duyuru yayınlama yetkiniz bulunmuyor.");
    }

    // Yalnızca ilgili kitle tipinde geçerli FK'ler atanır; diğerleri null
    private static int? GetTargetDepartmentId(CreateAnnouncementDto dto) =>
        dto.TargetAudience is AnnouncementAudience.Department
            or AnnouncementAudience.DepartmentStudents
            or AnnouncementAudience.DepartmentInstructors
            ? dto.TargetDepartmentId : null;

    private static int? GetTargetDepartmentId(UpdateAnnouncementDto dto) =>
        dto.TargetAudience is AnnouncementAudience.Department
            or AnnouncementAudience.DepartmentStudents
            or AnnouncementAudience.DepartmentInstructors
            ? dto.TargetDepartmentId : null;

    private static int? GetTargetFacultyId(CreateAnnouncementDto dto) =>
        dto.TargetAudience is AnnouncementAudience.Faculty
            or AnnouncementAudience.FacultyStudents
            or AnnouncementAudience.FacultyInstructors
            or AnnouncementAudience.FacultyManagement
            ? dto.TargetFacultyId : null;

    private static int? GetTargetFacultyId(UpdateAnnouncementDto dto) =>
        dto.TargetAudience is AnnouncementAudience.Faculty
            or AnnouncementAudience.FacultyStudents
            or AnnouncementAudience.FacultyInstructors
            or AnnouncementAudience.FacultyManagement
            ? dto.TargetFacultyId : null;

    private static int? GetTargetSectionId(CreateAnnouncementDto dto) =>
        dto.TargetAudience == AnnouncementAudience.CourseSection ? dto.TargetSectionId : null;

    private static int? GetTargetSectionId(UpdateAnnouncementDto dto) =>
        dto.TargetAudience == AnnouncementAudience.CourseSection ? dto.TargetSectionId : null;

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static string? BuildSectionInfo(InternalAnnouncement a) =>
        a.TargetSection is { } s
            ? $"{s.Course?.Name ?? "?"} - Şube {a.TargetSection.SectionNumber}"
            : null;

    private static AnnouncementListDto MapToList(InternalAnnouncement a)
    {
        var preview = a.Content.Length > 200 ? a.Content[..200] + "..." : a.Content;
        return new AnnouncementListDto(
            a.Id, a.Title, preview,
            a.TargetAudience,
            a.TargetDepartmentId, a.TargetDepartment?.Name,
            a.TargetFacultyId,    a.TargetFaculty?.Name,
            a.TargetSectionId,    BuildSectionInfo(a),
            a.IsPinned, a.StartsAt, a.ExpiresAt,
            a.CreatedByName, a.CreatedAt, a.IsActive);
    }

    private static AnnouncementDetailDto MapToDetail(InternalAnnouncement a) =>
        new(a.Id, a.Title, a.Content,
            a.TargetAudience,
            a.TargetDepartmentId, a.TargetDepartment?.Name,
            a.TargetFacultyId,    a.TargetFaculty?.Name,
            a.TargetSectionId,    BuildSectionInfo(a),
            a.IsPinned, a.StartsAt, a.ExpiresAt,
            a.CreatedByName, a.CreatedAt, a.UpdatedAt, a.IsActive);
}
