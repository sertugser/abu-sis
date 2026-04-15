import { Fragment, useMemo } from 'react';
import { cn } from '@/lib/utils';
import { parseScheduleSummary } from '@/lib/scheduleUtils';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Badge } from '@/components/ui/badge';
import { MapPin, Plus, User, Users, Clock, Wifi } from 'lucide-react';
import type { FacultyPlanningSection, FacultyPlanningTimeSlot } from '../../types/facultyPlanning';
import { STATUS_CONFIG } from './sectionUtils';

/* ── Gün tanımları ── */
const WEEKDAYS = [
  { day: 1, label: 'Pazartesi' },
  { day: 2, label: 'Salı' },
  { day: 3, label: 'Çarşamba' },
  { day: 4, label: 'Perşembe' },
  { day: 5, label: 'Cuma' },
];

/* ── 16 renk paleti — courseCode hash ile sabit renk ── */
const COURSE_COLORS = [
  { bg: 'bg-blue-100',    border: 'border-blue-300',    text: 'text-blue-900',    accent: '#2563eb' },
  { bg: 'bg-emerald-100', border: 'border-emerald-300', text: 'text-emerald-900', accent: '#059669' },
  { bg: 'bg-violet-100',  border: 'border-violet-300',  text: 'text-violet-900',  accent: '#7c3aed' },
  { bg: 'bg-amber-100',   border: 'border-amber-300',   text: 'text-amber-900',   accent: '#d97706' },
  { bg: 'bg-rose-100',    border: 'border-rose-300',    text: 'text-rose-900',    accent: '#e11d48' },
  { bg: 'bg-cyan-100',    border: 'border-cyan-300',    text: 'text-cyan-900',    accent: '#0891b2' },
  { bg: 'bg-orange-100',  border: 'border-orange-300',  text: 'text-orange-900',  accent: '#ea580c' },
  { bg: 'bg-indigo-100',  border: 'border-indigo-300',  text: 'text-indigo-900',  accent: '#4f46e5' },
  { bg: 'bg-teal-100',    border: 'border-teal-300',    text: 'text-teal-900',    accent: '#0d9488' },
  { bg: 'bg-pink-100',    border: 'border-pink-300',    text: 'text-pink-900',    accent: '#db2777' },
  { bg: 'bg-lime-100',    border: 'border-lime-300',    text: 'text-lime-900',    accent: '#65a30d' },
  { bg: 'bg-fuchsia-100', border: 'border-fuchsia-300', text: 'text-fuchsia-900', accent: '#c026d3' },
  { bg: 'bg-sky-100',     border: 'border-sky-300',     text: 'text-sky-900',     accent: '#0284c7' },
  { bg: 'bg-red-100',     border: 'border-red-300',     text: 'text-red-900',     accent: '#dc2626' },
  { bg: 'bg-yellow-100',  border: 'border-yellow-400',  text: 'text-yellow-900',  accent: '#ca8a04' },
  { bg: 'bg-purple-100',  border: 'border-purple-300',  text: 'text-purple-900',  accent: '#9333ea' },
];

const CONFLICT_COLOR = { bg: 'bg-red-50', border: 'border-red-400', text: 'text-red-900', accent: '#dc2626' };

/** Müfredat yılına göre renk: 1.sınıf = mavi, 2.sınıf = yeşil, 3.sınıf = mor, 4.sınıf = turuncu */
const YEAR_COLOR_MAP: Record<number, number> = { 1: 0, 2: 1, 3: 2, 4: 3 };

function getBlockColor(suggestedSemester: number, status: string) {
  if (status === 'Conflicted') return CONFLICT_COLOR;
  const year = suggestedSemester > 0 ? Math.ceil(suggestedSemester / 2) : 0;
  const colorIdx = YEAR_COLOR_MAP[year] ?? (year % COURSE_COLORS.length);
  return COURSE_COLORS[colorIdx];
}

/** Saat >= 18:00 ise akşam (online) dilimi */
function isEveningSlot(startTime: string): boolean {
  const h = parseInt(startTime.split(':')[0], 10);
  return h >= 18;
}

/* ── Blok tanımları ── */
interface ParsedBlock {
  sectionId: number;
  section: FacultyPlanningSection;
  day: number;
  startTime: string;
  endTime: string;
}

