namespace SIS.Domain.Entities;

/// <summary>
/// Dönem (+ opsiyonel program) bazlı devamsızlık politikası.
/// Devamsızlık limitleri artık global DEĞİL; her dönem kendi değerini taşır
/// (geçmiş dönem kendi limitini gösterir, global sonradan değişse de etkilenmez).
///
/// Çözümleme sırası (IAttendancePolicyResolver):
///   (TermId, ProgramId == hedef) override
///   → (TermId, ProgramId == null) o dönemin geneli
///   → global SystemConfig varsayılanı (Attendance.*).
///
/// ProgramId == null  → o dönemin tüm programları için varsayılanı.
/// ProgramId == X     → o dönemde sadece X programı için override.
/// Global SystemConfig anahtarları yalnızca yeni dönem açılırken snapshot kaynağıdır.
/// </summary>
public class TermAttendancePolicy
{
    public int Id { get; set; }

    public int TermId { get; set; }
    public AcademicTerm Term { get; set; } = null!;

    /// <summary>null = dönem geneli varsayılanı; dolu = o programa özel override.</summary>
    public int? ProgramId { get; set; }
    public AcademicProgram? Program { get; set; }

    /// <summary>Devamsızlık hesabında kullanılan dönem hafta sayısı (1-52).</summary>
    public int TermWeekCount { get; set; }

    /// <summary>Teorik derslerde devamsızlıktan kalma eşiği (%, 0-100).</summary>
    public decimal TheoryAbsenceLimitPercent { get; set; }

    /// <summary>Uygulama/lab derslerinde devamsızlıktan kalma eşiği (%, 0-100).</summary>
    public decimal LabAbsenceLimitPercent { get; set; }

    /// <summary>Sayma yöntemi: Separate | TotalHours | Passive | MarkOnly.</summary>
    public string Method { get; set; } = "Separate";

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
