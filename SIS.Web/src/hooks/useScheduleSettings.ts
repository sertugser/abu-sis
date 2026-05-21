import { useMemo } from 'react';
import { useSystemConfigs } from './useSystemConfig';
import type { DayOfWeekName, TimeSlotAllowedMode } from './useTimeSlots';

/**
 * Schedule.* SystemConfig anahtarlarını tipli bir nesnede toparlar.
 * Akıllı öner algoritması ve dialog bilgi kartı buradan beslenir.
 * Config yoksa fallback default'lar kullanılır (admin henüz ayar eklememişse bile çalışır).
 */
export interface ScheduleSettings {
  workDays: DayOfWeekName[];
  slotDurationMinutes: number;
  breakBetweenSlotsMinutes: number;
  morningStartTime: string;      // "HH:mm"
  lunchBreakStart: string;
  lunchBreakEnd: string;
  lunchBreakIsHard: boolean;
  afternoonEndTime: string;
  eveningStartTime: string;
  eveningSlotCount: number;
  faceToFaceCutoffTime: string;
  weekendOnlineOnly: boolean;
  isLoading: boolean;
}

const DEFAULTS: Omit<ScheduleSettings, 'isLoading'> = {
  workDays: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'],
  slotDurationMinutes: 50,
  breakBetweenSlotsMinutes: 5,
  morningStartTime: '09:00',
  lunchBreakStart: '11:40',
  lunchBreakEnd: '12:20',
  lunchBreakIsHard: true,
  afternoonEndTime: '18:30',
  eveningStartTime: '20:10',
  eveningSlotCount: 2,
  faceToFaceCutoffTime: '18:00',
  weekendOnlineOnly: true,
};

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
    const getWorkDays = (key: string, fallback: DayOfWeekName[]): DayOfWeekName[] => {
      const v = map.get(key);
      if (!v) return fallback;

      const parsed = v
        .split(',')
        .map(token => token.trim())
        .filter((token): token is DayOfWeekName =>
          ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'].includes(token)
        );

      return parsed.length > 0 ? parsed : fallback;
    };

    return {
      workDays:                 getWorkDays('Schedule.WorkDays',              DEFAULTS.workDays),
      slotDurationMinutes:      getInt('Schedule.SlotDurationMinutes',      DEFAULTS.slotDurationMinutes),
      breakBetweenSlotsMinutes: getInt('Schedule.BreakBetweenSlotsMinutes', DEFAULTS.breakBetweenSlotsMinutes),
      morningStartTime:         get   ('Schedule.MorningStartTime',          DEFAULTS.morningStartTime),
      lunchBreakStart:          get   ('Schedule.LunchBreakStart',           DEFAULTS.lunchBreakStart),
      lunchBreakEnd:            get   ('Schedule.LunchBreakEnd',             DEFAULTS.lunchBreakEnd),
      lunchBreakIsHard:         getBool('Schedule.LunchBreak.IsHard',        DEFAULTS.lunchBreakIsHard),
      afternoonEndTime:         get   ('Schedule.AfternoonEndTime',          DEFAULTS.afternoonEndTime),
      eveningStartTime:         get   ('Schedule.EveningStartTime',          DEFAULTS.eveningStartTime),
      eveningSlotCount:         getInt('Schedule.EveningSlotCount',          DEFAULTS.eveningSlotCount),
      faceToFaceCutoffTime:     get   ('Schedule.FaceToFaceCutoffTime',      DEFAULTS.faceToFaceCutoffTime),
      weekendOnlineOnly:        getBool('Schedule.WeekendOnlineOnly',        DEFAULTS.weekendOnlineOnly),
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

/** Gün hafta sonu mu? */
export function isWeekendDay(day: DayOfWeekName): boolean {
  return day === 'Saturday' || day === 'Sunday';
}

/**
 * Config kurallarına göre (start, day) için varsayılan AllowedMode hesapla.
 * - Başlangıç ≥ faceToFaceCutoff → OnlineOnly
 * - weekendOnlineOnly && hafta sonu → OnlineOnly
 * - Diğer → Both
 */
export function computeSuggestedMode(
  startTime: string,
  day: DayOfWeekName,
  settings: ScheduleSettings
): TimeSlotAllowedMode {
  const startMin = parseTimeToMinutes(startTime);
  const cutoffMin = parseTimeToMinutes(settings.faceToFaceCutoffTime);
  if (startMin >= cutoffMin) return 'OnlineOnly';
  if (settings.weekendOnlineOnly && isWeekendDay(day)) return 'OnlineOnly';
  return 'Both';
}

// ── Akıllı öner algoritması ──────────────────────────────────────────

export interface SlotSuggestion {
  startTime: string;    // "HH:mm"
  endTime: string;
  allowedMode: TimeSlotAllowedMode;
  reason: string;       // "Öğle arasından sonra", "Akşam slotu" gibi açıklama
}

/**
 * Verilen gün için bir sonraki slot önerisi.
 * - O gün slot yoksa → morningStart'tan başla
 * - Son slot varsa → bitiş + mola
 * - Öğle arasına denk gelirse → lunchEnd'e atla
 * - Afternoon cutoff'u geçerse → evening'e atla
 */
export function suggestNextSlot(
  day: DayOfWeekName,
  daySlotsStartEnd: Array<{ startTime: string; endTime: string }>,
  settings: ScheduleSettings,
  overrideDurationMinutes?: number
): SlotSuggestion {
  const duration = overrideDurationMinutes ?? settings.slotDurationMinutes;
  const breakMin = settings.breakBetweenSlotsMinutes;
  const lunchStart = parseTimeToMinutes(settings.lunchBreakStart);
  const lunchEnd = parseTimeToMinutes(settings.lunchBreakEnd);
  const afternoonEnd = parseTimeToMinutes(settings.afternoonEndTime);
  const eveningStart = parseTimeToMinutes(settings.eveningStartTime);

  const sorted = [...daySlotsStartEnd].sort((a, b) =>
    a.startTime.localeCompare(b.startTime)
  );

  let startMin: number;
  let reason: string;

  if (sorted.length === 0) {
    startMin = parseTimeToMinutes(settings.morningStartTime);
    reason = 'Günün ilk slotu';
  } else {
    const last = sorted[sorted.length - 1];
    startMin = parseTimeToMinutes(normalizeTime(last.endTime)) + breakMin;
    reason = `Son slot (${normalizeTime(last.endTime)}) sonrası + ${breakMin} dk mola`;
  }

  let endMin = startMin + duration;

  // Öğle arasına denk düşüyor mu? (start < lunchEnd && end > lunchStart)
  if (startMin < lunchEnd && endMin > lunchStart) {
    startMin = lunchEnd;
    endMin = startMin + duration;
    reason = 'Öğle arası atlandı';
  }

  // Öğleden sonra bitişini geçiyor mu ve akşam başlangıcından önceysek sıçra
  if (endMin > afternoonEnd && startMin < eveningStart) {
    startMin = eveningStart;
    endMin = startMin + duration;
    reason = 'Akşam slot başlangıcına sıçrandı';
  }

  const startTime = minutesToTime(startMin);
  const endTime = minutesToTime(endMin);
  const allowedMode = computeSuggestedMode(startTime, day, settings);

  return { startTime, endTime, allowedMode, reason };
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

// ── Süre preset'leri ─────────────────────────────────────────────────

export const DURATION_PRESETS: Array<{ minutes: number; label: string }> = [
  { minutes: 50, label: '50 dk (Tek ders)' },
  { minutes: 90, label: '90 dk (Çift blok)' },
  { minutes: 120, label: '120 dk (Uzun blok)' },
];