function parseSectionSchedule(section: FacultyPlanningSection): ParsedBlock[] {
  const slots = parseScheduleSummary(section.scheduleSummary);
  return slots.map(slot => ({
    sectionId: section.sectionId,
    section,
    day: slot.day,
    startTime: slot.startTime,
    endTime: slot.endTime,
  }));
}

function timeToMinutes(t: string): number {
  const [h, m] = t.split(':').map(Number);
  return h * 60 + m;
}

/* ── Plain CalendarCell ── */
function CalendarCell({
  isEvening, children,
}: {
  isEvening: boolean;
  children: React.ReactNode;
}) {
  return (
    <td
      className={cn(
        'border border-slate-200/80 p-1 align-top',
        isEvening ? 'bg-slate-50/60' : 'bg-white',
      )}
      style={{ minHeight: 56 }}
    >
      {children}
    </td>
  );
}

/* ── Popover detay ── */
function SectionPopoverContent({
  section, color,
}: {
  section: FacultyPlanningSection;
  color: typeof COURSE_COLORS[0];
}) {
  const statusCfg = STATUS_CONFIG[section.status];
  return (
    <div className="space-y-3 p-1">
      <div>
        <div className="flex items-center gap-2 mb-1.5">
          <div className="w-3 h-3 rounded-sm" style={{ backgroundColor: color.accent }} />
          <span className="font-mono font-bold text-base" style={{ color: color.accent }}>
            {section.courseCode}
          </span>
          <Badge variant={statusCfg?.variant ?? 'outline'} className="text-[10px]">
            {statusCfg?.label ?? section.status}
          </Badge>
        </div>
        <p className="text-sm font-medium text-slate-800">{section.courseName}</p>
        <p className="text-xs text-slate-500">Şube {section.sectionIndex}</p>
      </div>

      <div className="space-y-2 text-sm">
        <div className="flex items-center gap-2">
          <Users className="h-3.5 w-3.5 text-slate-400" />
          <span className="text-slate-500">Bölüm:</span>
          <span className="text-slate-700 font-medium ml-auto">{section.departmentCode}</span>
        </div>
        <div className="flex items-center gap-2">
          <Clock className="h-3.5 w-3.5 text-slate-400" />
          <span className="text-slate-500">Yüklenme:</span>
          <Badge variant="outline" className="text-[10px] ml-auto">{section.theoryHours}T+{section.labHours}L</Badge>
        </div>
        <div className="flex items-center gap-2">
          <Users className="h-3.5 w-3.5 text-slate-400" />
          <span className="text-slate-500">Kapasite:</span>
          <span className="text-slate-700 font-medium ml-auto">{section.capacity}</span>
        </div>
        <div className="flex items-center gap-2">
          <MapPin className="h-3.5 w-3.5 text-slate-400" />
          <span className="text-slate-500">Derslik:</span>
          <span className="text-slate-700 font-medium ml-auto">{section.classroomName ?? '—'}</span>
        </div>
        <div className="flex items-center gap-2">
          <User className="h-3.5 w-3.5 text-slate-400" />
          <span className="text-slate-500">Hoca:</span>
          <span className="text-slate-700 font-medium ml-auto max-w-[160px] truncate">
            {section.instructorName ?? '—'}
          </span>
        </div>
        {section.scheduleSummary && (
          <div className="pt-1 border-t border-slate-100">
            <span className="text-slate-500 text-xs">Program</span>
            <p className="mt-0.5 font-mono text-xs text-slate-700">{section.scheduleSummary}</p>
          </div>
        )}
      </div>
    </div>
  );
}

