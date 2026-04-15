import { useState, useMemo } from 'react';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { Skeleton } from '@/components/ui/skeleton';
import { X } from 'lucide-react';
import { cn } from '@/lib/utils';
import ClassroomSummaryCards from './ClassroomSummaryCards';
import type { ClassroomDashboard, ClassroomHeatmapCell, ClassroomOccupation } from '@/types/scheduling';

const DAY_LABELS = ['Pzt', 'Sal', 'Çar', 'Per', 'Cum'];
const DAY_FULL: Record<number, string> = {
  0: 'Pazartesi', 1: 'Salı', 2: 'Çarşamba', 3: 'Perşembe', 4: 'Cuma',
};

const OCCUPANCY_COLORS = [
  { max: 0, bg: 'bg-gray-50', border: 'border-gray-100', label: '%0' },
  { max: 0.25, bg: 'bg-emerald-50', border: 'border-emerald-100', label: '%1-25' },
  { max: 0.50, bg: 'bg-emerald-200', border: 'border-emerald-300', label: '%25-50' },
  { max: 0.75, bg: 'bg-amber-200', border: 'border-amber-300', label: '%50-75' },
  { max: 1.01, bg: 'bg-red-300', border: 'border-red-400', label: '%75+' },
];

function getOccupancyStyle(occ: number) {
  for (const c of OCCUPANCY_COLORS) {
    if (occ <= c.max) return c;
  }
  return OCCUPANCY_COLORS[OCCUPANCY_COLORS.length - 1];
}

interface SlotDef {
  slotOrder: number;
  startTime: string;
  endTime: string;
  label: string;
}

interface OccupancyHeatmapTabProps {
  dashboard: ClassroomDashboard | undefined;
  isLoading: boolean;
}

