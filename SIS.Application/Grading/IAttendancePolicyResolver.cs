using SIS.Domain.Enums;

namespace SIS.Application.Grading;

/// <summary>
/// Bir dönem (+ program) için yürürlükteki devamsızlık politikası.
/// Limit yüzdeleri, hafta sayısı ve sayma yöntemi tek bir yerden çözülür;
/// öğrenci sayfası, NA kesinleştirme ve admin echo aynı kaynağı kullanır (ikibaşlılık yok).
/// </summary>
public sealed record ResolvedAttendancePolicy(
    int TermWeekCount,
    decimal TheoryAbsenceLimitPercent,
    decimal LabAbsenceLimitPercent,
    AttendanceMethod Method);

/// <summary>
/// Dönem/program bazlı devamsızlık politikasını çözer.
///
/// Çözümleme sırası:
///   (TermId, ProgramId == hedef) override
///   → (TermId, ProgramId == null) dönem geneli
///   → global SystemConfig varsayılanı (Attendance.*).
///
/// Geçmiş dönemler kendi kayıtlarını taşıdığı için (migration/snapshot ile dondurulmuş)
/// global değer sonradan değişse de geçmiş dönemin gösterdiği limit değişmez.
/// </summary>
public interface IAttendancePolicyResolver
{
    /// <summary>
    /// Tek bir (dönem, program) için politika çözer. <paramref name="programId"/> null ise
    /// doğrudan dönem geneli → global zinciri uygulanır.
    /// </summary>
    Task<ResolvedAttendancePolicy> ResolveAsync(int termId, int? programId, CancellationToken ct = default);

    /// <summary>
    /// Verilen öğrenciler için her birinin ana (Main+aktif) programına göre politika çözer.
    /// Section bazlı NA kesinleştirmede, aynı şubedeki farklı program öğrencileri kendi
    /// programlarının limitiyle değerlendirilir.
    /// </summary>
    Task<IReadOnlyDictionary<int, ResolvedAttendancePolicy>> ResolveForStudentsAsync(
        IReadOnlyCollection<int> studentIds, int termId, CancellationToken ct = default);

    /// <summary>
    /// Hiç dönem politikası bulunmadığında kullanılan global SystemConfig varsayılanı.
    /// Admin panelinde "yeni dönem varsayılanı" referansı olarak gösterilir.
    /// </summary>
    Task<ResolvedAttendancePolicy> GetGlobalDefaultAsync(CancellationToken ct = default);
}