/* ── Tek ders bloğu ── */
function SectionBlock({
  section, rowSpan, isContinuation,
}: {
  section: FacultyPlanningSection;
  rowSpan: number;
  isContinuation: boolean;
}) {
  const color = getBlockColor(section.suggestedSemester, section.status);

  return (
    <Popover>
      <PopoverTrigger asChild>
        <div
          className={cn(
            'rounded-lg border-l-[3px] px-2 py-1.5 cursor-pointer transition-all',
            'hover:shadow-md hover:scale-[1.01]',
            color.bg, color.border, color.text,
          )}
          style={{ borderLeftColor: color.accent }}
          onClick={e => e.stopPropagation()}
        >
          {isContinuation ? (
            <span className="font-mono font-semibold text-xs opacity-60">{section.courseCode}</span>
          ) : (
            <>
              <div className="font-mono font-bold text-sm leading-tight">{section.courseCode}</div>
              <div className="text-xs leading-tight mt-0.5 opacity-80 truncate">{section.courseName}</div>
              {rowSpan >= 2 && (
                <div className="flex items-center gap-1 mt-1 text-[11px] opacity-70">
                  {section.classroomName && (
                    <span className="flex items-center gap-0.5">
                      <MapPin className="h-3 w-3" />
                      {section.classroomName}
                    </span>
                  )}
                  {section.instructorName && (
                    <span className="flex items-center gap-0.5 ml-1">
                      <User className="h-3 w-3" />
                      {section.instructorName.split(' ').pop()}
                    </span>
                  )}
                </div>
              )}
              {rowSpan >= 3 && (
                <div className="text-[10px] mt-0.5 opacity-60">
                  Ş.{section.sectionIndex} &middot; {section.capacity} kişi
                </div>
              )}
            </>
          )}
        </div>
      </PopoverTrigger>
      <PopoverContent className="w-80" side="right" align="start">
        <SectionPopoverContent
          section={section}
          color={color}
        />
      </PopoverContent>
    </Popover>
  );
}

/* ── Ana CalendarGrid ── */
export interface CalendarGridProps {
  sections: FacultyPlanningSection[];
  timeSlots: FacultyPlanningTimeSlot[];
  onEmptyCellClick?: (day: number, slotOrder: number, startTime: string, endTime: string) => void;
}

