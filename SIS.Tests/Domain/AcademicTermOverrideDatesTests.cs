using FluentAssertions;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using Xunit;

namespace SIS.Tests.Domain;

/// <summary>
/// AcademicTerm aktiflik + EffectiveStart/EffectiveEnd davranışı.
///
/// Yeni model (Proliz/Banner): aktiflik manuel <see cref="AcademicTerm.IsActivated"/>
/// bayrağına bağlıdır; TermStart/TermEnd ve OverrideStart/OverrideEnd tarihleri
/// aktifliği ARTIK BELİRLEMEZ. Override tarihleri yalnızca EffectiveStart/EffectiveEnd
/// computed alanlarını besler (görüntüleme/raporlama için dormant kalır).
/// </summary>
public class AcademicTermOverrideDatesTests
{
    private static DateTime U(int y, int m, int d) =>
        new(y, m, d, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GetStatus_Inactive_When_NotActivated_RegardlessOfDate()
    {
        var term = new AcademicTerm
        {
            IsActivated = false,
            TermStart = U(2026, 2, 1),
            TermEnd = U(2026, 6, 1),
        };

        // Tarih aralığının içinde olsa bile: açık değil → Inactive.
        term.GetStatus(U(2026, 4, 24)).Should().Be(TermStatus.Inactive);
    }

    [Fact]
    public void GetStatus_Active_When_Activated_RegardlessOfDate()
    {
        var term = new AcademicTerm
        {
            IsActivated = true,
            TermStart = U(2027, 2, 1),   // gelecek dönem
            TermEnd = U(2027, 6, 1),
        };

        // Dönem henüz başlamamış olsa da "Kullanıma Açık" → Active.
        term.GetStatus(U(2026, 4, 24)).Should().Be(TermStatus.Active);
    }

    [Fact]
    public void GetStatus_Cancelled_Trumps_Activated()
    {
        var term = new AcademicTerm
        {
            IsActivated = true,
            IsCancelled = true,
            TermStart = U(2026, 2, 1),
            TermEnd = U(2026, 6, 1),
        };

        term.GetStatus(U(2026, 4, 24)).Should().Be(TermStatus.Cancelled);
        term.IsActive(U(2026, 4, 24)).Should().BeFalse();
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
    public void EffectiveStart_Uses_Override_When_Present()
    {
        var term = new AcademicTerm
        {
            TermStart = U(2027, 2, 1),
            TermEnd = U(2027, 6, 1),
            OverrideStart = U(2026, 4, 1),
            OverrideEnd = U(2027, 7, 1),
        };

        term.EffectiveStart.Should().Be(U(2026, 4, 1));
        term.EffectiveEnd.Should().Be(U(2027, 7, 1));
    }
}
