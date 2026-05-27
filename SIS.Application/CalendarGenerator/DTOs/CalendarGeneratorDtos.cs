using SIS.Domain.Enums;

namespace SIS.Application.CalendarGenerator.DTOs;

public record CalendarTemplateItemDto(
    string Key,
    CalendarEventType EventType,
    string Title,
    int DefaultDurationDays,
    CalendarAnchorType AnchorType,
    string? AnchorKey,
    int OffsetDays,
    SemesterType[] ApplicableSemesters,
    bool IsRequired,
    int SortOrder,
    bool AvoidHolidayConflict = true
);

public record CalendarTemplateDto(
    int Id,
    string Name,
    string ChainCode,
    bool IsActive,
    IReadOnlyList<CalendarTemplateItemDto> Items
);

public record UpsertCalendarTemplateDto(
    string Name,
    string ChainCode,
    bool IsActive,
    IReadOnlyList<CalendarTemplateItemDto> Items
);

public record HolidayOverrideDto(
    int Year,
    DateTime StartDate,
    DateTime EndDate,
    bool IsEstimated = false,
    string? Source = null
);

public record HolidayDefinitionDto(
    int Id,
    string Name,
    string EnglishName,
    HolidayType Type,
    int? FixedMonth,
    int? FixedDay,
    int? HijriMonth,
    int? HijriDay,
    decimal DurationDays,
    bool IsHalfDayStart,
    bool IsOfficialHoliday,
    bool BlocksAcademicEvents,
    string Source,
    IReadOnlyList<HolidayOverrideDto> Overrides,
    bool IsActive
);

public record UpsertHolidayDefinitionDto(
    string Name,
    string EnglishName,
    HolidayType Type,
    int? FixedMonth,
    int? FixedDay,
    int? HijriMonth,
    int? HijriDay,
    decimal DurationDays,
    bool IsHalfDayStart,
    bool IsOfficialHoliday,
    bool BlocksAcademicEvents,
    string Source,
    IReadOnlyList<HolidayOverrideDto> Overrides,
    bool IsActive
);

public record CalendarConstraintDto(
    int Id,
    string AcademicYear,
    CalendarConstraintType ConstraintType,
    CalendarEventType? TargetEventType,
    string? TargetChainCode,
    string ParameterJson,
    bool IsHard,
    int Priority,
    string? Description
);

public record CalendarConstraintInputDto(
    CalendarConstraintType ConstraintType,
    CalendarEventType? TargetEventType,
    string? TargetChainCode,
    string ParameterJson,
    bool IsHard,
    int Priority,
    string? Description
);

public record ResolvedHolidayDto(
    string Name,
    string EnglishName,
    HolidayType Type,
    DateTime StartDate,
    DateTime EndDate,
    decimal DurationDays,
    bool IsOfficialHoliday,
    bool BlocksAcademicEvents,
    bool IsEstimated,
    string Source
);

public record GeneratedCalendarEventDto(
    string DraftEventId,
    SemesterType Semester,
    string TermKey,
    CalendarEventType EventType,
    string EventTypeLabel,
    string Title,
    DateTime StartDate,
    DateTime? EndDate,
    string ChainCode,
    string? TemplateItemKey,
    bool IsPublic,
    bool IsHoliday,
    bool BlocksAcademicEvents,
    bool IsFailed,
    string? Note
);

public record CalendarWarningDto(
    string Code,
    string Severity,
    string Message,
    string? DraftEventId = null
);

public record TermSummaryDto(
    SemesterType Semester,
    DateTime TermStart,
    DateTime TermEnd,
    int TeachingDays,
    int HolidayDaysInTerm,
    int ExamDays
);

public record GenerateCalendarRequestDto(
    string AcademicYear,
    DateTime? FallStartDate,
    DateTime? SpringStartDate,
    bool IncludeSummer,
    IReadOnlyList<CalendarConstraintInputDto>? Constraints,
    Dictionary<string, int>? TemplateDurationOverrides
);

public record GenerateCalendarResultDto(
    int DraftId,
    string AcademicYear,
    int QualityScore,
    IReadOnlyList<GeneratedCalendarEventDto> Events,
    IReadOnlyList<ResolvedHolidayDto> Holidays,
    IReadOnlyList<CalendarWarningDto> Warnings,
    IReadOnlyList<TermSummaryDto> TermSummaries
);

public record CalendarDraftDto(
    int Id,
    string AcademicYear,
    string Status,
    int QualityScore,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime ExpiresAt,
    IReadOnlyList<GeneratedCalendarEventDto> Events,
    IReadOnlyList<ResolvedHolidayDto> Holidays,
    IReadOnlyList<CalendarWarningDto> Warnings,
    IReadOnlyList<TermSummaryDto> TermSummaries
);

public record CalendarDraftSummaryDto(
    int Id,
    string AcademicYear,
    string Status,
    int QualityScore,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime ExpiresAt,
    DateTime? AppliedAt,
    int EventCount,
    int WarningCount
);

public record UpdateDraftEventDto(
    DateTime StartDate,
    DateTime? EndDate,
    string? Title,
    string? Note,
    bool? PropagateChanges = null
);

public record WhatIfRequestDto(
    string DraftEventId,
    DateTime StartDate,
    DateTime? EndDate,
    bool? PropagateChanges = null
);

public record WhatIfResultDto(
    IReadOnlyList<GeneratedCalendarEventDto> Events,
    IReadOnlyList<CalendarWarningDto> Warnings,
    int QualityScore
);

// Semesters: null/boş → taslaktaki tüm yarıyıllar uygulanır (geriye uyumlu).
// Dolu ise → yalnızca seçili yarıyıllar uygulanır; seçilmeyenler için ne dönem oluşturulur ne event yazılır.
public record ApplyCalendarDraftRequestDto(bool ForceOverwrite = false, IReadOnlyList<SemesterType>? Semesters = null);

public record ApplyCalendarDraftResultDto(
    bool Success,
    string Message,
    IReadOnlyList<int> TermIds
);
