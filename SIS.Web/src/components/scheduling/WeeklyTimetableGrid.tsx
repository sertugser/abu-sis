import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import {
  dayToColumnIndex,
  getGridHeight,
  getTimeLabels,
  slotHeight,
  timeToGridRow,
  WEEKDAY_LABELS,
} from '@/lib/scheduleUtils';
import type { ScheduleSlot } from '../../types/scheduling';

export interface TimetableEntry {
  id: string;
  label: string;
  courseCode: string;
  classroomName?: string;
  instructorName?: string;
  slots: ScheduleSlot[];
  hasConflict?: boolean;
}

interface WeeklyTimetableGridProps {
  entries: TimetableEntry[];
  title?: string;
}

const ENTRY_COLORS = [
  'bg-blue-100 border-blue-300 text-blue-900',
  'bg-emerald-100 border-emerald-300 text-emerald-900',
  'bg-violet-100 border-violet-300 text-violet-900',
  'bg-amber-100 border-amber-300 text-amber-900',
  'bg-rose-100 border-rose-300 text-rose-900',
  'bg-cyan-100 border-cyan-300 text-cyan-900',
  'bg-orange-100 border-orange-300 text-orange-900',
  'bg-teal-100 border-teal-300 text-teal-900',
  'bg-pink-100 border-pink-300 text-pink-900',
  'bg-indigo-100 border-indigo-300 text-indigo-900',
];

export default function WeeklyTimetableGrid({
  entries,
  title = 'Haftalık Görünüm',
}: WeeklyTimetableGridProps) {
  const [hoveredEntry, setHoveredEntry] = useState<{ entryId: string; slotIdx: number } | null>(null);

  const timeLabels = getTimeLabels(30);
  const gridHeight = getGridHeight();

  // Build color map per entry
  const colorMap = new Map<string, string>();
  entries.forEach((entry, idx) => {
    colorMap.set(entry.id, ENTRY_COLORS[idx % ENTRY_COLORS.length]);
  });

  return (
    <Card>
      <CardHeader className="flex items-center">
        <CardTitle className="text-lg">{title}</CardTitle>
      </CardHeader>
      <CardContent className="overflow-x-auto">
        {entries.length === 0 && (
          <div className="mb-4 rounded-xl border border-dashed border-border bg-muted/10 px-4 py-6 text-sm text-muted-foreground">
            Görselleştirilecek bir program kaydı bulunmuyor.
          </div>
        )}

        <div className="min-w-[700px]">
          {/* Header row */}
          <div className="grid grid-cols-[60px_repeat(5,1fr)] border-b border-border">
            <div className="p-2 text-xs font-medium text-muted-foreground text-center">Saat</div>
            {WEEKDAY_LABELS.map(day => (
              <div
                key={day}
                className="p-2 text-sm font-semibold text-foreground text-center border-l border-border"
              >
                {day}
              </div>
            ))}
          </div>

          {/* Grid body */}
          <div className="grid grid-cols-[60px_repeat(5,1fr)] relative">
            {/* Time labels column */}
            <div className="relative" style={{ height: gridHeight }}>
              {timeLabels.map((label, idx) => (
                <div
                  key={label}
                  className={cn(
                    'absolute left-0 right-0 pr-2 text-right text-[11px] text-muted-foreground',
                    label.endsWith(':00') ? 'font-medium' : 'opacity-70',
                  )}
                  style={{ top: idx * 60 - 7 }}
                >
                  {label}
                </div>
              ))}
            </div>

            {/* Day columns */}
            {Array.from({ length: 5 }).map((_, colIdx) => (
              <div
                key={colIdx}
                className="relative border-l border-border"
                style={{ height: gridHeight }}
              >
                {timeLabels.map((label, lineIdx) => (
                  <div
                    key={`${label}-${lineIdx}`}
                    className={cn(
                      'absolute left-0 right-0 border-t',
                      label.endsWith(':00') ? 'border-border/40' : 'border-border/20 border-dashed',
                    )}
                    style={{ top: lineIdx * 60 }}
                  />
                ))}

                {/* Schedule entries */}
                {entries.map((entry) =>
                  entry.slots
                    .filter(slot => dayToColumnIndex(slot.day) === colIdx)
                    .map((slot, slotIdx) => {
                      const top = timeToGridRow(slot.startTime);
                      const height = slotHeight(slot.startTime, slot.endTime);
                      const colors = colorMap.get(entry.id) ?? ENTRY_COLORS[0];
                      const isHovered =
                        hoveredEntry?.entryId === entry.id && hoveredEntry?.slotIdx === slotIdx;

                      return (
                        <div
                          key={`${entry.id}-${slot.day}-${slot.startTime}-${slotIdx}`}
                          className={cn(
                            'absolute left-1 right-1 rounded-md border px-1.5 py-1 text-[11px] leading-tight overflow-hidden cursor-default transition-shadow',
                            colors,
                            entry.hasConflict && 'ring-2 ring-red-500 border-red-500',
                            isHovered && 'shadow-lg z-20',
                          )}
                          style={{ top, height, zIndex: isHovered ? 20 : 10 }}
                          onMouseEnter={() => setHoveredEntry({ entryId: entry.id, slotIdx })}
                          onMouseLeave={() => setHoveredEntry(null)}
                        >
                          <div className="font-semibold truncate">{entry.courseCode}</div>
                          {height > 50 && (
                            <div className="truncate opacity-80">{entry.label}</div>
                          )}
                          {height > 70 && entry.classroomName && (
                            <div className="truncate opacity-70">{entry.classroomName}</div>
                          )}

                          {/* Tooltip on hover */}
                          {isHovered && (
                            <div className="absolute left-0 right-0 bottom-full mb-1 z-30 pointer-events-none">
                              <div className="bg-popover text-popover-foreground border border-border rounded-lg shadow-lg p-3 text-xs space-y-1 min-w-[180px]">
                                <div className="font-bold text-sm">{entry.courseCode}</div>
                                <div>{entry.label}</div>
                                {entry.classroomName && (
                                  <div className="flex items-center gap-1">
                                    <span className="text-muted-foreground">Derslik:</span>
                                    <span>{entry.classroomName}</span>
                                  </div>
                                )}
                                {entry.instructorName && (
                                  <div className="flex items-center gap-1">
                                    <span className="text-muted-foreground">Ogretim Uyesi:</span>
                                    <span>{entry.instructorName}</span>
                                  </div>
                                )}
                                <div className="flex items-center gap-1">
                                  <span className="text-muted-foreground">Saat:</span>
                                  <span>
                                    {slot.startTime.slice(0, 5)}-{slot.endTime.slice(0, 5)}
                                  </span>
                                </div>
                                {entry.hasConflict && (
                                  <Badge variant="destructive" className="text-[10px]">
                                    Cakisma var
                                  </Badge>
                                )}
                              </div>
                            </div>
                          )}
                        </div>
                      );
                    }),
                )}
              </div>
            ))}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
