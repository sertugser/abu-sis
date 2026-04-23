using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SIS.Application.CalendarGenerator.DTOs;
using SIS.Application.CalendarGenerator.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.CalendarGenerator;

public partial class CalendarGeneratorService : ICalendarGeneratorService
{
    private readonly AppDbContext _context;
    private readonly ICalendarTemplateService _templateService;
    private readonly IHolidayCalculatorService _holidayCalculator;
    private readonly IAcademicCalendarRuleService _ruleService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public CalendarGeneratorService(
        AppDbContext context,
        ICalendarTemplateService templateService,
        IHolidayCalculatorService holidayCalculator,
        IAcademicCalendarRuleService ruleService)
    {
        _context = context;
        _templateService = templateService;
        _holidayCalculator = holidayCalculator;
        _ruleService = ruleService;
    }

    public async Task<GenerateCalendarResultDto> GenerateAsync(GenerateCalendarRequestDto request, CancellationToken ct = default)
    {
        var startYear = ParseAcademicYearStart(request.AcademicYear);
        await _templateService.EnsureDefaultsAsync(ct);

        var templates = await _templateService.GetTemplatesAsync(ct);
        var holidays = await _holidayCalculator.ResolveHolidaysAsync(startYear, startYear + 1, ct);
        var warnings = new List<CalendarWarningDto>();
        var constraints = request.Constraints ?? [];
        var termStarts = BuildTermStarts(startYear, request);
        var events = new List<GeneratedCalendarEventDto>();

        foreach (var template in templates.Where(t => t.IsActive))
        {
            foreach (var item in template.Items.OrderBy(i => i.SortOrder))
            {
                foreach (var semester in item.ApplicableSemesters.Distinct())
                {
                    if (semester == SemesterType.Summer && !request.IncludeSummer)
                        continue;

                    var effectiveItem = ApplyDurationOverride(item, semester, request.TemplateDurationOverrides);
                    events.Add(PlaceTemplateItem(request.AcademicYear, template.ChainCode, effectiveItem, semester, termStarts[semester], holidays, constraints, warnings));
                }
            }
        }

        events = ApplyHolidayOverflowShifts(events, holidays);
        events = ApplyRegularSemesterSequenceRules(events, holidays);
        events = ApplyDependentExamScheduling(events, holidays);
        events = ApplyHolidayOverflowShifts(events, holidays);
        events = AlignClassesEndToWeekday(events);
        events.AddRange(BuildHolidayEvents(request.AcademicYear, holidays, termStarts, request.IncludeSummer));
        events = events.OrderBy(e => e.StartDate).ThenBy(e => e.SortWeight()).ToList();

        ValidateChronology(events, warnings);
        warnings.AddRange(ValidateRegulationRules(events, holidays));
        var qualityScore = CalculateQualityScore(warnings);
        var summaries = BuildTermSummaries(events, holidays);

        var now = DateTime.UtcNow;
        var draft = new GeneratedCalendarDraft
        {
            AcademicYear = request.AcademicYear,
            Status = GeneratedCalendarDraftStatus.Draft,
            EventsJson = Serialize(events),
            HolidaysJson = Serialize(holidays),
            WarningsJson = Serialize(warnings),
            QualityScore = qualityScore,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = now.AddDays(30)
        };

        _context.GeneratedCalendarDrafts.Add(draft);
        await _context.SaveChangesAsync(ct);

        return new GenerateCalendarResultDto(draft.Id, request.AcademicYear, qualityScore, events, holidays, warnings, summaries);
    }

    public async Task<CalendarDraftDto> GetDraftAsync(int draftId, CancellationToken ct = default)
    {
        var draft = await LoadDraftAsync(draftId, ct);
        if (RemoveLegacyShiftWarnings(draft))
            await _context.SaveChangesAsync(ct);

        return ToDraftDto(draft);
    }

    public async Task<IReadOnlyList<CalendarDraftSummaryDto>> ListDraftsAsync(int take = 50, CancellationToken ct = default)
    {
        var limit = Math.Clamp(take, 1, 100);
        var drafts = await _context.GeneratedCalendarDrafts
            .OrderByDescending(d => d.Id)
            .Take(limit)
            .ToListAsync(ct);

        var removedLegacyWarnings = false;
        foreach (var draft in drafts)
            removedLegacyWarnings |= RemoveLegacyShiftWarnings(draft);

        if (removedLegacyWarnings)
            await _context.SaveChangesAsync(ct);

        return drafts.Select(ToDraftSummaryDto).ToList();
    }

    public async Task DeleteDraftAsync(int draftId, CancellationToken ct = default)
    {
        var draft = await LoadDraftAsync(draftId, ct);
        _context.GeneratedCalendarDrafts.Remove(draft);
        await _context.SaveChangesAsync(ct);

        if (!await _context.GeneratedCalendarDrafts.AnyAsync(ct))
            await ResetDraftNumberingAsync(ct);
    }

