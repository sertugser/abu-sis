using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.CalendarGenerator.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.CalendarGenerator;

namespace SIS.Tests.Application;

public class CalendarGeneratorTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly CalendarTemplateService _templateService;
    private readonly HolidayCalculatorService _holidayService;
    private readonly AcademicCalendarRuleService _ruleService;
    private readonly CalendarGeneratorService _generatorService;

    public CalendarGeneratorTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _templateService = new CalendarTemplateService(_context);
        _holidayService = new HolidayCalculatorService(_context, _templateService);
        _ruleService = new AcademicCalendarRuleService(_context);
        _generatorService = new CalendarGeneratorService(_context, _templateService, _holidayService, _ruleService);
    }

    [Fact]
    public async Task HolidayCalculator_UsesDiyanetOverrides_For2026ReligiousHolidays()
    {
        var holidays = await _holidayService.ResolveHolidaysAsync(2026, 2026);

        holidays.Single(h => h.Name == "Ramazan Bayramı").Should().BeEquivalentTo(new
        {
            StartDate = Utc(2026, 3, 19),
            EndDate = Utc(2026, 3, 22),
            IsEstimated = false,
            Source = "Diyanet"
        });
        holidays.Single(h => h.Name == "Kurban Bayramı").Should().BeEquivalentTo(new
        {
            StartDate = Utc(2026, 5, 26),
            EndDate = Utc(2026, 5, 30),
            IsEstimated = false,
            Source = "Diyanet"
        });
    }

    [Fact]
    public async Task HolidayCalculator_ProducesFixedTurkishHolidays()
    {
        var holidays = await _holidayService.ResolveHolidaysAsync(2026, 2026);

        holidays.Should().Contain(h => h.Name == "Yılbaşı Tatili" && h.StartDate == Utc(2026, 1, 1));
        holidays.Should().Contain(h => h.Name == "Ulusal Egemenlik ve Çocuk Bayramı" && h.StartDate == Utc(2026, 4, 23));
        holidays.Should().Contain(h => h.Name == "Emek ve Dayanışma Günü" && h.StartDate == Utc(2026, 5, 1));
        holidays.Should().Contain(h => h.Name == "Atatürk'ü Anma, Gençlik ve Spor Bayramı" && h.StartDate == Utc(2026, 5, 19));
        holidays.Should().Contain(h => h.Name == "Demokrasi ve Milli Birlik Günü" && h.StartDate == Utc(2026, 7, 15));
        holidays.Should().Contain(h => h.Name == "Zafer Bayramı" && h.StartDate == Utc(2026, 8, 30));
        holidays.Should().Contain(h => h.Name == "Cumhuriyet Bayramı" && h.StartDate == Utc(2026, 10, 28) && h.EndDate == Utc(2026, 10, 29));
        holidays.Should().Contain(h => h.Name == "Atatürk'ü Anma Günü" && h.StartDate == Utc(2026, 11, 10) && !h.IsOfficialHoliday && h.BlocksAcademicEvents);
    }

    [Fact]
    public async Task HolidayCalculator_UsesHijriFallback_WhenDiyanetOverrideIsMissing()
    {
        var holidays = await _holidayService.ResolveHolidaysAsync(2027, 2027);

        holidays.Should().Contain(h => h.Name == "Ramazan Bayramı" && h.IsEstimated && h.Source.EndsWith(":fallback"));
        holidays.Should().Contain(h => h.Name == "Kurban Bayramı" && h.IsEstimated && h.Source.EndsWith(":fallback"));
    }

    [Fact]
    public async Task Generator_ReturnsChronologyWarning_WhenHardOrderIsBroken()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints:
            [
                new CalendarConstraintInputDto(
                    CalendarConstraintType.Pin,
                    CalendarEventType.FinalGradePublication,
                    "MAIN",
                    """{"startDate":"2026-01-01T00:00:00Z","endDate":"2026-01-01T00:00:00Z"}""",
                    IsHard: true,
                    Priority: 1,
                    Description: "Final ilanini sinavdan onceye sabitle")
            ],
            TemplateDurationOverrides: null));

        result.Warnings.Should().Contain(w => w.Code == "CHRONOLOGY" && w.Severity == "Error");
        result.QualityScore.Should().BeLessThan(100);
    }

    [Fact]
    public async Task Generator_DefaultCalendar_MeetsUndergraduateRegulationRules()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: true,
            Constraints: null,
            TemplateDurationOverrides: null));

        result.Warnings.Should().NotContain(w => w.Code.StartsWith("REG_", StringComparison.Ordinal));
        result.Events.Single(e => e.Semester == SemesterType.Summer && e.TemplateItemKey == "summer_exam")
            .StartDate.Should().BeAfter(result.Events.Single(e => e.Semester == SemesterType.Summer && e.TemplateItemKey == "summer_classes_end").StartDate);
    }

    [Fact]
    public async Task Generator_ReturnsRegulationError_WhenTeachingPeriodIsShort()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints:
            [
                new CalendarConstraintInputDto(
                    CalendarConstraintType.Pin,
                    CalendarEventType.ClassesEnd,
                    "MAIN",
                    """{"startDate":"2025-11-01T00:00:00Z","endDate":"2025-11-01T00:00:00Z"}""",
                    IsHard: true,
                    Priority: 1,
                    Description: "Ders bitisini erken sabitle")
            ],
            TemplateDurationOverrides: null));

        result.Warnings.Should().Contain(w => w.Code == "REG_MIN_TEACHING_WEEKS" && w.Severity == "Error");
    }

    [Fact]
    public async Task Generator_ReturnsRegulationError_WhenSummerExamStartsBeforeTeachingEnds()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: true,
            Constraints:
            [
                new CalendarConstraintInputDto(
                    CalendarConstraintType.Pin,
                    CalendarEventType.SummerSchoolExam,
                    "SUMMER",
                    """{"startDate":"2026-08-24T00:00:00Z","endDate":"2026-08-28T00:00:00Z"}""",
                    IsHard: true,
                    Priority: 1,
                    Description: "Yaz sinavini ders bitmeden baslat")
            ],
            TemplateDurationOverrides: null));

        result.Warnings.Should().Contain(w => w.Code == "REG_SUMMER_EXAM_ORDER" && w.Severity == "Error");
    }

    [Fact]
    public async Task Apply_RejectsDraft_WhenRegulationErrorsExist()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints:
            [
                new CalendarConstraintInputDto(
                    CalendarConstraintType.Pin,
                    CalendarEventType.ClassesEnd,
                    "MAIN",
                    """{"startDate":"2025-11-01T00:00:00Z","endDate":"2025-11-01T00:00:00Z"}""",
                    IsHard: true,
                    Priority: 1,
                    Description: "Ders bitisini erken sabitle")
            ],
            TemplateDurationOverrides: null));

        var act = async () => await _generatorService.ApplyAsync(result.DraftId, new ApplyCalendarDraftRequestDto(), userId: null);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Yönetmelik*");
    }

    [Fact]
    public async Task Apply_ReplacesExistingTermCalendarEvents_WhenDraftIsApplied()
    {
        var term = new AcademicTerm
        {
            Name = "2025-2026 Güz",
            ShortName = "25-26 Güz",
            EnglishName = "Fall 2025-2026",
            Year = 2025,
            Semester = SemesterType.Fall,
            TermType = TermType.Regular,
            Status = TermStatus.Draft,
            RegistrationStart = Utc(2025, 9, 15),
            RegistrationEnd = Utc(2025, 9, 19),
            AddDropStart = Utc(2025, 9, 29),
            AddDropEnd = Utc(2025, 10, 3),
            TermStart = Utc(2025, 9, 22),
            TermEnd = Utc(2026, 1, 2),
            WithdrawalDeadline = Utc(2025, 11, 7)
        };
        _context.AcademicTerms.Add(term);
        await _context.SaveChangesAsync();

        _context.TermCalendarEvents.Add(new TermCalendarEvent
        {
            TermId = term.Id,
            EventType = CalendarEventType.Other,
            Title = "Eski Takvim Isareti",
            StartDate = Utc(2025, 10, 1),
            IsPublic = true,
            IsGenerated = false
        });
        await _context.SaveChangesAsync();

        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        await _generatorService.ApplyAsync(result.DraftId, new ApplyCalendarDraftRequestDto(), userId: null);

        var titles = await _context.TermCalendarEvents
            .Where(e => e.TermId == term.Id)
            .Select(e => e.Title)
            .ToListAsync();

        titles.Should().NotContain("Eski Takvim Isareti");
        titles.Should().Contain("Derslerin Başlaması");
    }

    [Fact]
    public async Task WhatIf_ShiftsDependentMakeupEvents_WhenFinalExamMoves()
    {
        var generated = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var final = generated.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "final_exam");
        var makeup = generated.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "makeup_exam");

        var whatIf = await _generatorService.WhatIfAsync(
            generated.DraftId,
            new WhatIfRequestDto(final.DraftEventId, final.StartDate.AddDays(7), final.EndDate?.AddDays(7)));

        var shiftedMakeup = whatIf.Events.Single(e => e.DraftEventId == makeup.DraftEventId);
        shiftedMakeup.StartDate.Should().Be(makeup.StartDate.AddDays(7));
        shiftedMakeup.EndDate.Should().Be(makeup.EndDate?.AddDays(7));
    }

    [Fact]
    public async Task WhatIf_DoesNotShiftDependentEvents_WhenNonCriticalChangeSkipsDomino()
    {
        var generated = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var transfer = generated.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "fall_transfer_app");
        var registration = generated.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "fall_course_registration");

        var whatIf = await _generatorService.WhatIfAsync(
            generated.DraftId,
            new WhatIfRequestDto(
                transfer.DraftEventId,
                transfer.StartDate.AddDays(1),
                transfer.EndDate?.AddDays(1),
                PropagateChanges: false));

        var unchangedRegistration = whatIf.Events.Single(e => e.DraftEventId == registration.DraftEventId);
        unchangedRegistration.StartDate.Should().Be(registration.StartDate);
        unchangedRegistration.EndDate.Should().Be(registration.EndDate);
        whatIf.Warnings.Should().Contain(w => w.Code == "WHAT_IF" && w.Message.Contains("sadece seçili etkinlik"));
    }

    [Fact]
    public async Task WhatIf_ShiftsDependentEvents_WhenNonCriticalChangeUsesDomino()
    {
        var generated = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var transfer = generated.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "fall_transfer_app");
        var registration = generated.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "fall_course_registration");

        var whatIf = await _generatorService.WhatIfAsync(
            generated.DraftId,
            new WhatIfRequestDto(
                transfer.DraftEventId,
                transfer.StartDate.AddDays(1),
                transfer.EndDate?.AddDays(1),
                PropagateChanges: true));

        var shiftedRegistration = whatIf.Events.Single(e => e.DraftEventId == registration.DraftEventId);
        shiftedRegistration.StartDate.Should().Be(registration.StartDate.AddDays(1));
        shiftedRegistration.EndDate.Should().Be(registration.EndDate?.AddDays(1));
    }

    [Fact]
    public async Task WhatIf_ForcesDomino_WhenCriticalEventRequestsNoDomino()
    {
        var generated = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var final = generated.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "final_exam");
        var makeup = generated.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "makeup_exam");

        var whatIf = await _generatorService.WhatIfAsync(
            generated.DraftId,
            new WhatIfRequestDto(
                final.DraftEventId,
                final.StartDate.AddDays(3),
                final.EndDate?.AddDays(3),
                PropagateChanges: false));

        var shiftedMakeup = whatIf.Events.Single(e => e.DraftEventId == makeup.DraftEventId);
        shiftedMakeup.StartDate.Should().Be(makeup.StartDate.AddDays(3));
        shiftedMakeup.EndDate.Should().Be(makeup.EndDate?.AddDays(3));
    }

    [Fact]
    public async Task AcademicCalendarRules_HideLetterGrade_BeforePublicationDate()
    {
        var term = new AcademicTerm
        {
            Id = 1,
            Name = "2025-2026 Güz",
            ShortName = "25-26 Güz",
            EnglishName = "Fall 2025-2026",
            Year = 2025,
            Semester = SemesterType.Fall,
            TermType = TermType.Regular,
            TermStart = Utc(2025, 9, 22),
            TermEnd = Utc(2026, 1, 2),
            RegistrationStart = Utc(2025, 9, 15),
            RegistrationEnd = Utc(2025, 9, 19),
            AddDropStart = Utc(2025, 9, 29),
            AddDropEnd = Utc(2025, 10, 3),
            WithdrawalDeadline = Utc(2025, 11, 7)
        };
        _context.AcademicTerms.Add(term);
        _context.TermCalendarEvents.Add(new TermCalendarEvent
        {
            TermId = term.Id,
            EventType = CalendarEventType.FinalGradePublication,
            Title = "Final Not İlanı",
            StartDate = Utc(2026, 1, 21),
            IsPublic = true
        });
        await _context.SaveChangesAsync();

        var before = await _ruleService.CanStudentSeeLetterGradeAsync(term.Id, true, true, Utc(2026, 1, 20));
        var onDate = await _ruleService.CanStudentSeeLetterGradeAsync(term.Id, true, true, Utc(2026, 1, 21));

        before.Should().BeFalse();
        onDate.Should().BeTrue();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
