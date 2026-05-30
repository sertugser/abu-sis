using FluentAssertions;
using SIS.Application.Grading;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Tests.Application;

/// <summary>
/// Unit tests for AttendanceAbsenceCalculator.IsFailedByAbsence (method-aware overload).
///
/// The method-aware signature:
///   IsFailedByAbsence(AttendanceMethod, theoryAbs, labAbs, theoryLimit, labLimit, totalLimit)
///
/// Rules under test:
///   Separate  → fails if theoryAbs > theoryLimit OR labAbs > labLimit
///   TotalHours → fails if (theoryAbs + labAbs) > totalLimit
///   Passive   → never fails
///   MarkOnly  → never fails
/// </summary>
public sealed class AttendanceAbsenceCalculatorTests
{
    // ── helper ──────────────────────────────────────────────────────────────

    private static bool Check(
        AttendanceMethod method,
        int theoryAbs, int labAbs,
        int theoryLimit, int labLimit, int totalLimit)
        => AttendanceAbsenceCalculator.IsFailedByAbsence(
            method, theoryAbs, labAbs, theoryLimit, labLimit, totalLimit);

    // ── Separate ────────────────────────────────────────────────────────────

    [Fact]
    public void Separate_FailsWhenTheoryExceedsLimit()
    {
        // theoryAbs (14) > theoryLimit (13) → fail
        Check(AttendanceMethod.Separate, theoryAbs: 14, labAbs: 0, theoryLimit: 13, labLimit: 0, totalLimit: 13)
            .Should().BeTrue("theory absence exceeds limit");
    }

    [Fact]
    public void Separate_FailsWhenLabExceedsLimit()
    {
        // labAbs (5) > labLimit (4) → fail (even if theory is fine)
        Check(AttendanceMethod.Separate, theoryAbs: 0, labAbs: 5, theoryLimit: 99, labLimit: 4, totalLimit: 99)
            .Should().BeTrue("lab absence exceeds limit");
    }

    [Fact]
    public void Separate_PassesWhenBothAtLimit()
    {
        // Exactly at limit = allowed (strictly-greater-than semantics)
        Check(AttendanceMethod.Separate, theoryAbs: 13, labAbs: 4, theoryLimit: 13, labLimit: 4, totalLimit: 17)
            .Should().BeFalse("at-limit is not a failure");
    }

    [Fact]
    public void Separate_PassesWhenBothBelowLimit()
    {
        Check(AttendanceMethod.Separate, theoryAbs: 5, labAbs: 2, theoryLimit: 13, labLimit: 4, totalLimit: 17)
            .Should().BeFalse("below limit is not a failure");
    }

    [Fact]
    public void Separate_FailsWhenEitherExceeds_OtherIsZero()
    {
        // OR semantics — only lab over
        Check(AttendanceMethod.Separate, theoryAbs: 0, labAbs: 10, theoryLimit: 13, labLimit: 9, totalLimit: 22)
            .Should().BeTrue("lab over limit triggers failure regardless of theory");
    }

    // ── TotalHours ──────────────────────────────────────────────────────────

    [Fact]
    public void TotalHours_FailsWhenCombinedExceedsTotalLimit()
    {
        // theoryAbs(8) + labAbs(8) = 16 > totalLimit(15)
        Check(AttendanceMethod.TotalHours, theoryAbs: 8, labAbs: 8, theoryLimit: 13, labLimit: 9, totalLimit: 15)
            .Should().BeTrue("combined absence exceeds total limit");
    }

    [Fact]
    public void TotalHours_PassesWhenCombinedAtTotalLimit()
    {
        // Combined exactly at limit = allowed
        Check(AttendanceMethod.TotalHours, theoryAbs: 7, labAbs: 8, theoryLimit: 99, labLimit: 99, totalLimit: 15)
            .Should().BeFalse("combined at total limit is not a failure");
    }