export function CalendarGrid({
  sections, timeSlots, onEmptyCellClick,
}: CalendarGridProps) {
  /* Zaman satırları */
  const timeRows = useMemo(() => {
    const seen = new Map<number, { slotOrder: number; startTime: string; endTime: string }>();
    for (const slot of timeSlots) {
      if (!seen.has(slot.slotOrder)) {
        seen.set(slot.slotOrder, { slotOrder: slot.slotOrder, startTime: slot.startTime, endTime: slot.endTime });
      }
    }
    return Array.from(seen.entries())
      .sort(([a], [b]) => a - b)
      .map(([, v]) => v);
  }, [timeSlots]);

  /* Blokları parse et */
  const allBlocks = useMemo(() => {
    return sections
      .filter(s => s.status !== 'Unscheduled')
      .flatMap(s => parseSectionSchedule(s));
  }, [sections]);

  /* Hücre → blok haritası */
  const blockMap = useMemo(() => {
    const map = new Map<string, ParsedBlock[]>();
    for (const block of allBlocks) {
      for (const row of timeRows) {
        const rowStart = timeToMinutes(row.startTime.slice(0, 5));
        const rowEnd = timeToMinutes(row.endTime.slice(0, 5));
        const blockStart = timeToMinutes(block.startTime);
        const blockEnd = timeToMinutes(block.endTime);
        if (blockStart < rowEnd && blockEnd > rowStart) {
          const key = `${block.day}-${row.startTime}`;
          const arr = map.get(key) ?? [];
          if (!arr.some(b => b.sectionId === block.sectionId)) {
            arr.push(block);
          }
          map.set(key, arr);
        }
      }
    }
    return map;
  }, [allBlocks, timeRows]);

  /* rowSpan hesapla */
  const blockStartCells = useMemo(() => {
    const starts = new Map<string, { block: ParsedBlock; rowSpan: number }>();
    const covered = new Set<string>();

    for (const block of allBlocks) {
      const spanRows: number[] = [];
      for (let i = 0; i < timeRows.length; i++) {
        const row = timeRows[i];
        const rowStart = timeToMinutes(row.startTime.slice(0, 5));
        const rowEnd = timeToMinutes(row.endTime.slice(0, 5));
        const blockStart = timeToMinutes(block.startTime);
        const blockEnd = timeToMinutes(block.endTime);
        if (blockStart < rowEnd && blockEnd > rowStart) {
          spanRows.push(i);
        }
      }
      if (spanRows.length > 0) {
        const firstRow = timeRows[spanRows[0]];
        const startKey = `${block.sectionId}-${block.day}-${firstRow.startTime}`;
        if (!starts.has(startKey)) {
          starts.set(startKey, { block, rowSpan: spanRows.length });
          for (let k = 1; k < spanRows.length; k++) {
            covered.add(`${block.sectionId}-${block.day}-${timeRows[spanRows[k]].startTime}`);
          }
        }
      }
    }
    return { starts, covered };
  }, [allBlocks, timeRows]);

  /* Akşam dilimi başlangıç satırı bul */
  const eveningStartIdx = useMemo(() => {
    return timeRows.findIndex(r => isEveningSlot(r.startTime));
  }, [timeRows]);

  if (timeRows.length === 0) {
    return (
      <div className="h-full border border-slate-200 rounded-xl flex items-center justify-center bg-slate-50">
        <p className="text-base text-slate-400">Zaman dilimi bilgisi bulunamadı</p>
      </div>
    );
  }

  return (
    <div className="h-full border border-slate-200 rounded-xl overflow-auto bg-white shadow-sm">
      <table className="w-full border-collapse" style={{ tableLayout: 'fixed' }}>
        <colgroup>
          <col style={{ width: 72 }} />
          {WEEKDAYS.map(wd => <col key={wd.day} />)}
        </colgroup>
        <thead className="sticky top-0 z-10">
          <tr className="bg-slate-50/95 backdrop-blur-sm">
            <th className="border border-slate-200/80 p-2 text-center font-semibold text-slate-500 text-xs uppercase tracking-wider">
              Saat
            </th>
            {WEEKDAYS.map(wd => (
              <th key={wd.day} className="border border-slate-200/80 p-2 text-center">
                <div className="font-semibold text-slate-700 text-sm">{wd.label}</div>
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {/* Akşam dilimi ayırıcı */}
          {timeRows.map((row, rowIdx) => {
            const isEvening = isEveningSlot(row.startTime);
            const showEveningSeparator = rowIdx === eveningStartIdx && eveningStartIdx > 0;

            return (
              <Fragment key={row.startTime}>
                {showEveningSeparator && (
                  <tr>
                    <td colSpan={6} className="bg-slate-100 border border-slate-200/80 py-1 px-3">
                      <div className="flex items-center gap-2 text-xs text-slate-500 font-medium">
                        <Wifi className="h-3 w-3" />
                        <span>Akşam Dilimi — Sadece Online Dersler</span>
                        <div className="flex-1 h-px bg-slate-300" />
                      </div>
                    </td>
                  </tr>
                )}
                <tr style={{ height: 64 }}>
                  {/* Saat etiketi */}
                  <td className={cn(
                    'border border-slate-200/80 p-1 text-center align-top',
                    isEvening && 'bg-slate-50/60',
                  )}>
                    <span className="font-mono font-semibold text-sm text-slate-500">
                      {row.startTime.slice(0, 5)}
                    </span>
                    <div className="font-mono text-[10px] text-slate-300">{row.endTime.slice(0, 5)}</div>
                  </td>

                  {/* Gün hücreleri */}
                  {WEEKDAYS.map(wd => {
                    const cellKey = `${wd.day}-${row.startTime}`;
                    const cellBlocks = blockMap.get(cellKey) ?? [];

                    const isClickableEmpty = cellBlocks.length === 0 && !isEvening && onEmptyCellClick != null;

                    return (
                      <CalendarCell key={cellKey} isEvening={isEvening}>
                        <div
                          className={cn(
                            'flex flex-col gap-1',
                            cellBlocks.length > 0 && 'min-h-[3.5rem]',
                            isClickableEmpty && 'cursor-pointer hover:bg-blue-50/50 rounded transition-colors group min-h-[3.5rem] items-center justify-center',
                          )}
                          onClick={isClickableEmpty ? () => onEmptyCellClick(wd.day, row.slotOrder, row.startTime, row.endTime) : undefined}
                        >
                          {cellBlocks.map(b => {
                            const startKey = `${b.sectionId}-${b.day}-${row.startTime}`;
                            const isContinuation = blockStartCells.covered.has(startKey);
                            const startInfo = blockStartCells.starts.get(startKey);

                            return (
                              <SectionBlock
                                key={b.sectionId}
                                section={b.section}
                                rowSpan={startInfo?.rowSpan ?? 1}
                                isContinuation={isContinuation}
                              />
                            );
                          })}
                          {isClickableEmpty && (
                            <Plus className="h-4 w-4 text-blue-400 opacity-0 group-hover:opacity-100 transition-opacity" />
                          )}
                        </div>
                      </CalendarCell>
                    );
                  })}
                </tr>
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
