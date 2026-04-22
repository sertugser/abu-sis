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
    private readonly IDateTimeProvider _clock;

    public AdminTermService(
        AppDbContext context,
        ITimeSlotService timeSlotService,
        IDateTimeProvider clock)
    {
        _context = context;
        _timeSlotService = timeSlotService;
        _clock = clock;
    }

    public async Task<List<AdminTermDto>> GetAllTermsAsync()
    {
        var terms = await _context.AcademicTerms
            .Include(t => t.CalendarEvents)
            .OrderByDescending(t => t.Year)
            .ThenByDescending(t => t.TermType)
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
        var termType = ParseTermType(dto.TermType);

        var exists = await _context.AcademicTerms
            .AnyAsync(t => t.Year == dto.Year && t.TermType == termType);
        if (exists)
            throw new InvalidOperationException(
                $"{dto.Year} yılı için {GetTermTypeLabel(termType)} dönemi zaten mevcut.");

        ValidateTermDates(dto.RegistrationStart, dto.RegistrationEnd, dto.AddDropStart,
            dto.AddDropEnd, dto.TermStart, dto.TermEnd, dto.WithdrawalDeadline,
            dto.GradeEntryStart, dto.GradeEntryEnd);

        var term = new AcademicTerm
        {
            Name = dto.Name,
            ShortName = dto.ShortName,
            EnglishName = dto.EnglishName,
            ShortEnglishName = dto.ShortEnglishName,
            OtherName = string.IsNullOrWhiteSpace(dto.OtherName) ? null : dto.OtherName,
            Year = dto.Year,
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
            IsCancelled = false,
            YoksisStatus = ParseYoksisStatus(dto.YoksisStatus),
            CreatedAt = _clock.UtcNow
        };

        _context.AcademicTerms.Add(term);
        await _context.SaveChangesAsync();

        // TimeSlot grid'i oluşturuluyor — admin yeni term yarattıktan sonra hemen section atama yapabilsin
        await _timeSlotService.GenerateTimeSlotsForTermAsync(term.Id);

        return ToDto(term);
    }

    public async Task<AdminTermDto> UpdateTermAsync(int id, UpdateTermDto dto)
    {
        var term = await _context.AcademicTerms.FindAsync(id)
            ?? throw new KeyNotFoundException($"Dönem bulunamadı: {id}");

        if (term.IsCancelled)
            throw new InvalidOperationException("İptal edilmiş dönem düzenlenemez. Önce iptali geri alın.");

        ValidateTermDates(dto.RegistrationStart, dto.RegistrationEnd, dto.AddDropStart,
            dto.AddDropEnd, dto.TermStart, dto.TermEnd, dto.WithdrawalDeadline,
            dto.GradeEntryStart, dto.GradeEntryEnd);

        term.Name = dto.Name;
        term.ShortName = dto.ShortName;
        term.EnglishName = dto.EnglishName;
        term.ShortEnglishName = dto.ShortEnglishName;
        term.OtherName = string.IsNullOrWhiteSpace(dto.OtherName) ? null : dto.OtherName;
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
        term.YoksisStatus = ParseYoksisStatus(dto.YoksisStatus);
        term.UpdatedAt = _clock.UtcNow;

        await _context.SaveChangesAsync();
        return ToDto(term);
    }

    public async Task CancelTermAsync(int id)
    {
        var term = await _context.AcademicTerms.FindAsync(id)
            ?? throw new KeyNotFoundException($"Dönem bulunamadı: {id}");

        if (term.IsCancelled) return; // idempotent

        term.IsCancelled = true;
        term.UpdatedAt = _clock.UtcNow;

        // Cascade: bu döneme ait tüm section'ları Cancelled yap
        var sections = await _context.CourseSections
            .Where(cs => cs.TermId == id && cs.Status != CourseSectionStatus.Cancelled)
            .ToListAsync();
        foreach (var s in sections)
            s.Status = CourseSectionStatus.Cancelled;

        await _context.SaveChangesAsync();
    }

    public async Task UncancelTermAsync(int id)
    {
        var term = await _context.AcademicTerms.FindAsync(id)
            ?? throw new KeyNotFoundException($"Dönem bulunamadı: {id}");

        if (!term.IsCancelled) return; // idempotent

        term.IsCancelled = false;
        term.UpdatedAt = _clock.UtcNow;

        // Section'lar otomatik geri alınmaz — admin gerekirse tek tek restore etsin
        await _context.SaveChangesAsync();
    }

    public async Task UpdatePhaseOverrideAsync(int id, UpdateTermPhaseOverrideDto dto, int? userId)
    {
        var term = await _context.AcademicTerms.FindAsync(id)
            ?? throw new KeyNotFoundException($"Dönem bulunamadı: {id}");

        if (term.IsCancelled)
            throw new InvalidOperationException("İptal edilmiş dönemin fazları değiştirilemez.");

        switch (dto.Phase)
        {
            case "Registration": term.RegistrationOverride = dto.Value; break;
            case "AddDrop":      term.AddDropOverride = dto.Value; break;
            case "Withdrawal":   term.WithdrawalOverride = dto.Value; break;
            case "GradeEntry":   term.GradeEntryOverride = dto.Value; break;
            default: throw new ArgumentException($"Geçersiz faz: {dto.Phase}");
        }

        term.UpdatedAt = _clock.UtcNow;
        await _context.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private AdminTermDto ToDto(AcademicTerm t)
    {
        var now = _clock.UtcNow;
        var status = t.GetStatus(now);
        var generatedEvents = t.CalendarEvents.Where(e => e.IsGenerated).ToList();
        var generatedDraftId = generatedEvents
            .Select(e => e.GeneratedDraftId)
            .Where(id => id.HasValue)
            .Max();

        return new AdminTermDto(
            t.Id, t.Name, t.ShortName, t.EnglishName, t.ShortEnglishName, t.OtherName,
            t.Year,
            t.TermType.ToString(),
            GetTermTypeLabel(t.TermType),
            status.ToString(),
            GetStatusLabel(status),
            t.IsCancelled,
            t.YoksisStatus.ToString(),
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
            t.IsActive(now),
            t.CalendarEvents.Count,
            generatedEvents.Count,
            generatedDraftId,
            generatedEvents.Count > 0
        );
    }

    public static string GetStatusLabel(TermStatus s) => s switch
    {
        TermStatus.Inactive  => "Pasif",
        TermStatus.Active    => "Aktif",
        TermStatus.Cancelled => "İptal",
        _                    => s.ToString()
    };

    public static string GetTermTypeLabel(TermType t) => t switch
    {
        TermType.Fall         => "Güz",
        TermType.Spring       => "Bahar",
        TermType.Summer       => "Yaz",
        TermType.FallResit    => "Güz Tekders",
        TermType.SpringResit  => "Bahar Tekders",
        TermType.SummerResit  => "Yaz Tekders",
        TermType.SpringOther  => "Bahar Diğer",
        TermType.SummerOther  => "Yaz Diğer",
        TermType.Exemption    => "Muafiyet",
        _                     => t.ToString()
    };

    private static TermType ParseTermType(string s) => s switch
    {
        "Fall"         => TermType.Fall,
        "Spring"       => TermType.Spring,
        "Summer"       => TermType.Summer,
        "FallResit"    => TermType.FallResit,
        "SpringResit"  => TermType.SpringResit,
        "SummerResit"  => TermType.SummerResit,
        "SpringOther"  => TermType.SpringOther,
        "SummerOther"  => TermType.SummerOther,
        "Exemption"    => TermType.Exemption,
        _ => throw new ArgumentException($"Geçersiz dönem tipi: {s}")
    };

    private static YoksisStatus ParseYoksisStatus(string s) => s switch
    {
        "Inactive" => YoksisStatus.Inactive,
        "Active"   => YoksisStatus.Active,
        _ => throw new ArgumentException($"Geçersiz YÖKSİS durumu: {s}")
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
}
