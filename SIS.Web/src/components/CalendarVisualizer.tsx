import { useState, useMemo, useCallback, useEffect, useRef } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { calendarEventCategory, type CalendarEventCategory } from '@/lib/calendarEventTypes';
import { cn } from '@/lib/utils';

const TODAY = new Date();
const WEEKDAYS = ['PZT', 'SAL', 'ÇAR', 'PER', 'CUM', 'CMT', 'PAZ'];
const MONTH_NAMES = [
  'Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran',
  'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık',
];
const MAX_LANES = 4;

const CAT_STYLE: Record<CalendarEventCategory, { bg: string; border: string; text: string; label: string }> = {
  exam:         { bg: 'bg-red-100',    border: 'border-l-red-500',    text: 'text-red-800',    label: 'SINAV' },
  registration: { bg: 'bg-blue-100',   border: 'border-l-blue-500',   text: 'text-blue-800',   label: 'KAYIT' },
  holiday:      { bg: 'bg-green-100',  border: 'border-l-green-500',  text: 'text-green-800',  label: 'TATIL' },
  deadline:     { bg: 'bg-amber-100',  border: 'border-l-amber-500',  text: 'text-amber-800',  label: 'SON GUN' },
  result:       { bg: 'bg-gray-100',   border: 'border-l-gray-400',   text: 'text-gray-700',   label: 'SONUC' },
  application:  { bg: 'bg-teal-100',   border: 'border-l-teal-500',   text: 'text-teal-800',   label: 'BASVURU' },
  academic:     { bg: 'bg-indigo-100', border: 'border-l-indigo-500', text: 'text-indigo-800', label: 'AKADEMIK' },
};

export interface VisualizerEvent {
  id: string | number;
  eventType: string;
  title: string;
  startDate: string;
  endDate: string;
  note?: string | null;
  isChangedPreview?: boolean;
  templateItemKey?: string | null;
}

type ViewMonth = {
  y: number;
  m: number;
};

interface Day {
  day: number;
  isMonth: boolean;
  isToday: boolean;
  str: string;
}

interface Bar {
  event: VisualizerEvent;
  cat: CalendarEventCategory;
  col: number;
  span: number;
  lane: number;
  start: boolean;
  end: boolean;
}

export interface CalendarVisualizerProps {
  events: VisualizerEvent[];
  compact?: boolean;
  multiMonth?: boolean;
  inlineYearView?: boolean;
  filterableLegend?: boolean;
  /** Academic year start (e.g. 2025 for 2025-2026). When provided, the year view shows Sep of this year through Aug of the next. */
  academicYearStart?: number;
}

