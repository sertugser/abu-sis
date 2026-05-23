import { useMemo } from 'react';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { calendarEventCategory, type CalendarEventCategory } from '@/lib/calendarEventTypes';
import { dayDifference, eventDateRange, toStr, trDate, TODAY, type VisualizerEvent } from '@/lib/calendarVisualizer/geometry';
import { cn } from '@/lib/utils';

const MONTH_NAMES = [
  'Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran',
  'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık',
];

// Anlamlı bir okuma sırası: akademik akış → kayıt → başvuru → sınav → son gün → sonuç → tatil.
const CATEGORY_ORDER: CalendarEventCategory[] = [
  'academic', 'registration', 'application', 'exam', 'deadline', 'result', 'holiday',
];

// Google Calendar tarzı dolu/koyu bloklar (metin içeride, yüksek kontrast).
const SOLID_STYLE: Record<CalendarEventCategory, string> = {
  exam: 'bg-red-500 text-white',
  registration: 'bg-blue-500 text-white',
  application: 'bg-teal-600 text-white',
  deadline: 'bg-amber-500 text-amber-950',
  result: 'bg-slate-500 text-white',
  holiday: 'bg-emerald-600 text-white',
  academic: 'bg-indigo-500 text-white',
};

const CATEGORY_LABEL: Record<CalendarEventCategory, string> = {
  exam: 'SINAV',
  registration: 'KAYIT',
  application: 'BAŞVURU',
  deadline: 'SON GÜN',
  result: 'SONUÇ',
  holiday: 'TATİL',
  academic: 'AKADEMİK',
};

const DAY_WIDTH = 4; // px/gün → ay ~120px
const LANE_HEIGHT = 24;
const LANE_GAP = 4;
const GROUP_PADDING = 6;
const HEADER_HEIGHT = 36;
const LABEL_COL_WIDTH = 150;

// Blok genişliği: gerçek süre ile etiketin sığması arasından büyük olanı (Google all-day bloğu mantığı).
const CHAR_WIDTH = 6.4;
const BLOCK_PAD = 16; // px-2 (sol+sağ)
const MAX_BLOCK_WIDTH = 360; // çok uzun başlıklarda aşırı genişlemeyi sınırla (sonrası truncate)

interface TimelineBar {
  event: VisualizerEvent;
  cat: CalendarEventCategory;
  startDate: string;
  endDate: string;
  leftDays: number;
  spanDays: number;
  barWidth: number;
  lane: number;
}

interface TimelineGroup {
  cat: CalendarEventCategory;
  bars: TimelineBar[];
  height: number;
}

interface TimelineMonth {
  label: string;
  leftDays: number;
  days: number;
}

interface TimelineModel {
  totalDays: number;
  months: TimelineMonth[];
  groups: TimelineGroup[];
  todayLeftDays: number | null;
}

function daysInMonth(year: number, month1to12: number) {
  return new Date(Date.UTC(year, month1to12, 0)).getUTCDate();
}

type PackInput = Omit<TimelineBar, 'lane'>;

function packLanes(items: PackInput[]): { bars: TimelineBar[]; laneCount: number } {
  const laneEnds: number[] = []; // lane'in dolu olduğu son px-offset (exclusive)
  const placed = items.map(item => {
    const startPx = item.leftDays * DAY_WIDTH;
    let lane = 0;
    while (lane < laneEnds.length && laneEnds[lane] > startPx) lane++;

    const endPx = startPx + item.barWidth;
    if (lane === laneEnds.length)
      laneEnds.push(endPx);
    else
      laneEnds[lane] = endPx;

    return { ...item, lane };
  });

  return { bars: placed, laneCount: laneEnds.length };
}

