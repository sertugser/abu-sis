import { calendarEventCategory, type CalendarEventCategory } from '@/lib/calendarEventTypes';

const TODAY = new Date();

export interface VisualizerEvent {
  id: string | number;
  eventType: string;
  title: string;
  startDate: string;
  endDate: string;
  note?: string | null;
  isChangedPreview?: boolean;
  templateItemKey?: string | null;
  draggable?: boolean;
}

export type ViewMonth = {
  y: number;
  m: number;
};

export interface Day {
  day: number;
  isMonth: boolean;
  isToday: boolean;
  str: string;
}

export interface RenderedVisualizerEvent extends VisualizerEvent {
  sourceEventId: VisualizerEvent['id'];
  sourceStartDate: string;
  sourceEndDate: string;
}

export interface Bar {
  event: RenderedVisualizerEvent;
  cat: CalendarEventCategory;
  col: number;
  span: number;
  lane: number;
  start: boolean;
  end: boolean;
}

export { TODAY };

export function toStr(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export function sameDay(a: Date, b: Date) {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

export function trDate(iso: string) {
  return new Date(iso).toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' });
}

export function dateOnly(value?: string | null) {
  return value ? value.slice(0, 10) : '';
}

export function parseDateValue(value: string) {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day));
}

export function isWeekendDateValue(value: string) {
  const day = parseDateValue(value).getUTCDay();
  return day === 0 || day === 6;
}

export function shiftDateValue(value: string, days: number) {
  const shifted = parseDateValue(value);
  shifted.setUTCDate(shifted.getUTCDate() + days);
  return shifted.toISOString().substring(0, 10);
}

export function dayDifference(target: string, source: string) {
  return Math.round((parseDateValue(target).getTime() - parseDateValue(source).getTime()) / (24 * 60 * 60 * 1000));
}

export function eventDateRange(event: VisualizerEvent) {
  const startDate = dateOnly(event.startDate);
  const endDate = dateOnly(event.endDate) || startDate;
  if (!startDate)
    return null;

  return {
    startDate,
    endDate: endDate >= startDate ? endDate : startDate,
  };
}

export function shouldRenderWeekdaysOnly(event: VisualizerEvent) {
  return calendarEventCategory(event.eventType, event.title, event.templateItemKey) === 'exam';
}

export function weekdayDateSegments(startDate: string, endDate: string) {
  const segments: Array<{ startDate: string; endDate: string }> = [];
  let cursor = startDate;
  let segmentStart: string | null = null;

  while (cursor <= endDate) {
    if (isWeekendDateValue(cursor)) {
      if (segmentStart) {
        segments.push({ startDate: segmentStart, endDate: shiftDateValue(cursor, -1) });
        segmentStart = null;
      }
    } else if (!segmentStart) {
      segmentStart = cursor;
    }

    cursor = shiftDateValue(cursor, 1);
  }

  if (segmentStart)
    segments.push({ startDate: segmentStart, endDate });

  return segments.length > 0 ? segments : [{ startDate, endDate }];
}

export function monthKey(month: ViewMonth) {
  return `${month.y}-${month.m}`;
}

export function sameMonth(left: ViewMonth, right: ViewMonth) {
  return left.y === right.y && left.m === right.m;
}

export function addMonths(month: ViewMonth, delta: number): ViewMonth {
  const next = new Date(month.y, month.m + delta, 1);
  return { y: next.getFullYear(), m: next.getMonth() };
}

export function monthValue(month: ViewMonth) {
  return month.y * 12 + month.m;
}

export function monthFromDateValue(value?: string | null): ViewMonth | null {
  const input = dateOnly(value);
  if (!input) return null;

  const [year, month] = input.split('-').map(Number);
  if (!Number.isFinite(year) || !Number.isFinite(month) || month < 1 || month > 12)
    return null;

  // Makul olmayan yıllar (örn. set edilmemiş DateTime.MinValue → 0001) akademik yıl
  // ay aralığını on binlerce aya açar ve takvimi dondurur — bu tarihleri yok say.
  if (year < 1900 || year > 3000)
    return null;

  return { y: year, m: month - 1 };
}

export function buildMonthSequence(start: ViewMonth, end: ViewMonth): ViewMonth[] {
  const months: ViewMonth[] = [];
  const cursor = { ...start };
  while (monthValue(cursor) <= monthValue(end)) {
    months.push({ ...cursor });
    const next = addMonths(cursor, 1);
    cursor.y = next.y;
    cursor.m = next.m;
  }

  return months;
}

export function buildGrid(year: number, month: number): Day[][] {
  const first = new Date(year, month, 1);
  const count = new Date(year, month + 1, 0).getDate();
  let wd = first.getDay() - 1;
  if (wd < 0) wd = 6;

  const flat: Day[] = [];

  for (let i = wd - 1; i >= 0; i--) {
    const d = new Date(year, month, -i);
    flat.push({ day: d.getDate(), isMonth: false, isToday: sameDay(d, TODAY), str: toStr(d) });
  }
  for (let i = 1; i <= count; i++) {
    const d = new Date(year, month, i);
    flat.push({ day: i, isMonth: true, isToday: sameDay(d, TODAY), str: toStr(d) });
  }
  while (flat.length < 42) {
    const d = new Date(year, month + 1, flat.length - count - wd + 1);
    flat.push({ day: d.getDate(), isMonth: false, isToday: sameDay(d, TODAY), str: toStr(d) });
  }

  const weeks: Day[][] = [];
  for (let i = 0; i < 42; i += 7) weeks.push(flat.slice(i, i + 7));
  return weeks;
}

