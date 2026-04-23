using FluentAssertions;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using Xunit;

namespace SIS.Tests.Domain;

public class AcademicTermPhaseTests
{
    private static AcademicTerm BuildTerm(
        bool isCancelled = false,
        DateTime? regStart = null,
        DateTime? regEnd = null,
        DateTime? addDropStart = null,
        DateTime? addDropEnd = null,
        DateTime? withdrawalDeadline = null,
        DateTime? termStart = null,
        DateTime? termEnd = null,
        DateTime? gradeStart = null,
        DateTime? gradeEnd = null,
        bool? regOverride = null,
        bool? addDropOverride = null,
        bool? withdrawalOverride = null,
        bool? gradeOverride = null)
    {
        var d = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var ts = termStart ?? d;
        var te = termEnd ?? d.AddDays(120);
        return new AcademicTerm
        {
            IsCancelled = isCancelled,
            TermStart = ts,
            TermEnd = te,
            AcademicCalendar = new SIS.Domain.Entities.TermAcademicCalendar
            {
                RegistrationStart = regStart ?? d,
                RegistrationEnd = regEnd ?? d.AddDays(14),
                AddDropStart = addDropStart ?? d.AddDays(14),
                AddDropEnd = addDropEnd ?? d.AddDays(21),
                WithdrawalDeadline = withdrawalDeadline ?? d.AddDays(90),
                GradeEntryStart = gradeStart ?? d.AddDays(110),
                GradeEntryEnd = gradeEnd ?? d.AddDays(130),
                RegistrationOverride = regOverride,
                AddDropOverride = addDropOverride,
                WithdrawalOverride = withdrawalOverride,
                GradeEntryOverride = gradeOverride,
                CreatedAt = DateTime.UtcNow,
            }
        };
    }

    [Fact]
    public void IsRegistrationOpen_ReturnsFalse_WhenCancelled()
    {
        var term = BuildTerm(isCancelled: true);
        // Use a time that's within the term dates — still blocked because cancelled
        var now = term.TermStart.AddDays(1);
        term.IsRegistrationOpen(now).Should().BeFalse();
    }

    [Fact]
    public void IsRegistrationOpen_ReturnsTrue_WhenInDateRange_AndActive()
    {
        // Term is active: now is within [TermStart, TermEnd]
        var term = BuildTerm();
        var now = term.TermStart.AddDays(1); // within term dates
        term.IsRegistrationOpen(now).Should().BeTrue();
    }

    [Fact]
    public void IsRegistrationOpen_ReturnsFalse_WhenBeforeStart()
    {
        var term = BuildTerm();
        var now = term.AcademicCalendar!.RegistrationStart.AddDays(-1);
        term.IsRegistrationOpen(now).Should().BeFalse();
    }

    [Fact]
    public void IsRegistrationOpen_ReturnsFalse_WhenAfterEnd()
    {
        var term = BuildTerm();
        var now = term.AcademicCalendar!.RegistrationEnd.AddDays(1);
        term.IsRegistrationOpen(now).Should().BeFalse();
    }

    [Fact]
    public void IsRegistrationOpen_OverrideTrue_IgnoresDate()
    {
        var term = BuildTerm(regOverride: true);
        // now is within term dates (active) but past registration window
        var now = term.TermStart.AddDays(30);
        term.IsRegistrationOpen(now).Should().BeTrue();
    }

    [Fact]
    public void IsRegistrationOpen_OverrideFalse_IgnoresDate()
    {
        var term = BuildTerm(regOverride: false);
        var now = term.AcademicCalendar!.RegistrationStart.AddDays(1);
        term.IsRegistrationOpen(now).Should().BeFalse();
    }

    [Fact]
    public void IsRegistrationOpen_OverrideNull_RespectsDate()
    {
        var term = BuildTerm(regOverride: null);
        term.IsRegistrationOpen(term.AcademicCalendar!.RegistrationStart.AddDays(1)).Should().BeTrue();
        term.IsRegistrationOpen(term.AcademicCalendar!.RegistrationEnd.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void IsRegistrationOpen_OverrideTrue_StillRequiresActive()
    {
        // Term is not yet started (termStart is in the future from 'now')
        var futureStart = DateTime.UtcNow.AddDays(30);
        var term = BuildTerm(
            termStart: futureStart,
            termEnd: futureStart.AddDays(120),
            regOverride: true);
        // now = today, term hasn't started → IsActive returns false → IsRegistrationOpen false
        term.IsRegistrationOpen(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsAddDropOpen_FollowsSameRules()
    {
        var term = BuildTerm();
        term.IsAddDropOpen(term.AcademicCalendar!.AddDropStart.AddDays(1)).Should().BeTrue();
        term.IsAddDropOpen(term.AcademicCalendar!.AddDropEnd.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void IsWithdrawalOpen_UsesTermStartAndWithdrawalDeadline()
    {
        var term = BuildTerm();
        term.IsWithdrawalOpen(term.TermStart.AddDays(1)).Should().BeTrue();
        term.IsWithdrawalOpen(term.AcademicCalendar!.WithdrawalDeadline.AddDays(1)).Should().BeFalse();
        term.IsWithdrawalOpen(term.TermStart.AddDays(-1)).Should().BeFalse();
    }

    [Fact]
    public void IsGradeEntryOpen_UsesGradeEntryStartEnd()
    {
        var term = BuildTerm();
        term.IsGradeEntryOpen(term.AcademicCalendar!.GradeEntryStart.AddDays(1)).Should().BeTrue();
        term.IsGradeEntryOpen(term.AcademicCalendar!.GradeEntryEnd.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void IsActive_TrueWhenNowWithinTermDatesAndNotCancelled()
    {
        var term = BuildTerm();
        // now is within [TermStart, TermEnd]
        term.IsActive(term.TermStart.AddDays(1)).Should().BeTrue();
    }

    [Fact]
    public void IsActive_FalseWhenCancelled()
    {
        var term = BuildTerm(isCancelled: true);
        term.IsActive(term.TermStart.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void IsActive_FalseWhenBeforeTermStart()
    {
        var term = BuildTerm();
        term.IsActive(term.TermStart.AddDays(-1)).Should().BeFalse();
    }

    [Fact]
    public void IsActive_FalseWhenAfterTermEnd()
    {
        var term = BuildTerm();
        term.IsActive(term.TermEnd.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void GetStatus_ReturnsActive_WhenWithinDatesAndNotCancelled()
    {
        var term = BuildTerm();
        term.GetStatus(term.TermStart.AddDays(1)).Should().Be(TermStatus.Active);
    }

    [Fact]
    public void GetStatus_ReturnsCancelled_WhenCancelled()
    {
        var term = BuildTerm(isCancelled: true);
        term.GetStatus(term.TermStart.AddDays(1)).Should().Be(TermStatus.Cancelled);
    }

    [Fact]
    public void GetStatus_ReturnsInactive_WhenBeforeTerm()
    {
        var term = BuildTerm();
        term.GetStatus(term.TermStart.AddDays(-1)).Should().Be(TermStatus.Inactive);
    }
}
