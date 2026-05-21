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
            .Include(t => t.AcademicCalendar)
            .OrderByDescending(t => t.Year)
            .ThenByDescending(t => t.TermType)
            .ToListAsync();
        return terms.Select(ToDto).ToList();
    }

    public async Task<AdminTermDto> GetTermByIdAsync(int id)
    {
        var term = await _context.AcademicTerms
            .Include(t => t.CalendarEvents)
            .Include(t => t.AcademicCalendar)
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

        var termStart = DateTime.SpecifyKind(dto.TermStart, DateTimeKind.Utc);
        var termEnd = DateTime.SpecifyKind(dto.TermEnd, DateTimeKind.Utc);
        var overrideStart = dto.OverrideStart.HasValue
            ? DateTime.SpecifyKind(dto.OverrideStart.Value, DateTimeKind.Utc)
            : (DateTime?)null;
        var overrideEnd = dto.OverrideEnd.HasValue
            ? DateTime.SpecifyKind(dto.OverrideEnd.Value, DateTimeKind.Utc)
            : (DateTime?)null;

        ValidateDateRange(termStart, termEnd, overrideStart, overrideEnd);

        var term = new AcademicTerm
        {
            Name = dto.Name,
            ShortName = dto.ShortName,
            EnglishName = dto.EnglishName,
            ShortEnglishName = dto.ShortEnglishName,
            OtherName = string.IsNullOrWhiteSpace(dto.OtherName) ? null : dto.OtherName,
            Year = dto.Year,
            TermType = termType,
            TermStart = termStart,
            TermEnd = termEnd,
            OverrideStart = overrideStart,
            OverrideEnd = overrideEnd,
            IsActivated = true,
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

        var termStart = DateTime.SpecifyKind(dto.TermStart, DateTimeKind.Utc);
        var termEnd = DateTime.SpecifyKind(dto.TermEnd, DateTimeKind.Utc);
        var overrideStart = dto.OverrideStart.HasValue
            ? DateTime.SpecifyKind(dto.OverrideStart.Value, DateTimeKind.Utc)
            : (DateTime?)null;
        var overrideEnd = dto.OverrideEnd.HasValue
            ? DateTime.SpecifyKind(dto.OverrideEnd.Value, DateTimeKind.Utc)
            : (DateTime?)null;

        ValidateDateRange(termStart, termEnd, overrideStart, overrideEnd);

        term.Name = dto.Name;
        term.ShortName = dto.ShortName;
        term.EnglishName = dto.EnglishName;
        term.ShortEnglishName = dto.ShortEnglishName;
        term.OtherName = string.IsNullOrWhiteSpace(dto.OtherName) ? null : dto.OtherName;
        term.TermType = ParseTermType(dto.TermType);
        term.TermStart = termStart;
        term.TermEnd = termEnd;
        term.OverrideStart = overrideStart;
        term.OverrideEnd = overrideEnd;
        term.YoksisStatus = ParseYoksisStatus(dto.YoksisStatus);
        term.UpdatedAt = _clock.UtcNow;

        await _context.SaveChangesAsync();
        return ToDto(term);
    }

    public async Task ActivateTermAsync(int id)
    {
        var term = await _context.AcademicTerms.FindAsync(id)
            ?? throw new KeyNotFoundException($"Donem bulunamadi: {id}");

        if (term.IsCancelled)
            throw new InvalidOperationException("Iptal edilmis donem aktif edilemez. Once iptali geri alin.");

        if (term.IsActivated)
            return;

        term.IsActivated = true;
        term.UpdatedAt = _clock.UtcNow;
        await _context.SaveChangesAsync();

        await _timeSlotService.GenerateTimeSlotsForTermAsync(term.Id);
    }

    public async Task DeactivateTermAsync(int id)
    {
        var term = await _context.AcademicTerms.FindAsync(id)
            ?? throw new KeyNotFoundException($"Donem bulunamadi: {id}");

        if (term.IsCancelled)
            throw new InvalidOperationException("Iptal edilmis donem pasife alinamaz. Once iptali geri alin.");

        if (!term.IsActivated)
            return;

        term.IsActivated = false;
        term.UpdatedAt = _clock.UtcNow;
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Dönem ve override tarihleri için tutarlılık kontrolü.
    /// Hata durumunda ArgumentException fırlatır, mesaj admin'e gösterilir.
    /// </summary>
    private static void ValidateDateRange(
        DateTime termStart, DateTime termEnd,
        DateTime? overrideStart, DateTime? overrideEnd)
    {
        if (termEnd <= termStart)
            throw new ArgumentException("Dönem bitiş tarihi, başlangıçtan sonra olmalı.");

        if (overrideStart.HasValue && overrideStart.Value >= termStart)
            throw new ArgumentException(
                "Erken aktivasyon tarihi (OverrideStart) dönem başlangıcından ÖNCE olmalı. " +
                "Dönemi geciktirmek istiyorsanız bunun yerine TermStart'ı değiştirin.");

        if (overrideEnd.HasValue && overrideEnd.Value <= termEnd)
            throw new ArgumentException(
                "Uzatma tarihi (OverrideEnd) dönem bitişinden SONRA olmalı. " +
                "Dönemi kısaltmak istiyorsanız TermEnd'i değiştirin veya iptal edin.");

        if (overrideStart.HasValue && overrideEnd.HasValue && overrideEnd.Value <= overrideStart.Value)
            throw new ArgumentException("Uzatma tarihi, erken aktivasyon tarihinden sonra olmalı.");
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
            t.IsActivated,
            t.IsCancelled,
            t.YoksisStatus.ToString(),
            t.TermStart, t.TermEnd,
            t.OverrideStart, t.OverrideEnd,
            t.EffectiveStart, t.EffectiveEnd,
            t.IsActive(now),
            t.AcademicCalendar is not null,
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

}
