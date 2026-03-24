import type { ScheduleSlot } from '../types/scheduling';

const GRID_START_HOUR = 9;
const GRID_END_HOUR = 18;
const SLOT_HEIGHT_PER_MINUTE = 2; // px per minute

export { GRID_START_HOUR, GRID_END_HOUR, SLOT_HEIGHT_PER_MINUTE };

export function parseScheduleJson(json: string | null | undefined): ScheduleSlot[] {
  if (!json || json === '[]') {
    return [];
  }

  try {
    const parsed = JSON.parse(json);
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed
      .map((item: unknown) => {
        if (!item || typeof item !== 'object') {
          return null;
        }

        const normalizedItem = normalizeObjectKeys(item as Record<string, unknown>);
        const day = normalizeDayValue(normalizedItem.day ?? normalizedItem.dayOfWeek);
        const startTime = normalizeTime(normalizedItem.startTime);
        const endTime = normalizeTime(normalizedItem.endTime);

        if (day === null || !startTime || !endTime) {
          return null;
        }

        return { day, startTime, endTime } as ScheduleSlot;
      })
      .filter((slot): slot is ScheduleSlot => slot !== null);
  } catch {
    return [];
  }
}

function normalizeObjectKeys(item: Record<string, unknown>) {
  return Object.entries(item).reduce<Record<string, unknown>>((normalized, [key, value]) => {
    normalized[key.charAt(0).toLowerCase() + key.slice(1)] = value;
    return normalized;
  }, {});
}

function normalizeDayValue(value: unknown): number | null {
  if (typeof value === 'number' && value >= 0 && value <= 6) {
    return value;
  }

  if (typeof value !== 'string') {
    return null;
  }

  const normalized = value.trim().toLowerCase();
  const dayMap: Record<string, number> = {
    sunday: 0,
    monday: 1,
    tuesday: 2,
    wednesday: 3,
    thursday: 4,
    friday: 5,
    saturday: 6,
  };

  return dayMap[normalized] ?? null;
}

function normalizeTime(value: unknown): string | null {
  if (typeof value !== 'string' || !value) {
    return null;
  }

  // Accept "HH:mm", "HH:mm:ss", etc.
  const match = value.match(/^(\d{2}:\d{2})/);
  return match ? match[1] : null;
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
