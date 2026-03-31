using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AdminTermService : IAdminTermService
{
    private readonly AppDbContext _context;

    public AdminTermService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<AdminTermDto>> GetAllTermsAsync()
    {
        var terms = await _context.AcademicTerms
            .OrderByDescending(t => t.Year)
            .ThenByDescending(t => t.Semester)
            .ToListAsync();

        return terms.Select(ToDto).ToList();
    }

    public async Task<AdminTermDto> GetTermByIdAsync(int id)
    {
        var term = await _context.AcademicTerms.FindAsync(id)
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
            throw new InvalidOperationException(
                $"{dto.Year} yılı {dto.Semester} dönemi zaten mevcut.");

        ValidateTermDates(dto.RegistrationStart, dto.RegistrationEnd, dto.AddDropStart,
            dto.AddDropEnd, dto.TermStart, dto.TermEnd, dto.WithdrawalDeadline);

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
            IsActive = false
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
            dto.AddDropEnd, dto.TermStart, dto.TermEnd, dto.WithdrawalDeadline);

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

        await _context.SaveChangesAsync();
        return ToDto(term);
    }

    public async Task ActivateTermAsync(int id)
    {
        var target = await _context.AcademicTerms.FindAsync(id)
            ?? throw new KeyNotFoundException($"Dönem bulunamadı: {id}");

        if (target.Status == TermStatus.Draft)
            throw new InvalidOperationException("Draft durumundaki dönem aktif edilemez. Önce Planning'e geçirilmelidir.");

        if (target.Status >= TermStatus.Closed)
            throw new InvalidOperationException("Kapalı veya arşivlenmiş dönem aktif edilemez.");

        var allActive = await _context.AcademicTerms
            .Where(t => t.IsActive)
            .ToListAsync();
        foreach (var t in allActive)
            t.IsActive = false;

        target.IsActive = true;
        await _context.SaveChangesAsync();
    }

    public async Task TransitionStatusAsync(int id, string targetStatusStr)
    {
        var term = await _context.AcademicTerms.FindAsync(id)
            ?? throw new KeyNotFoundException($"Dönem bulunamadı: {id}");

        var targetStatus = ParseTermStatus(targetStatusStr);
        ValidateTransition(term.Status, targetStatus);

        // Geçiş guard'ları
        await RunTransitionGuardsAsync(term, targetStatus);

        term.Status = targetStatus;

        // Published veya sonrasındaki state'e geçişte otomatik IsActive = true
        if (targetStatus >= TermStatus.Published)
        {
            var allActive = await _context.AcademicTerms
                .Where(t => t.IsActive && t.Id != id)
                .ToListAsync();
            foreach (var t in allActive)
                t.IsActive = false;
            term.IsActive = true;
        }

        // Closed/Archived olunca IsActive = false
        if (targetStatus >= TermStatus.Closed)
            term.IsActive = false;

        // RegistrationOpen → AddDrop: Submitted/Draft kayıtları otomatik iptal
        if (targetStatus == TermStatus.AddDrop)
        {
            var pendingRegistrations = await _context.CourseRegistrations
                .Include(cr => cr.CourseSection)
                .Where(cr => cr.CourseSection.TermId == id
                    && (cr.Status == RegistrationStatus.Draft || cr.Status == RegistrationStatus.PendingAdvisorApproval))
                .ToListAsync();

            foreach (var reg in pendingRegistrations)
            {
                reg.Status = RegistrationStatus.Dropped;
                reg.UpdatedAt = DateTime.UtcNow;
                if (reg.CourseSection.EnrolledCount > 0)
                    reg.CourseSection.EnrolledCount--;
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task RunTransitionGuardsAsync(AcademicTerm term, TermStatus target)
    {
        var errors = new List<string>();

        switch (target)
        {
            case TermStatus.Planning:
                // Guard: Takvim tarihleri girilmiş mi?
                if (term.TermStart == default || term.TermEnd == default)
                    errors.Add("Dönem başlangıç ve bitiş tarihleri girilmelidir.");
                if (term.RegistrationStart == default || term.RegistrationEnd == default)
                    errors.Add("Kayıt başlangıç ve bitiş tarihleri girilmelidir.");
                break;

            case TermStatus.Scheduling:
                // Guard: En az bir CourseSection açılmış mı? Her section'da Instructor atanmış mı?
                var sections = await _context.CourseSections
                    .Where(cs => cs.TermId == term.Id && cs.IsActive)
                    .ToListAsync();

                if (sections.Count == 0)
                    errors.Add("Scheduling'e geçmek için en az bir ders şubesi açılmış olmalıdır.");
                else
                {
                    var noInstructor = sections.Where(s => !s.InstructorId.HasValue).ToList();
                    if (noInstructor.Count > 0)
                        errors.Add($"{noInstructor.Count} şubede öğretim üyesi atanmamış.");
                }
                break;

            case TermStatus.Published:
                // Guard: Tüm aktif section'larda slot+derslik+hoca atanmış mı?
                var activeSections = await _context.CourseSections
                    .Where(cs => cs.TermId == term.Id && cs.IsActive)
                    .ToListAsync();

                if (activeSections.Count == 0)
                    errors.Add("Yayınlamak için en az bir aktif ders şubesi olmalıdır.");

                var unscheduled = activeSections.Where(s =>
                    string.IsNullOrEmpty(s.ScheduleJson) || s.ScheduleJson == "[]").ToList();
                if (unscheduled.Count > 0)
                    errors.Add($"{unscheduled.Count} şubede zaman/derslik ataması yapılmamış.");

                var noInst = activeSections.Where(s => !s.InstructorId.HasValue).ToList();
                if (noInst.Count > 0)
                    errors.Add($"{noInst.Count} şubede öğretim üyesi atanmamış.");
                break;

            case TermStatus.Closed:
                // Guard: Tüm aktif section'lar finalize edilmiş mi?
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
        TermStatus.Draft             => [TermStatus.Planning],
        TermStatus.Planning          => [TermStatus.Scheduling, TermStatus.Draft],
        TermStatus.Scheduling        => [TermStatus.Published, TermStatus.Planning],
        TermStatus.Published         => [TermStatus.RegistrationOpen, TermStatus.Scheduling],
        TermStatus.RegistrationOpen  => [TermStatus.AddDrop],
        TermStatus.AddDrop           => [TermStatus.WithdrawalPeriod],
        TermStatus.WithdrawalPeriod  => [TermStatus.GradeEntry],
        TermStatus.GradeEntry        => [TermStatus.Closed],
        TermStatus.Closed            => [TermStatus.Archived],
        TermStatus.Archived          => [],
        _                            => []
    };

    private static TermStatus ParseTermStatus(string s) => s switch
    {
        "Draft"            => TermStatus.Draft,
        "Planning"         => TermStatus.Planning,
        "Scheduling"       => TermStatus.Scheduling,
        "Published"        => TermStatus.Published,
        "RegistrationOpen" => TermStatus.RegistrationOpen,
        "AddDrop"          => TermStatus.AddDrop,
        "WithdrawalPeriod" => TermStatus.WithdrawalPeriod,
        "GradeEntry"       => TermStatus.GradeEntry,
        "Closed"           => TermStatus.Closed,
        "Archived"         => TermStatus.Archived,
        _ => throw new ArgumentException($"Geçersiz dönem durumu: {s}")
    };

    private static string GetStatusLabel(TermStatus s) => s switch
    {
        TermStatus.Draft            => "Taslak",
        TermStatus.Planning         => "Planlama",
        TermStatus.Scheduling       => "Programlama",
        TermStatus.Published        => "Yayınlandı",
        TermStatus.RegistrationOpen => "Kayıt Açık",
        TermStatus.AddDrop          => "Ders Ekle/Bırak",
        TermStatus.WithdrawalPeriod => "Çekilme Dönemi",
        TermStatus.GradeEntry       => "Not Girişi",
        TermStatus.Closed           => "Kapalı",
        TermStatus.Archived         => "Arşivlendi",
        _                           => s.ToString()
    };

    private static void ValidateTermDates(DateTime regStart, DateTime regEnd,
        DateTime addDropStart, DateTime addDropEnd,
        DateTime termStart, DateTime termEnd, DateTime withdrawalDeadline)
    {
        if (regStart >= regEnd)
            throw new InvalidOperationException("Kayıt başlangıç tarihi bitiş tarihinden önce olmalıdır.");
        if (termStart >= termEnd)
            throw new InvalidOperationException("Dönem başlangıç tarihi bitiş tarihinden önce olmalıdır.");
        if (addDropStart >= addDropEnd)
            throw new InvalidOperationException("Ders ekleme/bırakma başlangıç tarihi bitiş tarihinden önce olmalıdır.");
        if (regEnd > termEnd)
            throw new InvalidOperationException("Kayıt bitiş tarihi dönem bitiş tarihinden sonra olamaz.");
        if (addDropEnd > termEnd)
            throw new InvalidOperationException("Ders ekleme/bırakma bitiş tarihi dönem bitiş tarihinden sonra olamaz.");
        if (withdrawalDeadline > termEnd)
            throw new InvalidOperationException("Çekilme tarihi dönem bitiş tarihinden sonra olamaz.");
        if (withdrawalDeadline < termStart)
            throw new InvalidOperationException("Çekilme tarihi dönem başlangıç tarihinden önce olamaz.");
    }

    // ── Calendar Events ──────────────────────────────────────────────────────

    public async Task<List<TermCalendarEventDto>> GetCalendarEventsAsync(int termId)
    {
        _ = await _context.AcademicTerms.FindAsync(termId)
            ?? throw new KeyNotFoundException($"Dönem bulunamadı: {termId}");

        var events = await _context.TermCalendarEvents
            .Where(e => e.TermId == termId)
            .OrderBy(e => e.StartDate)
            .ToListAsync();

        return events.Select(ToEventDto).ToList();
    }

    public async Task<TermCalendarEventDto> CreateCalendarEventAsync(int termId, CreateCalendarEventDto dto)
    {
        var calTerm = await _context.AcademicTerms.FindAsync(termId)
            ?? throw new KeyNotFoundException($"Dönem bulunamadı: {termId}");
        if (calTerm.Status == TermStatus.Archived)
            throw new InvalidOperationException("Arşivlenmiş dönemde takvim etkinliği oluşturulamaz.");

        var ev = new TermCalendarEvent
        {
            TermId = termId,
            EventType = ParseCalendarEventType(dto.EventType),
            Title = dto.Title,
            StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc),
            EndDate = dto.EndDate.HasValue
                ? DateTime.SpecifyKind(dto.EndDate.Value, DateTimeKind.Utc)
                : null,
            IsPublic = dto.IsPublic,
            Note = dto.Note
        };

        _context.TermCalendarEvents.Add(ev);
        await _context.SaveChangesAsync();
        return ToEventDto(ev);
    }

    public async Task<TermCalendarEventDto> UpdateCalendarEventAsync(int termId, int eventId, UpdateCalendarEventDto dto)
    {
        var calTerm2 = await _context.AcademicTerms.FindAsync(termId);
        if (calTerm2?.Status == TermStatus.Archived)
            throw new InvalidOperationException("Arşivlenmiş dönemde takvim etkinliği düzenlenemez.");

        var ev = await _context.TermCalendarEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && e.TermId == termId)
            ?? throw new KeyNotFoundException($"Takvim etkinliği bulunamadı: {eventId}");

        ev.EventType = ParseCalendarEventType(dto.EventType);
        ev.Title = dto.Title;
        ev.StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc);
        ev.EndDate = dto.EndDate.HasValue
            ? DateTime.SpecifyKind(dto.EndDate.Value, DateTimeKind.Utc)
            : null;
        ev.IsPublic = dto.IsPublic;
        ev.Note = dto.Note;

        await _context.SaveChangesAsync();
        return ToEventDto(ev);
    }

    public async Task DeleteCalendarEventAsync(int termId, int eventId)
    {
        var calTerm3 = await _context.AcademicTerms.FindAsync(termId);
        if (calTerm3?.Status == TermStatus.Archived)
            throw new InvalidOperationException("Arşivlenmiş dönemde takvim etkinliği silinemez.");

        var ev = await _context.TermCalendarEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && e.TermId == termId)
            ?? throw new KeyNotFoundException($"Takvim etkinliği bulunamadı: {eventId}");

        _context.TermCalendarEvents.Remove(ev);
        await _context.SaveChangesAsync();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static AdminTermDto ToDto(AcademicTerm t)
    {
        var now = DateTime.UtcNow;
        return new AdminTermDto(
            t.Id,
            t.Name,
            t.ShortName,
            t.EnglishName,
            t.Year,
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
            t.IsActive,
            t.Status.ToString(),
            GetStatusLabel(t.Status),
            GetAllowedTransitions(t.Status).Select(s => s.ToString()).ToArray(),
            t.RegistrationStart,
            t.RegistrationEnd,
            t.AddDropStart,
            t.AddDropEnd,
            t.TermStart,
            t.TermEnd,
            t.WithdrawalDeadline,
            t.IsActive && now >= t.RegistrationStart && now <= t.RegistrationEnd,
            t.IsActive && now >= t.AddDropStart && now <= t.AddDropEnd
        );
    }

    private static TermCalendarEventDto ToEventDto(TermCalendarEvent e) =>
        new(
            e.Id,
            e.TermId,
            e.EventType.ToString(),
            e.EventType switch
            {
                CalendarEventType.CourseRegistration => "Ders Kayıt",
                CalendarEventType.NewStudentRegistration => "Yeni Öğrenci Ders Kayıt",
                CalendarEventType.AdvisorApproval => "Danışman Onayı",
                CalendarEventType.AddDrop => "Ders Ekle/Bırak",
                CalendarEventType.AddDropApproval => "Ders Ekle/Bırak Onayı",
                CalendarEventType.CourseWithdrawal => "Ders Çekilme",
                CalendarEventType.AdvisorWithdrawal => "Danışman Dersten Çekme",
                CalendarEventType.MidtermExam => "Ara Sınav",
                CalendarEventType.MidtermGradeEntry => "Ara Sınav Not Girişi",
                CalendarEventType.MidtermGradePublication => "Ara Sınav Not Yayımlama",
                CalendarEventType.FinalExam => "Dönem Sonu Sınavı",
                CalendarEventType.FinalGradeEntry => "Final Not Girişi",
                CalendarEventType.FinalGradePublication => "Final Not Yayımlama",
                CalendarEventType.MakeupExam => "Bütünleme Sınavı",
                CalendarEventType.MakeupGradeEntry => "Bütünleme Not Girişi",
                CalendarEventType.MakeupGradePublication => "Bütünleme Not Yayımlama",
                CalendarEventType.NationalHoliday => "Milli Tatil",
                CalendarEventType.ReligiousHoliday => "Dini Tatil",
                CalendarEventType.ClassesStart => "Derslerin Başlangıcı",
                CalendarEventType.ClassesEnd => "Derslerin Bitişi",
                CalendarEventType.TuitionPayment => "Harç Ödeme",
                CalendarEventType.GraduateApplication => "Lisansüstü Başvuru",
                CalendarEventType.HorizontalTransfer => "Yatay Geçiş",
                CalendarEventType.DoubleMajor => "Çift Anadal/Yan Dal",
                CalendarEventType.Other => "Diğer",
                _ => e.EventType.ToString()
            },
            e.Title,
            e.StartDate,
            e.EndDate,
            e.IsPublic,
            e.Note
        );

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

    private static CalendarEventType ParseCalendarEventType(string s) => s switch
    {
        "CourseRegistration" => CalendarEventType.CourseRegistration,
        "NewStudentRegistration" => CalendarEventType.NewStudentRegistration,
        "AdvisorApproval" => CalendarEventType.AdvisorApproval,
        "AddDrop" => CalendarEventType.AddDrop,
        "AddDropApproval" => CalendarEventType.AddDropApproval,
        "CourseWithdrawal" => CalendarEventType.CourseWithdrawal,
        "AdvisorWithdrawal" => CalendarEventType.AdvisorWithdrawal,
        "MidtermExam" => CalendarEventType.MidtermExam,
        "MidtermGradeEntry" => CalendarEventType.MidtermGradeEntry,
        "MidtermGradePublication" => CalendarEventType.MidtermGradePublication,
        "FinalExam" => CalendarEventType.FinalExam,
        "FinalGradeEntry" => CalendarEventType.FinalGradeEntry,
        "FinalGradePublication" => CalendarEventType.FinalGradePublication,
        "MakeupExam" => CalendarEventType.MakeupExam,
        "MakeupGradeEntry" => CalendarEventType.MakeupGradeEntry,
        "MakeupGradePublication" => CalendarEventType.MakeupGradePublication,
        "NationalHoliday" => CalendarEventType.NationalHoliday,
        "ReligiousHoliday" => CalendarEventType.ReligiousHoliday,
        "ClassesStart" => CalendarEventType.ClassesStart,
        "ClassesEnd" => CalendarEventType.ClassesEnd,
        "TuitionPayment" => CalendarEventType.TuitionPayment,
        "GraduateApplication" => CalendarEventType.GraduateApplication,
        "HorizontalTransfer" => CalendarEventType.HorizontalTransfer,
        "DoubleMajor" => CalendarEventType.DoubleMajor,
        "Other" => CalendarEventType.Other,
        _ => throw new ArgumentException($"Geçersiz etkinlik tipi: {s}")
    };
}
