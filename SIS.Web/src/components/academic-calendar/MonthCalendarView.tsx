import { useState, useMemo, useCallback } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import { displayTurkishText } from '@/lib/turkishText';
import { calendarEventCategory, CAT_STYLE, CALENDAR_EVENT_LABELS, type CalendarEventCategory } from '@/lib/calendarEventTypes';
import type { TermCalendarEventDto } from '@/hooks/useAcademicCalendar';

/* ─── Constants ─────────────────────────────────────────────────────── */

const TODAY = new Date();
const WEEKDAYS = ['PZT', 'SAL', 'ÇAR', 'PER', 'CUM', 'CMT', 'PAZ'];
const MONTH_NAMES = [
  'Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran',
  'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık',
];
const MAX_LANES = 3;

/* ─── Props ─────────────────────────────────────────────────────────── */

export interface MonthCalendarViewProps {
  events: TermCalendarEventDto[];
  termStart: string;
  termEnd: string;
}

/* ─── Date helpers ──────────────────────────────────────────────────── */

function toStr(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function sameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear()
    && a.getMonth() === b.getMonth()
    && a.getDate() === b.getDate();
}

function isoToDateStr(iso: string): string {
  return iso.substring(0, 10);
}

function trDate(iso: string): string {
  return new Date(iso).toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' });
}

/* ─── Grid types & builders ─────────────────────────────────────────── */

interface Day {
  day: number;
  isMonth: boolean;
  isToday: boolean;
  str: string;
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

/* ─── Normalized event shape ────────────────────────────────────────── */

interface NormalizedEvent {
  id: number;
  eventType: string;
  title: string;
  startDate: string;  // YYYY-MM-DD
  endDate: string;    // YYYY-MM-DD
  note: string | null;
}

function normalizeEvents(events: TermCalendarEventDto[]): NormalizedEvent[] {
  return events.map(e => ({
    id: e.id,
    eventType: e.eventType,
    title: displayTurkishText(e.title),
    startDate: isoToDateStr(e.startDate),
    endDate: e.endDate ? isoToDateStr(e.endDate) : isoToDateStr(e.startDate),
    note: e.note,
  }));
}

/* ─── Event bar layout per week ─────────────────────────────────────── */

interface Bar {
  event: NormalizedEvent;
  cat: CalendarEventCategory;
  col: number;
  span: number;
  lane: number;
  start: boolean;
  end: boolean;
}

function weekBars(week: Day[], events: NormalizedEvent[]): Bar[] {
  const wStart = week[0].str;
  const wEnd = week[6].str;

  const active = events.filter(e => e.startDate <= wEnd && e.endDate >= wStart);

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
        if (week[i].str <= ev.endDate) { endCol = i; break; }
      }
    }

    let lane = 0;
    while (lane < lanes.length && lanes[lane] >= col) lane++;
    if (lane >= lanes.length) lanes.push(-1);
    lanes[lane] = endCol;

    return {
      event: ev,
      cat: calendarEventCategory(ev.eventType),
      col,
      span: endCol - col + 1,
      lane,
      start: ev.startDate >= wStart,
      end: ev.endDate <= wEnd,
    };
  });
}

/* ═══════════════════════════════════════════════════════════════════════
   MonthCalendarView component
   ═══════════════════════════════════════════════════════════════════════ */

