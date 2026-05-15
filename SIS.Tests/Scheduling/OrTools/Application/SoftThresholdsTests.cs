using FluentAssertions;
using SIS.Application.Scheduling.Solver;
using Xunit;

namespace SIS.Tests.Scheduling.OrTools.Application;

public class SoftThresholdsTests
{
    [Fact]
    public void Default_MatchesV1Parity()
    {
        var s = new SoftThresholds();

        s.S1MinOccupancyPercent.Should().Be(60);
        s.S1MaxOccupancyPercent.Should().Be(85);
        s.S1Weight.Should().Be(1);

        // Plan 13 V1 clock-hour alignment (2026-05-15): V2 encoder artık V1 ile
        // birebir clock-hour hesabı yapıyor (AverageSlotDurationMinutes ile çevirim).
        // Default cap 6 saat — V1 ile aynı.
        s.S2MaxDailyHours.Should().Be(6);
        s.S2Weight.Should().Be(10);

        s.S3MaxSectionsPerSlot.Should().Be(5);
        s.S3Weight.Should().Be(5);

        s.S4MinCoPairThreshold.Should().Be(20);
        s.S4Weight.Should().Be(1);

        s.S5LunchBreakStart.Should().Be(new TimeOnly(11, 40));
        s.S5LunchBreakEnd.Should().Be(new TimeOnly(12, 20));
        s.S5Weight.Should().Be(8);

        s.S7MaxGapSlots.Should().Be(3);
        s.S7Weight.Should().Be(3);

        s.S8Weight.Should().Be(4);

        s.S12MaxImbalancePercent.Should().Be(20);
        s.S12Weight.Should().Be(2);

        s.S20PenaltyPerPair.Should().Be(5);
        s.S20Weight.Should().Be(1);

        s.DistDiscouragedWeight.Should().Be(50);
        s.DistStronglyDiscouragedWeight.Should().Be(200);
    }
}

public class CoEnrollmentPairInputTests
{
    [Fact]
    public void Construct_StoresValues()
    {
        var p = new CoEnrollmentPairInput(CourseId1: 10, CourseId2: 20, StudentCount: 35);
        p.CourseId1.Should().Be(10);
        p.CourseId2.Should().Be(20);
        p.StudentCount.Should().Be(35);
    }
}

public class SolverRequestSoftFieldsTests
{
    [Fact]
    public void Default_SoftThresholdsNotNull_CoEnrollmentEmpty_AdjacentYearEmpty()
    {
        var req = new SolverRequest { TermId = 1, FacultyId = 1 };
        req.SoftThresholds.Should().NotBeNull();
        req.SoftThresholds.S1MinOccupancyPercent.Should().Be(60);
        req.CoEnrollmentPairs.Should().BeEmpty();
        req.AdjacentYearSoftPairs.Should().BeEmpty();
    }
}
