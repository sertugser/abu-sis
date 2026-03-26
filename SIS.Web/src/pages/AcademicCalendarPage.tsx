import { useState, useMemo, useCallback } from 'react';
import { ChevronLeft, ChevronRight, ExternalLink } from 'lucide-react';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import {
  ACADEMIC_CALENDAR_SOURCE_URL,
  SPRING_2025_2026_CALENDAR,
  type AcademicCalendarEntry,
} from '../lib/academicCalendar';

/* ─── Constants ─────────────────────────────────────────────────────── */

const TODAY = new Date();
const WEEKDAYS = ['PZT', 'SAL', 'ÇAR', 'PER', 'CUM', 'CMT', 'PAZ'];
const MONTH_NAMES = [
  'Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran',
  'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık',
];
const MAX_LANES = 3;

/* ─── Categories ────────────────────────────────────────────────────── */

type Cat = 'exam' | 'registration' | 'holiday' | 'deadline' | 'result' | 'application' | 'academic';

const CAT_MAP: Record<string, Cat> = {
  'midterms': 'exam', 'finals': 'exam', 'resit-exams': 'exam',
  'midterm-makeup': 'exam', 'prep-exemption': 'exam',
  'course-registration': 'registration', 'graduate-registration': 'registration',
  'add-drop': 'registration', 'transfer-registration': 'registration',
  'ramadan-feast': 'holiday', 'eid-al-adha': 'holiday',
  'national-sovereignty': 'holiday', 'labour-day': 'holiday',
  'atatürk-commemoration': 'holiday',
  'withdrawal-deadline': 'deadline', 'freeze-deadline': 'deadline',
  'midterm-makeup-deadline': 'deadline', 'resit-deadline': 'deadline',
  'midterm-results': 'result', 'final-results': 'result',
  'resit-results': 'result', 'transfer-results': 'result', 'graduate-results': 'result',
  'graduate-applications': 'application', 'horizontal-transfer': 'application',
  'double-major': 'application', 'international-postgraduate': 'application',
  'tuition-payment': 'academic', 'classes-start': 'academic', 'classes-end': 'academic',
};

const CAT_STYLE: Record<Cat, { bg: string; border: string; text: string; label: string }> = {
  exam:         { bg: 'bg-red-100',    border: 'border-l-red-500',    text: 'text-red-800',    label: 'SINAV' },
  registration: { bg: 'bg-blue-100',   border: 'border-l-blue-500',   text: 'text-blue-800',   label: 'KAYIT' },
  holiday:      { bg: 'bg-green-100',  border: 'border-l-green-500',  text: 'text-green-800',  label: 'TATİL' },
  deadline:     { bg: 'bg-amber-100',  border: 'border-l-amber-500',  text: 'text-amber-800',  label: 'SON GÜN' },
  result:       { bg: 'bg-gray-100',   border: 'border-l-gray-400',   text: 'text-gray-700',   label: 'SONUÇ' },
  application:  { bg: 'bg-teal-100',   border: 'border-l-teal-500',   text: 'text-teal-800',   label: 'BAŞVURU' },
  academic:     { bg: 'bg-indigo-100', border: 'border-l-indigo-500', text: 'text-indigo-800', label: 'AKADEMİK' },
};

/* ─── Date helpers ──────────────────────────────────────────────────── */

