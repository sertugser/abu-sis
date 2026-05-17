const GRID_START_HOUR = 9;
const GRID_END_HOUR = 18;
const SLOT_HEIGHT_PER_MINUTE = 2; // px per minute

export { GRID_START_HOUR, GRID_END_HOUR, SLOT_HEIGHT_PER_MINUTE };

// 2026-05-10 — Plan E: parseScheduleSummary helper'ı silindi.
// Eskiden frontend backend'in Türkçe schedule string'ini regex'le ayrıştırıyordu —
// tek source-of-truth artık backend'in `schedules` ScheduleBlockDto array'i. Frontend
// component'ler doğrudan tüketir, kayıp veri yok (BlockType + per-block classroom).

/**
 * Convert a time string (HH:mm) to the grid row position (px offset from top).
 */
export function timeToGridRow(time: string): number {
  const [hours, minutes] = time.split(':').map(Number);
  const totalMinutes = (hours - GRID_START_HOUR) * 60 + minutes;
  return totalMinutes * SLOT_HEIGHT_PER_MINUTE;
}

/**
 * Calculate slot height in px based on duration.
 */
export function slotHeight(startTime: string, endTime: string): number {
  const [sh, sm] = startTime.split(':').map(Number);
  const [eh, em] = endTime.split(':').map(Number);
  const durationMinutes = (eh * 60 + em) - (sh * 60 + sm);
  return Math.max(durationMinutes * SLOT_HEIGHT_PER_MINUTE, SLOT_HEIGHT_PER_MINUTE * 30);
}

/**
 * Generate the hour labels for the Y axis.
 */
export function getHourLabels(): string[] {
  const labels: string[] = [];
  for (let h = GRID_START_HOUR; h <= GRID_END_HOUR; h++) {
    labels.push(`${String(h).padStart(2, '0')}:00`);
  }
  return labels;
}

export function getTimeLabels(intervalMinutes = 30): string[] {
  const labels: string[] = [];
  const totalMinutes = (GRID_END_HOUR - GRID_START_HOUR) * 60;

  for (let minute = 0; minute <= totalMinutes; minute += intervalMinutes) {
    const hour = GRID_START_HOUR + Math.floor(minute / 60);
    const minutes = minute % 60;
    labels.push(`${String(hour).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`);
  }

  return labels;
}

/**
 * Total grid height in px.
 */
export function getGridHeight(): number {
  return (GRID_END_HOUR - GRID_START_HOUR) * 60 * SLOT_HEIGHT_PER_MINUTE;
}

/**
 * Map day numbers to column indices (Mon=0..Fri=4).
 */
export function dayToColumnIndex(day: number): number | null {
  // DayOfWeek: 0=Sunday, 1=Monday, ..., 5=Friday, 6=Saturday
  if (day >= 1 && day <= 5) {
    return day - 1;
  }
  return null;
}

export const WEEKDAY_LABELS = ['Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma'] as const;

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