function buildTimelineModel(events: VisualizerEvent[]): TimelineModel | null {
  const ranges = events.flatMap(event => {
    const range = eventDateRange(event);
    if (!range)
      return [];

    return [{
      event,
      cat: calendarEventCategory(event.eventType, event.title, event.templateItemKey),
      startDate: range.startDate,
      endDate: range.endDate,
    }];
  });

  if (ranges.length === 0)
    return null;

  let minStart = ranges[0].startDate;
  let maxEnd = ranges[0].endDate;
  for (const range of ranges) {
    if (range.startDate < minStart) minStart = range.startDate;
    if (range.endDate > maxEnd) maxEnd = range.endDate;
  }

  // Domain'i ay sınırlarına yasla: ilk ayın 1'i → son ayın son günü.
  const domainStart = `${minStart.slice(0, 7)}-01`;
  const endYear = Number(maxEnd.slice(0, 4));
  const endMonth = Number(maxEnd.slice(5, 7));
  const domainEnd = `${maxEnd.slice(0, 7)}-${String(daysInMonth(endYear, endMonth)).padStart(2, '0')}`;
  const totalDays = dayDifference(domainEnd, domainStart) + 1;

  const startYear = Number(domainStart.slice(0, 4));
  const startMonth = Number(domainStart.slice(5, 7));
  const monthCount = (endYear - startYear) * 12 + (endMonth - startMonth) + 1;

  const months: TimelineMonth[] = [];
  for (let i = 0; i < monthCount; i++) {
    const year = startYear + Math.floor((startMonth - 1 + i) / 12);
    const month = ((startMonth - 1 + i) % 12) + 1;
    const monthFirst = `${year}-${String(month).padStart(2, '0')}-01`;
    months.push({
      label: `${MONTH_NAMES[month - 1]} ${year}`,
      leftDays: dayDifference(monthFirst, domainStart),
      days: daysInMonth(year, month),
    });
  }

  const groups: TimelineGroup[] = [];
  for (const cat of CATEGORY_ORDER) {
    const items: PackInput[] = ranges
      .filter(range => range.cat === cat)
      .sort((a, b) => {
        const byStart = a.startDate.localeCompare(b.startDate);
        return byStart !== 0 ? byStart : b.endDate.localeCompare(a.endDate);
      })
      .map(range => {
        const spanDays = dayDifference(range.endDate, range.startDate) + 1;
        const leftDays = dayDifference(range.startDate, domainStart);
        const labelPx = Math.min(range.event.title.length * CHAR_WIDTH + BLOCK_PAD, MAX_BLOCK_WIDTH);
        const barWidth = Math.max(spanDays * DAY_WIDTH, labelPx);

        return {
          event: range.event,
          cat: range.cat,
          startDate: range.startDate,
          endDate: range.endDate,
          leftDays,
          spanDays,
          barWidth,
        };
      });

    if (items.length === 0)
      continue;

    const { bars, laneCount } = packLanes(items);
    const height = GROUP_PADDING * 2 + laneCount * LANE_HEIGHT + (laneCount - 1) * LANE_GAP;
    groups.push({ cat, bars, height });
  }

  const todayStr = toStr(TODAY);
  const todayLeftDays = todayStr >= domainStart && todayStr <= domainEnd
    ? dayDifference(todayStr, domainStart)
    : null;

  return { totalDays, months, groups, todayLeftDays };
}

export interface AcademicTimelineProps {
  events: VisualizerEvent[];
}