export function MonthCalendarView({ events, termStart }: MonthCalendarViewProps) {
  const initialMonth = useMemo(() => {
    const d = new Date(termStart);
    return { y: d.getFullYear(), m: d.getMonth() };
  }, [termStart]);

  const [vm, setVm] = useState(initialMonth);

  const normalizedEvents = useMemo(() => normalizeEvents(events), [events]);
  const weeks = useMemo(() => buildGrid(vm.y, vm.m), [vm]);

  const weekData = useMemo(
    () => weeks.map(w => {
      const bars = weekBars(w, normalizedEvents);
      return { bars, maxLane: bars.reduce((mx, b) => Math.max(mx, b.lane), -1) + 1 };
    }),
    [weeks, normalizedEvents],
  );

  const prev = useCallback(
    () => setVm(({ y, m }) => m === 0 ? { y: y - 1, m: 11 } : { y, m: m - 1 }),
    [],
  );
  const next = useCallback(
    () => setVm(({ y, m }) => m === 11 ? { y: y + 1, m: 0 } : { y, m: m + 1 }),
    [],
  );
  const goToday = useCallback(
    () => setVm({ y: TODAY.getFullYear(), m: TODAY.getMonth() }),
    [],
  );

  return (
    <div className="space-y-4">
      {/* ── Month navigation header ──────────────────────────────── */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <h2 className="text-xl font-bold text-[#003366]">
            {MONTH_NAMES[vm.m]} {vm.y}
          </h2>
          <div className="flex gap-1">
            <button
              onClick={prev}
              className="p-2 rounded-lg border border-gray-300 hover:bg-gray-100 text-gray-600"
              aria-label="Önceki ay"
            >
              <ChevronLeft className="h-5 w-5" />
            </button>
            <button
              onClick={next}
              className="p-2 rounded-lg border border-gray-300 hover:bg-gray-100 text-gray-600"
              aria-label="Sonraki ay"
            >
              <ChevronRight className="h-5 w-5" />
            </button>
          </div>
        </div>
        <button
          onClick={goToday}
          className="rounded-lg border border-gray-300 px-4 py-1.5 text-sm font-semibold text-gray-700 hover:bg-gray-50"
        >
          Bugün
        </button>
      </div>

      {/* ── Calendar grid ────────────────────────────────────────── */}
      <div className="border border-gray-200 rounded-xl overflow-hidden bg-white shadow-sm">
        {/* Weekday header */}
        <div className="grid grid-cols-7 border-b border-gray-200 bg-gray-50">
          {WEEKDAYS.map(d => (
            <div
              key={d}
              className="text-center text-[11px] font-bold text-gray-400 uppercase tracking-wider py-3 border-r border-gray-100 last:border-r-0"
            >
              {d}
            </div>
          ))}
        </div>

        {/* Week rows */}
        {weeks.map((week, wi) => {
          const { bars, maxLane } = weekData[wi];
          const visible = bars.filter(b => b.lane < MAX_LANES);
          const overflow = bars.length - visible.length;
          const eventH = Math.min(maxLane, MAX_LANES) * 28 + (overflow > 0 ? 20 : 0);

          return (
            <div
              key={wi}
              className="relative border-b border-gray-100 last:border-b-0 overflow-hidden"
            >
              {/* Day number cells */}
              <div className="grid grid-cols-7">
                {week.map((day, di) => (
                  <div
                    key={di}
                    className={cn(
                      'border-r border-gray-100 last:border-r-0 px-2 pt-2',
                      !day.isMonth && 'bg-gray-50/60',
                    )}
                    style={{ minHeight: `${Math.max(110, 34 + eventH)}px` }}
                  >
                    <span
                      className={cn(
                        'inline-flex items-center justify-center w-7 h-7 text-sm rounded-full',
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

              {/* Event bars */}
              {visible.map((b, i) => {
                const s = CAT_STYLE[b.cat];
                const isContinuation = !b.start;
                const label = CALENDAR_EVENT_LABELS[b.event.eventType] ?? b.event.eventType;
                return (
                  <TooltipProvider key={`${b.event.id}-${wi}-${i}`} delayDuration={100}>
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <div
                          className={cn(
                            'absolute cursor-default transition-shadow hover:shadow-md hover:z-10',
                            isContinuation
                              ? cn('h-1.5 rounded-full', s.bg.replace('-100', '-300'))
                              : cn('flex items-center h-6 border-l-[3px] px-2 text-[13px] font-medium truncate rounded', s.bg, s.border, s.text),
                          )}
                          style={{
                            top: isContinuation
                              ? `${34 + b.lane * 28 + 10}px`
                              : `${34 + b.lane * 28}px`,
                            left: `calc(${(b.col / 7) * 100}% + 3px)`,
                            width: `calc(${(b.span / 7) * 100}% - 6px)`,
                          }}
                        >
                          {!isContinuation && (
                            <>
                              <span className="uppercase text-[10px] font-bold opacity-60 mr-1.5 shrink-0 hidden sm:inline">
                                {s.label}
                              </span>
                              <span className="truncate">{b.event.title}</span>
                            </>
                          )}
                        </div>
                      </TooltipTrigger>
                      <TooltipContent side="top" className="max-w-xs">
                        <p className="font-semibold">{b.event.title}</p>
                        <p className="text-xs text-gray-500 mt-0.5">
                          {label}
                        </p>
                        <p className="text-xs text-gray-500">
                          {trDate(b.event.startDate)}
                          {b.event.endDate !== b.event.startDate && ` — ${trDate(b.event.endDate)}`}
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
                  className="absolute left-2 text-xs font-semibold text-[#003366]"
                  style={{ top: `${34 + MAX_LANES * 28}px` }}
                >
                  +{overflow} daha
                </div>
              )}
            </div>
          );
        })}
      </div>

      {/* ── Legend ───────────────────────────────────────────────── */}
      <div className="flex flex-wrap gap-4 px-1">
        {(Object.entries(CAT_STYLE) as [CalendarEventCategory, typeof CAT_STYLE[CalendarEventCategory]][]).map(([, s]) => (
          <div key={s.label} className="flex items-center gap-1.5">
            <div className={cn('w-4 h-3 rounded-sm border-l-[3px]', s.bg, s.border)} />
            <span className="text-[11px] text-gray-500 font-medium">{s.label}</span>
          </div>
        ))}
      </div>
    </div>
  );
}
