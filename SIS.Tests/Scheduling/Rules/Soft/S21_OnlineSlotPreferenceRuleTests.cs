using FluentAssertions;
using SIS.Application.Scheduling.Rules;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.Rules.Soft;
using Xunit;
using static SIS.Tests.Scheduling.Rules.TestRuleHelpers;

namespace SIS.Tests.Scheduling.Rules.Soft;

public sealed class S21_OnlineSlotPreferenceRuleTests
{
    [Fact]
    public void Online_BothSlot_Penalty()
    {
        // Online ders Both slot'a yerleşmiş → her blok için 5 puan penalty.
        var rule = new S21_OnlineSlotPreferenceRule();
        var c = Candidate(id: 1, courseId: 100, method: TeachingMethod.Online,
            blocks: Block(day: 1, startHour: 16, durationHours: 1));
        var snapshot = new RuntimeSnapshot
        {
            TermId = 1,
            Sections = new[] { c },
            SlotAllowedModes = new Dictionary<(int, TimeOnly), TimeSlotAllowedMode>
            {
                [(1, new TimeOnly(16, 0))] = TimeSlotAllowedMode.Both,
            },
        };
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot, Config = DefaultConfig() };

        rule.CalculatePenalty(ctx).RawPenalty.Should().BeApproximately(5, 0.01);
    }

    [Fact]
    public void Online_OnlineOnlySlot_NoPenalty()
    {
        var rule = new S21_OnlineSlotPreferenceRule();
        var c = Candidate(id: 1, courseId: 100, method: TeachingMethod.Online,
            blocks: Block(day: 1, startHour: 18, durationHours: 1));
        var snapshot = new RuntimeSnapshot
        {
            TermId = 1,
            Sections = new[] { c },
            SlotAllowedModes = new Dictionary<(int, TimeOnly), TimeSlotAllowedMode>
            {
                [(1, new TimeOnly(18, 0))] = TimeSlotAllowedMode.OnlineOnly,
            },
        };
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot, Config = DefaultConfig() };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void FaceToFace_NotInScope_NoPenalty()
    {
        // FaceToFace ders kapsam dışı — kural sadece Online'ı yönlendirir.
        var rule = new S21_OnlineSlotPreferenceRule();
        var c = Candidate(id: 1, courseId: 100, method: TeachingMethod.FaceToFace,
            blocks: Block(day: 1, startHour: 16, durationHours: 1));
        var snapshot = new RuntimeSnapshot
        {
            TermId = 1,
            Sections = new[] { c },
            SlotAllowedModes = new Dictionary<(int, TimeOnly), TimeSlotAllowedMode>
            {
                [(1, new TimeOnly(16, 0))] = TimeSlotAllowedMode.Both,
            },
        };
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot, Config = DefaultConfig() };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void Hybrid_NotInScope_NoPenalty()
    {
        var rule = new S21_OnlineSlotPreferenceRule();
        var c = Candidate(id: 1, courseId: 100, method: TeachingMethod.Hybrid,
            blocks: Block(day: 1, startHour: 16, durationHours: 1));
        var snapshot = new RuntimeSnapshot
        {
            TermId = 1,
            Sections = new[] { c },
            SlotAllowedModes = new Dictionary<(int, TimeOnly), TimeSlotAllowedMode>
            {
                [(1, new TimeOnly(16, 0))] = TimeSlotAllowedMode.Both,
            },
        };
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot, Config = DefaultConfig() };

        rule.CalculatePenalty(ctx).RawPenalty.Should().Be(0);
    }

    [Fact]
    public void Online_MultipleBothBlocks_AccumulatesPenalty()
    {
        var rule = new S21_OnlineSlotPreferenceRule();
        var c = Candidate(id: 1, courseId: 100, method: TeachingMethod.Online,
            blocks: new[]
            {
                Block(day: 1, startHour: 16, durationHours: 1),
                Block(day: 1, startHour: 17, durationHours: 1),
                Block(day: 4, startHour: 14, durationHours: 1),
            });
        var snapshot = new RuntimeSnapshot
        {
            TermId = 1,
            Sections = new[] { c },
            SlotAllowedModes = new Dictionary<(int, TimeOnly), TimeSlotAllowedMode>
            {
                [(1, new TimeOnly(16, 0))] = TimeSlotAllowedMode.Both,
                [(1, new TimeOnly(17, 0))] = TimeSlotAllowedMode.Both,
                [(4, new TimeOnly(14, 0))] = TimeSlotAllowedMode.Both,
            },
        };
        var ctx = new RuleContext { Candidate = c, Snapshot = snapshot, Config = DefaultConfig() };

        // 3 blok × 5 puan = 15
        rule.CalculatePenalty(ctx).RawPenalty.Should().BeApproximately(15, 0.01);
    }
}
