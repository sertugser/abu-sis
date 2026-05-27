import { useMemo } from 'react';
import { useSystemConfigs } from './useSystemConfig';
import type { DayOfWeekName } from './useTimeSlots';

/**
 * Schedule.* SystemConfig anahtarlarını tipli bir nesnede toparlar.
 * Akıllı öner algoritması ve dialog bilgi kartı buradan beslenir.
 * Config yoksa fallback default'lar kullanılır (admin henüz ayar eklememişse bile çalışır).
 */
export interface ScheduleSettings {
  slotDurationMinutes: number;
  breakBetweenSlotsMinutes: number;
  morningStartTime: string;      // "HH:mm"
  lunchBreakStart: string;
  lunchBreakEnd: string;
  afternoonEndTime: string;
  eveningStartTime: string;
  eveningSlotCount: number;
  faceToFaceCutoffTime: string;
  weekendOnlineOnly: boolean;
  /** Schedule.WorkDays — ders yapılabilen günler (System.DayOfWeek numarası). */
  workDays: DayOfWeekName[];
  /** Schedule.LunchBreak.IsHard — açıksa öğle arasına slot konulamaz (backend hard kural). */
  lunchBreakIsHard: boolean;
  isLoading: boolean;
}

const DEFAULTS: Omit<ScheduleSettings, 'isLoading'> = {
  slotDurationMinutes: 50,
  breakBetweenSlotsMinutes: 5,
  morningStartTime: '09:00',
  lunchBreakStart: '11:40',
  lunchBreakEnd: '12:20',
  afternoonEndTime: '18:30',
  eveningStartTime: '20:10',
  eveningSlotCount: 2,
  faceToFaceCutoffTime: '18:00',
  weekendOnlineOnly: true,
  // Schedule.WorkDays seed'i Pzt–Cuma; key hiç yoksa bu fallback kullanılır.
  workDays: [1, 2, 3, 4, 5],
  // Backend GetBoolAsync fallback'iyle birebir: key yoksa false (kural pasif).
  lunchBreakIsHard: false,
};

/** Gün adı (Schedule.WorkDays formatı) → System.DayOfWeek numarası (Sun=0..Sat=6). */
const DAY_NAME_TO_NUMBER: Record<string, DayOfWeekName> = {
  sunday: 0, monday: 1, tuesday: 2, wednesday: 3, thursday: 4, friday: 5, saturday: 6,
};

/** "Monday,Tuesday,..." → [1,2,...]. Geçersiz/boş ise Pzt–Cuma fallback. */
function parseWorkDays(raw: string): DayOfWeekName[] {
  const parsed = raw
    .split(',')
    .map(token => token.trim().toLowerCase())
    .filter(token => token.length > 0)
    .map(token => DAY_NAME_TO_NUMBER[token])
    .filter((day): day is DayOfWeekName => day !== undefined);
  return parsed.length > 0 ? parsed : DEFAULTS.workDays;
}

export function useScheduleSettings(): ScheduleSettings {
  const { data: configs, isLoading: systemLoading } = useSystemConfigs();

  return useMemo(() => {
    const map = new Map((configs ?? []).map(c => [c.key, c.value]));
    const get = (key: string, fallback: string): string => map.get(key) ?? fallback;
    const getInt = (key: string, fallback: number): number => {
      const v = map.get(key);
      if (!v) return fallback;
      const parsed = parseInt(v, 10);
      return Number.isFinite(parsed) ? parsed : fallback;
    };
    const getBool = (key: string, fallback: boolean): boolean => {
      const v = map.get(key);
      if (!v) return fallback;
      return v.toLowerCase() === 'true';
    };

    return {
      slotDurationMinutes:      getInt('Schedule.SlotDurationMinutes',      DEFAULTS.slotDurationMinutes),
      breakBetweenSlotsMinutes: getInt('Schedule.BreakBetweenSlotsMinutes', DEFAULTS.breakBetweenSlotsMinutes),
      morningStartTime:         get   ('Schedule.MorningStartTime',          DEFAULTS.morningStartTime),
      lunchBreakStart:          get   ('Schedule.LunchBreakStart',           DEFAULTS.lunchBreakStart),
      lunchBreakEnd:            get   ('Schedule.LunchBreakEnd',             DEFAULTS.lunchBreakEnd),
      afternoonEndTime:         get   ('Schedule.AfternoonEndTime',          DEFAULTS.afternoonEndTime),
      eveningStartTime:         get   ('Schedule.EveningStartTime',          DEFAULTS.eveningStartTime),
      eveningSlotCount:         getInt('Schedule.EveningSlotCount',          DEFAULTS.eveningSlotCount),
      faceToFaceCutoffTime:     get   ('Schedule.FaceToFaceCutoffTime',      DEFAULTS.faceToFaceCutoffTime),
      weekendOnlineOnly:        getBool('Schedule.WeekendOnlineOnly',        DEFAULTS.weekendOnlineOnly),
      workDays:                 parseWorkDays(get('Schedule.WorkDays', 'Monday,Tuesday,Wednesday,Thursday,Friday')),
      lunchBreakIsHard:         getBool('Schedule.LunchBreak.IsHard',        DEFAULTS.lunchBreakIsHard),
      isLoading: systemLoading,
    };
  }, [configs, systemLoading]);
}

// ── Zaman yardımcıları ────────────────────────────────────────────────

/** "HH:mm" → dakika (midnight sonrası). "09:30" → 570. */
export function parseTimeToMinutes(time: string): number {
  const [h, m] = time.split(':').map(Number);
  return (h || 0) * 60 + (m || 0);
}

/** Dakika → "HH:mm". 570 → "09:30". */
export function minutesToTime(minutes: number): string {
  const h = Math.floor(minutes / 60) % 24;
  const m = minutes % 60;
  return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}`;
}

/** Bir saat "HH:mm" veya "HH:mm:ss" formatında gelebilir — 5 karaktere normalize et. */
export function normalizeTime(time: string): string {
  return time.length >= 5 ? time.substring(0, 5) : time;
}

/** Gün hafta sonu mu? (System.DayOfWeek: Sun=0, Sat=6) */
export function isWeekendDay(day: DayOfWeekName): boolean {
  return day === 0 || day === 6;
}

// ── Çakışma kontrolü ─────────────────────────────────────────────────

/**
 * Aynı gün içinde çakışan slot var mı? Kendi slot (excludeId) hariç tutar.
 * Kesişim: a.start < b.end && a.end > b.start
 */
export function findOverlappingSlot<T extends { id: number; dayOfWeek: DayOfWeekName; startTime: string; endTime: string }>(
  daySlots: T[],
  day: DayOfWeekName,
  start: string,
  end: string,
  excludeId: number | null
): T | null {
  const startMin = parseTimeToMinutes(start);
  const endMin = parseTimeToMinutes(end);

  for (const s of daySlots) {
    if (s.dayOfWeek !== day) continue;
    if (excludeId !== null && s.id === excludeId) continue;
    const sStart = parseTimeToMinutes(normalizeTime(s.startTime));
    const sEnd = parseTimeToMinutes(normalizeTime(s.endTime));
    if (startMin < sEnd && endMin > sStart) return s;
  }
  return null;
}
