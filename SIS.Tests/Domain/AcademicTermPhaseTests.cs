using FluentAssertions;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using Xunit;

namespace SIS.Tests.Domain;

public class AcademicTermPhaseTests
{
    private static AcademicTerm BuildTerm(
        TermStatus status = TermStatus.Ready,
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
        return new AcademicTerm
        {
            Status = status,
            RegistrationStart = regStart ?? d,
            RegistrationEnd = regEnd ?? d.AddDays(14),
            AddDropStart = addDropStart ?? d.AddDays(14),
            AddDropEnd = addDropEnd ?? d.AddDays(21),
            WithdrawalDeadline = withdrawalDeadline ?? d.AddDays(90),
            TermStart = termStart ?? d,
            TermEnd = termEnd ?? d.AddDays(120),
            GradeEntryStart = gradeStart ?? d.AddDays(110),
            GradeEntryEnd = gradeEnd ?? d.AddDays(130),
            RegistrationOverride = regOverride,
            AddDropOverride = addDropOverride,
            WithdrawalOverride = withdrawalOverride,
            GradeEntryOverride = gradeOverride
        };
    }

    [Fact]
    public void IsRegistrationOpen_ReturnsFalse_WhenStatusNotReady()
    {
        var term = BuildTerm(status: TermStatus.Preparing);
        var now = term.RegistrationStart.AddDays(1);
        term.IsRegistrationOpen(now).Should().BeFalse();
    }

    [Fact]
    public void IsRegistrationOpen_ReturnsTrue_WhenInDateRange_AndReady()
    {
        var term = BuildTerm();
        var now = term.RegistrationStart.AddDays(1);
        term.IsRegistrationOpen(now).Should().BeTrue();
    }

    [Fact]
    public void IsRegistrationOpen_ReturnsFalse_WhenBeforeStart()
    {
        var term = BuildTerm();
        var now = term.RegistrationStart.AddDays(-1);
        term.IsRegistrationOpen(now).Should().BeFalse();
    }

    [Fact]
    public void IsRegistrationOpen_ReturnsFalse_WhenAfterEnd()
    {
        var term = BuildTerm();
        var now = term.RegistrationEnd.AddDays(1);
        term.IsRegistrationOpen(now).Should().BeFalse();
    }

    [Fact]
    public void IsRegistrationOpen_OverrideTrue_IgnoresDate()
    {
        var term = BuildTerm(regOverride: true);
        var now = term.RegistrationEnd.AddDays(30);
        term.IsRegistrationOpen(now).Should().BeTrue();
    }

    [Fact]
    public void IsRegistrationOpen_OverrideFalse_IgnoresDate()
    {
        var term = BuildTerm(regOverride: false);
        var now = term.RegistrationStart.AddDays(1);
        term.IsRegistrationOpen(now).Should().BeFalse();
    }

    [Fact]
    public void IsRegistrationOpen_OverrideNull_RespectsDate()
    {
        var term = BuildTerm(regOverride: null);
        term.IsRegistrationOpen(term.RegistrationStart.AddDays(1)).Should().BeTrue();
        term.IsRegistrationOpen(term.RegistrationEnd.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void IsRegistrationOpen_OverrideTrue_StillRequiresReady()
    {
        var term = BuildTerm(status: TermStatus.Draft, regOverride: true);
        term.IsRegistrationOpen(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsAddDropOpen_FollowsSameRules()
    {
        var term = BuildTerm();
        term.IsAddDropOpen(term.AddDropStart.AddDays(1)).Should().BeTrue();
        term.IsAddDropOpen(term.AddDropEnd.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void IsWithdrawalOpen_UsesTermStartAndWithdrawalDeadline()
    {
        var term = BuildTerm();
        term.IsWithdrawalOpen(term.TermStart.AddDays(1)).Should().BeTrue();
        term.IsWithdrawalOpen(term.WithdrawalDeadline.AddDays(1)).Should().BeFalse();
        term.IsWithdrawalOpen(term.TermStart.AddDays(-1)).Should().BeFalse();
    }

    [Fact]
    public void IsGradeEntryOpen_UsesGradeEntryStartEnd()
    {
        var term = BuildTerm();
        term.IsGradeEntryOpen(term.GradeEntryStart.AddDays(1)).Should().BeTrue();
        term.IsGradeEntryOpen(term.GradeEntryEnd.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void IsActive_EqualsReadyStatus()
    {
        BuildTerm(status: TermStatus.Ready).IsActive.Should().BeTrue();
        BuildTerm(status: TermStatus.Preparing).IsActive.Should().BeFalse();
        BuildTerm(status: TermStatus.Draft).IsActive.Should().BeFalse();
        BuildTerm(status: TermStatus.Closed).IsActive.Should().BeFalse();
    }
}
