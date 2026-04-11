import { useMemo } from 'react';
import { useDroppable } from '@dnd-kit/core';
import { cn } from '@/lib/utils';
import type { FacultyPlanningSection, FacultyPlanningTimeSlot } from '../../types/facultyPlanning';

const WEEKDAYS = [
  { day: 1, label: 'Pazartesi' },
  { day: 2, label: 'Sali' },
  { day: 3, label: 'Carsamba' },
  { day: 4, label: 'Persembe' },
  { day: 5, label: 'Cuma' },
];

const DAY_ABBR: Record<string, number> = {
  'Pzt': 1, 'Sal': 2, 'Çar': 3, 'Per': 4, 'Cum': 5,
  'Mon': 1, 'Tue': 2, 'Wed': 3, 'Thu': 4, 'Fri': 5,
};

interface ParsedBlock {
  sectionId: number;
  section: FacultyPlanningSection;
  day: number;
  startTime: string;
  endTime: string;
}

function parseScheduleSummary(section: FacultyPlanningSection): ParsedBlock[] {
  if (!section.scheduleSummary) return [];
  const blocks: ParsedBlock[] = [];
  // Format: "Pzt 09:00-10:50, Çar 09:00-10:50"
  const parts = section.scheduleSummary.split(',').map(s => s.trim());
  for (const part of parts) {
    const match = part.match(/^(\S+)\s+(\d{2}:\d{2})-(\d{2}:\d{2})$/);
    if (match) {
      const dayNum = DAY_ABBR[match[1]];
      if (dayNum) {
        blocks.push({
          sectionId: section.sectionId,
          section,
          day: dayNum,
          startTime: match[2],
          endTime: match[3],
        });
      }
    }
  }
  return blocks;
}

function timeToMinutes(t: string): number {
  const [h, m] = t.split(':').map(Number);
  return h * 60 + m;
}

const STATUS_COLORS: Record<string, string> = {
  Scheduled: 'bg-blue-100 border-blue-300 text-blue-900 hover:bg-blue-200',
  Conflicted: 'bg-red-100 border-red-300 text-red-900 hover:bg-red-200',
  Partial: 'bg-amber-100 border-amber-300 text-amber-900 hover:bg-amber-200',
};

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
        'border p-0.5 align-top relative',
        isEmpty && isDraggingSection && 'bg-blue-50 border-blue-200',
        isEmpty && isOver && 'bg-blue-100 border-blue-400 border-2',
      )}
      onClick={onClick}
      style={{ cursor: isEmpty ? 'pointer' : 'default' }}
    >
      {children}
    </td>
  );
}

interface CalendarGridProps {
  sections: FacultyPlanningSection[];
  timeSlots: FacultyPlanningTimeSlot[];
  selectedSectionId: number | null;
  onSelectSection: (section: FacultyPlanningSection) => void;
  onCellClick: (day: number, startTime: string) => void;
  isDraggingSection?: boolean;
}

export function CalendarGrid({ sections, timeSlots, selectedSectionId, onSelectSection, onCellClick, isDraggingSection = false }: CalendarGridProps) {
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
      .flatMap(s => parseScheduleSummary(s));
  }, [sections]);

  // Build a lookup: day -> startTime -> blocks
  const blockMap = useMemo(() => {
    const map = new Map<string, ParsedBlock[]>();
    for (const block of allBlocks) {
      // Find which rows this block spans
      for (const row of timeRows) {
        const rowStart = timeToMinutes(row.startTime.slice(0, 5));
        const rowEnd = timeToMinutes(row.endTime.slice(0, 5));
        const blockStart = timeToMinutes(block.startTime);
        const blockEnd = timeToMinutes(block.endTime);
        // Block overlaps this row if block starts before row ends and block ends after row starts
        if (blockStart < rowEnd && blockEnd > rowStart) {
          const key = `${block.day}-${row.startTime}`;
          const arr = map.get(key) ?? [];
          // avoid duplicates
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
          // Mark non-first rows as covered
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
      <div className="flex-1 border rounded-lg flex items-center justify-center">
        <p className="text-sm text-muted-foreground">Zaman dilimi bilgisi bulunamadi</p>
      </div>
    );
  }

  return (
    <div className="flex-1 border rounded-lg overflow-auto">
      <table className="w-full border-collapse text-xs">
        <thead>
          <tr className="bg-muted/50">
            <th className="border p-1.5 w-16 text-center font-medium">Saat</th>
            {WEEKDAYS.map(wd => (
              <th key={wd.day} className="border p-1.5 text-center font-medium min-w-[120px]">
                {wd.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {timeRows.map((row) => (
            <tr key={row.startTime} className="h-12">
              <td className="border p-1 text-center text-muted-foreground font-mono whitespace-nowrap">
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

                        // If this is a continuation cell (not the first row), render a thinner indicator
                        if (isCovered) {
                          return (
                            <div
                              key={b.sectionId}
                              className={cn(
                                'rounded px-1 py-0.5 border cursor-pointer text-[10px]',
                                STATUS_COLORS[b.section.status] ?? 'bg-gray-100 border-gray-300',
                                selectedSectionId === b.sectionId && 'ring-2 ring-primary',
                              )}
                              onClick={(e) => { e.stopPropagation(); onSelectSection(b.section); }}
                            >
                              <span className="font-mono font-bold opacity-50">{b.section.courseCode}</span>
                            </div>
                          );
                        }

                        // First row of the block
                        return (
                          <div
                            key={b.sectionId}
                            className={cn(
                              'rounded px-1.5 py-1 border cursor-pointer',
                              STATUS_COLORS[b.section.status] ?? 'bg-gray-100 border-gray-300',
                              selectedSectionId === b.sectionId && 'ring-2 ring-primary',
                            )}
                            onClick={(e) => { e.stopPropagation(); onSelectSection(b.section); }}
                          >
                            <div className="font-mono font-bold text-[11px] leading-tight">{b.section.courseCode}</div>
                            {startInfo && startInfo.rowSpan >= 2 && (
                              <div className="text-[10px] leading-tight truncate opacity-75">
                                {b.section.classroomName ?? ''}
                              </div>
                            )}
                          </div>
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
