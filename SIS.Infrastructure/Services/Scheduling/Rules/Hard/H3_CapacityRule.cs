using SIS.Application.Scheduling.Rules;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling.Rules.Hard;

/// <summary>
/// Plan 11 Faz 3.3 — H3 Derslik Kapasitesi.
/// Atanmış dersliğin kapasitesi section kontenjanını karşılamalı. V1 evaluator paralel-split aware
/// combined capacity'ye bakar (RuntimeBlock'larda her bloğun ClassroomCapacity'si toplanır);
/// V2 encoder ise per-classroom kontrol (Lecture: section.Capacity > c.Capacity → forbid; Lab:
/// minCapPerGroup > c.Capacity → forbid). Runtime view: candidate.Blocks'tan toplam kapasiteyi
/// inşa et — RuntimeSnapshotFactory paralel-split combined hesabını yapacak (Faz 2.5).
/// </summary>
public sealed class H3_CapacityRule : IRulePredicate
{
    public string Code => "H3";
    public RuleCategory DefaultCategory => RuleCategory.Hard;

    public IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx)
    {
        var c = ctx.Candidate;
        if (c.Blocks.Count == 0)
            return Array.Empty<RuleViolation>();

        // Online section'lar için classroom yok → kapasite kontrolü atlanır.
        if (c.TeachingMethod == TeachingMethod.Online)
            return Array.Empty<RuleViolation>();

        // Paralel-split aware combined capacity: aynı (Day, Start, End) penceresindeki
        // tüm blokların ClassroomCapacity toplamı (V1 SectionSnapshotFactory.ResolveClassroomMetadata
        // paritesi). Bloklar farklı pencerelerde ise her pencere ayrı kontrol edilebilir; ancak
        // V1 davranışı tek bir "primary window" combined kapasitesi → V1 ile birebir uyum için
        // ilk pencereyi kullanıyoruz.
        var firstBlock = c.Blocks[0];
        var primaryWindowCapacity = c.Blocks
            .Where(b => b.Day == firstBlock.Day
                     && b.StartTime == firstBlock.StartTime
                     && b.EndTime == firstBlock.EndTime
                     && b.ClassroomId.HasValue)
            .Sum(b => b.ClassroomCapacity);

        if (primaryWindowCapacity > 0 && primaryWindowCapacity < c.Capacity)
        {
            return new[]
            {
                new RuleViolation(
                    Code: Code,
                    ConflictType: "CapacityConflict",
                    Message: $"Derslik kapasitesi section kontenjanini karsilamiyor ({primaryWindowCapacity}/{c.Capacity}).",
                    SectionId: c.SectionId)
            };
        }

        return Array.Empty<RuleViolation>();
    }
}
