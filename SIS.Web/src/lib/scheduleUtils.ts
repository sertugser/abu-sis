// 2026-05-10 — Plan E: parseScheduleSummary helper'ı silindi.
// Eskiden frontend backend'in Türkçe schedule string'ini regex'le ayrıştırıyordu —
// tek source-of-truth artık backend'in `schedules` ScheduleBlockDto array'i. Frontend
// component'ler doğrudan tüketir, kayıp veri yok (BlockType + per-block classroom).
//
// 2026-05-30 — Sabit 09:00–18:00 ızgara yardımcıları (timeToGridRow, slotHeight,
// getTimeLabels, getGridHeight, getHourLabels, dayToColumnIndex, WEEKDAY_LABELS) silindi.
// Tek takvim ızgarası artık `components/scheduling/WeekTimetableGrid` + veri-odaklı
// `lib/timetableGrid` (dinamik aralık). Burada yalnızca gün-adı yardımcıları kaldı.

/**
 * DayOfWeek numarasını (.NET convention: 0=Pazar, 1=Pazartesi, ..., 6=Cumartesi)
 * Türkçe tam isme çevirir. Eşleşme yoksa boş string döner.
 */
export function dayNumberToTurkish(day: number): string {
  const names: Record<number, string> = {
    0: 'Pazar',
    1: 'Pazartesi',
    2: 'Salı',
    3: 'Çarşamba',
    4: 'Perşembe',
    5: 'Cuma',
    6: 'Cumartesi',
  };
  return names[day] ?? '';
}

/**
 * Haftalık sıralama: Pazartesi önce, Pazar sonda.
 * .NET convention'da Pazar=0 olduğundan 7'ye map'lenir ki sıralamada sonda çıksın.
 */
export function weekOrderKey(day: number): number {
  return day === 0 ? 7 : day;
}
