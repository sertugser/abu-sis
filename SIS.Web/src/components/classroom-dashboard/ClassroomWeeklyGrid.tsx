import { useMemo } from 'react';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import type { ClassroomScheduleSlot, TimeSlotSummary } from '@/types/scheduling';

const DAY_LABELS = ['Pzt', 'Sal', 'Çar', 'Per', 'Cum'];

interface ClassroomWeeklyGridProps {
  schedules: ClassroomScheduleSlot[];
  /** Dönemin gerçek slotları (akşam dahil). Boşsa saat satırı çizilmez. */
  slots: TimeSlotSummary[];
  /** Vurgulanacak (gün, slotOrder) çiftleri — Boş Ara'da aranan pencere. */
  highlightSlots?: { day: number; slotOrder: number }[];
}

/** İki "HH:mm" aralığı kesişiyor mu (string karşılaştırması kronolojik). */
function overlaps(aStart: string, aEnd: string, bStart: string, bEnd: string): boolean {
  return aStart < bEnd && bStart < aEnd;
}

export default function ClassroomWeeklyGrid({ schedules, slots, highlightSlots }: ClassroomWeeklyGridProps) {
  // (day, slotOrder) → o slotla çakışan ders
  const slotMap = useMemo(() => {
    const map = new Map<string, ClassroomScheduleSlot>();
    for (const slot of slots) {
      for (const s of schedules) {
        if (overlaps(s.startTime, s.endTime, slot.startTime, slot.endTime)) {
          map.set(`${s.day}-${slot.slotOrder}`, s);
        }
      }
    }
    return map;
  }, [schedules, slots]);

  const highlightSet = useMemo(() => {
    const set = new Set<string>();
    for (const hs of highlightSlots ?? []) {
      set.add(`${hs.day}-${hs.slotOrder}`);
    }
    return set;
  }, [highlightSlots]);

  if (slots.length === 0) {
    return <p className="text-xs text-muted-foreground py-2">Bu dönem için tanımlı ders saati bulunamadı.</p>;
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full border-collapse text-xs">
        <thead>
          <tr>
            <th className="w-20 text-muted-foreground font-medium text-left py-1">Saat</th>
            {DAY_LABELS.map(d => (
              <th key={d} className="text-muted-foreground font-medium text-center py-1">{d}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {slots.map(slot => (
            <tr key={slot.slotOrder}>
              <td className="text-muted-foreground font-mono py-0.5 pr-2 whitespace-nowrap">
                {slot.startTime}
              </td>
              {DAY_LABELS.map((_, dayIdx) => {
                const day = dayIdx + 1;
                const key = `${day}-${slot.slotOrder}`;
                const sched = slotMap.get(key);
                const isHighlighted = highlightSet.has(key);

                return (
                  <td key={dayIdx} className="p-0.5">
                    <TooltipProvider delayDuration={100}>
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <div className={cn(
                            'h-8 rounded-md border flex items-center justify-center font-medium transition-colors',
                            sched
                              ? 'bg-blue-100 border-blue-200 text-blue-800 text-[10px]'
                              : isHighlighted
                                ? 'bg-emerald-100 border-emerald-300 text-emerald-700 text-[10px]'
                                : 'bg-gray-50 border-gray-100 text-gray-300',
                          )}>
                            {sched ? sched.courseCode : isHighlighted ? 'Boş' : ''}
                          </div>
                        </TooltipTrigger>
                        <TooltipContent side="top" className="text-xs">
                          {sched ? (
                            <>
                              <p className="font-semibold">{sched.courseCode} — {sched.courseName}</p>
                              <p className="text-muted-foreground">{sched.instructorName || 'Atanmamış'}</p>
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
