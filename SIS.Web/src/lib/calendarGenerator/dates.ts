import type { SemesterType } from '@/hooks/useCalendarGenerator';

export type DateSelectionRange = {
  min: string;
  max: string;
  label: string;
};

export function defaultAcademicYear() {
  const now = new Date();
  const start = now.getMonth() >= 6 ? now.getFullYear() : now.getFullYear() - 1;
  return `${start}-${start + 1}`;
}

export function academicYearStart(academicYear: string) {
  const parsed = Number(academicYear.split('-')[0]);
  return Number.isFinite(parsed) ? parsed : new Date().getFullYear();
}

export function formatAcademicYear(startYear: number) {
  return `${startYear}-${startYear + 1}`;
}

export function shiftAcademicYear(academicYear: string, offset: number) {
  return formatAcademicYear(academicYearStart(academicYear) + offset);
}

export function dateValue(year: number, month: number, day: number) {
  return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

export function formatUtcDateValue(date: Date) {
  return dateValue(date.getUTCFullYear(), date.getUTCMonth() + 1, date.getUTCDate());
}

export function nearestMondayDateValue(year: number, month: number, day: number) {
  const base = new Date(Date.UTC(year, month - 1, day));
  const weekday = base.getUTCDay();
  const previousOffset = weekday === 0 ? -6 : 1 - weekday;
  const nextOffset = weekday === 0 ? 1 : 8 - weekday;
  const chosenOffset = Math.abs(previousOffset) <= Math.abs(nextOffset) ? previousOffset : nextOffset;
  const adjusted = new Date(base);
  adjusted.setUTCDate(base.getUTCDate() + chosenOffset);
  return dateValue(adjusted.getUTCFullYear(), adjusted.getUTCMonth() + 1, adjusted.getUTCDate());
}

export function dateInput(value?: string | null) {
  return value ? value.substring(0, 10) : '';
}

export function utcDate(value: string | null) {
  return value ? `${value}T00:00:00.000Z` : null;
}

export function parseUtcDisplayDate(value?: string | null) {
  const input = dateInput(value);
  if (!input) return null;
  const [year, month, day] = input.split('-').map(Number);
  if (!year || !month || !day) return null;
  return new Date(Date.UTC(year, month - 1, day));
}

export function fmt(value?: string | null) {
  if (!value) return '-';
  const date = parseUtcDisplayDate(value);
  return date
    ? date.toLocaleDateString('tr-TR', { day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC' })
    : '-';
}

export function fmtLong(value?: string | null) {
  if (!value) return '-';
  const date = parseUtcDisplayDate(value);
  return date
    ? date.toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' })
    : '-';
}

export function buildDateSelectionRange(minYear: number, minMonth: number, minDay: number, maxYear: number, maxMonth: number, maxDay: number): DateSelectionRange {
  const min = dateValue(minYear, minMonth, minDay);
  const max = dateValue(maxYear, maxMonth, maxDay);
  return {
    min,
    max,
    label: `${fmtLong(utcDate(min))} - ${fmtLong(utcDate(max))}`,
  };
}

export function buildAcademicYearSelectionRange(academicYear: string, includeSummer: boolean) {
  const startYear = academicYearStart(academicYear);
  return includeSummer
    ? buildDateSelectionRange(startYear, 7, 1, startYear + 1, 9, 30)
    : buildDateSelectionRange(startYear, 7, 1, startYear + 1, 6, 30);
}

export function buildSemesterSelectionRange(academicYear: string, semester: SemesterType) {
  const startYear = academicYearStart(academicYear);
  switch (semester) {
    case 'Fall':
      return buildDateSelectionRange(startYear, 7, 1, startYear + 1, 1, 31);
    case 'Spring':
      return buildDateSelectionRange(startYear + 1, 1, 1, startYear + 1, 6, 30);
    case 'Summer':
      return buildDateSelectionRange(startYear + 1, 6, 1, startYear + 1, 9, 30);
    default:
      return buildAcademicYearSelectionRange(academicYear, true);
  }
}

export function isDateValueWithinRange(value: string, range: DateSelectionRange) {
  return !value || (value >= range.min && value <= range.max);
}

export function maxDateValue(left: string, right: string) {
  return left > right ? left : right;
}

export function fixedDayMonth(day?: number | null, month?: number | null) {
  if (!day || !month) return '-';
  const date = new Date(2026, month - 1, day);
  return date.toLocaleDateString('tr-TR', { day: 'numeric', month: 'long' });
}

export function diffWeeks(start: string, end: string) {
  const startDate = parseUtcDisplayDate(start);
  const endDate = parseUtcDisplayDate(end);
  if (!startDate || !endDate) return null;
  return Math.floor((endDate.getTime() - startDate.getTime()) / (7 * 24 * 60 * 60 * 1000));
}

export function parseDateOnly(value?: string | null) {
  const input = dateInput(value);
  if (!input) return null;
  const [year, month, day] = input.split('-').map(Number);
  if (!year || !month || !day) return null;
  return new Date(Date.UTC(year, month - 1, day));
}

export function addDays(date: Date, days: number) {
  const next = new Date(date);
  next.setUTCDate(next.getUTCDate() + days);
  return next;
}

export function dayDifference(target: string, source: string) {
  const targetDate = parseUtcDisplayDate(target);
  const sourceDate = parseUtcDisplayDate(source);
  if (!targetDate || !sourceDate) return null;

  return Math.round((targetDate.getTime() - sourceDate.getTime()) / (24 * 60 * 60 * 1000));
}

export function shiftDateValue(value: string, days: number) {
  const date = parseUtcDisplayDate(value);
  if (!date) return null;

  return formatUtcDateValue(addDays(date, days));
}

export function rangesOverlap(aStart: Date, aEnd: Date, bStart: Date, bEnd: Date) {
  return aStart.getTime() <= bEnd.getTime() && bStart.getTime() <= aEnd.getTime();
}

export function isWeekend(date: Date) {
  const day = date.getUTCDay();
  return day === 0 || day === 6;
}

export function fmtDateOnly(date: Date) {
  return date.toLocaleDateString('tr-TR', { day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC' });
}

export function holidayRangeText(start: Date, end: Date) {
  const startText = fmtDateOnly(start);
  const endText = fmtDateOnly(end);
  return startText === endText ? startText : `${startText} - ${endText}`;
}