function toStr(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function sameDay(a: Date, b: Date) {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
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
  let wd = first.getDay() - 1;          // Monday=0
  if (wd < 0) wd = 6;

  const flat: Day[] = [];

  // previous-month padding
  for (let i = wd - 1; i >= 0; i--) {
    const d = new Date(year, month, -i);
    flat.push({ day: d.getDate(), isMonth: false, isToday: sameDay(d, TODAY), str: toStr(d) });
  }
  // current month
  for (let i = 1; i <= count; i++) {
    const d = new Date(year, month, i);
    flat.push({ day: i, isMonth: true, isToday: sameDay(d, TODAY), str: toStr(d) });
  }
  // next-month padding — always fill to exactly 6 rows (42 cells)
  while (flat.length < 42) {
    const d = new Date(year, month + 1, flat.length - count - wd + 1);
    flat.push({ day: d.getDate(), isMonth: false, isToday: sameDay(d, TODAY), str: toStr(d) });
  }

  const weeks: Day[][] = [];
  for (let i = 0; i < 42; i += 7) weeks.push(flat.slice(i, i + 7));
  return weeks;
}

/* ─── Event bars per week ───────────────────────────────────────────── */

interface Bar {
  event: AcademicCalendarEntry;
  cat: Cat;
  col: number;      // 0-6 start column
  span: number;     // 1-7
  lane: number;
  start: boolean;   // event starts this week
  end: boolean;     // event ends this week
}

function weekBars(week: Day[], events: AcademicCalendarEntry[]): Bar[] {
  const wStart = week[0].str;
  const wEnd = week[6].str;

  const active = events.filter(
    e => e.startDate && e.endDate && e.startDate <= wEnd && e.endDate >= wStart,
  );

  // earlier start first, then longer first → stable lanes for spanning events
  active.sort((a, b) => {
    const c = a.startDate!.localeCompare(b.startDate!);
    return c !== 0 ? c : b.endDate!.localeCompare(a.endDate!);
  });

  const lanes: number[] = [];              // lanes[i] = last occupied col

  return active.map(ev => {
    const col = ev.startDate! <= wStart
      ? 0
      : Math.max(0, week.findIndex(d => d.str >= ev.startDate!));

    let endCol = 6;
    if (ev.endDate! < wEnd) {
      for (let i = 6; i >= 0; i--) {
        if (week[i].str <= ev.endDate!) { endCol = i; break; }
      }
    }

    // lane allocation
    let lane = 0;
    while (lane < lanes.length && lanes[lane] >= col) lane++;
    if (lane >= lanes.length) lanes.push(-1);
    lanes[lane] = endCol;

    return {
      event: ev,
      cat: CAT_MAP[ev.id] || 'academic',
      col,
      span: endCol - col + 1,
      lane,
      start: ev.startDate! >= wStart,
      end: ev.endDate! <= wEnd,
    };
  });
}

/* ═══════════════════════════════════════════════════════════════════════
   Component
   ═══════════════════════════════════════════════════════════════════════ */

export default function AcademicCalendarPage() {
  const [vm, setVm] = useState({ y: TODAY.getFullYear(), m: TODAY.getMonth() });

  const weeks = useMemo(() => buildGrid(vm.y, vm.m), [vm]);

  const weekData = useMemo(
    () => weeks.map(w => {
      const bars = weekBars(w, SPRING_2025_2026_CALENDAR);
      return { bars, maxLane: bars.reduce((mx, b) => Math.max(mx, b.lane), -1) + 1 };
    }),
    [weeks],
  );

  const prev = useCallback(() => setVm(({ y, m }) => m === 0 ? { y: y - 1, m: 11 } : { y, m: m - 1 }), []);
  const next = useCallback(() => setVm(({ y, m }) => m === 11 ? { y: y + 1, m: 0 } : { y, m: m + 1 }), []);
  const today = useCallback(() => setVm({ y: TODAY.getFullYear(), m: TODAY.getMonth() }), []);

  return (
    <div className="space-y-4">
      {/* ── Header ──────────────────────────────────────────────────── */}
      <div>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-4">
            <h2 className="text-xl font-bold text-[#003366]">
              {MONTH_NAMES[vm.m]} {vm.y}
            </h2>
            <div className="flex gap-1">
              <button onClick={prev} className="p-2 rounded-lg border border-gray-300 hover:bg-gray-100 text-gray-600">
                <ChevronLeft className="h-5 w-5" />
              </button>
              <button onClick={next} className="p-2 rounded-lg border border-gray-300 hover:bg-gray-100 text-gray-600">
                <ChevronRight className="h-5 w-5" />
              </button>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <a
              href={ACADEMIC_CALENDAR_SOURCE_URL}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1.5 rounded-lg border border-gray-300 px-3 py-1.5 text-sm font-medium text-gray-600 hover:bg-gray-50"
            >
              Resmi kaynak <ExternalLink className="h-3.5 w-3.5" />
            </a>
            <button
              onClick={today}
              className="rounded-lg border border-gray-300 px-4 py-1.5 text-sm font-semibold text-gray-700 hover:bg-gray-50"
            >
              Bugün
            </button>
          </div>
        </div>
      </div>

      {/* ── Calendar grid ───────────────────────────────────────────── */}
      <div className="border border-gray-200 rounded-xl overflow-hidden bg-white shadow-sm">
        {/* Weekday header */}
        <div className="grid grid-cols-7 border-b border-gray-200 bg-gray-50">
          {WEEKDAYS.map(d => (
            <div key={d} className="text-center text-[11px] font-bold text-gray-400 uppercase tracking-wider py-3 border-r border-gray-100 last:border-r-0">
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
            <div key={wi} className="relative border-b border-gray-100 last:border-b-0 overflow-hidden">
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

              {/* Event bars (positioned absolutely) */}
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
                          {b.event.startLabel} — {b.event.endLabel}
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

      {/* ── Legend ───────────────────────────────────────────────────── */}
      <div className="flex flex-wrap gap-4 px-1">
        {(Object.entries(CAT_STYLE) as [Cat, typeof CAT_STYLE[Cat]][]).map(([, s]) => (
          <div key={s.label} className="flex items-center gap-1.5">
            <div className={cn('w-4 h-3 rounded-sm border-l-[3px]', s.bg, s.border)} />
            <span className="text-[11px] text-gray-500 font-medium">{s.label}</span>
          </div>
        ))}
      </div>
    </div>
  );
}