function toStr(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function sameDay(a: Date, b: Date) {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

function trDate(iso: string) {
  return new Date(iso).toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' });
}

function monthKey(month: ViewMonth) {
  return `${month.y}-${month.m}`;
}

function sameMonth(left: ViewMonth, right: ViewMonth) {
  return left.y === right.y && left.m === right.m;
}

function addMonths(month: ViewMonth, delta: number): ViewMonth {
  const next = new Date(month.y, month.m + delta, 1);
  return { y: next.getFullYear(), m: next.getMonth() };
}

function monthValue(month: ViewMonth) {
  return month.y * 12 + month.m;
}

function buildGrid(year: number, month: number): Day[][] {
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

function weekBars(week: Day[], events: VisualizerEvent[]): Bar[] {
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

function buildMonthRange(events: VisualizerEvent[], fallbackMonth: ViewMonth): ViewMonth[] {
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

function isMonthInRange(target: ViewMonth, months: ViewMonth[]) {
  return months.some(month => sameMonth(month, target));
}

function closestMonthTo(target: ViewMonth, months: ViewMonth[]) {
  if (months.length === 0) return target;

  return months.reduce((closest, month) => {
    const currentDistance = Math.abs(monthValue(month) - monthValue(target));
    const closestDistance = Math.abs(monthValue(closest) - monthValue(target));
    return currentDistance < closestDistance ? month : closest;
  });
}

function buildAcademicYearMonths(events: VisualizerEvent[], fallbackAnchor: ViewMonth, explicitStartYear?: number): ViewMonth[] {
  // When an explicit academic year start is provided, use it directly
  if (explicitStartYear !== undefined) {
    const start = { y: explicitStartYear, m: 8 };
    return Array.from({ length: 12 }, (_, index) => addMonths(start, index));
  }

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
  const start = { y: startYear, m: 8 };

  return Array.from({ length: 12 }, (_, index) => addMonths(start, index));
}

export function CalendarVisualizer({
  events,
  compact = false,
  multiMonth = false,
  inlineYearView = false,
  filterableLegend = false,
  academicYearStart: explicitAcademicYearStart,
}: CalendarVisualizerProps) {
  const initialMonth = useMemo<ViewMonth>(() => ({ y: TODAY.getFullYear(), m: TODAY.getMonth() }), []);
  const [vm, setVm] = useState<ViewMonth>(initialMonth);
  const [showYearView, setShowYearView] = useState(false);
  const [activeCategory, setActiveCategory] = useState<CalendarEventCategory | null>(null);
  const scrollContainerRef = useRef<HTMLDivElement | null>(null);
  const monthRefs = useRef<Record<string, HTMLDivElement | null>>({});
  const lastEventSignatureRef = useRef<string | null>(null);
  const programmaticScrollRef = useRef(false);
  const scrollResetTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const visibleEvents = useMemo(
    () => activeCategory
      ? events.filter(event => calendarEventCategory(event.eventType, event.title, event.templateItemKey) === activeCategory)
      : events,
    [activeCategory, events],
  );
  const allMonths = useMemo(() => buildMonthRange(visibleEvents, initialMonth), [visibleEvents, initialMonth]);
  const academicYearMonths = useMemo(() => buildAcademicYearMonths(events, vm, explicitAcademicYearStart), [events, vm, explicitAcademicYearStart]);
  const eventSignature = useMemo(
    () => visibleEvents.map(event => `${event.id}:${event.startDate}:${event.endDate}:${event.isChangedPreview ? '1' : '0'}`).join('|'),
    [visibleEvents],
  );

  useEffect(() => {
    if (lastEventSignatureRef.current === eventSignature)
      return;

    lastEventSignatureRef.current = eventSignature;
    setVm(prev => {
      if (isMonthInRange(initialMonth, allMonths)) {
        if (multiMonth) programmaticScrollRef.current = true;
        return sameMonth(prev, initialMonth) ? prev : initialMonth;
      }

      if (isMonthInRange(prev, allMonths))
        return prev;

      if (multiMonth) programmaticScrollRef.current = true;
      return allMonths[0] ?? initialMonth;
    });
  }, [allMonths, eventSignature, initialMonth, multiMonth]);

  useEffect(() => {
    if (!multiMonth || !programmaticScrollRef.current) return;

    const container = scrollContainerRef.current;
    const target = monthRefs.current[monthKey(vm)];
    if (!container || !target) return;

    const containerRect = container.getBoundingClientRect();
    const targetRect = target.getBoundingClientRect();
    const nextTop = targetRect.top - containerRect.top + container.scrollTop;
    container.scrollTo({ top: nextTop, behavior: 'smooth' });

    if (scrollResetTimerRef.current) clearTimeout(scrollResetTimerRef.current);
    scrollResetTimerRef.current = setTimeout(() => {
      programmaticScrollRef.current = false;
    }, 400);
  }, [multiMonth, vm]);

  useEffect(() => {
    return () => {
      if (scrollResetTimerRef.current) clearTimeout(scrollResetTimerRef.current);
    };
  }, []);

  const viewMonths = useMemo(() => {
    if (!multiMonth || visibleEvents.length === 0) return [{ y: vm.y, m: vm.m }];
    return allMonths;
  }, [allMonths, visibleEvents.length, multiMonth, vm]);

  const scrollToMonth = useCallback((month: ViewMonth) => {
    if (multiMonth) programmaticScrollRef.current = true;
    setVm(month);
  }, [multiMonth]);

  const updateVisibleMonth = useCallback(() => {
    if (!multiMonth || programmaticScrollRef.current) return;

    const container = scrollContainerRef.current;
    if (!container) return;

    const containerRect = container.getBoundingClientRect();
    const anchorTop = containerRect.top + 12;

    let nextMonth = vm;
    let bestDistance = Number.POSITIVE_INFINITY;

    for (const month of allMonths) {
      const node = monthRefs.current[monthKey(month)];
      if (!node) continue;

      const rect = node.getBoundingClientRect();
      if (rect.bottom < containerRect.top || rect.top > containerRect.bottom) continue;

      const distance = Math.abs(rect.top - anchorTop);
      if (distance < bestDistance) {
        bestDistance = distance;
        nextMonth = month;
      }
    }

    if (!sameMonth(nextMonth, vm)) setVm(nextMonth);
  }, [allMonths, multiMonth, vm]);

  const prev = useCallback(() => {
    if (!multiMonth) {
      setVm(prevMonth => addMonths(prevMonth, -1));
      return;
    }

    const currentIndex = allMonths.findIndex(month => sameMonth(month, vm));
    if (currentIndex > 0) scrollToMonth(allMonths[currentIndex - 1]);
  }, [allMonths, multiMonth, scrollToMonth, vm]);

  const next = useCallback(() => {
    if (!multiMonth) {
      setVm(prevMonth => addMonths(prevMonth, 1));
      return;
    }

    const currentIndex = allMonths.findIndex(month => sameMonth(month, vm));
    if (currentIndex >= 0 && currentIndex < allMonths.length - 1) scrollToMonth(allMonths[currentIndex + 1]);
  }, [allMonths, multiMonth, scrollToMonth, vm]);

  const today = useCallback(() => {
    scrollToMonth(isMonthInRange(initialMonth, allMonths) ? initialMonth : closestMonthTo(initialMonth, allMonths));
  }, [allMonths, initialMonth, scrollToMonth]);

  function renderYearMonth(y: number, m: number) {
    const weeks = buildGrid(y, m);
    const weekData = weeks.map(w => {
      const bars = weekBars(w, visibleEvents);
      return { bars, maxLane: bars.reduce((mx, b) => Math.max(mx, b.lane), -1) + 1 };
    });

    return (
      <div key={`year-${y}-${m}`} className="min-h-0 overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
        <div className="flex items-center justify-between bg-[#003366] px-3 py-1.5 text-white">
          <h3 className="text-sm font-bold leading-none">{MONTH_NAMES[m]} {y}</h3>
          {sameMonth({ y, m }, vm) && (
            <span className="rounded-full bg-white/15 px-1.5 py-0.5 text-[9px] font-bold text-white">
              GÖRÜNEN
            </span>
          )}
        </div>

        <div className="grid grid-cols-7 border-b border-gray-200 bg-gray-50 text-center text-[9px] font-bold uppercase tracking-wide text-gray-400">
          {WEEKDAYS.map(day => (
            <div key={day} className="border-r border-gray-100 py-1 last:border-r-0">
              {day.substring(0, 2)}
            </div>
          ))}
        </div>

        {weeks.map((week, wi) => {
          const { bars, maxLane } = weekData[wi];
          const eventTopBase = 17;
          const eventH = maxLane * 12;
          const cellMinHeight = Math.max(36, eventTopBase + eventH);

          return (
            <div key={wi} className="relative border-b border-gray-100 last:border-b-0 overflow-hidden">
              <div className="grid grid-cols-7">
                {week.map((day, di) => (
                  <div
                    key={di}
                    className={cn(
                      'border-r border-gray-100 px-1 pt-1 text-[10px] leading-none last:border-r-0',
                      !day.isMonth && 'bg-gray-50/60',
                    )}
                    style={{ minHeight: `${cellMinHeight}px` }}
                  >
                    <span
                      className={cn(
                        'inline-flex h-4 w-4 items-center justify-center rounded-full',
                        day.isToday && 'bg-[#003366] text-white font-bold',
                        !day.isToday && day.isMonth && 'text-gray-800 font-medium',
                        !day.isMonth && 'text-gray-300',
                      )}
                    >
                      {day.day}
                    </span>
                  </div>
                ))}
              </div>

              {bars.map((b, i) => {
                const s = CAT_STYLE[b.cat];

                return (
                  <TooltipProvider key={`year-${b.event.id}-${y}-${m}-${wi}-${i}`} delayDuration={100}>
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <div
                          className={cn(
                            'absolute flex h-3.5 cursor-default items-center overflow-hidden rounded border-l-[3px] px-1 text-[9px] font-bold leading-none transition-shadow hover:z-10 hover:shadow-md',
                            s.bg,
                            s.border,
                            s.text,
                            b.event.isChangedPreview && 'ring-1 ring-primary bg-primary/20',
                          )}
                          style={{
                            top: `${eventTopBase + b.lane * 12}px`,
                            left: `calc(${(b.col / 7) * 100}% + 2px)`,
                            width: `calc(${(b.span / 7) * 100}% - 4px)`,
                          }}
                        >
                          <span className="truncate">{b.event.title}</span>
                        </div>
                      </TooltipTrigger>
                      <TooltipContent side="top" className="max-w-xs z-[100]">
                        <p className="font-semibold">{b.event.title}</p>
                        <p className="text-xs text-gray-500 mt-0.5">
                          {trDate(b.event.startDate)}
                          {b.event.endDate !== b.event.startDate && ` - ${trDate(b.event.endDate)}`}
                        </p>
                      </TooltipContent>
                    </Tooltip>
                  </TooltipProvider>
                );
              })}
            </div>
          );
        })}
      </div>
    );
  }

  function renderMonth(y: number, m: number) {
    const weeks = buildGrid(y, m);
    const weekData = weeks.map(w => {
      const bars = weekBars(w, visibleEvents);
      return { bars, maxLane: bars.reduce((mx, b) => Math.max(mx, b.lane), -1) + 1 };
    });

    return (
      <div
        key={`${y}-${m}`}
        ref={node => {
          monthRefs.current[monthKey({ y, m })] = node;
        }}
        className={cn('border border-gray-200 rounded-xl overflow-hidden bg-white shadow-sm', multiMonth && 'mb-4')}
      >
        {multiMonth && (
          <div className="bg-[#003366] text-white px-3 py-1.5 text-xs font-bold tracking-wide uppercase text-center border-b border-[#002244]">
            {MONTH_NAMES[m]} {y}
          </div>
        )}

        <div className="grid grid-cols-7 border-b border-gray-200 bg-gray-50">
          {WEEKDAYS.map(d => (
            <div key={d} className="text-center text-[9px] sm:text-[11px] font-bold text-gray-400 uppercase tracking-wider py-1.5 sm:py-3 border-r border-gray-100 last:border-r-0">
              {d.substring(0, 3)}
            </div>
          ))}
        </div>

        {weeks.map((week, wi) => {
          const { bars, maxLane } = weekData[wi];
          const visible = bars.filter(b => b.lane < MAX_LANES);
          const overflow = bars.length - visible.length;
          const eventH = Math.min(maxLane, MAX_LANES) * 24 + (overflow > 0 ? 16 : 0);
          const eventTopBase = compact ? 28 : 36;
          const cellMinHeight = Math.max(compact ? 56 : 96, eventTopBase + eventH);

          return (
            <div key={wi} className="relative border-b border-gray-100 last:border-b-0 overflow-hidden">
              <div className="grid grid-cols-7">
                {week.map((day, di) => (
                  <div
                    key={di}
                    className={cn(
                      'border-r border-gray-100 last:border-r-0 px-1 pt-1 sm:px-2 sm:pt-2',
                      !day.isMonth && 'bg-gray-50/60',
                    )}
                    style={{ minHeight: `${cellMinHeight}px` }}
                  >
                    <span
                      className={cn(
                        'inline-flex items-center justify-center w-5 h-5 sm:w-7 sm:h-7 text-xs sm:text-sm rounded-full',
                        day.isToday && 'bg-[#003366] text-white font-bold',
                        !day.isToday && day.isMonth && 'text-gray-800 font-medium',
                        !day.isMonth && 'text-gray-300',
                      )}
                    >
                      {day.day}
                    </span>
                  </div>
                ))}
              </div>

              {visible.map((b, i) => {
                const s = CAT_STYLE[b.cat];
                const isContinuation = !b.start;
                return (
                  <TooltipProvider key={`${b.event.id}-${wi}-${i}`} delayDuration={100}>
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <div
                          className={cn(
                            'absolute cursor-default transition-shadow hover:shadow-md hover:z-10',
                            isContinuation
                              ? cn('h-1.5 rounded-full', s.bg.replace('-100', '-300'))
                              : cn('flex items-center h-5 sm:h-6 border-l-[3px] px-1 sm:px-2 text-[10px] sm:text-[13px] font-medium truncate rounded', s.bg, s.border, s.text),
                            b.event.isChangedPreview && 'ring-2 ring-primary border-primary bg-primary/20 bg-blend-multiply',
                          )}
                          style={{
                            top: isContinuation
                              ? `${eventTopBase + b.lane * 24 + 8}px`
                              : `${eventTopBase + b.lane * 24}px`,
                            left: `calc(${(b.col / 7) * 100}% + 3px)`,
                            width: `calc(${(b.span / 7) * 100}% - 6px)`,
                          }}
                        >
                          {!isContinuation && (
                            <>
                              <span className="uppercase text-[9px] sm:text-[10px] font-bold opacity-60 mr-1.5 shrink-0 hidden sm:inline">
                                {s.label}
                              </span>
                              <span className="truncate">{b.event.title}</span>
                            </>
                          )}
                        </div>
                      </TooltipTrigger>
                      <TooltipContent side="top" className="max-w-xs z-[100]">
                        <p className="font-semibold">{b.event.title}</p>
                        {b.event.isChangedPreview && (
                          <p className="text-xs text-primary font-bold my-0.5">Onizlemede Degistirildi</p>
                        )}
                        <p className="text-xs text-gray-500 mt-0.5">
                          {trDate(b.event.startDate)}
                          {b.event.endDate !== b.event.startDate && ` - ${trDate(b.event.endDate)}`}
                        </p>
                        {b.event.note && (
                          <p className="text-xs text-amber-600 mt-1">{b.event.note}</p>
                        )}
                      </TooltipContent>
                    </Tooltip>
                  </TooltipProvider>
                );
              })}

              {overflow > 0 && (
                <div
                  className="absolute left-1 sm:left-2 text-[10px] sm:text-xs font-semibold text-[#003366]"
                  style={{ top: `${eventTopBase + MAX_LANES * 24}px` }}
                >
                  +{overflow} daha
                </div>
              )}
            </div>
          );
        })}
      </div>
    );
  }

  function renderLegend(prefix: string, iconClass = 'h-3 w-4', textClass = 'text-[10px] font-semibold text-gray-500') {
    return (
      <>
        {(Object.entries(CAT_STYLE) as [CalendarEventCategory, typeof CAT_STYLE[CalendarEventCategory]][]).map(([category, s]) => {
          const selected = activeCategory === category;
          const content = (
            <>
              <div className={cn(iconClass, 'rounded-sm border-l-[3px]', s.bg, s.border)} />
              <span className={cn(textClass, selected && 'text-[#003366]')}>{s.label}</span>
            </>
          );

          if (!filterableLegend) {
            return (
              <div key={`${prefix}-${s.label}`} className="flex items-center gap-1.5">
                {content}
              </div>
            );
          }

          return (
            <button
              key={`${prefix}-${s.label}`}
              type="button"
              aria-pressed={selected}
              title={selected ? 'Filtreyi kaldır' : `${s.label} etkinliklerini göster`}
              onClick={() => setActiveCategory(current => current === category ? null : category)}
              className={cn(
                'flex items-center gap-1.5 rounded-full border px-2 py-1 transition',
                selected
                  ? 'border-[#003366]/40 bg-[#003366]/10 shadow-sm'
                  : 'border-transparent hover:border-gray-200 hover:bg-white',
              )}
            >
              {content}
            </button>
          );
        })}
        {filterableLegend && activeCategory && (
          <button
            type="button"
            onClick={() => setActiveCategory(null)}
            className="rounded-full border border-gray-200 px-2 py-1 text-[10px] font-semibold text-gray-500 transition hover:bg-white hover:text-[#003366]"
          >
            Tümünü göster
          </button>
        )}
      </>
    );
  }

  function renderInlineYearView() {
    const firstMonth = academicYearMonths[0];
    const lastMonth = academicYearMonths[11];

    return (
      <div className="flex flex-col overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm">
        <div className="flex shrink-0 flex-col gap-2 border-b border-gray-200 px-4 py-3 sm:flex-row sm:items-center sm:justify-between sm:px-6">
          <div>
            <p className="text-xs font-bold uppercase tracking-[0.2em] text-gray-400">Akademik yıl</p>
            <h2 className="text-xl font-bold text-[#003366] sm:text-2xl">
              {firstMonth && MONTH_NAMES[firstMonth.m]} {firstMonth?.y}
              {' - '}
              {lastMonth && MONTH_NAMES[lastMonth.m]} {lastMonth?.y}
            </h2>
            <div className="mt-2 flex flex-wrap gap-2">
              {renderLegend('inline-year-legend')}
            </div>
          </div>
        </div>
        <div className="bg-slate-50 p-2">
          <div className="grid min-h-0 gap-2 lg:grid-cols-4">
            {academicYearMonths.map(month => renderYearMonth(month.y, month.m))}
          </div>
        </div>
      </div>
    );
  }

  if (inlineYearView) {
    return renderInlineYearView();
  }

  return (
    <div className={cn('space-y-3', multiMonth && 'flex flex-col max-h-[70vh]')}>
      <div className={cn(multiMonth && 'sticky top-0 z-10 bg-white pb-1')}>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h2 className="text-lg font-bold text-[#003366]">
              {MONTH_NAMES[vm.m]} {vm.y}
            </h2>
          </div>
          <div className="flex flex-wrap items-center justify-end gap-2">
            <div className="flex gap-1">
              <button type="button" onClick={prev} className="p-2 rounded-lg border border-gray-300 hover:bg-gray-100 text-gray-600">
                <ChevronLeft className="h-5 w-5" />
              </button>
              <button type="button" onClick={next} className="p-2 rounded-lg border border-gray-300 hover:bg-gray-100 text-gray-600">
                <ChevronRight className="h-5 w-5" />
              </button>
            </div>
            <button
              type="button"
              onClick={() => setShowYearView(true)}
              className="rounded-lg border border-[#003366]/30 bg-[#003366]/5 px-3 py-1.5 text-sm font-semibold text-[#003366] hover:bg-[#003366]/10"
            >
              Yıl olarak görüntüle
            </button>
            <button
              type="button"
              onClick={today}
              className="rounded-lg border border-gray-300 px-4 py-1.5 text-sm font-semibold text-gray-700 hover:bg-gray-50"
            >
              Bugün
            </button>
          </div>
        </div>
      </div>

      <div
        ref={scrollContainerRef}
        onScroll={updateVisibleMonth}
        className={cn(multiMonth && 'overflow-y-auto pr-2 styled-scrollbar flex-1')}
      >
        {viewMonths.map(month => renderMonth(month.y, month.m))}
      </div>

      <div className="flex flex-wrap gap-2 px-1 border-t border-gray-100 pt-2">
        {renderLegend('month-legend', 'w-4 h-3', 'text-[11px] text-gray-500 font-medium')}
      </div>

      {showYearView && (
        <div className="fixed inset-0 z-[70] flex items-center justify-center bg-slate-950/60 p-2">
          <div className="flex h-[calc(100dvh-1rem)] w-full max-w-none flex-col overflow-hidden rounded-2xl bg-white shadow-2xl">
            <div className="flex shrink-0 flex-col gap-2 border-b border-gray-200 px-4 py-3 sm:flex-row sm:items-center sm:justify-between sm:px-6">
              <div>
                <p className="text-xs font-bold uppercase tracking-[0.2em] text-gray-400">Akademik yıl</p>
                <h2 className="text-xl font-bold text-[#003366] sm:text-2xl">
                  {academicYearMonths[0] && MONTH_NAMES[academicYearMonths[0].m]} {academicYearMonths[0]?.y}
                  {' - '}
                  {academicYearMonths[11] && MONTH_NAMES[academicYearMonths[11].m]} {academicYearMonths[11]?.y}
                </h2>
                <div className="mt-2 flex flex-wrap gap-2">
                  {renderLegend('year-legend')}
                </div>
              </div>
              <button
                type="button"
                onClick={() => setShowYearView(false)}
                className="self-start rounded-lg border border-gray-300 px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-50 sm:self-auto"
              >
                Kapat
              </button>
            </div>
            <div className="min-h-0 flex-1 overflow-y-auto bg-slate-50 p-2">
              <div className="grid min-h-0 gap-2 lg:grid-cols-4">
                {academicYearMonths.map(month => renderYearMonth(month.y, month.m))}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
