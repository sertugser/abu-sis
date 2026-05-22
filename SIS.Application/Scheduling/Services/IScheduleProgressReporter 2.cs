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
/// bu yapıyı bekler. v2-ortools 3 aşamalı:
/// "started" → "v2-building-request" → "v2-solving" → "complete" (iptalde "failed").
/// </summary>
/// <param name="Stage">"started" | "v2-building-request" | "v2-solving" | "complete" | "failed"</param>
/// <param name="Percent">0-100 global progress (started=0, building=10, solving=50, complete=100).</param>
/// <param name="Current">İşlenen item sayısı (placed sections vb.). Backend her event'te yollamayabilir.</param>
/// <param name="Total">Toplam item (target sections vb.). Backend her event'te yollamayabilir.</param>
/// <param name="Detail">Kullanıcıya gösterilecek Türkçe mesaj (ör. "34 ders için en uygun program aranıyor...").</param>
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
