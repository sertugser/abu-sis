import {
  addDays,
  fmtDateOnly,
  holidayRangeText,
  isWeekend,
  parseDateOnly,
  rangesOverlap,
} from '@/lib/calendarGenerator/dates';
import { semesterLabel } from '@/lib/calendarGenerator/semesters';
import { daySpan } from '@/lib/calendarGenerator/eventRules';
import {
  displayText,
  type CalendarView,
  type ShiftWarningRow,
  type WarningRow,
} from '@/lib/calendarGenerator/types';
import type { CalendarWarningDto, GeneratedCalendarEventDto } from '@/hooks/useCalendarGenerator';

export function isLegacyShiftWarning(warning: CalendarWarningDto) {
  return isShiftWarning(warning)
    && !displayText(warning.message).toLocaleLowerCase('tr-TR').includes('yeni tarih:');
}

export function isShiftWarning(warning: CalendarWarningDto) {
  return warning.code === 'HOLIDAY_SHIFT' || warning.code === 'PROHIBITION_SHIFT';
}

export function warningBadge(warning: CalendarWarningDto) {
  if (isShiftWarning(warning) || warning.code === 'DOMINO' || warning.code === 'SINGLE_EVENT_UPDATE')
    return { label: 'Değiştirildi', variant: 'success' as const };

  if (warning.code === 'WHAT_IF')
    return { label: 'Önizleme', variant: 'info' as const };

  if (warning.severity === 'Error')
    return { label: 'Hata', variant: 'destructive' as const };

  if (warning.severity === 'Warning')
    return { label: 'Uyarı', variant: 'warning' as const };

  return { label: 'Bilgi', variant: 'secondary' as const };
}

export function parseShiftDays(message: string) {
  const match = displayText(message).match(/(\d+)\s*g[üu]n/i);
  if (!match) return null;
  const value = Number(match[1]);
  return Number.isFinite(value) && value > 0 ? value : null;
}

export function reconstructLegacyHolidayReasons(current: CalendarView, event: GeneratedCalendarEventDto, shiftDays: number) {
  const finalStart = parseDateOnly(event.startDate);
  const finalEnd = parseDateOnly(event.endDate ?? event.startDate);
  if (!finalStart || !finalEnd) return [];

  const originalStart = addDays(finalStart, -shiftDays);
  const originalEnd = addDays(finalEnd, -shiftDays);
  const reasons: string[] = [];

  for (let offset = 0; offset < shiftDays; offset++) {
    const start = addDays(originalStart, offset);
    const end = addDays(originalEnd, offset);

    current.holidays
      .filter(holiday => holiday.blocksAcademicEvents)
      .forEach(holiday => {
        const holidayStart = parseDateOnly(holiday.startDate);
        const holidayEnd = parseDateOnly(holiday.endDate);
        if (holidayStart && holidayEnd && rangesOverlap(start, end, holidayStart, holidayEnd)) {
          reasons.push(`${displayText(holiday.name)} (${holidayRangeText(holidayStart, holidayEnd)})`);
        }
      });

    if (isWeekend(start))
      reasons.push(`hafta sonu (${fmtDateOnly(start)})`);
  }

  return [...new Set(reasons)];
}

export function extractShiftReason(message: string, title: string) {
  const text = displayText(message).replace(displayText(title), '').trim();
  const [reason] = text.split(/\s+nedeniyle\s+/i);
  return reason?.trim() || 'takvim çakışması';
}

export function joinedTitles(titles: string[]) {
  if (titles.length <= 1) return titles[0] ?? 'Etkinlik';
  if (titles.length === 2) return `${titles[0]} ve ${titles[1]}`;
  return `${titles.slice(0, -1).join(', ')} ve ${titles[titles.length - 1]}`;
}

