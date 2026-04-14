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

        holidays.Single(h => h.Name == "Ramazan Bayrami").Should().BeEquivalentTo(new
        {
            StartDate = Utc(2026, 3, 19),
            EndDate = Utc(2026, 3, 22),
            IsEstimated = false,
            Source = "Diyanet"
        });
        holidays.Single(h => h.Name == "Kurban Bayrami").Should().BeEquivalentTo(new
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

        holidays.Should().Contain(h => h.Name == "Yilbasi Tatili" && h.StartDate == Utc(2026, 1, 1));
        holidays.Should().Contain(h => h.Name == "Ulusal Egemenlik ve Cocuk Bayrami" && h.StartDate == Utc(2026, 4, 23));
        holidays.Should().Contain(h => h.Name == "Emek ve Dayanisma Gunu" && h.StartDate == Utc(2026, 5, 1));
        holidays.Should().Contain(h => h.Name == "Ataturk'u Anma, Genclik ve Spor Bayrami" && h.StartDate == Utc(2026, 5, 19));
        holidays.Should().Contain(h => h.Name == "Demokrasi ve Milli Birlik Gunu" && h.StartDate == Utc(2026, 7, 15));
        holidays.Should().Contain(h => h.Name == "Zafer Bayrami" && h.StartDate == Utc(2026, 8, 30));
        holidays.Should().Contain(h => h.Name == "Cumhuriyet Bayrami" && h.StartDate == Utc(2026, 10, 28) && h.EndDate == Utc(2026, 10, 29));
        holidays.Should().Contain(h => h.Name == "Ataturk'u Anma Gunu" && h.StartDate == Utc(2026, 11, 10) && !h.IsOfficialHoliday && h.BlocksAcademicEvents);
    }

    [Fact]
    public async Task HolidayCalculator_UsesHijriFallback_WhenDiyanetOverrideIsMissing()
    {
        var holidays = await _holidayService.ResolveHolidaysAsync(2027, 2027);

        holidays.Should().Contain(h => h.Name == "Ramazan Bayrami" && h.IsEstimated && h.Source.EndsWith(":fallback"));
        holidays.Should().Contain(h => h.Name == "Kurban Bayrami" && h.IsEstimated && h.Source.EndsWith(":fallback"));
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
    public async Task AcademicCalendarRules_HideLetterGrade_BeforePublicationDate()
    {
        var term = new AcademicTerm
        {
            Id = 1,
            Name = "2025-2026 Guz",
            ShortName = "25-26 Guz",
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
            Title = "Final Not Ilani",
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
