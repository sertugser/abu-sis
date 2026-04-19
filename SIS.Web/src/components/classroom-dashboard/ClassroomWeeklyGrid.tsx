import { useMemo } from 'react';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import type { ClassroomScheduleSlot } from '@/types/scheduling';

const DAY_LABELS = ['Pzt', 'Sal', 'Çar', 'Per', 'Cum'];
const HOURS = [8, 9, 10, 11, 12, 13, 14, 15, 16, 17];

interface ClassroomWeeklyGridProps {
  schedules: ClassroomScheduleSlot[];
  highlightSlots?: { day: number; startHour: number; endHour: number }[];
}

export default function ClassroomWeeklyGrid({ schedules, highlightSlots }: ClassroomWeeklyGridProps) {
  const slotMap = useMemo(() => {
    const map = new Map<string, ClassroomScheduleSlot>();
    for (const s of schedules) {
      for (let h = s.startHour; h <= s.endHour; h++) {
        map.set(`${s.day}-${h}`, s);
      }
    }
    return map;
  }, [schedules]);

  const highlightSet = useMemo(() => {
    if (!highlightSlots) return new Set<string>();
    const set = new Set<string>();
    for (const hs of highlightSlots) {
      for (let h = hs.startHour; h <= hs.endHour; h++) {
        set.add(`${hs.day}-${h}`);
      }
    }
    return set;
  }, [highlightSlots]);

  return (
    <div className="overflow-x-auto">
      <table className="w-full border-collapse text-xs">
        <thead>
          <tr>
            <th className="w-14 text-muted-foreground font-medium text-left py-1">Saat</th>
            {DAY_LABELS.map(d => (
              <th key={d} className="text-muted-foreground font-medium text-center py-1">{d}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {HOURS.map(hour => (
            <tr key={hour}>
              <td className="text-muted-foreground font-mono py-0.5 pr-2">
                {String(hour).padStart(2, '0')}:00
              </td>
              {DAY_LABELS.map((_, dayIdx) => {
                const key = `${dayIdx + 1}-${hour}`;
                const slot = slotMap.get(key);
                const isHighlighted = highlightSet.has(key);

                return (
                  <td key={dayIdx} className="p-0.5">
                    <TooltipProvider delayDuration={100}>
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <div className={cn(
                            'h-8 rounded-md border flex items-center justify-center font-medium transition-colors',
                            slot
                              ? 'bg-blue-100 border-blue-200 text-blue-800 text-[10px]'
                              : isHighlighted
                                ? 'bg-emerald-100 border-emerald-300 text-emerald-700 text-[10px]'
                                : 'bg-gray-50 border-gray-100 text-gray-300',
                          )}>
                            {slot ? slot.courseCode : isHighlighted ? 'Boş' : ''}
                          </div>
                        </TooltipTrigger>
                        <TooltipContent side="top" className="text-xs">
                          {slot ? (
                            <>
                              <p className="font-semibold">{slot.courseCode} — {slot.courseName}</p>
                              <p className="text-muted-foreground">{slot.instructorName || 'Atanmamış'}</p>
                            </>
                          ) : isHighlighted ? (
                            <p className="text-emerald-700">Müsait slot</p>
                          ) : (
                            <p>Boş</p>
                          )}
                        </TooltipContent>
                      </Tooltip>
                    </TooltipProvider>
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
