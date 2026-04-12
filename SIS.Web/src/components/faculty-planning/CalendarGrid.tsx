import { useMemo } from 'react';
import { useDroppable } from '@dnd-kit/core';
import { cn } from '@/lib/utils';
import { parseScheduleSummary } from '@/lib/scheduleUtils';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { X } from 'lucide-react';
import type { FacultyPlanningSection, FacultyPlanningTimeSlot } from '../../types/facultyPlanning';
import { STATUS_CONFIG } from './sectionUtils';

const WEEKDAYS = [
  { day: 1, label: 'Pazartesi' },
  { day: 2, label: 'Salı' },
  { day: 3, label: 'Çarşamba' },
  { day: 4, label: 'Perşembe' },
  { day: 5, label: 'Cuma' },
];

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

function getCalendarColors(status: string): string {
  return STATUS_CONFIG[status]?.calendarColors ?? 'bg-gray-100 border-gray-300';
}

interface DroppableCellProps {
  day: number;
  startTime: string;
  endTime: string;
  isEmpty: boolean;
  isDraggingSection: boolean;
  children: React.ReactNode;
  onClick: () => void;
}

function DroppableCell({ day, startTime, endTime, isEmpty, isDraggingSection, children, onClick }: DroppableCellProps) {
  const { setNodeRef, isOver } = useDroppable({
    id: `cell-${day}-${startTime}`,
    data: { day, startTime, endTime },
    disabled: !isEmpty,
  });

  return (
    <td
      ref={setNodeRef}
      className={cn(
        'border border-slate-200 p-0.5 align-top relative',
        isEmpty && isDraggingSection && 'bg-blue-50/50 border-blue-200',
        isEmpty && isOver && 'bg-blue-100 border-blue-400 border-2',
      )}
      onClick={onClick}
      style={{ cursor: isEmpty ? 'pointer' : 'default' }}
    >
      {children}
    </td>
  );
}

function SectionPopoverContent({
  section,
  onUnassign,
  isUnassigning,
}: {
  section: FacultyPlanningSection;
  onUnassign: (sectionId: number) => void;
  isUnassigning: boolean;
}) {
  const statusCfg = STATUS_CONFIG[section.status];
  return (
    <div className="space-y-3">
      <div>
        <div className="flex items-center gap-2 mb-1">
          <span className="font-mono font-bold text-sm" style={{ color: '#003366' }}>
            {section.courseCode}
          </span>
          <Badge variant={statusCfg?.variant ?? 'outline'} className="text-[10px]">
            {statusCfg?.label ?? section.status}
          </Badge>
        </div>
        <p className="text-sm text-slate-700">{section.courseName}</p>
        <p className="text-xs text-slate-500">Şube {section.sectionIndex}</p>
      </div>

      <div className="space-y-1.5 text-xs">
        <div className="flex justify-between">
          <span className="text-slate-500">Bölüm</span>
          <span className="text-slate-700">{section.departmentCode}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-slate-500">Yüklenme</span>
          <Badge variant="outline" className="text-[10px] px-1.5 py-0 border-slate-300">
            {section.theoryHours}T+{section.labHours}L
          </Badge>
        </div>
        <div className="flex justify-between">
          <span className="text-slate-500">Kapasite</span>
          <span className="text-slate-700">{section.capacity}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-slate-500">Derslik</span>
          <span className="text-slate-700">{section.classroomName ?? '—'}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-slate-500">Hoca</span>
          <span className="text-slate-700 text-right max-w-[140px] truncate">
            {section.instructorName ?? '—'}
          </span>
        </div>
        {section.scheduleSummary && (
          <div>
            <span className="text-slate-500">Program</span>
            <p className="mt-0.5 font-mono text-slate-700">{section.scheduleSummary}</p>
          </div>
        )}
      </div>

      {section.status !== 'Unscheduled' && (
        <Button
          variant="destructive"
          size="sm"
          className="w-full"
          disabled={isUnassigning}
          onClick={() => onUnassign(section.sectionId)}
        >
          <X className="h-3 w-3 mr-1" />
          {isUnassigning ? 'Kaldırılıyor...' : 'Atamayı Kaldır'}
        </Button>
      )}
    </div>
  );
}

interface CalendarGridProps {
  sections: FacultyPlanningSection[];
  timeSlots: FacultyPlanningTimeSlot[];
  onCellClick: (day: number, startTime: string) => void;
  onUnassign: (sectionId: number) => void;
  isUnassigning: boolean;
  isDraggingSection?: boolean;
}

