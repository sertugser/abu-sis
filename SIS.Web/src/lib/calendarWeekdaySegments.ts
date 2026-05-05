import { calendarEventCategory } from './calendarEventTypes';

export interface WeekdaySegmentEventLike {
  eventType: string;
  title?: string | null;
  templateItemKey?: string | null;
  startDate: string;
  endDate?: string | null;
}

export interface WeekdayDateRange {
  startDate: string;
  endDate: string;
}

function toDateInput(value?: string | null) {
  return value ? value.slice(0, 10) : '';
}

function parseDateOnly(value?: string | null) {
  const input = toDateInput(value);
  if (!input)
    return null;

  const [year, month, day] = input.split('-').map(Number);
  if (!year || !month || !day)
    return null;

  return new Date(Date.UTC(year, month - 1, day));
}

function formatDateOnly(date: Date) {
  return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, '0')}-${String(date.getUTCDate()).padStart(2, '0')}`;
}

function addDays(date: Date, days: number) {
  const next = new Date(date);
  next.setUTCDate(next.getUTCDate() + days);
  return next;
}

function isWeekend(date: Date) {
  const day = date.getUTCDay();
  return day === 0 || day === 6;
}

function shouldUseWeekdayOnlySegments(event: WeekdaySegmentEventLike) {
  return calendarEventCategory(event.eventType, event.title, event.templateItemKey) === 'exam';
}

export function getWeekdayOnlyDateRanges(event: WeekdaySegmentEventLike): WeekdayDateRange[] {
  const startDate = toDateInput(event.startDate);
  const endDate = toDateInput(event.endDate) || startDate;

  if (!startDate)
    return [];

  if (!shouldUseWeekdayOnlySegments(event))
    return [{ startDate, endDate }];

  const start = parseDateOnly(startDate);
  const end = parseDateOnly(endDate);
  if (!start || !end || start.getTime() > end.getTime())
    return [{ startDate, endDate }];

  const ranges: WeekdayDateRange[] = [];
  let current = start;
  let segmentStart: Date | null = null;

  while (current.getTime() <= end.getTime()) {
    if (isWeekend(current)) {
      if (segmentStart) {
        ranges.push({
          startDate: formatDateOnly(segmentStart),
          endDate: formatDateOnly(addDays(current, -1)),
        });
        segmentStart = null;
      }
    } else if (!segmentStart) {
      segmentStart = current;
    }

    current = addDays(current, 1);
  }

  if (segmentStart) {
    ranges.push({
      startDate: formatDateOnly(segmentStart),
      endDate: formatDateOnly(addDays(current, -1)),
    });
  }

  return ranges.length > 0 ? ranges : [{ startDate, endDate }];
}

export function formatWeekdayOnlyDateRanges(
  event: WeekdaySegmentEventLike,
  formatDate: (value: string) => string,
) {
  return getWeekdayOnlyDateRanges(event)
    .map(range => range.startDate === range.endDate
      ? formatDate(range.startDate)
      : `${formatDate(range.startDate)} - ${formatDate(range.endDate)}`)
    .join(', ');
}

export function countWeekdayOnlyEventDays(event: WeekdaySegmentEventLike) {
  return getWeekdayOnlyDateRanges(event).reduce((total, range) => {
    const start = parseDateOnly(range.startDate);
    const end = parseDateOnly(range.endDate);
    if (!start || !end)
      return total;

    const days = Math.floor((end.getTime() - start.getTime()) / (24 * 60 * 60 * 1000)) + 1;
    return total + Math.max(days, 0);
  }, 0);
}