export default function OccupancyHeatmapTab({ dashboard, isLoading }: OccupancyHeatmapTabProps) {
  const [selectedCell, setSelectedCell] = useState<{ day: number; slotOrder: number } | null>(null);

  const assignedCount = dashboard ? dashboard.classrooms.filter(c => c.schedules.length > 0).length : 0;

  const slots = useMemo<SlotDef[]>(() => {
    if (!dashboard?.heatmap?.length) return [];
    const slotMap = new Map<number, SlotDef>();
    for (const cell of dashboard.heatmap) {
      if (!slotMap.has(cell.slotOrder)) {
        slotMap.set(cell.slotOrder, {
          slotOrder: cell.slotOrder,
          startTime: cell.startTime,
          endTime: cell.endTime,
          label: cell.label,
        });
      }
    }
    return [...slotMap.values()].sort((a, b) => a.slotOrder - b.slotOrder);
  }, [dashboard]);

  const peakSlot = useMemo(() => {
    if (!dashboard?.heatmap?.length) return null;
    let best = dashboard.heatmap[0];
    for (const cell of dashboard.heatmap) {
      if (cell.occupancy > best.occupancy) best = cell;
    }
    return best.occupancy > 0
      ? { day: best.day, hour: best.hour, startTime: best.startTime, occupancy: best.occupancy }
      : null;
  }, [dashboard]);

  const cellMap = useMemo(() => {
    const map = new Map<string, ClassroomHeatmapCell>();
    for (const cell of dashboard?.heatmap ?? []) {
      map.set(`${cell.day}-${cell.slotOrder}`, cell);
    }
    return map;
  }, [dashboard]);

  if (isLoading) {
    return (
      <div className="space-y-4">
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
          {[1, 2, 3, 4].map(i => <Skeleton key={i} className="h-20 rounded-xl" />)}
        </div>
        <Skeleton className="h-80 rounded-xl" />
      </div>
    );
  }

  const selectedHeatmapCell = selectedCell ? cellMap.get(`${selectedCell.day}-${selectedCell.slotOrder}`) : null;
  const selectedSlotDef = selectedCell ? slots.find(s => s.slotOrder === selectedCell.slotOrder) : null;

  return (
    <div className="space-y-4">
      <div className="bg-white border rounded-xl p-5">
        <div className="flex items-center justify-between gap-4 mb-4 flex-wrap">
          <h3 className="text-sm font-semibold">Doluluk Haritası</h3>
          <ClassroomSummaryCards
            totalClassrooms={dashboard?.totalClassrooms ?? 0}
            occupancyRate={dashboard?.occupancyRate ?? 0}
            assignedCount={assignedCount}
            emptySlots={dashboard?.emptySlots ?? 0}
            peakSlot={peakSlot}
          />
        </div>

        <div className="overflow-x-auto">
          <table className="w-full border-collapse">
            <thead>
              <tr>
                <th className="w-24 text-xs text-muted-foreground font-medium text-left py-1">Slot</th>
                {DAY_LABELS.map(d => (
                  <th key={d} className="text-xs text-muted-foreground font-medium text-center py-1">{d}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {slots.map(slot => (
                <tr key={slot.slotOrder}>
                  <td className="text-[11px] text-muted-foreground font-mono py-0.5 pr-2 whitespace-nowrap">
                    {slot.label}
                  </td>
                  {DAY_LABELS.map((_, dayIdx) => {
                    const cell = cellMap.get(`${dayIdx}-${slot.slotOrder}`);
                    const occ = cell?.occupancy ?? 0;
                    const style = getOccupancyStyle(occ);
                    const isSelected = selectedCell?.day === dayIdx && selectedCell?.slotOrder === slot.slotOrder;

                    return (
                      <td key={dayIdx} className="p-0.5">
                        <TooltipProvider delayDuration={100}>
                          <Tooltip>
                            <TooltipTrigger asChild>
                              <div
                                className={cn(
                                  'h-10 rounded-md border flex items-center justify-center text-[10px] font-semibold transition-colors cursor-pointer',
                                  style.bg, style.border,
                                  isSelected && 'ring-2 ring-[#003366] ring-offset-1',
                                )}
                                onClick={() => setSelectedCell(isSelected ? null : { day: dayIdx, slotOrder: slot.slotOrder })}
                              >
                                {(cell?.usedClassrooms ?? 0) > 0 ? cell!.usedClassrooms : ''}
                              </div>
                            </TooltipTrigger>
                            <TooltipContent side="top" className="text-xs max-w-xs">
                              <p className="font-semibold">{DAY_LABELS[dayIdx]} {slot.label}</p>
                              <p>{cell?.usedClassrooms ?? 0}/{dashboard?.totalClassrooms ?? 0} derslik dolu (%{Math.round(occ * 100)})</p>
                              {cell?.occupations?.slice(0, 5).map((o, i) => (
                                <p key={i} className="text-muted-foreground">{o.classroomName}: {o.courseCode} ({o.enrolledCount}/{o.capacity})</p>
                              ))}
                              {(cell?.occupations?.length ?? 0) > 5 && (
                                <p className="text-muted-foreground">+{(cell!.occupations.length) - 5} daha...</p>
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

        {/* Cell detail panel */}
        {selectedCell && (
          <div className="mt-4 border rounded-lg overflow-hidden">
            <div className="flex items-center justify-between px-4 py-2.5 bg-[#003366]/5 border-b">
              <div className="flex items-center gap-2">
                <span className="text-sm font-semibold text-[#003366]">
                  {DAY_FULL[selectedCell.day] ?? DAY_LABELS[selectedCell.day]} — {selectedSlotDef?.label ?? ''}
                </span>
                <span className="text-xs text-muted-foreground">
                  {selectedHeatmapCell?.occupations?.length ?? 0} ders
                </span>
              </div>
              <button onClick={() => setSelectedCell(null)} className="h-6 w-6 flex items-center justify-center rounded-md hover:bg-gray-200 transition-colors">
                <X className="h-3.5 w-3.5 text-muted-foreground" />
              </button>
            </div>
            {!selectedHeatmapCell?.occupations?.length ? (
              <p className="text-sm text-muted-foreground text-center py-6">Bu slotta ders yok.</p>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="text-xs">Derslik</TableHead>
                    <TableHead className="text-xs">Ders</TableHead>
                    <TableHead className="text-xs text-right">Doluluk</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {selectedHeatmapCell.occupations.map((o: ClassroomOccupation, i: number) => (
                    <TableRow key={i}>
                      <TableCell className="text-xs font-medium">{o.classroomName}</TableCell>
                      <TableCell className="text-xs">
                        <span className="font-mono font-semibold">{o.courseCode}</span>
                        <span className="ml-1.5 text-muted-foreground">{o.courseName}</span>
                      </TableCell>
                      <TableCell className="text-xs text-right font-mono">{o.enrolledCount}/{o.capacity}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </div>
        )}

        {/* Legend */}
        <div className="flex items-center gap-3 mt-4 pt-3 border-t">
          <span className="text-[11px] text-muted-foreground font-medium">Doluluk:</span>
          {OCCUPANCY_COLORS.map(c => (
            <div key={c.label} className="flex items-center gap-1">
              <div className={cn('w-4 h-3 rounded-sm border', c.bg, c.border)} />
              <span className="text-[10px] text-muted-foreground">{c.label}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
