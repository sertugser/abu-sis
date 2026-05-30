namespace SIS.Application.Common.Services;

/// <summary>
/// Harf notu "ilan" görünürlüğünün TEK kaynağı. Yalnızca AKTİF dönemin notları ilan
/// durumuna bağlıdır: aktif dönemde henüz ilan edilmemiş notlar öğrenci/yönetici
/// görünümlerinde gizlenir. Geçmiş (sonuçlanmış) dönemlerin notları her zaman görünür.
///
/// Transkript, Dönem Ortalamaları, Müfredat Durumu ve Seçmeli Havuz İlerlemesi — hepsi
/// bu TEK servisten okur. Böylece "biri gizliyor, diğeri göstermiyor" sapması (ikibaşlılık)
/// ortadan kalkar; bir ders her ekranda aynı şekilde "alındı/görünür" sayılır.
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
