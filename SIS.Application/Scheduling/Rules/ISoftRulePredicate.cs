namespace SIS.Application.Scheduling.Rules;

/// <summary>
/// Plan 12 — Soft kural arayüzü. <see cref="IRulePredicate"/>'ı extend eder; <see cref="IRulePredicate.Evaluate"/>
/// (hard) genelde boş violation listesi döner. RuleEngine <see cref="CalculatePenalty"/>'i
/// effective category <c>Soft</c> ise çağırır.
///
/// <para>Hybrid kurallar (örn. DIST) hem <see cref="IRulePredicate.Evaluate"/>
/// (hard ihlal) hem <see cref="CalculatePenalty"/> (soft penalty) içeriği döndürebilir;
/// RuleEngine Plan 10 Category toggle'a göre uygun olanını dispatch eder.</para>
/// </summary>
public interface ISoftRulePredicate : IRulePredicate
{
    /// <summary>
    /// Snapshot'a karşı candidate için penalty hesaplar. Sync (hot-path), throw etmez.
    /// Penalty 0 ise <see cref="SoftRuleResult.Zero"/> dönmek tercih edilir.
    /// </summary>
    SoftRuleResult CalculatePenalty(RuleContext ctx);
}
