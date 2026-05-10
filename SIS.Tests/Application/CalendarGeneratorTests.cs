using System.Text.Json;
using System.Text.Json.Serialization;
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
    private static readonly JsonSerializerOptions TestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

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
        holidays.Should().NotContain(h => h.Name == "Atatürk'ü Anma Günü");
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
    public async Task Generator_DoesNotPlaceAcademicEvents_OnBlockingHolidays()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: true,
            Constraints: null,
            TemplateDurationOverrides: null));

        var blockingHolidayRanges = result.Holidays
            .Where(h => h.BlocksAcademicEvents)
            .Select(h => new { Start = h.StartDate.Date, End = h.EndDate.Date })
            .ToList();

        foreach (var ev in result.Events.Where(e =>
            !e.IsHoliday
            && e.EventType is not CalendarEventType.CourseRegistration
            && e.EventType is not CalendarEventType.AddDrop
            && ((e.EndDate ?? e.StartDate).Date - e.StartDate.Date).Days + 1 < 15))
        {
            var eventEnd = (ev.EndDate ?? ev.StartDate).Date;
            blockingHolidayRanges.Should().OnlyContain(range =>
                eventEnd < range.Start || ev.StartDate.Date > range.End);
        }
    }

    [Fact]
    public async Task Generator_OnlineRegistrationAndAdvisorApproval_RunSevenCalendarDays()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var onlineEvents = result.Events.Where(e =>
            e.EventType is CalendarEventType.CourseRegistration or CalendarEventType.AddDrop).ToList();

        onlineEvents.Should().NotBeEmpty();
        foreach (var ev in onlineEvents)
        {
            var end = ev.EndDate ?? ev.StartDate;
            ((end.Date - ev.StartDate.Date).Days + 1).Should().Be(7);
        }
    }

    [Fact]
    public async Task Generator_PlacesSummerExams_OnMonday()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: true,
            Constraints: null,
            TemplateDurationOverrides: null));

        var summerExam = result.Events.Single(e =>
            e.Semester == SemesterType.Summer && e.TemplateItemKey == "summer_exam");
        var summerExcuseExam = result.Events.Single(e =>
            e.Semester == SemesterType.Summer && e.TemplateItemKey == "summer_excuse_exam");

        summerExam.StartDate.DayOfWeek.Should().Be(DayOfWeek.Monday);
        summerExcuseExam.StartDate.Should().BeAfter(summerExam.StartDate);
    }

    [Fact]
    public async Task Generator_PlacesMainExamTypeEvents_OnMonday()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: true,
            Constraints: null,
            TemplateDurationOverrides: null));

        var examTypes = new[]
        {
            CalendarEventType.MidtermExam,
            CalendarEventType.FinalExam,
            CalendarEventType.MakeupExam,
            CalendarEventType.PrepSchoolExam,
            CalendarEventType.SummerSchoolExam
        };

        result.Events
            .Where(e => examTypes.Contains(e.EventType))
            .Should()
            .OnlyContain(e => e.StartDate.DayOfWeek == DayOfWeek.Monday);
    }

    [Fact]
    public async Task Generator_AllowsLongAdministrativeWindow_ToOverlapSingleDayHoliday()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var graduateApplication = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "fall_graduate_app");
        var zaferBayrami = result.Holidays.First(h => h.Name == "Zafer Bayramı" && h.StartDate == Utc(2025, 8, 30));

        graduateApplication.StartDate.Should().Be(Utc(2025, 8, 4));
        graduateApplication.EndDate.Should().Be(Utc(2025, 9, 11));
        graduateApplication.IsFailed.Should().BeFalse();
        graduateApplication.StartDate.Should().BeOnOrBefore(zaferBayrami.StartDate);
        (graduateApplication.EndDate ?? graduateApplication.StartDate).Should().BeOnOrAfter(zaferBayrami.EndDate);
    }

    [Fact]
    public async Task WhatIf_AllowsLongAdministrativeWindow_OnNonReligiousMultiDayHoliday()
    {
        var generated = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var graduateApplication = generated.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "fall_graduate_app");

        var whatIf = await _generatorService.WhatIfAsync(
            generated.DraftId,
            new WhatIfRequestDto(graduateApplication.DraftEventId, Utc(2025, 10, 20), Utc(2025, 11, 7), false));

        var moved = whatIf.Events.Single(e => e.DraftEventId == graduateApplication.DraftEventId);
        moved.StartDate.Should().Be(Utc(2025, 10, 20));
        moved.EndDate.Should().Be(Utc(2025, 11, 7));
    }

    [Fact]
    public async Task Generator_WhatIf_TrimsTrailingWeekend_FromLongApplicationWindow()
    {
        var generated = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var graduateApplication = generated.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "fall_graduate_app");

        var whatIf = await _generatorService.WhatIfAsync(
            generated.DraftId,
            new WhatIfRequestDto(graduateApplication.DraftEventId, Utc(2025, 10, 20), Utc(2025, 11, 9), false));

        var moved = whatIf.Events.Single(e => e.DraftEventId == graduateApplication.DraftEventId);
        moved.StartDate.Should().Be(Utc(2025, 10, 20));
        moved.EndDate.Should().Be(Utc(2025, 11, 7));
    }

    [Fact]
    public async Task Generator_PlacesClassesEnd_OnWeekday()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: true,
            Constraints: null,
            TemplateDurationOverrides: null));

        result.Events
            .Where(e => e.EventType == CalendarEventType.ClassesEnd)
            .Should()
            .OnlyContain(e => e.StartDate.DayOfWeek != DayOfWeek.Saturday && e.StartDate.DayOfWeek != DayOfWeek.Sunday);
    }

    [Fact]
    public async Task Generator_CanCreateMultipleDrafts_BackToBack_WithoutRestart()
    {
        var first = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var second = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 29),
            Utc(2026, 3, 2),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        second.DraftId.Should().NotBe(first.DraftId);
        second.Events.Should().NotBeEmpty();
        var draftCount = await _context.GeneratedCalendarDrafts.CountAsync();
        draftCount.Should().Be(2);
    }

    [Fact]
    public async Task Generator_ShiftsExcuseExam_WhenItWouldOverlap_BlockingHoliday()
    {
        await AddBlockingHolidayAsync("Test Tatili", "Test Holiday", 11, 12);

        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var excuseExam = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "midterm_excuse_exam");

        var excuseExamEnd = excuseExam.EndDate ?? excuseExam.StartDate;
        excuseExam.StartDate.Should().BeOnOrAfter(Utc(2025, 11, 13));
        excuseExamEnd.Should().BeOnOrAfter(excuseExam.StartDate);
        excuseExamEnd.Date.Should().NotBe(Utc(2025, 11, 12).Date);
    }

    [Fact]
    public async Task Generator_PlacesMidtermExcuseExam_OnNextMondayAfterMidterm_WithoutWeekend()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var midterm = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "midterm_exam");
        var excuseExam = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "midterm_excuse_exam");

        var expectedStart = MondayOnOrAfter((midterm.EndDate ?? midterm.StartDate).AddDays(1));
        excuseExam.StartDate.Should().Be(expectedStart);
        EventContainsWeekend(excuseExam).Should().BeFalse();
    }

    [Fact]
    public async Task Generator_PlacesSummerExcuseExam_OnNextMondayAfterSummerExam_WithoutWeekend()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: true,
            Constraints: null,
            TemplateDurationOverrides: null));

        var summerExam = result.Events.Single(e =>
            e.Semester == SemesterType.Summer && e.TemplateItemKey == "summer_exam");
        var excuseExam = result.Events.Single(e =>
            e.Semester == SemesterType.Summer && e.TemplateItemKey == "summer_excuse_exam");

        var expectedStart = MondayOnOrAfter((summerExam.EndDate ?? summerExam.StartDate).AddDays(1));
        excuseExam.StartDate.Should().Be(expectedStart);
        EventContainsWeekend(excuseExam).Should().BeFalse();
    }

    [Fact]
    public async Task Generator_ChoosesNextMidtermWeek_WhenPreviousWeekWouldSacrificePreMidtermWeeks()
    {
        await AddBlockingHolidayAsync("Ara Sınav Haftası Test Tatili", "Midterm Week Test Holiday", 11, 12);

        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var midterm = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "midterm_exam");
        var final = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "final_exam");
        var excuseApplication = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "midterm_excuse_application");
        var excuseExam = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "midterm_excuse_exam");
        var publication = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "midterm_publication");

        midterm.StartDate.Should().Be(Utc(2025, 11, 17));
        midterm.EndDate.Should().Be(Utc(2025, 11, 21));
        ExamEventsShouldNotOverlap(result.Events, result.Holidays);
        CountTeachingWeeksBetween(Utc(2025, 9, 21), midterm.StartDate).Should().BeGreaterThanOrEqualTo(7);
        CountTeachingWeeksBetween(midterm.EndDate!.Value, final.StartDate).Should().Be(6);

        excuseApplication.StartDate.Should().BeAfter(midterm.EndDate!.Value);
        excuseExam.StartDate.Should().BeOnOrAfter(excuseApplication.StartDate);
        publication.StartDate.Should().BeAfter(excuseExam.EndDate ?? excuseExam.StartDate);
        result.Warnings.Should().NotContain(w => w.Code == "CHRONOLOGY");
    }

    [Fact]
    public async Task Generator_ChoosesNextMidtermWeek_WhenItBestBalancesTeachingWeeks()
    {
        await AddBlockingHolidayAsync("Erken Vize Haftası Test Tatili", "Early Midterm Week Test Holiday", 11, 5);

        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints:
            [
                new CalendarConstraintInputDto(
                    CalendarConstraintType.Pin,
                    CalendarEventType.MidtermExam,
                    "MAIN",
                    """{"startDate":"2025-11-03T00:00:00Z","endDate":"2025-11-09T00:00:00Z","templateItemKey":"midterm_exam","semester":"Fall"}""",
                    IsHard: true,
                    Priority: 1,
                    Description: "Ara sinavi erken haftaya sabitle")
            ],
            TemplateDurationOverrides: null));

        var midterm = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "midterm_exam");
        var final = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "final_exam");

        midterm.StartDate.Should().Be(Utc(2025, 11, 10));
        midterm.EndDate.Should().Be(Utc(2025, 11, 14));
        ExamEventsShouldNotOverlap(result.Events, result.Holidays);
        CountTeachingWeeksBetween(Utc(2025, 9, 21), midterm.StartDate).Should().Be(7);
        CountTeachingWeeksBetween(midterm.EndDate!.Value, final.StartDate).Should().Be(7);
        result.Warnings.Should().NotContain(w => w.Code == "CHRONOLOGY");
    }

    [Fact]
    public async Task Generator_ShiftsFinalWeek_AndDependentDates_WhenSingleDayHolidayFallsInsideExamWeek()
    {
        await AddBlockingHolidayAsync("Final Haftası Test Tatili", "Final Week Test Holiday", 1, 6);

        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var final = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "final_exam");
        var finalPublication = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "final_publication");
        var makeupApplication = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "makeup_application");
        var makeupExam = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "makeup_exam");
        var makeupPublication = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "makeup_publication");

        final.StartDate.Should().Be(Utc(2026, 1, 12));
        final.EndDate.Should().Be(Utc(2026, 1, 23));
        ExamEventsShouldNotOverlap(result.Events, result.Holidays);

        finalPublication.StartDate.Should().BeAfter(final.EndDate!.Value);
        makeupApplication.StartDate.Should().BeBefore(makeupExam.StartDate);
        makeupExam.StartDate.Should().BeAfter(finalPublication.StartDate);
        makeupPublication.StartDate.Should().BeAfter(makeupExam.EndDate ?? makeupExam.StartDate);
        result.Warnings.Should().NotContain(w => w.Code == "CHRONOLOGY");
    }

    [Fact]
    public async Task Generator_HolidayShiftWarning_IncludesConcreteConflictAndNewDate()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        result.Warnings.Should().Contain(w =>
            w.Code == "HOLIDAY_SHIFT"
            && w.Message.Contains("hafta sonu (01.11.2025)", StringComparison.Ordinal)
            && w.Message.Contains("geriye", StringComparison.Ordinal)
            && w.Message.Contains("Yeni tarih: 31.10.2025", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Generator_PlacesMidterms_OnEighthWeekMonday_ToFriday()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var fallClassesStart = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "classes_start");
        var springClassesStart = result.Events.Single(e =>
            e.Semester == SemesterType.Spring && e.TemplateItemKey == "classes_start");
        var fallMidterm = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "midterm_exam");
        var springMidterm = result.Events.Single(e =>
            e.Semester == SemesterType.Spring && e.TemplateItemKey == "midterm_exam");

        fallMidterm.StartDate.Should().Be(Utc(2025, 11, 10));
        fallMidterm.EndDate.Should().Be(fallMidterm.StartDate.AddDays(4));
        fallMidterm.EndDate.Value.DayOfWeek.Should().Be(DayOfWeek.Friday);
        fallMidterm.StartDate.DayOfWeek.Should().Be(DayOfWeek.Monday);

        springMidterm.StartDate.Should().Be(springClassesStart.StartDate.AddDays(42));
        springMidterm.EndDate.Should().Be(springMidterm.StartDate.AddDays(4));
        springMidterm.EndDate.Value.DayOfWeek.Should().Be(DayOfWeek.Friday);
        springMidterm.StartDate.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public async Task Generator_PlacesFinals_OnMonday_ForTwoWeeks()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var fallFinal = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "final_exam");
        var springFinal = result.Events.Single(e =>
            e.Semester == SemesterType.Spring && e.TemplateItemKey == "final_exam");

        fallFinal.StartDate.Should().Be(Utc(2026, 1, 5));
        fallFinal.EndDate.Should().Be(fallFinal.StartDate.AddDays(11));
        fallFinal.EndDate.Value.DayOfWeek.Should().Be(DayOfWeek.Friday);
        fallFinal.StartDate.DayOfWeek.Should().Be(DayOfWeek.Monday);

        springFinal.StartDate.Should().Be(Utc(2026, 6, 8));
        springFinal.EndDate.Should().Be(springFinal.StartDate.AddDays(11));
        springFinal.EndDate.Value.DayOfWeek.Should().Be(DayOfWeek.Friday);
        springFinal.StartDate.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public async Task Generator_SchedulesMakeup_AfterFinalEnd_WithBusinessDayBuffer_AndMondayRounding()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var fallFinalPublication = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "final_publication");
        var fallMakeupApplication = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "makeup_application");
        var fallMakeupExam = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "makeup_exam");
        var springFinalPublication = result.Events.Single(e =>
            e.Semester == SemesterType.Spring && e.TemplateItemKey == "final_publication");
        var springMakeupApplication = result.Events.Single(e =>
            e.Semester == SemesterType.Spring && e.TemplateItemKey == "makeup_application");
        var springMakeupExam = result.Events.Single(e =>
            e.Semester == SemesterType.Spring && e.TemplateItemKey == "makeup_exam");

        fallMakeupExam.StartDate.Should().Be(Utc(2026, 1, 26));
        fallMakeupExam.EndDate.Should().Be(Utc(2026, 1, 30));
        fallMakeupExam.StartDate.DayOfWeek.Should().Be(DayOfWeek.Monday);
        fallMakeupExam.EndDate!.Value.DayOfWeek.Should().Be(DayOfWeek.Friday);
        fallMakeupExam.StartDate.Should().BeOnOrAfter(fallFinalPublication.StartDate.AddDays(4));
        fallMakeupApplication.StartDate.Should().Be(Utc(2026, 1, 23));

        springMakeupExam.StartDate.Should().Be(Utc(2026, 6, 29));
        springMakeupExam.EndDate.Should().Be(Utc(2026, 7, 3));
        springMakeupExam.StartDate.DayOfWeek.Should().Be(DayOfWeek.Monday);
        springMakeupExam.EndDate!.Value.DayOfWeek.Should().Be(DayOfWeek.Friday);
        springMakeupExam.StartDate.Should().BeOnOrAfter(springFinalPublication.StartDate.AddDays(4));
        springMakeupApplication.StartDate.Should().Be(Utc(2026, 6, 26));
    }

    [Fact]
    public async Task Generator_RealignsPersistedLegacyMainTemplate_ToMondayMidtermRule()
    {
        await _templateService.ReplaceTemplatesAsync(
        [
            new UpsertCalendarTemplateDto(
                "Ana Akademik SÃ¼reÃ§",
                "MAIN",
                true,
                [
                    new CalendarTemplateItemDto("classes_start", CalendarEventType.ClassesStart, "Derslerin Başlaması", 1, CalendarAnchorType.FromTermStart, null, 0, [SemesterType.Fall, SemesterType.Spring], true, 1),
                    new CalendarTemplateItemDto("midterm_exam", CalendarEventType.MidtermExam, "Ara SÄ±navlar", 6, CalendarAnchorType.FromTermStart, null, 43, [SemesterType.Fall, SemesterType.Spring], true, 2),
                    new CalendarTemplateItemDto("final_exam", CalendarEventType.FinalExam, "DÃ¶nem Sonu SÄ±navlarÄ±", 12, CalendarAnchorType.FromTermStart, null, 105, [SemesterType.Fall, SemesterType.Spring], true, 3)
                ]),
            new UpsertCalendarTemplateDto(
                "Yaz Ã–ÄŸretimi",
                "SUMMER",
                true,
                [
                    new CalendarTemplateItemDto("summer_classes_start", CalendarEventType.ClassesStart, "Yaz Ã–ÄŸretimi Derslerin Başlaması", 1, CalendarAnchorType.FromTermStart, null, 0, [SemesterType.Summer], true, 1),
                    new CalendarTemplateItemDto("summer_classes_end", CalendarEventType.ClassesEnd, "Yaz Ã–ÄŸretimi Derslerin Sona Ermesi", 1, CalendarAnchorType.FromTermStart, null, 46, [SemesterType.Summer], true, 2),
                    new CalendarTemplateItemDto("summer_exam", CalendarEventType.SummerSchoolExam, "Yaz Ã–ÄŸretimi SÄ±navlarÄ±", 5, CalendarAnchorType.FromTermStart, null, 49, [SemesterType.Summer], true, 3)
                ])
        ]);

        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var fallMidterm = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "midterm_exam");

        fallMidterm.StartDate.Should().Be(Utc(2025, 11, 10));
        fallMidterm.StartDate.DayOfWeek.Should().Be(DayOfWeek.Monday);
        fallMidterm.EndDate.Should().Be(Utc(2025, 11, 14));
    }

    [Fact]
    public async Task Generator_AlignsSpringMidterm_ToMonday()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var springMidterm = result.Events.Single(e =>
            e.Semester == SemesterType.Spring && e.TemplateItemKey == "midterm_exam");

        springMidterm.StartDate.Should().Be(Utc(2026, 4, 6));
        springMidterm.StartDate.DayOfWeek.Should().Be(DayOfWeek.Monday);
        springMidterm.EndDate.Should().Be(Utc(2026, 4, 10));
    }

    [Fact]
    public async Task Generator_ShiftsWeekendPinnedSingleDayEvent_Backward_InsteadOfForward()
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
                    CalendarEventType.CourseWithdrawal,
                    "MAIN",
                    """{"startDate":"2025-11-01T00:00:00Z","endDate":"2025-11-01T00:00:00Z"}""",
                    IsHard: true,
                    Priority: 1,
                    Description: "Dersten Ã§ekilme tarihini cumartesiye sabitle")
            ],
            TemplateDurationOverrides: null));

        var withdrawal = result.Events.Single(e =>
            e.Semester == SemesterType.Fall && e.TemplateItemKey == "withdrawal_deadline");

        withdrawal.StartDate.Should().Be(Utc(2025, 11, 10));
        result.Warnings.Should().Contain(w =>
            w.Code == "HOLIDAY_SHIFT"
            && w.Message.Contains("hafta sonu (01.11.2025)", StringComparison.Ordinal)
            && w.Message.Contains("geriye", StringComparison.Ordinal)
            && w.Message.Contains("Yeni tarih: 31.10.2025", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetDraft_RemovesLegacyShiftWarnings_FromStoredDraft()
    {
        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var draft = await _context.GeneratedCalendarDrafts.SingleAsync(d => d.Id == result.DraftId);
        draft.WarningsJson = Json(new List<CalendarWarningDto>
        {
            new CalendarWarningDto("HOLIDAY_SHIFT", "Warning", "old generic shift warning", "legacy-event"),
            new CalendarWarningDto("REG_TEST", "Error", "real warning", "real-event")
        });
        draft.QualityScore = 75;
        await _context.SaveChangesAsync();

        var loaded = await _generatorService.GetDraftAsync(result.DraftId);

        loaded.Warnings.Should().NotContain(w => w.Code == "HOLIDAY_SHIFT");
        loaded.Warnings.Should().ContainSingle(w => w.Code == "REG_TEST");
        loaded.QualityScore.Should().Be(80);

        var persisted = await _context.GeneratedCalendarDrafts.AsNoTracking().SingleAsync(d => d.Id == result.DraftId);
        persisted.WarningsJson.Should().NotContain("old generic shift warning");
        persisted.QualityScore.Should().Be(80);
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
        var fallTerm = new AcademicTerm
        {
            Name = "2025-2026 Güz",
            ShortName = "25-26 Güz",
            EnglishName = "Fall 2025-2026",
            Year = 2025,
            TermType = TermType.Fall,
            TermStart = Utc(2025, 9, 22),
            TermEnd = Utc(2026, 1, 2),
            AcademicCalendar = new TermAcademicCalendar
            {
                RegistrationStart = Utc(2025, 9, 15),
                RegistrationEnd = Utc(2025, 9, 19),
                AddDropStart = Utc(2025, 9, 29),
                AddDropEnd = Utc(2025, 10, 3),
                WithdrawalDeadline = Utc(2025, 11, 7),
                GradeEntryStart = Utc(2026, 1, 5),
                GradeEntryEnd = Utc(2026, 1, 21),
                CreatedAt = DateTime.UtcNow
            }
        };
        var springTerm = new AcademicTerm
        {
            Name = "2025-2026 Bahar",
            ShortName = "25-26 Bahar",
            EnglishName = "Spring 2025-2026",
            Year = 2025,
            TermType = TermType.Spring,
            TermStart = Utc(2026, 2, 23),
            TermEnd = Utc(2026, 6, 5),
            AcademicCalendar = new TermAcademicCalendar
            {
                RegistrationStart = Utc(2026, 2, 16),
                RegistrationEnd = Utc(2026, 2, 20),
                AddDropStart = Utc(2026, 3, 2),
                AddDropEnd = Utc(2026, 3, 6),
                WithdrawalDeadline = Utc(2026, 4, 3),
                GradeEntryStart = Utc(2026, 6, 8),
                GradeEntryEnd = Utc(2026, 6, 24),
                CreatedAt = DateTime.UtcNow
            }
        };
        _context.AcademicTerms.AddRange(fallTerm, springTerm);
        await _context.SaveChangesAsync();

        _context.TermCalendarEvents.Add(new TermCalendarEvent
        {
            TermId = fallTerm.Id,
            EventType = CalendarEventType.Other,
            Title = "Eski Takvim İşareti",
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

        await _generatorService.ApplyAsync(result.DraftId, new ApplyCalendarDraftRequestDto(ForceOverwrite: true), userId: null);

        var titles = await _context.TermCalendarEvents
            .Where(e => e.TermId == fallTerm.Id)
            .Select(e => e.Title)
            .ToListAsync();

        titles.Should().NotContain("Eski Takvim Isareti");
        titles.Should().Contain("Derslerin Başlaması");
    }

    [Fact]
    public async Task Apply_UsesMatchingTerms_WhenAcademicTermsDoNotMatchDraftSemesters()
    {
        _context.AcademicTerms.Add(new AcademicTerm
        {
            Name = "2025-2026 Guz",
            ShortName = "25-26 Guz",
            EnglishName = "Fall 2025-2026",
            Year = 2025,
            TermType = TermType.Fall,
            TermStart = Utc(2025, 9, 22),
            TermEnd = Utc(2026, 1, 2),
            AcademicCalendar = new TermAcademicCalendar
            {
                RegistrationStart = Utc(2025, 9, 15),
                RegistrationEnd = Utc(2025, 9, 19),
                AddDropStart = Utc(2025, 9, 29),
                AddDropEnd = Utc(2025, 10, 3),
                WithdrawalDeadline = Utc(2025, 11, 7),
                GradeEntryStart = Utc(2026, 1, 5),
                GradeEntryEnd = Utc(2026, 1, 21),
                CreatedAt = DateTime.UtcNow
            }
        });
        await _context.SaveChangesAsync();

        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var applied = await _generatorService.ApplyAsync(result.DraftId, new ApplyCalendarDraftRequestDto(), userId: null);

        applied.Success.Should().BeTrue();
        applied.Message.Should().Contain("Atlanan dönemler");
        applied.Message.Should().Contain("Bahar");
        applied.TermIds.Should().ContainSingle();

        var titles = await _context.TermCalendarEvents
            .Where(e => e.TermId == 1)
            .Select(e => e.Title)
            .ToListAsync();

        titles.Should().Contain("Derslerin Başlaması");
    }

    [Fact]
    public async Task Apply_SkipsCancelledTerms_AndAppliesRemainingTerms()
    {
        var fallTerm = new AcademicTerm
        {
            Name = "2025-2026 Güz",
            ShortName = "25-26 Güz",
            EnglishName = "Fall 2025-2026",
            Year = 2025,
            TermType = TermType.Fall,
            TermStart = Utc(2025, 9, 22),
            TermEnd = Utc(2026, 1, 2),
            IsCancelled = true
        };
        var springTerm = new AcademicTerm
        {
            Name = "2025-2026 Bahar",
            ShortName = "25-26 Bahar",
            EnglishName = "Spring 2025-2026",
            Year = 2025,
            TermType = TermType.Spring,
            TermStart = Utc(2026, 2, 23),
            TermEnd = Utc(2026, 6, 5)
        };
        _context.AcademicTerms.AddRange(fallTerm, springTerm);
        await _context.SaveChangesAsync();

        var result = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var applied = await _generatorService.ApplyAsync(result.DraftId, new ApplyCalendarDraftRequestDto(), userId: null);

        applied.Success.Should().BeTrue();
        applied.TermIds.Should().ContainSingle().Which.Should().Be(springTerm.Id);
        applied.Message.Should().Contain("Güz");
        applied.Message.Should().Contain("iptal");

        var fallEvents = await _context.TermCalendarEvents
            .Where(e => e.TermId == fallTerm.Id)
            .ToListAsync();
        fallEvents.Should().BeEmpty();

        var springTitles = await _context.TermCalendarEvents
            .Where(e => e.TermId == springTerm.Id)
            .Select(e => e.Title)
            .ToListAsync();
        springTitles.Should().Contain("Derslerin Başlaması");
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
        shiftedMakeup.StartDate.Should().Be(Utc(2026, 2, 2));
        shiftedMakeup.EndDate.Should().Be(Utc(2026, 2, 6));
    }

    [Fact]
    public async Task WhatIf_RecalculatesMidtermPublicationDeadline_WhenSpringMidtermMovesEarlier()
    {
        var generated = await _generatorService.GenerateAsync(new GenerateCalendarRequestDto(
            "2025-2026",
            Utc(2025, 9, 22),
            Utc(2026, 2, 23),
            IncludeSummer: false,
            Constraints: null,
            TemplateDurationOverrides: null));

        var springMidterm = generated.Events.Single(e =>
            e.Semester == SemesterType.Spring && e.TemplateItemKey == "midterm_exam");

        var whatIf = await _generatorService.WhatIfAsync(
            generated.DraftId,
            new WhatIfRequestDto(
                springMidterm.DraftEventId,
                Utc(2026, 4, 1),
                Utc(2026, 4, 7)));

        var excuseApplication = whatIf.Events.Single(e =>
            e.Semester == SemesterType.Spring && e.TemplateItemKey == "midterm_excuse_application");
        var excuseExam = whatIf.Events.Single(e =>
            e.Semester == SemesterType.Spring && e.TemplateItemKey == "midterm_excuse_exam");
        var publication = whatIf.Events.Single(e =>
            e.Semester == SemesterType.Spring && e.TemplateItemKey == "midterm_publication");

        excuseApplication.StartDate.Should().Be(Utc(2026, 4, 8));
        excuseExam.StartDate.Should().Be(MondayOnOrAfter(Utc(2026, 4, 8)));
        EventContainsWeekend(excuseExam).Should().BeFalse();
        publication.StartDate.Should().BeAfter(excuseExam.EndDate ?? excuseExam.StartDate);
        publication.StartDate.Should().BeOnOrBefore(Utc(2026, 4, 21));
        whatIf.Warnings.Should().NotContain(w => w.Code == "REG_GRADE_PUBLICATION");
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
    public async Task WhatIf_RejectsCriticalEvent_WhenMovedOntoBlockingHoliday()
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

        var act = () => _generatorService.WhatIfAsync(
            generated.DraftId,
            new WhatIfRequestDto(final.DraftEventId, Utc(2026, 1, 1), Utc(2026, 1, 12)));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("resmi tatil");
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
        shiftedMakeup.StartDate.Should().Be(Utc(2026, 2, 2));
        shiftedMakeup.EndDate.Should().Be(Utc(2026, 2, 6));
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
            TermType = TermType.Fall,
            TermStart = Utc(2025, 9, 22),
            TermEnd = Utc(2026, 1, 2),
            AcademicCalendar = new TermAcademicCalendar
            {
                RegistrationStart = Utc(2025, 9, 15),
                RegistrationEnd = Utc(2025, 9, 19),
                AddDropStart = Utc(2025, 9, 29),
                AddDropEnd = Utc(2025, 10, 3),
                WithdrawalDeadline = Utc(2025, 11, 7),
                GradeEntryStart = Utc(2026, 1, 5),
                GradeEntryEnd = Utc(2026, 1, 21),
                CreatedAt = DateTime.UtcNow
            }
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

    private async Task AddBlockingHolidayAsync(string name, string englishName, int month, int day)
    {
        await _templateService.UpsertHolidayDefinitionAsync(
            id: null,
            new UpsertHolidayDefinitionDto(
                name,
                englishName,
                HolidayType.Fixed,
                FixedMonth: month,
                FixedDay: day,
                HijriMonth: null,
                HijriDay: null,
                DurationDays: 1,
                IsHalfDayStart: false,
                IsOfficialHoliday: true,
                BlocksAcademicEvents: true,
                Source: "Test",
                Overrides: [],
                IsActive: true));
    }

    private static void ExamEventsShouldNotOverlap(
        IReadOnlyList<GeneratedCalendarEventDto> events,
        IReadOnlyList<ResolvedHolidayDto> holidays)
    {
        var blockingHolidays = holidays.Where(h => h.BlocksAcademicEvents).ToList();
        var examTypes = new[]
        {
            CalendarEventType.MidtermExam,
            CalendarEventType.FinalExam,
            CalendarEventType.MakeupExam,
            CalendarEventType.PrepSchoolExam,
            CalendarEventType.SummerSchoolExam
        };

        events
            .Where(e => examTypes.Contains(e.EventType))
            .Should()
            .OnlyContain(e => blockingHolidays.All(h =>
                (e.EndDate ?? e.StartDate).Date < h.StartDate.Date || e.StartDate.Date > h.EndDate.Date));
    }

    private static int CountTeachingWeeksBetween(DateTime fromExclusive, DateTime toExclusive)
    {
        var count = 0;
        for (var weekStart = MondayOnOrAfter(fromExclusive.AddDays(1)); weekStart < toExclusive.Date; weekStart = weekStart.AddDays(7))
            count++;

        return count;
    }

    private static DateTime MondayOnOrAfter(DateTime date)
    {
        var current = date.Date;
        while (current.DayOfWeek != DayOfWeek.Monday)
            current = current.AddDays(1);

        return Utc(current.Year, current.Month, current.Day);
    }

    private static bool EventContainsWeekend(GeneratedCalendarEventDto ev)
    {
        var end = ev.EndDate ?? ev.StartDate;
        for (var day = ev.StartDate.Date; day <= end.Date; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                return true;
        }

        return false;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private static string Json<T>(T value) =>
        JsonSerializer.Serialize(value, TestJsonOptions);
}