export function AcademicTimeline({ events }: AcademicTimelineProps) {
  const model = useMemo(() => buildTimelineModel(events), [events]);

  if (!model) {
    return (
      <div className="rounded-2xl border border-gray-200 bg-white p-10 text-center text-gray-400 shadow-sm">
        Zaman çizelgesi için etkinlik bulunmuyor.
      </div>
    );
  }

  const { totalDays, months, groups, todayLeftDays } = model;
  const totalWidth = totalDays * DAY_WIDTH;
  const firstMonth = months[0];
  const lastMonth = months[months.length - 1];

  return (
    <div className="overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm">
      <div className="flex items-center justify-between gap-3 border-b border-gray-200 px-4 py-3">
        <div>
          <p className="text-xs font-bold uppercase tracking-[0.2em] text-gray-400">Zaman çizelgesi</p>
          <h2 className="text-lg font-bold text-[#003366]">
            {firstMonth?.label} – {lastMonth?.label}
          </h2>
        </div>
        <p className="hidden text-xs text-gray-400 sm:block">
          Yatay kaydırarak gezin · barın üstüne gelince detay
        </p>
      </div>

      <div className="flex">
        {/* Sabit kategori sütunu (yatay scroll'dan etkilenmez) */}
        <div className="shrink-0 border-r border-gray-200 bg-gray-50" style={{ width: LABEL_COL_WIDTH }}>
          <div style={{ height: HEADER_HEIGHT }} className="border-b border-gray-200" />
          {groups.map(group => (
            <div
              key={group.cat}
              style={{ height: group.height }}
              className="flex items-center gap-2 border-b border-gray-100 px-3 last:border-b-0"
            >
              <span className={cn('h-3 w-3 shrink-0 rounded-sm', SOLID_STYLE[group.cat])} />
              <span className="truncate text-xs font-semibold text-gray-600">{CATEGORY_LABEL[group.cat]}</span>
            </div>
          ))}
        </div>

        {/* Kaydırılabilir zaman alanı */}
        <div className="flex-1 overflow-x-auto styled-scrollbar">
          <div style={{ width: totalWidth }}>
            {/* Ay başlığı */}
            <div className="relative border-b border-gray-200 bg-gray-50" style={{ height: HEADER_HEIGHT }}>
              {months.map(month => (
                <div
                  key={month.label}
                  className="absolute top-0 flex h-full items-center border-r border-gray-200 px-2 text-xs font-semibold text-gray-500"
                  style={{ left: month.leftDays * DAY_WIDTH, width: month.days * DAY_WIDTH }}
                >
                  <span className="truncate">{month.label}</span>
                </div>
              ))}
            </div>

            {/* Şeritler */}
            <div className="relative">
              {todayLeftDays !== null && (
                <div
                  className="pointer-events-none absolute bottom-0 top-0 z-20 w-0.5 bg-rose-500"
                  style={{ left: todayLeftDays * DAY_WIDTH }}
                >
                  <span className="absolute left-0 top-0 -translate-x-1/2 rounded bg-rose-500 px-1 text-[9px] font-bold text-white">
                    bugün
                  </span>
                </div>
              )}

              {groups.map(group => (
                <div
                  key={group.cat}
                  className="relative border-b border-gray-100 last:border-b-0"
                  style={{ height: group.height }}
                >
                  {/* Ay ızgara çizgileri */}
                  {months.map(month => (
                    <div
                      key={month.label}
                      className="absolute bottom-0 top-0 border-r border-gray-100"
                      style={{ left: (month.leftDays + month.days) * DAY_WIDTH }}
                    />
                  ))}

                  {group.bars.map((bar, index) => (
                    <TooltipProvider key={`${bar.event.id}-${index}`} delayDuration={100}>
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <div
                            className={cn(
                              'absolute flex items-center rounded-md px-2 shadow-sm transition-shadow hover:z-10 hover:shadow-md',
                              SOLID_STYLE[bar.cat],
                              bar.event.isChangedPreview && 'ring-2 ring-offset-1 ring-primary',
                            )}
                            style={{
                              left: bar.leftDays * DAY_WIDTH,
                              width: bar.barWidth,
                              top: GROUP_PADDING + bar.lane * (LANE_HEIGHT + LANE_GAP),
                              height: LANE_HEIGHT,
                            }}
                          >
                            {/* sticky: uzun bloklarda başlık kaydırırken sol kenarda görünür kalır */}
                            <span className="sticky left-0 block max-w-full truncate text-[11px] font-semibold leading-none">
                              {bar.event.title}
                            </span>
                          </div>
                        </TooltipTrigger>
                        <TooltipContent side="top" className="z-[100] max-w-xs">
                          <p className="font-semibold">{bar.event.title}</p>
                          <p className="mt-0.5 text-xs text-gray-500">
                            {trDate(bar.startDate)}
                            {bar.endDate !== bar.startDate && ` - ${trDate(bar.endDate)}`}
                          </p>
                          {bar.event.note && (
                            <p className="mt-1 text-xs text-amber-600">{bar.event.note}</p>
                          )}
                        </TooltipContent>
                      </Tooltip>
                    </TooltipProvider>
                  ))}
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