export function readableShiftReason(reason: string) {
  const cleanReason = displayText(reason).trim();
  const weekendDates = [...cleanReason.matchAll(/hafta sonu \(([^)]+)\)/gi)]
    .map(match => match[1])
    .filter(Boolean);
  const otherReason = cleanReason
    .replace(/hafta sonu \([^)]+\)(,\s*)?/gi, '')
    .replace(/,\s*$/, '')
    .trim();
  const parts: string[] = [];

  if (weekendDates.length > 0) {
    parts.push(
      `${joinedTitles(weekendDates)} ${weekendDates.length === 1 ? 'tarihinin' : 'tarihlerinin'} hafta sonuna denk gelmesi`
    );
  }

  if (otherReason) {
    parts.push(
      otherReason === 'yasak aralık'
        ? 'tanımlı yasak aralıkla çakışması'
        : `${otherReason} ile çakışması`
    );
  }

  return parts.length > 0 ? joinedTitles(parts) : cleanReason;
}

export function shiftWarningMessage(row: ShiftWarningRow) {
  const subject = joinedTitles(row.titles);
  const cause = row.titles.length === 1
    ? readableShiftReason(row.reason)
    : `tarihlerinin ${readableShiftReason(row.reason)}`;
  return `${row.semester} döneminde ${subject}, ${cause} nedeniyle ${row.newDate} tarihine kaydırıldı.`;
}

export function buildShiftWarningRow(current: CalendarView, warning: CalendarWarningDto, event: GeneratedCalendarEventDto): ShiftWarningRow | null {
  const shiftDays = parseShiftDays(warning.message);
  if (isLegacyShiftWarning(warning) && !shiftDays)
    return null;

  const title = displayText(event.title);
  const reason = warning.code === 'HOLIDAY_SHIFT' && shiftDays
    ? reconstructLegacyHolidayReasons(current, event, shiftDays).join(', ') || 'takvim çakışması'
    : extractShiftReason(warning.message, title);
  const shiftText = shiftDays ? `${shiftDays} gün kaydırıldı` : 'kaydırıldı';
  const semester = semesterLabel(event.semester);
  const newDate = daySpan(event);
  const key = `${warning.severity}::${warning.code}::${event.semester}::${reason}::${shiftText}::${newDate}`;
  const badge = warningBadge(warning);
  const row: ShiftWarningRow = {
    warning,
    message: '',
    detail: null,
    key,
    badgeLabel: badge.label,
    badgeVariant: badge.variant,
    titles: [title],
    semester,
    reason,
    shiftText,
    newDate,
  };
  row.message = shiftWarningMessage(row);
  return row;
}

export function legacyShiftMessage(current: CalendarView, warning: CalendarWarningDto, event: GeneratedCalendarEventDto) {
  const shiftDays = parseShiftDays(warning.message);
  const reason = warning.code === 'HOLIDAY_SHIFT' && shiftDays
    ? reconstructLegacyHolidayReasons(current, event, shiftDays).join(', ') || 'takvim çakışması'
    : 'yasak aralık';
  return `${semesterLabel(event.semester)} döneminde ${displayText(event.title)}, ${reason} nedeniyle ${daySpan(event)} tarihine kaydırıldı.`;
}

export function warningRows(current: CalendarView) {
  const seen = new Set<string>();
  const shiftGroups = new Map<string, ShiftWarningRow>();
  return current.warnings.reduce<WarningRow[]>((rows, warning) => {
    const event = warning.draftEventId
      ? current.events.find(item => item.draftEventId === warning.draftEventId) ?? null
      : null;

    if (isShiftWarning(warning) && event) {
      const nextRow = buildShiftWarningRow(current, warning, event);
      if (!nextRow)
        return rows;

      const existing = shiftGroups.get(nextRow.key);
      if (existing) {
        for (const title of nextRow.titles) {
          if (!existing.titles.includes(title))
            existing.titles.push(title);
        }
        existing.message = shiftWarningMessage(existing);
        existing.detail = null;
        return rows;
      }

      shiftGroups.set(nextRow.key, nextRow);
      rows.push(nextRow);
      return rows;
    }

    const message = isLegacyShiftWarning(warning)
      ? event ? legacyShiftMessage(current, warning, event) : null
      : displayText(warning.message);

    if (!message)
      return rows;

    const detail = event ? `Etkinlik: ${semesterLabel(event.semester)} · ${daySpan(event)}` : null;
    const key = `${warning.severity}::${warning.code}::${message}`;
    const badge = warningBadge(warning);
    if (seen.has(key)) return rows;
    seen.add(key);
    rows.push({ warning, message, detail, key, badgeLabel: badge.label, badgeVariant: badge.variant });
    return rows;
  }, []);
}