export function CalendarGrid({ sections, timeSlots, onCellClick, onUnassign, isUnassigning, isDraggingSection = false }: CalendarGridProps) {
  // Get unique time rows from time slots, grouped by slotOrder
  const timeRows = useMemo(() => {
    const seen = new Map<number, { startTime: string; endTime: string }>();
    for (const slot of timeSlots) {
      if (!seen.has(slot.slotOrder)) {
        seen.set(slot.slotOrder, { startTime: slot.startTime, endTime: slot.endTime });
      }
    }
    return Array.from(seen.entries())
      .sort(([a], [b]) => a - b)
      .map(([, v]) => v);
  }, [timeSlots]);

  // Parse all scheduled sections into blocks
  const allBlocks = useMemo(() => {
    return sections
      .filter(s => s.status !== 'Unscheduled')
      .flatMap(s => parseSectionSchedule(s));
  }, [sections]);

  // Build a lookup: day -> startTime -> blocks
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

  // Track which cells are the "start" of a block so we can render block only once with rowSpan
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

  if (timeRows.length === 0) {
    return (
      <div className="flex-1 h-full border border-slate-200 rounded-lg flex items-center justify-center">
        <p className="text-sm text-slate-500">Zaman dilimi bilgisi bulunamadı</p>
      </div>
    );
  }

  return (
    <div className="flex-1 h-full border border-slate-200 rounded-lg overflow-auto">
      <table className="w-full border-collapse text-xs">
        <thead>
          <tr className="bg-slate-50">
            <th className="border border-slate-200 p-1.5 w-16 text-center font-medium text-slate-600">Saat</th>
            {WEEKDAYS.map(wd => (
              <th key={wd.day} className="border border-slate-200 p-1.5 text-center font-medium text-slate-600 min-w-[120px]">
                {wd.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {timeRows.map((row) => (
            <tr key={row.startTime} className="h-12">
              <td className="border border-slate-200 p-1 text-center text-slate-500 font-mono whitespace-nowrap">
                {row.startTime.slice(0, 5)}
              </td>
              {WEEKDAYS.map(wd => {
                const cellKey = `${wd.day}-${row.startTime}`;
                const cellBlocks = blockMap.get(cellKey) ?? [];
                const isEmpty = cellBlocks.length === 0;

                return (
                  <DroppableCell
                    key={cellKey}
                    day={wd.day}
                    startTime={row.startTime}
                    endTime={row.endTime}
                    isEmpty={isEmpty}
                    isDraggingSection={isDraggingSection}
                    onClick={() => {
                      if (isEmpty) {
                        onCellClick(wd.day, row.startTime);
                      }
                    }}
                  >
                    <div className="flex flex-col gap-0.5 min-h-[2.5rem]">
                      {cellBlocks.map(b => {
                        const startKey = `${b.sectionId}-${b.day}-${row.startTime}`;
                        const isCovered = blockStartCells.covered.has(startKey);
                        const startInfo = blockStartCells.starts.get(startKey);

                        // Continuation cell (not the first row)
                        if (isCovered) {
                          return (
                            <Popover key={b.sectionId}>
                              <PopoverTrigger asChild>
                                <div
                                  className={cn(
                                    'rounded px-1 py-0.5 border cursor-pointer text-[10px]',
                                    getCalendarColors(b.section.status),
                                  )}
                                  onClick={(e) => e.stopPropagation()}
                                >
                                  <span className="font-mono font-bold opacity-50">{b.section.courseCode}</span>
                                </div>
                              </PopoverTrigger>
                              <PopoverContent className="w-72" side="right" align="start">
                                <SectionPopoverContent
                                  section={b.section}
                                  onUnassign={onUnassign}
                                  isUnassigning={isUnassigning}
                                />
                              </PopoverContent>
                            </Popover>
                          );
                        }

                        // First row of the block
                        return (
                          <Popover key={b.sectionId}>
                            <PopoverTrigger asChild>
                              <div
                                className={cn(
                                  'rounded px-1.5 py-1 border cursor-pointer',
                                  getCalendarColors(b.section.status),
                                )}
                                onClick={(e) => e.stopPropagation()}
                              >
                                <div className="font-mono font-bold text-[11px] leading-tight">{b.section.courseCode}</div>
                                {startInfo && startInfo.rowSpan >= 2 && (
                                  <div className="text-[10px] leading-tight truncate opacity-75">
                                    {b.section.classroomName ?? ''}
                                  </div>
                                )}
                              </div>
                            </PopoverTrigger>
                            <PopoverContent className="w-72" side="right" align="start">
                              <SectionPopoverContent
                                section={b.section}
                                onUnassign={onUnassign}
                                isUnassigning={isUnassigning}
                              />
                            </PopoverContent>
                          </Popover>
                        );
                      })}
                    </div>
                  </DroppableCell>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