    [Fact]
    public void TotalHours_PassesWhenCombinedBelowTotalLimit()
    {
        Check(AttendanceMethod.TotalHours, theoryAbs: 5, labAbs: 5, theoryLimit: 99, labLimit: 99, totalLimit: 15)
            .Should().BeFalse("combined below total limit is not a failure");
    }

    [Fact]
    public void TotalHours_IgnoresSeparateLimits()
    {
        // theoryAbs(20) exceeds theoryLimit(13) individually, but combined(20+0=20) <= totalLimit(25)
        // TotalHours ignores per-track limits → should not fail
        Check(AttendanceMethod.TotalHours, theoryAbs: 20, labAbs: 0, theoryLimit: 13, labLimit: 9, totalLimit: 25)
            .Should().BeFalse("TotalHours method ignores individual track limits");
    }

    // ── Passive ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(100, 100, 0, 0, 0)]
    [InlineData(14, 5, 13, 4, 17)]
    public void Passive_NeverFails(int theoryAbs, int labAbs, int theoryLimit, int labLimit, int totalLimit)
    {
        Check(AttendanceMethod.Passive, theoryAbs, labAbs, theoryLimit, labLimit, totalLimit)
            .Should().BeFalse("Passive method never triggers attendance failure");
    }

    // ── MarkOnly ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(100, 100, 0, 0, 0)]
    [InlineData(14, 5, 13, 4, 17)]
    public void MarkOnly_NeverFails(int theoryAbs, int labAbs, int theoryLimit, int labLimit, int totalLimit)
    {
        Check(AttendanceMethod.MarkOnly, theoryAbs, labAbs, theoryLimit, labLimit, totalLimit)
            .Should().BeFalse("MarkOnly method never triggers attendance failure");
    }

    // ── Legacy 4-arg overload (regression guard) ─────────────────────────────

    [Fact]
    public void LegacyOverload_StillWorksForSeparateBehaviour()
    {
        // The original 4-arg overload must remain unchanged (Separate semantics).
        AttendanceAbsenceCalculator.IsFailedByAbsence(14, 0, 13, 4)
            .Should().BeTrue("14 > 13 theory limit");

        AttendanceAbsenceCalculator.IsFailedByAbsence(13, 4, 13, 4)
            .Should().BeFalse("exactly at limit is allowed");
    }

    // ── ParseAttendanceMethod helper ─────────────────────────────────────────

    [Theory]
    [InlineData("Separate", AttendanceMethod.Separate)]
    [InlineData("separate", AttendanceMethod.Separate)]
    [InlineData("SEPARATE", AttendanceMethod.Separate)]
    [InlineData("TotalHours", AttendanceMethod.TotalHours)]
    [InlineData("totalhours", AttendanceMethod.TotalHours)]
    [InlineData("Passive", AttendanceMethod.Passive)]
    [InlineData("MarkOnly", AttendanceMethod.MarkOnly)]
    [InlineData("markonly", AttendanceMethod.MarkOnly)]
    [InlineData(null, AttendanceMethod.Separate)]
    [InlineData("", AttendanceMethod.Separate)]
    [InlineData("Unknown", AttendanceMethod.Separate)]
    public void ParseAttendanceMethod_ParsesCorrectly(string? input, AttendanceMethod expected)
    {
        AttendanceMethodExtensions.ParseAttendanceMethod(input)
            .Should().Be(expected);
    }

    // ── CalculateLimitHours (Math.Ceiling, strict-greater semantics) ─────────

    [Theory]
    [InlineData(42, 30, 13)]   // ceil(12.6) = 13
    [InlineData(28, 20, 6)]    // ceil(5.6)  = 6
    [InlineData(0, 30, 0)]     // sıfır toplam → sıfır limit
    [InlineData(100, 0, 0)]    // sıfır yüzde → sıfır limit
    [InlineData(70, 100, 70)]  // %100 → tam toplam
    public void CalculateLimitHours_UsesCeiling(int totalHours, decimal percent, int expected)
    {
        AttendanceAbsenceCalculator.CalculateLimitHours(totalHours, percent)
            .Should().Be(expected);
    }

