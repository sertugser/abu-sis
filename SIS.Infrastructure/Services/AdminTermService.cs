using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Application.Common.Time;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AdminTermService : IAdminTermService
{
    private readonly AppDbContext _context;
    private readonly ITimeSlotService _timeSlotService;
    private readonly ISchedulingService _schedulingService;
    private readonly IDateTimeProvider _clock;

    public AdminTermService(
        AppDbContext context,
        ITimeSlotService timeSlotService,
        ISchedulingService schedulingService,
        IDateTimeProvider clock)
    {
        _context = context;
        _timeSlotService = timeSlotService;
        _schedulingService = schedulingService;
        _clock = clock;
    }

    public async Task<List<AdminTermDto>> GetAllTermsAsync()
    {
        var terms = await _context.AcademicTerms
            .Include(t => t.CalendarEvents)
            .OrderByDescending(t => t.Year)
            .ThenByDescending(t => t.Semester)
            .ToListAsync();
        return terms.Select(ToDto).ToList();
    }

    public async Task<AdminTermDto> GetTermByIdAsync(int id)
    {
        var term = await _context.AcademicTerms
            .Include(t => t.CalendarEvents)
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new KeyNotFoundException($"Dönem bulunamadı: {id}");
        return ToDto(term);
    }

    public async Task<AdminTermDto> CreateTermAsync(CreateTermDto dto)
    {
        var semester = ParseSemester(dto.Semester);
        var termType = ParseTermType(dto.TermType);

        var exists = await _context.AcademicTerms
            .AnyAsync(t => t.Year == dto.Year && t.Semester == semester);
        if (exists)
            throw new InvalidOperationException($"{dto.Year} yılı {dto.Semester} dönemi zaten mevcut.");

        ValidateTermDates(dto.RegistrationStart, dto.RegistrationEnd, dto.AddDropStart,
            dto.AddDropEnd, dto.TermStart, dto.TermEnd, dto.WithdrawalDeadline,
            dto.GradeEntryStart, dto.GradeEntryEnd);

        var term = new AcademicTerm
        {
            Name = dto.Name,
            ShortName = dto.ShortName,
            EnglishName = dto.EnglishName,
            Year = dto.Year,
            Semester = semester,
            TermType = termType,
            RegistrationStart = DateTime.SpecifyKind(dto.RegistrationStart, DateTimeKind.Utc),
            RegistrationEnd = DateTime.SpecifyKind(dto.RegistrationEnd, DateTimeKind.Utc),
            AddDropStart = DateTime.SpecifyKind(dto.AddDropStart, DateTimeKind.Utc),
            AddDropEnd = DateTime.SpecifyKind(dto.AddDropEnd, DateTimeKind.Utc),
            TermStart = DateTime.SpecifyKind(dto.TermStart, DateTimeKind.Utc),
            TermEnd = DateTime.SpecifyKind(dto.TermEnd, DateTimeKind.Utc),
            WithdrawalDeadline = DateTime.SpecifyKind(dto.WithdrawalDeadline, DateTimeKind.Utc),
            GradeEntryStart = DateTime.SpecifyKind(dto.GradeEntryStart, DateTimeKind.Utc),
            GradeEntryEnd = DateTime.SpecifyKind(dto.GradeEntryEnd, DateTimeKind.Utc),
            Status = TermStatus.Draft,
            CreatedAt = _clock.UtcNow
        };

        _context.AcademicTerms.Add(term);
        await _context.SaveChangesAsync();
        return ToDto(term);
    }

    public async Task<AdminTermDto> UpdateTermAsync(int id, UpdateTermDto dto)
    {
        var term = await _context.AcademicTerms.FindAsync(id)
            ?? throw new KeyNotFoundException($"Dönem bulunamadı: {id}");

        if (term.Status >= TermStatus.Closed)
            throw new InvalidOperationException("Kapalı veya arşivlenmiş dönemler düzenlenemez.");

        ValidateTermDates(dto.RegistrationStart, dto.RegistrationEnd, dto.AddDropStart,
            dto.AddDropEnd, dto.TermStart, dto.TermEnd, dto.WithdrawalDeadline,
            dto.GradeEntryStart, dto.GradeEntryEnd);

        term.Name = dto.Name;
        term.ShortName = dto.ShortName;
        term.EnglishName = dto.EnglishName;
        term.TermType = ParseTermType(dto.TermType);
        term.RegistrationStart = DateTime.SpecifyKind(dto.RegistrationStart, DateTimeKind.Utc);
        term.RegistrationEnd = DateTime.SpecifyKind(dto.RegistrationEnd, DateTimeKind.Utc);
        term.AddDropStart = DateTime.SpecifyKind(dto.AddDropStart, DateTimeKind.Utc);
        term.AddDropEnd = DateTime.SpecifyKind(dto.AddDropEnd, DateTimeKind.Utc);
        term.TermStart = DateTime.SpecifyKind(dto.TermStart, DateTimeKind.Utc);
        term.TermEnd = DateTime.SpecifyKind(dto.TermEnd, DateTimeKind.Utc);
        term.WithdrawalDeadline = DateTime.SpecifyKind(dto.WithdrawalDeadline, DateTimeKind.Utc);
        term.GradeEntryStart = DateTime.SpecifyKind(dto.GradeEntryStart, DateTimeKind.Utc);
        term.GradeEntryEnd = DateTime.SpecifyKind(dto.GradeEntryEnd, DateTimeKind.Utc);
        term.UpdatedAt = _clock.UtcNow;

        await _context.SaveChangesAsync();
        return ToDto(term);
    }

    public async Task TransitionStatusAsync(int id, string targetStatusStr)
    {
        var term = await _context.AcademicTerms.FindAsync(id)
            ?? throw new KeyNotFoundException($"Dönem bulunamadı: {id}");

        var targetStatus = ParseTermStatus(targetStatusStr);
        ValidateTransition(term.Status, targetStatus);
        await RunTransitionGuardsAsync(term, targetStatus);

        var oldStatus = term.Status;
        term.Status = targetStatus;
        term.UpdatedAt = _clock.UtcNow;

        // Draft → Preparing: TimeSlot grid'i oluştur
        if (targetStatus == TermStatus.Preparing && oldStatus == TermStatus.Draft)
        {
            await _timeSlotService.GenerateTimeSlotsForTermAsync(id);
        }

        // Audit interceptor otomatik olarak Term Modified'ı AuditLog'a yazacak
        await _context.SaveChangesAsync();
    }

    public async Task UpdatePhaseOverrideAsync(int id, UpdateTermPhaseOverrideDto dto, int? userId)
    {
        var term = await _context.AcademicTerms.FindAsync(id)
            ?? throw new KeyNotFoundException($"Dönem bulunamadı: {id}");

        if (term.Status != TermStatus.Ready)
            throw new InvalidOperationException("Override sadece Ready durumundaki dönemler için ayarlanabilir.");

        switch (dto.Phase)
        {
            case "Registration": term.RegistrationOverride = dto.Value; break;
            case "AddDrop":      term.AddDropOverride = dto.Value; break;
            case "Withdrawal":   term.WithdrawalOverride = dto.Value; break;
            case "GradeEntry":   term.GradeEntryOverride = dto.Value; break;
            default: throw new ArgumentException($"Geçersiz faz: {dto.Phase}");
        }

        term.UpdatedAt = _clock.UtcNow;
        // Audit interceptor otomatik olarak override değişikliğini AuditLog'a yazacak
        await _context.SaveChangesAsync();
    }

    private async Task RunTransitionGuardsAsync(AcademicTerm term, TermStatus target)
    {
        var errors = new List<string>();

        switch (target)
        {
            case TermStatus.Preparing when term.Status == TermStatus.Draft:
                // Guard 0: Temel tarih + derslik kontrolleri
                if (term.TermStart == default || term.TermEnd == default)
                    errors.Add("Dönem başlangıç ve bitiş tarihleri girilmelidir.");
                if (term.RegistrationStart == default || term.RegistrationEnd == default)
                    errors.Add("Kayıt başlangıç ve bitiş tarihleri girilmelidir.");
                if (term.GradeEntryStart == default || term.GradeEntryEnd == default)
                    errors.Add("Not girişi başlangıç ve bitiş tarihleri girilmelidir.");

                var hasClassroomAssignment = await _context.ClassroomTermAssignments
                    .AnyAsync(cta => cta.TermId == term.Id);
                if (!hasClassroomAssignment)
                    errors.Add("Derslik-fakülte ataması yapılmadan hazırlığa geçilemez.");
                break;

            case TermStatus.Ready:
                // Guard: Tek Ready invariant
                var otherReady = await _context.AcademicTerms
                    .AnyAsync(t => t.Id != term.Id && t.Status == TermStatus.Ready);
                if (otherReady)
                    errors.Add("Başka bir dönem zaten Ready (aktif) durumda. Önce onu Closed/Preparing'e alın.");

                var activeSections = await _context.CourseSections
                    .Where(cs => cs.TermId == term.Id && cs.IsActive)
                    .ToListAsync();

                if (activeSections.Count == 0)
                    errors.Add("Yayınlamak için en az bir aktif ders şubesi olmalıdır.");

                var activeSectionIds = activeSections.Select(s => s.Id).ToList();
                var scheduledIds = await _context.SectionSchedules
                    .AsNoTracking()
                    .Where(ss => activeSectionIds.Contains(ss.CourseSectionId))
                    .Select(ss => ss.CourseSectionId)
                    .Distinct()
                    .ToListAsync();
                var unscheduled = activeSections.Where(s => !scheduledIds.Contains(s.Id)).ToList();
                if (unscheduled.Count > 0)
                    errors.Add($"{unscheduled.Count} şubede zaman ataması yapılmamış.");

                var noClassroom = activeSections.Where(s => !s.ClassroomId.HasValue).ToList();
                if (noClassroom.Count > 0)
                    errors.Add($"{noClassroom.Count} şubede derslik atanmamış.");

                var noInst = activeSections.Where(s => !s.InstructorId.HasValue).ToList();
                if (noInst.Count > 0)
                    errors.Add($"{noInst.Count} şubede öğretim üyesi atanmamış.");

                var validation = await _schedulingService.ValidateExistingAsync(term.Id);
                if (!validation.IsValid && validation.Conflicts.Count > 0)
                    errors.Add($"{validation.Conflicts.Count} hard constraint çakışması var.");

                var facultiesWithSections = await _context.CourseSections
                    .Where(cs => cs.TermId == term.Id && cs.IsActive && cs.Course!.DepartmentId.HasValue)
                    .Select(cs => cs.Course!.Department!.FacultyId)
                    .Distinct()
                    .ToListAsync();

                var approvedFacultyIds = await _context.FacultyScheduleApprovals
                    .Where(fsa => fsa.TermId == term.Id)
                    .Select(fsa => fsa.FacultyId)
                    .ToListAsync();

                var unapproved = facultiesWithSections.Except(approvedFacultyIds).ToList();
                if (unapproved.Count > 0)
                {
                    var names = await _context.Faculties
                        .Where(f => unapproved.Contains(f.Id))
                        .Select(f => f.Name)
                        .ToListAsync();
                    errors.Add($"Şu fakültelerin dekan onayı eksik: {string.Join(", ", names)}");
                }
                break;

            case TermStatus.Closed:
                var unfinalized = await _context.CourseSections
                    .Where(cs => cs.TermId == term.Id && cs.IsActive && !cs.IsFinalized)
                    .CountAsync();
                if (unfinalized > 0)
                    errors.Add($"{unfinalized} şubede not girişi tamamlanmamış (finalize edilmemiş).");
                break;
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Geçiş engellendi ({GetStatusLabel(term.Status)} → {GetStatusLabel(target)}):\n• " +
                string.Join("\n• ", errors));
    }

    private static void ValidateTransition(TermStatus current, TermStatus target)
    {
        var allowed = GetAllowedTransitions(current);
        if (!allowed.Contains(target))
            throw new InvalidOperationException(
                $"'{GetStatusLabel(current)}' aşamasından '{GetStatusLabel(target)}' aşamasına geçiş yapılamaz. " +
                $"İzin verilen geçişler: {string.Join(", ", allowed.Select(GetStatusLabel))}");
    }

    private static TermStatus[] GetAllowedTransitions(TermStatus current) => current switch
    {
        TermStatus.Draft     => [TermStatus.Preparing],
        TermStatus.Preparing => [TermStatus.Ready, TermStatus.Draft],
        TermStatus.Ready     => [TermStatus.Preparing, TermStatus.Closed],
        TermStatus.Closed    => [TermStatus.Archived],
        TermStatus.Archived  => [],
        _                    => []
    };

    private static TermStatus ParseTermStatus(string s) => s switch
    {
        "Draft"     => TermStatus.Draft,
        "Preparing" => TermStatus.Preparing,
        "Ready"     => TermStatus.Ready,
        "Closed"    => TermStatus.Closed,
        "Archived"  => TermStatus.Archived,
        _ => throw new ArgumentException($"Geçersiz dönem durumu: {s}")
    };

    public static string GetStatusLabel(TermStatus s) => s switch
    {
        TermStatus.Draft     => "Taslak",
        TermStatus.Preparing => "Hazırlık",
        TermStatus.Ready     => "Yayında",
        TermStatus.Closed    => "Kapalı",
        TermStatus.Archived  => "Arşivlendi",
        _                    => s.ToString()
    };

    private static void ValidateTermDates(DateTime regStart, DateTime regEnd,
        DateTime addDropStart, DateTime addDropEnd,
        DateTime termStart, DateTime termEnd, DateTime withdrawalDeadline,
        DateTime gradeStart, DateTime gradeEnd)
    {
        if (regStart >= regEnd)
            throw new InvalidOperationException("Kayıt başlangıç tarihi bitiş tarihinden önce olmalıdır.");
        if (termStart >= termEnd)
            throw new InvalidOperationException("Dönem başlangıç tarihi bitiş tarihinden önce olmalıdır.");
        if (addDropStart >= addDropEnd)
            throw new InvalidOperationException("Ders ekleme/bırakma başlangıç tarihi bitiş tarihinden önce olmalıdır.");
        if (gradeStart >= gradeEnd)
            throw new InvalidOperationException("Not girişi başlangıç tarihi bitiş tarihinden önce olmalıdır.");
        if (regEnd > termEnd)
            throw new InvalidOperationException("Kayıt bitiş tarihi dönem bitiş tarihinden sonra olamaz.");
        if (addDropEnd > termEnd)
            throw new InvalidOperationException("Ders ekleme/bırakma bitiş tarihi dönem bitiş tarihinden sonra olamaz.");
        if (withdrawalDeadline > termEnd)
            throw new InvalidOperationException("Çekilme tarihi dönem bitiş tarihinden sonra olamaz.");
        if (withdrawalDeadline < termStart)
            throw new InvalidOperationException("Çekilme tarihi dönem başlangıç tarihinden önce olamaz.");
    }

    private AdminTermDto ToDto(AcademicTerm t)
    {
        var now = _clock.UtcNow;
        var generatedEvents = t.CalendarEvents.Where(e => e.IsGenerated).ToList();
        var generatedDraftId = generatedEvents
            .Select(e => e.GeneratedDraftId)
            .Where(id => id.HasValue)
            .Max();

        return new AdminTermDto(
            t.Id, t.Name, t.ShortName, t.EnglishName, t.Year,
            t.Semester.ToString(),
            t.Semester switch
            {
                SemesterType.Fall => "Güz",
                SemesterType.Spring => "Bahar",
                SemesterType.Summer => "Yaz",
                _ => t.Semester.ToString()
            },
            t.TermType.ToString(),
            t.TermType switch
            {
                TermType.Regular => "Normal",
                TermType.Summer => "Yaz",
                TermType.Intensive => "Tekders",
                TermType.Exemption => "Muafiyet",
                _ => t.TermType.ToString()
            },
            t.Status.ToString(),
            GetStatusLabel(t.Status),
            GetAllowedTransitions(t.Status).Select(s => s.ToString()).ToArray(),
            t.RegistrationStart, t.RegistrationEnd,
            t.AddDropStart, t.AddDropEnd,
            t.TermStart, t.TermEnd,
            t.WithdrawalDeadline,
            t.GradeEntryStart, t.GradeEntryEnd,
            t.RegistrationOverride, t.AddDropOverride, t.WithdrawalOverride, t.GradeEntryOverride,
            t.IsRegistrationOpen(now),
            t.IsAddDropOpen(now),
            t.IsWithdrawalOpen(now),
            t.IsGradeEntryOpen(now),
            t.IsActive,
            t.CalendarEvents.Count,
            generatedEvents.Count,
            generatedDraftId,
            generatedEvents.Count > 0
        );
    }

    private static SemesterType ParseSemester(string s) => s switch
    {
        "Fall" => SemesterType.Fall,
        "Spring" => SemesterType.Spring,
        "Summer" => SemesterType.Summer,
        _ => throw new ArgumentException($"Geçersiz dönem tipi: {s}")
    };

    private static TermType ParseTermType(string s) => s switch
    {
        "Regular" => TermType.Regular,
        "Summer" => TermType.Summer,
        "Intensive" => TermType.Intensive,
        "Exemption" => TermType.Exemption,
        _ => throw new ArgumentException($"Geçersiz dönem türü: {s}")
    };
}
