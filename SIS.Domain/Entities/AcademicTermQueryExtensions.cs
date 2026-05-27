namespace SIS.Domain.Entities;

/// <summary>
/// "Aktif dönem" çözümlemesi için TEK kaynak. Önceden bu mantık (~30 yer)
/// her sorguya <c>!IsCancelled &amp;&amp; IsActivated &amp;&amp; TermStart &lt;= now &amp;&amp; TermEnd &gt;= now</c>
/// şeklinde kopyalanmıştı. Artık aktiflik manuel "Kullanıma Aç" bayrağına bağlıdır
/// (Proliz/Banner modeli): tarih aralığı aktifliği belirlemez.
/// </summary>
public static class AcademicTermQueryExtensions
{
    /// <summary>
    /// Yönetici tarafından "Kullanıma Açık" işaretlenmiş, iptal edilmemiş dönem(ler).
    /// Tek-aktif değişmezi <c>AdminTermService.ActivateTermAsync</c>'te korunur; buradaki
    /// <c>OrderByDescending</c> yalnızca emniyet amaçlı tiebreak'tir (birden fazla açık
    /// kalırsa en yeni başlangıçlı dönem seçilir). Çağıran genelde
    /// <c>.ActiveTerm().FirstOrDefaultAsync()</c> kullanır.
    /// </summary>
    public static IQueryable<AcademicTerm> ActiveTerm(this IQueryable<AcademicTerm> query) =>
        query
            .Where(t => !t.IsCancelled && t.IsActivated)
            .OrderByDescending(t => t.TermStart);
}
