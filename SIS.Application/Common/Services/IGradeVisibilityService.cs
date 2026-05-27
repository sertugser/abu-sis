namespace SIS.Application.Common.Services;

/// <summary>
/// Harf notu "ilan" görünürlüğünün TEK kaynağı. Yalnızca AKTİF dönemin notları ilan
/// durumuna bağlıdır: aktif dönemde henüz ilan edilmemiş notlar öğrenci/yönetici
/// görünümlerinde gizlenir. Geçmiş (sonuçlanmış) dönemlerin notları her zaman görünür.
///
/// Hem transkript hem Dönem Ortalamaları bu servisten okur — böylece "biri gizliyor,
/// diğeri göstermiyor" sapması (ikibaşlılık) ortadan kalkar.
/// </summary>
public interface IGradeVisibilityService
{
    /// <summary>
    /// Verilen dönemler için görünürlük haritası: termId → notlar görünür mü?
    /// Aktif olmayan dönemler her zaman <c>true</c>.
    /// </summary>
    Task<Dictionary<int, bool>> BuildTermGradeVisibilityAsync(
        IEnumerable<int> termIds,
        CancellationToken ct = default);
}
