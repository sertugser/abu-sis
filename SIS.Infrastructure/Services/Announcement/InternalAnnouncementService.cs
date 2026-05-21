using Microsoft.EntityFrameworkCore;
using SIS.Application.Announcement.Configuration;
using SIS.Application.Announcement.DTOs;
using SIS.Application.Announcement.Security;
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
    private readonly IWebModuleControlService _webModuleControls;
    private readonly IAnnouncementSettingsProvider _settingsProvider;
    private readonly IAnnouncementHtmlSanitizer _sanitizer;
    private readonly IAnnouncementAttachmentService? _attachmentService;

    public InternalAnnouncementService(
        AppDbContext context,
        IAnnouncementSettingsProvider settingsProvider,
        IAnnouncementHtmlSanitizer sanitizer,
        IWebModuleSettingsProvider? webSettings = null,
        IAnnouncementAttachmentService? attachmentService = null,
        IWebModuleControlService? webModuleControls = null)
    {
        _context = context;
        _settingsProvider = settingsProvider;
        _sanitizer = sanitizer;
        _webSettings = webSettings ?? new NullWebModuleSettingsProvider();
        _webModuleControls = webModuleControls ?? new NullWebModuleControlService();
        _attachmentService = attachmentService;
    }

    // ── Active feed (kullanıcının görebileceği duyurular) ─────────────────────

    public async Task<List<AnnouncementListDto>> GetActiveForUserAsync(
        string userId,
        IEnumerable<string> roles,
        int? userDepartmentId,
        int? userFacultyId,
        IEnumerable<int> userEnrolledSectionIds,
        string? advisorUserId,
        int? userStudentYear,
        int? maxResults,
        CancellationToken ct = default)
    {
        var feedAccess = await _webModuleControls.EvaluateAsync("announcements-feed", ct: ct);
        if (!feedAccess.IsOpen)
            return [];
        var scope = BuildScope(userId, roles, userDepartmentId, userFacultyId, userEnrolledSectionIds, advisorUserId, userStudentYear);

        IQueryable<InternalAnnouncement> query = _context.InternalAnnouncements
            .AsNoTracking()
            .Include(a => a.TargetDepartment)
            .Include(a => a.TargetFaculty)
            .Include(a => a.TargetSection).ThenInclude(s => s!.Course)
            .Include(a => a.Category);

        query = ApplyActiveDateFilter(query, DateTime.UtcNow);
        query = ApplyVisibilityFilter(query, scope);

        var settings = await _settingsProvider.GetAsync(ct);
        query = settings.PinnedFirst
            ? query.OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.CreatedAt)
            : query.OrderByDescending(a => a.CreatedAt);

        // Caller (frontend) explicit max gönderdiyse onu kullan; aksi halde student için
        // admin tarafından yapılandırılmış varsayılana düş. Bu sayede bildirim zili
        // (max=10) student için 5'e düşürülmez ama widget'lar default davranışı korur.
        var effectiveMaxResults = maxResults
            ?? (scope.IsStudent ? (int?)settings.StudentTopCount : null);

        if (effectiveMaxResults is > 0)
            query = query.Take(effectiveMaxResults.Value);

        var items = await query.ToListAsync(ct);

        // Okundu durumu + attachment sayılarını tek sorguda batch fetch et (N+1 önlemi)
        var ids = items.Select(a => a.Id).ToList();
        var readSet         = await FetchReadSetAsync(userId, ids, ct);
        var attachmentCounts = await FetchAttachmentCountsAsync(ids, ct);

        return items
            .Select(a => MapToList(a, readSet.Contains(a.Id), attachmentCounts.GetValueOrDefault(a.Id)))
            .ToList();
    }

    // ── Management page feeds ─────────────────────────────────────────────────

    public async Task<AnnouncementDetailDto?> GetByIdAsync(int id, int? requestingUserId = null, CancellationToken ct = default)
    {
        var a = await _context.InternalAnnouncements
            .AsNoTracking()
            .Include(x => x.TargetDepartment)
            .Include(x => x.TargetFaculty)
            .Include(x => x.TargetSection).ThenInclude(s => s!.Course)
            .Include(x => x.Category)
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (a is null) return null;

        bool isReadByMe = false;
        if (requestingUserId is int uid)
        {
            isReadByMe = await _context.AnnouncementReads
                .AsNoTracking()
                .AnyAsync(r => r.AnnouncementId == id && r.UserId == uid, ct);
        }

        return MapToDetail(a, isReadByMe);
    }

    public async Task<List<AnnouncementListDto>> GetAllAsync(CancellationToken ct = default)
    {
        // Admin görünümünde "okundu" durumu anlamsız; her zaman false döner.
        var items = await _context.InternalAnnouncements
            .AsNoTracking()
            .Include(a => a.TargetDepartment)
            .Include(a => a.TargetFaculty)
            .Include(a => a.TargetSection).ThenInclude(s => s!.Course)
            .Include(a => a.Category)
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        var counts = await FetchAttachmentCountsAsync(items.Select(a => a.Id), ct);
        return items.Select(a => MapToList(a, isReadByMe: false, counts.GetValueOrDefault(a.Id))).ToList();
    }

    public async Task<List<AnnouncementListDto>> GetMyCreatedAsync(string userId, CancellationToken ct = default)
    {
        var items = await _context.InternalAnnouncements
            .AsNoTracking()
            .Include(a => a.TargetDepartment)
            .Include(a => a.TargetFaculty)
            .Include(a => a.TargetSection).ThenInclude(s => s!.Course)
            .Include(a => a.Category)
            .Where(a => a.CreatedBy == userId)
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        var counts = await FetchAttachmentCountsAsync(items.Select(a => a.Id), ct);
        return items.Select(a => MapToList(a, isReadByMe: false, counts.GetValueOrDefault(a.Id))).ToList();
    }

    public async Task<bool> CanUserSeeAsync(
        int announcementId,
        string userId,
        IEnumerable<string> roles,
        int? userDepartmentId,
        int? userFacultyId,
        IEnumerable<int> userEnrolledSectionIds,
        string? advisorUserId,
        int? userStudentYear,
        CancellationToken ct = default)
    {
        var scope = BuildScope(userId, roles, userDepartmentId, userFacultyId, userEnrolledSectionIds, advisorUserId, userStudentYear);
        var query = ApplyActiveDateFilter(_context.InternalAnnouncements.AsNoTracking(), DateTime.UtcNow);
        query = ApplyVisibilityFilter(query, scope);
        return await query.AnyAsync(a => a.Id == announcementId, ct);
    }

    // ── İstatistik (Madde 9) ──────────────────────────────────────────────────

    public async Task<AnnouncementStatsDto?> GetStatsAsync(int announcementId, int maxReaders = 20, CancellationToken ct = default)
    {
        const int trendDays = 14;

        var exists = await _context.InternalAnnouncements
            .AsNoTracking()
            .AnyAsync(a => a.Id == announcementId, ct);
        if (!exists) return null;

        var readsQuery = _context.AnnouncementReads
            .AsNoTracking()
            .Where(r => r.AnnouncementId == announcementId);

        var now = DateTime.UtcNow;
        var last24hCutoff = now.AddHours(-24);
        var trendCutoff   = now.AddDays(-trendDays);

        var readCount         = await readsQuery.CountAsync(ct);
        var acknowledgedCount = await readsQuery.CountAsync(r => r.AcknowledgedAt != null, ct);
        var readsInLast24h    = await readsQuery.CountAsync(r => r.ReadAt >= last24hCutoff, ct);
        var attachmentCount   = await _context.AnnouncementAttachments
            .CountAsync(a => a.AnnouncementId == announcementId, ct);

        DateTime? firstReadAt = null;
        DateTime? lastReadAt  = null;
        if (readCount > 0)
        {
            firstReadAt = await readsQuery.MinAsync(r => (DateTime?)r.ReadAt, ct);
            lastReadAt  = await readsQuery.MaxAsync(r => (DateTime?)r.ReadAt, ct);
        }

        // ── Günlük okuma trendi: son 14 gün, eksik günler 0 ile dolduruluyor ──
        var groupedRaw = await readsQuery
            .Where(r => r.ReadAt >= trendCutoff)
            .GroupBy(r => r.ReadAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var groupedMap = groupedRaw.ToDictionary(x => DateOnly.FromDateTime(x.Day), x => x.Count);

        var today      = DateOnly.FromDateTime(now);
        var dailyReads = Enumerable.Range(0, trendDays)
            .Select(offset =>
            {
                var day   = today.AddDays(-(trendDays - 1 - offset));
                var count = groupedMap.GetValueOrDefault(day);
                return new AnnouncementReadsByDayDto(day, count);
            })
            .ToList();

        var recentReaders = await readsQuery
            .OrderByDescending(r => r.ReadAt)
            .Take(maxReaders)
            .Join(
                _context.Users.AsNoTracking(),
                read => read.UserId,
                user => user.Id,
                (read, user) => new AnnouncementReaderDto(
                    read.UserId,
                    BuildDisplayName(user.FirstName, user.LastName, user.UserName),
                    user.Email,
                    read.ReadAt,
                    read.AcknowledgedAt))
            .ToListAsync(ct);

        return new AnnouncementStatsDto(
            announcementId, readCount, acknowledgedCount, attachmentCount,
            firstReadAt, lastReadAt, recentReaders, dailyReads, readsInLast24h);
    }

    private static string BuildDisplayName(string? firstName, string? lastName, string? userName)
    {
        var name = $"{firstName} {lastName}".Trim();
        if (!string.IsNullOrWhiteSpace(name)) return name;
        return userName ?? "(bilinmeyen)";
    }

    // ── Pop-up duyurular (Madde 6) ────────────────────────────────────────────

    public async Task<List<AnnouncementDetailDto>> GetPendingPopupsAsync(
        string userId,
        IEnumerable<string> roles,
        int? userDepartmentId,
        int? userFacultyId,
        IEnumerable<int> userEnrolledSectionIds,
        string? advisorUserId,
        int? userStudentYear,
        CancellationToken ct = default)
    {
        if (!int.TryParse(userId, out var uid))
            return new List<AnnouncementDetailDto>();

        var scope = BuildScope(userId, roles, userDepartmentId, userFacultyId, userEnrolledSectionIds, advisorUserId, userStudentYear);

        IQueryable<InternalAnnouncement> query = _context.InternalAnnouncements
            .AsNoTracking()
            .Include(a => a.TargetDepartment)
            .Include(a => a.TargetFaculty)
            .Include(a => a.TargetSection).ThenInclude(s => s!.Course)
            .Include(a => a.Category)
            .Include(a => a.Attachments);

        query = ApplyActiveDateFilter(query, DateTime.UtcNow);
        query = ApplyVisibilityFilter(query, scope);

        // Sadece pop-up olanlar; ve henüz "kapatma şartı" yerine getirilmemiş olanlar:
        //   • RequiresAcknowledgement=true  → AcknowledgedAt set olunca kapanır
        //   • RequiresAcknowledgement=false → AnnouncementRead kaydı (ReadAt) olunca kapanır
        var pending = await query
            .Where(a => a.IsPopup)
            .Where(a => !_context.AnnouncementReads.Any(r =>
                r.AnnouncementId == a.Id
                && r.UserId == uid
                && (!a.RequiresAcknowledgement || r.AcknowledgedAt != null)))
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return pending.Select(a => MapToDetail(a, isReadByMe: false)).ToList();
    }

    public async Task<bool> AcknowledgeAsync(int announcementId, int userId, CancellationToken ct = default)
    {
        var announcement = await _context.InternalAnnouncements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == announcementId, ct);
        if (announcement is null) return false;

        var now = DateTime.UtcNow;
        var existing = await _context.AnnouncementReads
            .FirstOrDefaultAsync(r => r.AnnouncementId == announcementId && r.UserId == userId, ct);

        if (existing is null)
        {
            _context.AnnouncementReads.Add(new AnnouncementRead
            {
                AnnouncementId = announcementId,
                UserId         = userId,
                ReadAt         = now,
                AcknowledgedAt = now,
            });
        }
        else if (existing.AcknowledgedAt is null)
        {
            existing.AcknowledgedAt = now;
            // ReadAt zaten varsa dokunma
        }
        else
        {
            return false; // zaten acknowledged
        }

        try
        {
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    // ── Read tracking ─────────────────────────────────────────────────────────

    public async Task<bool> MarkAsReadAsync(int announcementId, int userId, CancellationToken ct = default)
    {
        var exists = await _context.InternalAnnouncements
            .AsNoTracking()
            .AnyAsync(a => a.Id == announcementId, ct);
        if (!exists) return false;

        var alreadyRead = await _context.AnnouncementReads
            .AsNoTracking()
            .AnyAsync(r => r.AnnouncementId == announcementId && r.UserId == userId, ct);
        if (alreadyRead) return false;

        _context.AnnouncementReads.Add(new AnnouncementRead
        {
            AnnouncementId = announcementId,
            UserId         = userId,
            ReadAt         = DateTime.UtcNow,
        });

        try
        {
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Race condition: paralel çağrı zaten kaydı oluşturdu — idempotent davran
            return false;
        }
    }

    public async Task<int> MarkAllAsReadAsync(
        string userId,
        IEnumerable<string> roles,
        int? userDepartmentId,
        int? userFacultyId,
        IEnumerable<int> userEnrolledSectionIds,
        string? advisorUserId,
        int? userStudentYear,
        CancellationToken ct = default)
    {
        if (!int.TryParse(userId, out var uid))
            return 0;

        var scope = BuildScope(userId, roles, userDepartmentId, userFacultyId, userEnrolledSectionIds, advisorUserId, userStudentYear);

        var visibleIds = await BuildVisibleIdsQuery(scope, DateTime.UtcNow).ToListAsync(ct);
        if (visibleIds.Count == 0) return 0;

        var alreadyReadIds = await _context.AnnouncementReads
            .AsNoTracking()
            .Where(r => r.UserId == uid && visibleIds.Contains(r.AnnouncementId))
            .Select(r => r.AnnouncementId)
            .ToListAsync(ct);

        var unreadIds = visibleIds.Except(alreadyReadIds).ToList();
        if (unreadIds.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var id in unreadIds)
        {
            _context.AnnouncementReads.Add(new AnnouncementRead
            {
                AnnouncementId = id,
                UserId         = uid,
                ReadAt         = now,
            });
        }

        try
        {
            await _context.SaveChangesAsync(ct);
            return unreadIds.Count;
        }
        catch (DbUpdateException)
        {
            // Paralel çağrı bazılarını eklemiş olabilir — ne kadar kayıt olduğunu yeniden hesapla
            return await _context.AnnouncementReads
                .CountAsync(r => r.UserId == uid && unreadIds.Contains(r.AnnouncementId), ct);
        }
    }

    public async Task<int> GetUnreadCountAsync(
        string userId,
        IEnumerable<string> roles,
        int? userDepartmentId,
        int? userFacultyId,
        IEnumerable<int> userEnrolledSectionIds,
        string? advisorUserId,
        int? userStudentYear,
        CancellationToken ct = default)
    {
        if (!int.TryParse(userId, out var uid))
            return 0;

        var scope = BuildScope(userId, roles, userDepartmentId, userFacultyId, userEnrolledSectionIds, advisorUserId, userStudentYear);

        var visibleQuery = BuildVisibleIdsQuery(scope, DateTime.UtcNow);

        // Visible ve henüz okunmamış olanları say (LEFT JOIN, IsRead null)
        return await visibleQuery
            .Where(id => !_context.AnnouncementReads
                .Any(r => r.AnnouncementId == id && r.UserId == uid))
            .CountAsync(ct);
    }

    // ── Visibility helpers ───────────────────────────────────────────────────

    /// <summary>Rollerden ve scope parametrelerinden derive edilen kullanıcı görünüm bağlamı.</summary>
    private sealed record UserVisibilityScope(
        string UserId,
        bool IsStudent,
        bool IsInstructor,
        bool IsAcademicAdvisor,
        bool IsDeptMgr,
        bool IsDean,
        bool IsAdminStaff,
        bool IsSystemAdmin,
        bool IsManagement,
        int? UserDepartmentId,
        int? UserFacultyId,
        List<int> EnrolledSectionIds,
        string? AdvisorUserId,
        int? UserStudentYear);

    private static UserVisibilityScope BuildScope(
        string userId,
        IEnumerable<string> roles,
        int? userDepartmentId,
        int? userFacultyId,
        IEnumerable<int> userEnrolledSectionIds,
        string? advisorUserId,
        int? userStudentYear)
    {
        var roleSet           = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var isStudent         = roleSet.Contains("Student");
        var isInstructor      = roleSet.Contains("Instructor");
        var isAcademicAdvisor = roleSet.Contains("AcademicAdvisor");
        var isDeptMgr         = roleSet.Contains("DepartmentManager");
        var isDean            = roleSet.Contains("Dean");
        var isAdminStaff      = roleSet.Contains("AdministrativeStaff");
        var isSystemAdmin     = roleSet.Contains("SystemAdministrator");
        var isManagement      = isDeptMgr || isDean || isSystemAdmin || isAdminStaff;

        return new UserVisibilityScope(
            userId,
            isStudent, isInstructor, isAcademicAdvisor,
            isDeptMgr, isDean, isAdminStaff, isSystemAdmin, isManagement,
            userDepartmentId, userFacultyId,
            userEnrolledSectionIds.ToList(),
            advisorUserId, userStudentYear);
    }

    /// <summary>Sadece aktif tarih aralığındaki duyuruları döndüren ortak filtre.</summary>
    private static IQueryable<InternalAnnouncement> ApplyActiveDateFilter(
        IQueryable<InternalAnnouncement> source, DateTime now) =>
        source.Where(a => a.IsActive
                       && a.StartsAt <= now
                       && (a.ExpiresAt == null || a.ExpiresAt > now));

    /// <summary>
    /// Kullanıcının görmesi gereken duyuruları audience hiyerarşisine göre filtreler.
    /// Tek yerde tanımlı, her kullanım için ortak (Active feed, MarkAllAsRead, UnreadCount).
    /// </summary>
    private static IQueryable<InternalAnnouncement> ApplyVisibilityFilter(
        IQueryable<InternalAnnouncement> source,
        UserVisibilityScope scope)
    {
        var userId            = scope.UserId;
        var sectionIds        = scope.EnrolledSectionIds;
        var userDepartmentId  = scope.UserDepartmentId;
        var userFacultyId     = scope.UserFacultyId;
        var advisorUserId     = scope.AdvisorUserId;
        var userStudentYear   = scope.UserStudentYear;
        var isStudent         = scope.IsStudent;
        var isInstructor      = scope.IsInstructor;
        var isAcademicAdvisor = scope.IsAcademicAdvisor;
        var isDeptMgr         = scope.IsDeptMgr;
        var isDean            = scope.IsDean;
        var isAdminStaff      = scope.IsAdminStaff;
        var isSystemAdmin     = scope.IsSystemAdmin;
        var isManagement      = scope.IsManagement;

        return source.Where(a =>
            // Yazar her zaman kendi duyurusunu görür (önizleme + doğrulama amacıyla)
            a.CreatedBy == userId

            || a.TargetAudience == AnnouncementAudience.All
            || (a.TargetAudience == AnnouncementAudience.Students   && isStudent
                && (a.TargetStudentYear == null || a.TargetStudentYear == userStudentYear))
            || (a.TargetAudience == AnnouncementAudience.Instructors && (isInstructor || isAcademicAdvisor))
            || (a.TargetAudience == AnnouncementAudience.Management  && isManagement)

            || (a.TargetAudience == AnnouncementAudience.Department
                && userDepartmentId.HasValue && a.TargetDepartmentId == userDepartmentId)
            || (a.TargetAudience == AnnouncementAudience.DepartmentStudents
                && isStudent && userDepartmentId.HasValue && a.TargetDepartmentId == userDepartmentId
                && (a.TargetStudentYear == null || a.TargetStudentYear == userStudentYear))
            || (a.TargetAudience == AnnouncementAudience.DepartmentInstructors
                && (isInstructor || isAcademicAdvisor) && userDepartmentId.HasValue && a.TargetDepartmentId == userDepartmentId)

            || (a.TargetAudience == AnnouncementAudience.Faculty
                && userFacultyId.HasValue && a.TargetFacultyId == userFacultyId)
            || (a.TargetAudience == AnnouncementAudience.FacultyStudents
                && isStudent && userFacultyId.HasValue && a.TargetFacultyId == userFacultyId
                && (a.TargetStudentYear == null || a.TargetStudentYear == userStudentYear))
            || (a.TargetAudience == AnnouncementAudience.FacultyInstructors
                && (isInstructor || isAcademicAdvisor) && userFacultyId.HasValue && a.TargetFacultyId == userFacultyId)
            || (a.TargetAudience == AnnouncementAudience.FacultyManagement
                && isDeptMgr && userFacultyId.HasValue && a.TargetFacultyId == userFacultyId)

            || (a.TargetAudience == AnnouncementAudience.CourseSection
                && isStudent
                && a.TargetSectionId != null
                && sectionIds.Contains(a.TargetSectionId.Value))

            || (a.TargetAudience == AnnouncementAudience.MyAdvisees
                && isStudent
                && advisorUserId != null
                && a.CreatedBy == advisorUserId)

            || (a.TargetAudience == AnnouncementAudience.AcademicAdvisors        && isAcademicAdvisor)
            || (a.TargetAudience == AnnouncementAudience.DepartmentManagers      && isDeptMgr)
            || (a.TargetAudience == AnnouncementAudience.Deans                   && isDean)
            || (a.TargetAudience == AnnouncementAudience.AdministrativeStaffRole && isAdminStaff)

            // "Hepsi" audience'ları: göndericinin altındaki tüm rolleri kapsar.
            // SysAdmin "AllForSysAdmin" yazdığında kendisi de görür (her zaman görür kuralıyla).
            || (a.TargetAudience == AnnouncementAudience.AllForSysAdmin
                && (isSystemAdmin || isAdminStaff || isDean || isDeptMgr || isAcademicAdvisor || isInstructor || isStudent))
            || (a.TargetAudience == AnnouncementAudience.AllForAdmin
                && (isAdminStaff || isDean || isDeptMgr || isAcademicAdvisor || isInstructor || isStudent))
            || (a.TargetAudience == AnnouncementAudience.AllForDean
                && (isDean || isDeptMgr || isAcademicAdvisor || isInstructor || isStudent))
            || (a.TargetAudience == AnnouncementAudience.AllForDeptMgr
                && (isDeptMgr || isAcademicAdvisor || isInstructor || isStudent))
        );
    }

    private IQueryable<int> BuildVisibleIdsQuery(UserVisibilityScope scope, DateTime now)
    {
        var query = ApplyActiveDateFilter(_context.InternalAnnouncements.AsNoTracking(), now);
        query = ApplyVisibilityFilter(query, scope);
        return query.Select(a => a.Id);
    }

    /// <summary>Duyuru ID listesi için attachment sayılarını tek sorguda batch fetch eder.</summary>
    private async Task<Dictionary<int, int>> FetchAttachmentCountsAsync(
        IEnumerable<int> announcementIds,
        CancellationToken ct)
    {
        var ids = announcementIds.ToList();
        if (ids.Count == 0)
            return new Dictionary<int, int>();

        return await _context.AnnouncementAttachments
            .AsNoTracking()
            .Where(att => ids.Contains(att.AnnouncementId))
            .GroupBy(att => att.AnnouncementId)
            .Select(g => new { AnnouncementId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AnnouncementId, x => x.Count, ct);
    }

    /// <summary>Verilen kullanıcı için duyuru ID'lerinden okunmuş olanları tek sorguda getirir.</summary>
    private async Task<HashSet<int>> FetchReadSetAsync(
        string userId,
        IEnumerable<int> announcementIds,
        CancellationToken ct)
    {
        if (!int.TryParse(userId, out var uid))
            return [];

        var ids = announcementIds.ToList();
        if (ids.Count == 0) return [];

        return (await _context.AnnouncementReads
            .AsNoTracking()
            .Where(r => r.UserId == uid && ids.Contains(r.AnnouncementId))
            .Select(r => r.AnnouncementId)
            .ToListAsync(ct))
            .ToHashSet();
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
        var settings = await _settingsProvider.GetAsync(ct);

        var (normalizedTitle, normalizedContent, normalizedFormat) =
            ValidateAndNormalize(dto.Title, dto.Content, dto.ContentFormat, settings);

        var roleSet = senderRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Hangi rolün hangi hedef kitleyi seçebileceğini doğrula
        await ValidateHierarchicalRulesAsync(dto, roleSet, senderDepartmentId, senderFacultyId, createdBy, ct);

        var categoryId = await ResolveCategoryIdAsync(dto.CategoryId, ct);

        var announcement = new InternalAnnouncement
        {
            Title                   = normalizedTitle,
            Content                 = normalizedContent,
            ContentFormat           = normalizedFormat,
            CategoryId              = categoryId,
            TargetAudience          = dto.TargetAudience,
            TargetDepartmentId      = GetTargetDepartmentId(dto),
            TargetFacultyId         = GetTargetFacultyId(dto),
            TargetSectionId         = GetTargetSectionId(dto),
            TargetStudentYear       = GetTargetStudentYear(dto.TargetAudience, dto.TargetStudentYear, settings),
            IsPinned                = dto.IsPinned,
            IsPopup                 = dto.IsPopup,
            RequiresAcknowledgement = dto.IsPopup && dto.RequiresAcknowledgement,
            StartsAt                = dto.StartsAt ?? DateTime.UtcNow,
            ExpiresAt               = dto.ExpiresAt,
            CreatedBy               = createdBy,
            CreatedByName           = createdByName,
            CreatedAt               = DateTime.UtcNow,
            IsActive                = true,
        };

        _context.InternalAnnouncements.Add(announcement);
        await _context.SaveChangesAsync(ct);

        await _context.Entry(announcement).Reference(a => a.TargetDepartment).LoadAsync(ct);
        await _context.Entry(announcement).Reference(a => a.TargetFaculty).LoadAsync(ct);
        if (announcement.TargetSectionId.HasValue)
            await _context.Entry(announcement).Reference(a => a.TargetSection).LoadAsync(ct);
        return MapToDetail(announcement, isReadByMe: false);
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
            .Include(a => a.Attachments)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (announcement is null) return null;

        if (!isAdminOverride && announcement.CreatedBy != requestingUserId)
            throw new UnauthorizedAccessException("Yalnızca kendi oluşturduğunuz duyuruları düzenleyebilirsiniz.");

        var settings = await _settingsProvider.GetAsync(ct);
        var (normalizedTitle, normalizedContent, normalizedFormat) =
            ValidateAndNormalize(dto.Title, dto.Content, dto.ContentFormat, settings);

        var categoryId = await ResolveCategoryIdAsync(dto.CategoryId, ct);

        announcement.Title                   = normalizedTitle;
        announcement.Content                 = normalizedContent;
        announcement.ContentFormat           = normalizedFormat;
        announcement.CategoryId              = categoryId;
        announcement.TargetAudience          = dto.TargetAudience;
        announcement.TargetDepartmentId      = GetTargetDepartmentId(dto);
        announcement.TargetFacultyId         = GetTargetFacultyId(dto);
        announcement.TargetSectionId         = GetTargetSectionId(dto);
        announcement.TargetStudentYear       = GetTargetStudentYear(dto.TargetAudience, dto.TargetStudentYear, settings);
        announcement.IsPinned                = dto.IsPinned;
        announcement.IsPopup                 = dto.IsPopup;
        announcement.RequiresAcknowledgement = dto.IsPopup && dto.RequiresAcknowledgement;
        announcement.StartsAt                = dto.StartsAt;
        announcement.ExpiresAt               = dto.ExpiresAt;
        announcement.UpdatedAt               = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        await _context.Entry(announcement).Reference(a => a.TargetDepartment).LoadAsync(ct);
        await _context.Entry(announcement).Reference(a => a.TargetFaculty).LoadAsync(ct);
        if (announcement.TargetSectionId.HasValue)
            await _context.Entry(announcement).Reference(a => a.TargetSection).LoadAsync(ct);
        return MapToDetail(announcement, isReadByMe: false);
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

        // Önce ekleri fiziksel olarak temizle (DB cascade satırları siler, dosyalar kalır).
        // Attachment service null olabilir (eski test ortamı) — null-safe.
        if (_attachmentService is not null)
        {
            await _attachmentService.DeleteAllForAnnouncementAsync(id, ct);
        }

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
        var settings = await _settingsProvider.GetAsync(ct);

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
                Array.Empty<AnnouncementSectionOptionDto>(),
                settings
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
                Array.Empty<AnnouncementSectionOptionDto>(),
                settings
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
                Array.Empty<AnnouncementSectionOptionDto>(),
                settings
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
                Array.Empty<AnnouncementSectionOptionDto>(),
                settings
            );
        }

        // ── AcademicAdvisor → yalnızca danışmanı olduğu öğrenciler ──────────
        if (roleSet.Contains("academicadvisor"))
        {
            return new AnnouncementCreateContextDto(
                "Instructor",
                new[] { AnnouncementAudience.MyAdvisees },
                null, null, null, null,
                Array.Empty<AnnouncementSectionOptionDto>(),
                settings
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
                )).ToList(),
                settings
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

    /// <summary>
    /// Sınıf filtresi yalnızca öğrenci hedefli audience'larda anlamlıdır. Aksi halde null'a sıfırlanır.
    /// Değer settings.StudentYearFilterMin/Max aralığı dışındaysa <see cref="InvalidOperationException"/> fırlatır.
    /// </summary>
    private static int? GetTargetStudentYear(
        AnnouncementAudience audience,
        int? rawYear,
        AnnouncementSettings settings)
    {
        var supportsYearFilter = audience is AnnouncementAudience.Students
            or AnnouncementAudience.DepartmentStudents
            or AnnouncementAudience.FacultyStudents;

        if (!supportsYearFilter || rawYear is null)
            return null;

        var year = rawYear.Value;
        if (year < settings.StudentYearFilterMin || year > settings.StudentYearFilterMax)
            throw new InvalidOperationException(
                $"Sınıf filtresi {settings.StudentYearFilterMin}-{settings.StudentYearFilterMax} aralığında olmalıdır.");

        return year;
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static string? BuildSectionInfo(InternalAnnouncement a) =>
        a.TargetSection is { } s
            ? $"{s.Course?.Name ?? "?"} - Şube {a.TargetSection.SectionNumber}"
            : null;

    private static AnnouncementListDto MapToList(InternalAnnouncement a, bool isReadByMe, int attachmentCount)
    {
        var preview = BuildPreview(a.Content, a.ContentFormat);
        return new AnnouncementListDto(
            a.Id, a.Title, preview, a.ContentFormat,
            a.TargetAudience,
            a.TargetDepartmentId, a.TargetDepartment?.Name,
            a.TargetFacultyId,    a.TargetFaculty?.Name,
            a.TargetSectionId,    BuildSectionInfo(a),
            a.TargetStudentYear,
            MapCategorySummary(a.Category),
            a.IsPinned, a.IsPopup, a.RequiresAcknowledgement,
            a.StartsAt, a.ExpiresAt,
            a.CreatedByName, a.CreatedAt, a.IsActive, isReadByMe, attachmentCount);
    }

    private static AnnouncementDetailDto MapToDetail(InternalAnnouncement a, bool isReadByMe) =>
        new(a.Id, a.Title, a.Content, a.ContentFormat,
            a.TargetAudience,
            a.TargetDepartmentId, a.TargetDepartment?.Name,
            a.TargetFacultyId,    a.TargetFaculty?.Name,
            a.TargetSectionId,    BuildSectionInfo(a),
            a.TargetStudentYear,
            MapCategorySummary(a.Category),
            a.IsPinned, a.IsPopup, a.RequiresAcknowledgement,
            a.StartsAt, a.ExpiresAt,
            a.CreatedByName, a.CreatedAt, a.UpdatedAt, a.IsActive, isReadByMe,
            (a.Attachments ?? new List<AnnouncementAttachment>())
                .OrderBy(att => att.UploadedAt)
                .Select(att => new AnnouncementAttachmentSummaryDto(
                    att.Id, att.FileName, att.MimeType, att.SizeBytes, att.UploadedAt))
                .ToList());

    private static AnnouncementCategorySummaryDto? MapCategorySummary(AnnouncementCategory? c) =>
        c is null ? null : new AnnouncementCategorySummaryDto(c.Id, c.Name, c.ColorHex, c.IconName);

    /// <summary>
    /// CategoryId varsa veritabanında olup olmadığını doğrular. Bulunamazsa hata fırlatır.
    /// </summary>
    private async Task<int?> ResolveCategoryIdAsync(int? rawCategoryId, CancellationToken ct)
    {
        if (rawCategoryId is null) return null;

        var exists = await _context.AnnouncementCategories
            .AsNoTracking()
            .AnyAsync(c => c.Id == rawCategoryId.Value, ct);
        if (!exists)
            throw new InvalidOperationException($"Seçilen kategori bulunamadı (ID: {rawCategoryId.Value}).");

        return rawCategoryId;
    }

    /// <summary>
    /// Liste preview için HTML içerikten plain text üretir. Plain format değişmez.
    /// </summary>
    private static string BuildPreview(string content, string contentFormat)
    {
        const int previewLength = 200;

        var text = string.Equals(contentFormat, AnnouncementSanitizationPolicy.ContentFormats.Html, StringComparison.OrdinalIgnoreCase)
            ? StripHtml(content)
            : content;

        return text.Length > previewLength
            ? text[..previewLength] + "..."
            : text;
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);
        var text = System.Net.WebUtility.HtmlDecode(doc.DocumentNode.InnerText ?? string.Empty);
        return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
    }

    /// <summary>
    /// Title/Content/Format'i sınırlar dahilinde doğrular ve HTML ise sanitize eder.
    /// </summary>
    private (string Title, string Content, string Format) ValidateAndNormalize(
        string? rawTitle,
        string? rawContent,
        string? rawFormat,
        AnnouncementSettings settings)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
            throw new InvalidOperationException("Duyuru başlığı boş olamaz.");
        if (string.IsNullOrWhiteSpace(rawContent))
            throw new InvalidOperationException("Duyuru içeriği boş olamaz.");

        var title = rawTitle.Trim();
        if (title.Length > settings.MaxTitleLength)
            throw new InvalidOperationException(
                $"Başlık en fazla {settings.MaxTitleLength} karakter olabilir.");

        var format = NormalizeContentFormat(rawFormat);
        var content = rawContent.Trim();

        if (format == AnnouncementSanitizationPolicy.ContentFormats.Html)
        {
            content = _sanitizer.Sanitize(
                content,
                allowImages: settings.AllowImagesInContent,
                allowTables: settings.AllowTablesInContent);
        }

        if (content.Length > settings.MaxContentLength)
            throw new InvalidOperationException(
                $"İçerik en fazla {settings.MaxContentLength} karakter olabilir.");

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Duyuru içeriği boş olamaz.");

        return (title, content, format);
    }

    private static string NormalizeContentFormat(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return AnnouncementSanitizationPolicy.ContentFormats.Plain;

        var trimmed = raw.Trim().ToLowerInvariant();
        return trimmed switch
        {
            AnnouncementSanitizationPolicy.ContentFormats.Html  => AnnouncementSanitizationPolicy.ContentFormats.Html,
            AnnouncementSanitizationPolicy.ContentFormats.Plain => AnnouncementSanitizationPolicy.ContentFormats.Plain,
            _ => throw new InvalidOperationException(
                $"Geçersiz içerik formatı: '{raw}'. İzin verilen: plain, html."),
        };
    }
}
