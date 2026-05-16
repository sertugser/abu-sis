using FluentAssertions;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.Rules;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling.Rules.Hard;
using Xunit;

namespace SIS.Tests.Scheduling.Rules.Hard;

public sealed class H17_FaceToFaceSlotRuleTests
{
    private static RuleContext BuildCtx(
        TeachingMethod method,
        TimeSlotAllowedMode slotMode,
        int day = 1,
        int startHour = 9)
    {
        var start = new TimeOnly(startHour, 0);
        var end = new TimeOnly(startHour + 1, 0);
        var candidate = new RuntimeCandidate
        {
            SectionId = 1,
            CourseId = 100,
            CourseCode = "TEST101",
            TeachingMethod = method,
            Blocks = new[]
            {
                new RuntimeBlock(day, start, end, ClassroomId: 1,
                    ClassroomName: "C1", ClassroomType: ClassroomType.Lecture, ClassroomCapacity: 30)
            },
        };

        var snapshot = new RuntimeSnapshot
        {
            TermId = 1,
            Sections = new[] { candidate },
            SlotAllowedModes = new Dictionary<(int Day, TimeOnly StartTime), TimeSlotAllowedMode>
            {
                [(day, start)] = slotMode,
            },
        };

        return new RuleContext
        {
            Candidate = candidate,
            Snapshot = snapshot,
            Config = new SchedulingConstraintConfig(),
        };
    }

    [Fact]
    public void FaceToFace_OnOnlineOnlySlot_Reports()
    {
        var rule = new H17_FaceToFaceSlotRule();
        var violations = rule.Evaluate(BuildCtx(TeachingMethod.FaceToFace, TimeSlotAllowedMode.OnlineOnly));

        violations.Should().ContainSingle();
        violations[0].ConflictType.Should().Be("OnlineOnlySlotConflict");
        violations[0].Code.Should().Be("H17");
    }

    [Fact]
    public void Online_OnFaceToFaceOnlySlot_Reports()
    {
        var rule = new H17_FaceToFaceSlotRule();
        var violations = rule.Evaluate(BuildCtx(TeachingMethod.Online, TimeSlotAllowedMode.FaceToFaceOnly));

        violations.Should().ContainSingle();
        violations[0].ConflictType.Should().Be("FaceToFaceOnlySlotConflict");
    }

    [Fact]
    public void Hybrid_AcceptsAnySlotMode_NoViolation()
    {
        var rule = new H17_FaceToFaceSlotRule();
        var violations = rule.Evaluate(BuildCtx(TeachingMethod.Hybrid, TimeSlotAllowedMode.OnlineOnly));

        violations.Should().BeEmpty();
    }

    [Fact]
    public void FaceToFace_OnBothSlot_NoViolation()
    {
        var rule = new H17_FaceToFaceSlotRule();
        var violations = rule.Evaluate(BuildCtx(TeachingMethod.FaceToFace, TimeSlotAllowedMode.Both));

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Online_OnOnlineOnlySlot_NoViolation()
    {
        var rule = new H17_FaceToFaceSlotRule();
        var violations = rule.Evaluate(BuildCtx(TeachingMethod.Online, TimeSlotAllowedMode.OnlineOnly));

        violations.Should().BeEmpty();
    }

    [Fact]
    public void EmptySlotModes_Skips()
    {
        var rule = new H17_FaceToFaceSlotRule();
        var c = BuildCtx(TeachingMethod.FaceToFace, TimeSlotAllowedMode.OnlineOnly);
        var ctxWithoutModes = new RuleContext
        {
            Candidate = c.Candidate,
            Snapshot = new RuntimeSnapshot
            {
                TermId = c.Snapshot.TermId,
                Sections = c.Snapshot.Sections,
                SlotAllowedModes = new Dictionary<(int, TimeOnly), TimeSlotAllowedMode>(),
            },
            Config = c.Config,
        };

        var violations = rule.Evaluate(ctxWithoutModes);
        violations.Should().BeEmpty();
    }
}
