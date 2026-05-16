namespace SIS.Application.Scheduling.Solver;

/// <summary>
/// CP-SAT solver tarafından her improving solution'da (veya heartbeat'ten) yayınlanan
/// ham progress payload'u. AutoScheduleEngine bu sinyali kullanıcı için anlamlı
/// yüzde + Türkçe mesaja çevirip Hub'a push eder.
///
/// Saf veri (no methods, no logic) — Application katmanında, no dependency.
/// </summary>
/// <param name="WallTimeSeconds">Solver'ın o ana kadar harcadığı duvar saati süresi.</param>
/// <param name="MaxTimeSeconds">Solver için yapılandırılan max süre (timeout).</param>
/// <param name="ObjectiveValue">Mevcut en iyi çözümün hedef değeri (lower = better).</param>
/// <param name="BestObjectiveBound">Solver'ın ulaşabileceğini düşündüğü alt sınır.
/// ObjectiveValue ile arası "gap"i verir; ikisi eşitleyince Optimal demektir.</param>
/// <param name="NumSolutionsFound">Bulunan iyileştirici çözüm sayısı (1, 2, 3, ...).</param>
/// <param name="Source">"solver" (improving solution callback) veya "heartbeat" (zamana göre).</param>
public sealed record SolverProgressInfo(
    double WallTimeSeconds,
    double MaxTimeSeconds,
    double ObjectiveValue,
    double BestObjectiveBound,
    int NumSolutionsFound,
    string Source);
