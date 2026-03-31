import type { ScheduleSlot } from '../types/scheduling';

const GRID_START_HOUR = 9;
const GRID_END_HOUR = 18;
const SLOT_HEIGHT_PER_MINUTE = 2; // px per minute

export { GRID_START_HOUR, GRID_END_HOUR, SLOT_HEIGHT_PER_MINUTE };

/**
 * Turkish day name → DayOfWeek number mapping.
 * Handles both full names (Pazartesi) and short names (Pzt).
 */
const TURKISH_DAY_MAP: Record<string, number> = {
  pazar: 0,
  paz: 0,
  pazartesi: 1,
  pzt: 1,
  sali: 2,
  sal: 2,
  carsamba: 3,
  car: 3,
  persembe: 4,
  per: 4,
  cuma: 5,
  cum: 5,
  cumartesi: 6,
  cmt: 6,
};

/**
 * Parse a backend scheduleSummary string into structured ScheduleSlot[].
 * Expected format: "Pazartesi 09:00-09:50, Carsamba 13:00-16:00"
 */
export function parseScheduleSummary(summary: string | null | undefined): ScheduleSlot[] {
  if (!summary || !summary.trim()) {
    return [];
  }

  const parts = summary.split(',').map(p => p.trim()).filter(Boolean);
  const slots: ScheduleSlot[] = [];

  for (const part of parts) {
    // Match: "DayName HH:mm-HH:mm"
    const match = part.match(/^(\S+)\s+(\d{2}:\d{2})-(\d{2}:\d{2})$/);
    if (!match) continue;

    const dayStr = match[1].toLowerCase();
    const day = TURKISH_DAY_MAP[dayStr];
    if (day === undefined) continue;

    slots.push({ day, startTime: match[2], endTime: match[3] });
  }

  return slots;
}

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
