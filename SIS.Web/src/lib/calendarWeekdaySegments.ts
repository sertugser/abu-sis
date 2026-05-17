export interface WeekdaySegmentEventLike {
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

export function getWeekdayOnlyDateRanges(event: WeekdaySegmentEventLike): WeekdayDateRange[] {
  const startDate = toDateInput(event.startDate);
  const endDate = toDateInput(event.endDate) || startDate;

  if (!startDate)
    return [];

  return [{ startDate, endDate: endDate >= startDate ? endDate : startDate }];
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
