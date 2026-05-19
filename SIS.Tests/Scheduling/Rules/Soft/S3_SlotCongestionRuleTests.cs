using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Infrastructure.Services.Scheduling.Rules.Soft;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Soft;

public sealed class S3_SlotCongestionRuleTests
{
    [Fact]
    public void SlotOverCapacity_Penalty()
    {
        // 5 section aynı (Mon, 9) slotunda + candidate da aynı slot → snapshot 6 entry.
        // V1 quirk: existingSlotKeys (6) + candidateSlotKeys (1) concat → count = 7, cap=5
        // (7-5) * 5 = 10
        var rule = new S3_SlotCongestionRule();
        var others = Enumerable.Range(1, 5).Select(i => Candidate(
            id: i, courseId: 100 + i, instructorId: i,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: i))).ToArray();
        var c = Candidate(id: 99, courseId: 999, instructorId: 99,
            blocks: Block(day: 1, startHour: 9, durationHours: 2, classroomId: 99));
        var ctx = new RuleContext
        {
            Candidate = c, Snapshot = Snapshot(others.Append(c).ToArray()),
            Config = DefaultConfig(cfg => { cfg.S3Enabled = true; cfg.S3MaxSectionsPerSlot = 5; }),
        };

        rule.CalculatePenalty(ctx).RawPenalty.Should().BeApproximately(10, 0.01);
    }

    [Fact]
    public void SlotWithinCapacity_NoPenalty()
    {
        var rule = new S3_SlotCongestionRule();
        var c = Candidate(id: 1, courseId: 100, instructorId: 1,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => { cfg.S3Enabled = true; cfg.S3MaxSectionsPerSlot = 5; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void DifferentSlots_NoPenalty()
    {
        // 5 section farklı saatlerde → her slot count=1, hiçbir slot cap'i aşmaz
        var rule = new S3_SlotCongestionRule();
        var others = Enumerable.Range(1, 5).Select(i => Candidate(
            id: i, courseId: 100 + i, instructorId: i,
            blocks: Block(day: 1, startHour: 9 + i * 2, durationHours: 2, classroomId: i))).ToArray();
        var c = Candidate(id: 99, courseId: 999, instructorId: 99,
            blocks: Block(day: 2, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(others.Append(c).ToArray()),
            Config = DefaultConfig(cfg => { cfg.S3Enabled = true; cfg.S3MaxSectionsPerSlot = 5; }) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void S3Disabled_NoPenalty()
    {
        var rule = new S3_SlotCongestionRule();
        var c = Candidate(id: 1, courseId: 100, instructorId: 1,
            blocks: Block(day: 1, startHour: 9, durationHours: 2));
        var ctx = new RuleContext { Candidate = c, Snapshot = Snapshot(c),
            Config = DefaultConfig(cfg => cfg.S3Enabled = false) };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }
}