    public async Task<CalendarDraftDto> UpdateDraftEventAsync(int draftId, string draftEventId, UpdateDraftEventDto dto, CancellationToken ct = default)
    {
        var draft = await LoadDraftAsync(draftId, ct);
        EnsureEditable(draft);

        var events = Deserialize<List<GeneratedCalendarEventDto>>(draft.EventsJson);
        var target = events.FirstOrDefault(e => e.DraftEventId == draftEventId)
            ?? throw new KeyNotFoundException($"Draft event not found: {draftEventId}");
        if (target.IsHoliday)
            throw new InvalidOperationException("Resmi tatil ve bloklayici ozel gunler taslakta tarih duzenleme ile degistirilemez.");
        var oldStart = target.StartDate;
        var oldEnd = target.EndDate ?? target.StartDate;
        var newStart = EnsureUtcDate(dto.StartDate);
        var newEnd = EnsureUtcDate(dto.EndDate ?? dto.StartDate);

        events = events.Select(e => e.DraftEventId == draftEventId
                ? e with
                {
                    StartDate = newStart,
                    EndDate = newStart.Date == newEnd.Date ? null : newEnd,
                    Title = string.IsNullOrWhiteSpace(dto.Title) ? e.Title : dto.Title.Trim(),
                    Note = string.IsNullOrWhiteSpace(dto.Note) ? e.Note : dto.Note.Trim()
                }
                : e)
            .ToList();

        var propagateChanges = IsCriticalEvent(target.EventType) || dto.PropagateChanges != false;
        if (propagateChanges)
            events = Propagate(events, draftEventId, oldStart, oldEnd, newStart, newEnd);
        var holidays = Deserialize<List<ResolvedHolidayDto>>(draft.HolidaysJson);
        events = ApplyHolidayOverflowShifts(events, holidays);
        events = ApplyRegularSemesterSequenceRules(events, holidays);
        events = ApplyDependentExamScheduling(events, holidays);
        events = ApplyHolidayOverflowShifts(events, holidays);
        events = AlignClassesEndToWeekday(events);
        var warnings = new List<CalendarWarningDto>
        {
            new(
                propagateChanges ? "DOMINO" : "SINGLE_EVENT_UPDATE",
                "Info",
                propagateChanges
                    ? "Etkinlik değişikliği bağımlı sonraki etkinliklere yansıtıldı."
                    : "Etkinlik değişikliği sadece seçili etkinliğe uygulandı.",
                draftEventId)
        };
        ValidateChronology(events, warnings);
        warnings.AddRange(ValidateRegulationRules(events, holidays));

        draft.EventsJson = Serialize(events.OrderBy(e => e.StartDate).ThenBy(e => e.SortWeight()).ToList());
        draft.WarningsJson = Serialize(warnings);
        draft.QualityScore = CalculateQualityScore(warnings);
        draft.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return ToDraftDto(draft);
    }

    public async Task<WhatIfResultDto> WhatIfAsync(int draftId, WhatIfRequestDto request, CancellationToken ct = default)
    {
        var draft = await LoadDraftAsync(draftId, ct);
        var events = Deserialize<List<GeneratedCalendarEventDto>>(draft.EventsJson);
        var target = events.FirstOrDefault(e => e.DraftEventId == request.DraftEventId)
            ?? throw new KeyNotFoundException($"Draft event not found: {request.DraftEventId}");
        if (target.IsHoliday)
            throw new InvalidOperationException("Resmi tatil ve bloklayici ozel gunler icin what-if tarih degisikligi yapilamaz.");
        var newStart = EnsureUtcDate(request.StartDate);
        var newEnd = EnsureUtcDate(request.EndDate ?? request.StartDate);

        events = events.Select(e => e.DraftEventId == request.DraftEventId
                ? e with { StartDate = newStart, EndDate = newStart.Date == newEnd.Date ? null : newEnd }
                : e)
            .ToList();

        var propagateChanges = IsCriticalEvent(target.EventType) || request.PropagateChanges != false;
        if (propagateChanges)
            events = Propagate(events, request.DraftEventId, target.StartDate, target.EndDate ?? target.StartDate, newStart, newEnd);
        var holidays = Deserialize<List<ResolvedHolidayDto>>(draft.HolidaysJson);
        events = ApplyHolidayOverflowShifts(events, holidays);
        events = ApplyRegularSemesterSequenceRules(events, holidays);
        events = ApplyDependentExamScheduling(events, holidays);
        events = ApplyHolidayOverflowShifts(events, holidays);
        events = AlignClassesEndToWeekday(events);
        var warnings = new List<CalendarWarningDto>
        {
            new(
                "WHAT_IF",
                "Info",
                propagateChanges
                    ? "Bu sonuc taslak kaydedilmeden, zincirleme kaydirma dahil hesaplandi."
                    : "Bu sonuç taslak kaydedilmeden, sadece seçili etkinlik değiştirilerek hesaplandı.",
                request.DraftEventId)
        };
        ValidateChronology(events, warnings);
        warnings.AddRange(ValidateRegulationRules(events, holidays));

        return new WhatIfResultDto(events.OrderBy(e => e.StartDate).ThenBy(e => e.SortWeight()).ToList(), warnings, CalculateQualityScore(warnings));
    }
}
