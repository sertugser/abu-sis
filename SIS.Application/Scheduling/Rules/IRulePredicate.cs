namespace SIS.Application.Scheduling.Rules;

/// <summary>
/// Plan 11 — Tek source-of-truth hard-rule predicate. V2 encoder'lar bu predicate'lara
/// CP-SAT modellemesi sarar; runtime check (manuel atama, dashboard, apply) doğrudan
/// <see cref="Evaluate"/> çağırır. Plan 10 Category toggle ResolveCategory üzerinden hâlâ
/// DB-driven (RuleEngine düzeyinde uygulanır).
///
/// Implementation kuralları:
///   <list type="bullet">
///     <item>Sync (manuel atama hot-path; async I/O yok).</item>
///     <item>Throw etmemeli; ihlalleri liste olarak döner.</item>
///     <item>Boş liste ⇒ ihlal yok.</item>
///     <item>Self-skip (paralel-lab vb.) RulePredicate içinde uygulanır — bkz. H1 ClassroomConflictRule.</item>
///   </list>
/// </summary>
public interface IRulePredicate
{
    /// <summary>Constraint code (H1, H2, H3, H4, H5, H6, H8, H10, H11, H12, H13, H15, H16, H17, H-DL, H-DO, H-WL).</summary>
    string Code { get; }

    /// <summary>Default category. Plan 10 toggle DB'de Soft'a çekilirse RuleEngine bu predicate'i çağırmaz.</summary>
    RuleCategory DefaultCategory { get; }

    /// <summary>İhlal varsa violation listesi; yoksa boş.</summary>
    IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx);
}
