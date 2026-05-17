namespace SIS.Application.Scheduling.Rules;

/// <summary>
/// Plan 11 — Runtime rule category. Mevcut <c>ConstraintCategory</c> ile aynı semantik;
/// yeni namespace içinde Faz 5'te V1 evaluator silindiğinde de erişilebilir. Plan 10
/// Category toggle (DB-driven Hard/Soft) bu enum'a map'lenir.
/// </summary>
public enum RuleCategory
{
    Hard = 0,
    Soft = 1,
}