    // ── CalculateAbsenceHours (slot projeksiyonu + teori/lab kovalama) ───────

    [Fact]
    public void CalculateAbsenceHours_AbsentRecord_CountsAllSlots_SplitByInferredBlock()
    {
        // theory=2, lab=1 → 3 slot; slot1-2 teori, slot3 lab (schedule yoksa çıkarım).
        var records = new[] { Absent(totalSlotCount: 3) };

        var (theory, lab) = AttendanceAbsenceCalculator.CalculateAbsenceHours(
            records, theoryHours: 2, labHours: 1, applicationHours: null, scheduleSlots: null);

        theory.Should().Be(2);
        lab.Should().Be(1);
    }

    [Fact]
    public void CalculateAbsenceHours_ApplicationHours_BucketedAsLab()
    {
        // theory=2, lab=0, application=1 → fallback slot sayısı 3; slot3 (teoriden büyük) Lab kovasına.
        var records = new[] { Absent(totalSlotCount: 3) };

        var (theory, lab) = AttendanceAbsenceCalculator.CalculateAbsenceHours(
            records, theoryHours: 2, labHours: 0, applicationHours: 1, scheduleSlots: null);

        theory.Should().Be(2);
        lab.Should().Be(1, "uygulama saatleri Lab kovasına yazılır");
    }

    [Fact]
    public void CalculateAbsenceHours_PresentWithPartialSlots_CountsMissingOnly()
    {
        // 3 slotluk derste sadece 1. saate katılmış → 2 ve 3 devamsız (teori1 var, teori2 + lab eksik).
        var records = new[] { Present(totalSlotCount: 3, presentCsv: "1") };

        var (theory, lab) = AttendanceAbsenceCalculator.CalculateAbsenceHours(
            records, theoryHours: 2, labHours: 1, applicationHours: null, scheduleSlots: null);

        theory.Should().Be(1, "2. teori saati eksik");
        lab.Should().Be(1, "lab saati eksik");
    }

    [Theory]
    [InlineData(AttendanceStatus.Late)]
    [InlineData(AttendanceStatus.Excused)]
    public void CalculateAbsenceHours_LateOrExcused_CountsZeroAbsence(AttendanceStatus status)
    {
        var records = new[] { new AttendanceRecord { Status = status, TotalSlotCount = 3 } };

        var (theory, lab) = AttendanceAbsenceCalculator.CalculateAbsenceHours(
            records, theoryHours: 2, labHours: 1, applicationHours: null, scheduleSlots: null);

        theory.Should().Be(0);
        lab.Should().Be(0);
    }

    [Fact]
    public void CalculateAbsenceHours_UsesScheduleBlockType_WhenProvided()
    {
        // Schedule verilince blok tipi çıkarım yerine SectionSchedule'dan gelir.
        var slots = new List<AttendanceAbsenceCalculator.AttendanceSlotInfo>
        {
            new(1, ScheduleBlockType.Lab),
            new(2, ScheduleBlockType.Theory),
        };
        var records = new[] { Absent(totalSlotCount: 2) };

        var (theory, lab) = AttendanceAbsenceCalculator.CalculateAbsenceHours(
            records, theoryHours: 1, labHours: 1, applicationHours: null, scheduleSlots: slots);

        theory.Should().Be(1);
        lab.Should().Be(1);
    }

    private static AttendanceRecord Absent(int totalSlotCount) =>
        new() { Status = AttendanceStatus.Absent, TotalSlotCount = totalSlotCount };

    private static AttendanceRecord Present(int totalSlotCount, string presentCsv) =>
        new() { Status = AttendanceStatus.Present, TotalSlotCount = totalSlotCount, PresentSlotNumbersCsv = presentCsv };
}
