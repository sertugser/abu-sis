namespace SIS.Application.Scheduling.Services;

/// <summary>
/// Aktif çalışan auto-schedule run'larının CancellationTokenSource'unu kullanıcı bazında tutar.
/// SignalR Hub.Cancel(userId) çağrıldığında bu registry üzerinden lookup yapılır ve
/// AutoScheduleEngine'in CancellationToken'ı tetiklenir.
///
/// Tek kullanıcı aynı anda birden çok run tetiklerse: yeni run eskinin CTS'ini override eder
/// (eski run zaten ilerlemiş olur, en güncel olan iptal edilmek istenir).
/// </summary>
public interface IScheduleCancelRegistry
{
    /// <summary>
    /// Yeni başlatılan bir run için CTS kaydet. Aynı userId için varsa override eder.
    /// </summary>
    void Register(string userId, CancellationTokenSource cts);

    /// <summary>
    /// Kullanıcının aktif run'ını iptal et. Run yoksa sessizce no-op döner.
    /// </summary>
    /// <returns>İptal başarılıysa true, kayıt yoksa false.</returns>
    bool Cancel(string userId);

    /// <summary>
    /// Run tamamlandığında (success/fail/cancel fark etmez) kaydı temizle.
    /// </summary>
    void Unregister(string userId);
}