export function weekBars(week: Day[], events: RenderedVisualizerEvent[]): Bar[] {
  const wStart = week[0].str;
  const wEnd = week[6].str;

  const active = events.filter(
    e => e.startDate <= wEnd && e.endDate >= wStart,
  );

  active.sort((a, b) => {
    const c = a.startDate.localeCompare(b.startDate);
    return c !== 0 ? c : b.endDate.localeCompare(a.endDate);
  });

  const lanes: number[] = [];

  return active.map(ev => {
    const col = ev.startDate <= wStart
      ? 0
      : Math.max(0, week.findIndex(d => d.str >= ev.startDate));

    let endCol = 6;
    if (ev.endDate < wEnd) {
      for (let i = 6; i >= 0; i--) {
        if (week[i].str <= ev.endDate) {
          endCol = i;
          break;
        }
      }
    }

    let lane = 0;
    while (lane < lanes.length && lanes[lane] >= col) lane++;
    if (lane >= lanes.length) lanes.push(-1);
    lanes[lane] = endCol;

    return {
      event: ev,
      cat: calendarEventCategory(ev.eventType, ev.title, ev.templateItemKey),
      col,
      span: endCol - col + 1,
      lane,
      start: ev.startDate >= wStart,
      end: ev.endDate <= wEnd,
    };
  });
}

export function buildMonthRange(events: VisualizerEvent[], fallbackMonth: ViewMonth): ViewMonth[] {
  if (events.length === 0) return [fallbackMonth];

  const dates = events
    .flatMap(e => [e.startDate, e.endDate])
    .filter(Boolean)
    .sort();

  if (dates.length === 0) return [fallbackMonth];

  const minDate = new Date(dates[0]);
  const maxDate = new Date(dates[dates.length - 1]);
  minDate.setDate(1);
  maxDate.setDate(1);

  const months: ViewMonth[] = [];
  const cursor = new Date(minDate);
  while (cursor <= maxDate) {
    months.push({ y: cursor.getFullYear(), m: cursor.getMonth() });
    cursor.setMonth(cursor.getMonth() + 1);
  }

  return months;
}

export function isMonthInRange(target: ViewMonth, months: ViewMonth[]) {
  return months.some(month => sameMonth(month, target));
}

export function closestMonthTo(target: ViewMonth, months: ViewMonth[]) {
  if (months.length === 0) return target;

  return months.reduce((closest, month) => {
    const currentDistance = Math.abs(monthValue(month) - monthValue(target));
    const closestDistance = Math.abs(monthValue(closest) - monthValue(target));
    return currentDistance < closestDistance ? month : closest;
  });
}

export function buildAcademicYearMonths(events: VisualizerEvent[], fallbackAnchor: ViewMonth, explicitStartYear?: number): ViewMonth[] {
  const eventMonths = events
    .flatMap(event => [event.startDate, event.endDate])
    .filter(Boolean)
    .map(date => monthFromDateValue(date))
    .filter((month): month is ViewMonth => month !== null);

  let start: ViewMonth;
  if (explicitStartYear !== undefined) {
    start = { y: explicitStartYear, m: 8 };
  } else {
    const eventDates = events
      .flatMap(event => [event.startDate, event.endDate])
      .filter(Boolean)
      .map(date => new Date(date))
      .filter(date => !Number.isNaN(date.getTime()));

    // Find the EARLIEST event date in a fall month (Sep+) to anchor the academic year.
    // Using earliest prevents summer events that extend into Sep of the next year
    // from shifting the entire year view forward by one year.
    const fallEvent = eventDates
      .filter(date => date.getMonth() >= 8)
      .sort((left, right) => left.getTime() - right.getTime())[0];
    const anchor = fallEvent
      ? { y: fallEvent.getFullYear(), m: fallEvent.getMonth() }
      : fallbackAnchor;
    const startYear = anchor.m >= 8 ? anchor.y : anchor.y - 1;
    start = { y: startYear, m: 8 };
  }

  let end = addMonths(start, 11);
  for (const eventMonth of eventMonths) {
    if (monthValue(eventMonth) < monthValue(start))
      start = eventMonth;
    if (monthValue(eventMonth) > monthValue(end))
      end = eventMonth;
  }

  return buildMonthSequence(start, end);
}

export function collectDropTargetDates(months: ViewMonth[]) {
  const dates = new Set<string>();

  for (const month of months) {
    const weeks = buildGrid(month.y, month.m);
    for (const week of weeks) {
      for (const day of week) {
        dates.add(day.str);
      }
    }
  }

  return Array.from(dates);
}
