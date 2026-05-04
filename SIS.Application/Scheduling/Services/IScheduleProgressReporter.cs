namespace SIS.Application.Scheduling.Services;

/// <summary>
/// Auto-schedule (ve diğer uzun süreli scheduling işlemleri) sırasında ilerleme
/// bilgisini istemcilere yayınlamak için reporter abstraction'ı.
///
/// Application katmanında tanımlanır; gerçek implementation (SignalR Hub vb.)
/// API katmanında olur. Infrastructure → API yönünde ters bağımlılık önler.
///
/// Default registration NoOp olabilir — testlerde veya reporter yokken algoritma
/// sessiz çalışır, hata vermez.
/// </summary>
public interface IScheduleProgressReporter
{
    /// <summary>
    /// Tek bir progress event yayınlar. Hata atmamalı (no-throw) — algoritmanın
    /// asıl akışı reporter sorunlarından etkilenmesin.
    /// </summary>
    Task ReportAsync(string userId, ScheduleProgressEvent evt, CancellationToken ct = default);
}

/// <summary>
/// Auto-schedule progress event payload'ı. Frontend "ScheduleProgress" event handler
/// bu yapıyı bekler.
/// </summary>
/// <param name="Stage">"started" | "construction" | "sa" | "lns" | "complete" | "failed"</param>
/// <param name="Percent">0-100. Stage kümülatif progress (örn. SA aşaması 33-66 aralığı).</param>
/// <param name="Current">İşlenen item sayısı (placed sections, SA iter, vb.).</param>
/// <param name="Total">Toplam beklenen item (target sections, max iter, vb.).</param>
/// <param name="Detail">Kullanıcıya gösterilecek mesaj (ör. "CENG102-3 yerleştirildi").</param>
public sealed record ScheduleProgressEvent(
    string Stage,
    int Percent,
    int Current,
    int Total,
    string? Detail);

/// <summary>
/// Reporter yokken/test ortamında kullanılan no-op fallback.
/// </summary>
public sealed class NoOpScheduleProgressReporter : IScheduleProgressReporter
{
    public Task ReportAsync(string userId, ScheduleProgressEvent evt, CancellationToken ct = default)
        => Task.CompletedTask;
}

/// <summary>
/// Algoritma loop'larının (Construction / SA / LNS) içinden çağrılan periodic progress
/// callback'i. Fire-and-forget kullanılır: algoritma sync devam eder, callback async
/// işler (SignalR send). Implementor exception atmamalı (no-throw kontratı).
/// </summary>
/// <param name="stage">"construction" | "sa" | "lns"</param>
/// <param name="percent">0-100 global progress</param>
/// <param name="current">İşlenen item sayısı</param>
/// <param name="total">Toplam item sayısı</param>
/// <param name="detail">Kullanıcı için kısa metin</param>
public delegate void ScheduleProgressCallback(
    string stage, int percent, int current, int total, string? detail);
