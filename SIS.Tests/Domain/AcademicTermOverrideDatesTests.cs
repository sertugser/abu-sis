using FluentAssertions;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using Xunit;

namespace SIS.Tests.Domain;

/// <summary>
/// AcademicTerm.OverrideStart + OverrideEnd davranışı için birim testler.
/// Kural: OverrideStart varsa efektif başlangıç onun; OverrideEnd varsa efektif bitiş onun.
/// Null ise TermStart/TermEnd kullanılır.
/// </summary>
public class AcademicTermOverrideDatesTests
{
    private static DateTime U(int y, int m, int d) =>
        new(y, m, d, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GetStatus_Returns_Inactive_When_No_Override_And_Today_Before_TermStart()
    {
        var term = new AcademicTerm
        {
            TermStart = U(2027, 2, 1),
            TermEnd = U(2027, 6, 1),
        };

        var status = term.GetStatus(U(2026, 4, 24));

        status.Should().Be(TermStatus.Inactive);
        term.EffectiveStart.Should().Be(term.TermStart);
        term.EffectiveEnd.Should().Be(term.TermEnd);
    }

    [Fact]
    public void GetStatus_Returns_Active_When_OverrideStart_Brings_Term_Earlier()
    {
        var term = new AcademicTerm
        {
            TermStart = U(2027, 2, 1),       // gelecek
            TermEnd = U(2027, 6, 1),
            OverrideStart = U(2026, 4, 1),   // erken aktivasyon — bugünden önce
        };

        var status = term.GetStatus(U(2026, 4, 24));

        status.Should().Be(TermStatus.Active);
        term.EffectiveStart.Should().Be(U(2026, 4, 1));
        term.EffectiveEnd.Should().Be(U(2027, 6, 1));
    }

    [Fact]
    public void GetStatus_Returns_Active_When_OverrideEnd_Extends_Term()
    {
        var term = new AcademicTerm
        {
            TermStart = U(2026, 2, 1),
            TermEnd = U(2026, 3, 1),        // bitmiş
            OverrideEnd = U(2026, 6, 30),   // uzatma — bugünden sonra
        };

        var status = term.GetStatus(U(2026, 4, 24));

        status.Should().Be(TermStatus.Active);
        term.EffectiveEnd.Should().Be(U(2026, 6, 30));
    }

    [Fact]
    public void GetStatus_Returns_Inactive_When_OverrideEnd_Also_In_Past()
    {
        var term = new AcademicTerm
        {
            TermStart = U(2026, 1, 1),
            TermEnd = U(2026, 2, 1),
            OverrideEnd = U(2026, 3, 1),    // uzatma ama yine geçmiş
        };

        var status = term.GetStatus(U(2026, 4, 24));

        status.Should().Be(TermStatus.Inactive);
    }

    [Fact]
    public void GetStatus_Cancelled_Trumps_Override()
    {
        var term = new AcademicTerm
        {
            TermStart = U(2026, 2, 1),
            TermEnd = U(2026, 6, 1),
            OverrideStart = U(2026, 1, 1),
            OverrideEnd = U(2027, 1, 1),
            IsCancelled = true,
        };

        var status = term.GetStatus(U(2026, 4, 24));

        status.Should().Be(TermStatus.Cancelled);
    }

    [Fact]
    public void EffectiveStart_Falls_Back_To_TermStart_When_OverrideStart_Null()
    {
        var term = new AcademicTerm
        {
            TermStart = U(2026, 2, 16),
            TermEnd = U(2026, 6, 5),
            OverrideStart = null,
            OverrideEnd = null,
        };

        term.EffectiveStart.Should().Be(U(2026, 2, 16));
        term.EffectiveEnd.Should().Be(U(2026, 6, 5));
    }

    [Fact]
    public void IsActive_False_For_Cancelled_Even_Within_Override_Range()
    {
        var term = new AcademicTerm
        {
            TermStart = U(2026, 2, 1),
            TermEnd = U(2026, 6, 1),
            OverrideStart = U(2026, 1, 1),
            OverrideEnd = U(2027, 1, 1),
            IsCancelled = true,
        };

        term.IsActive(U(2026, 4, 24)).Should().BeFalse();
    }
}
