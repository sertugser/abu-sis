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

interface OccupancyHeatmapTabProps {
  dashboard: ClassroomDashboard | undefined;
  conflictsCount: number;
  isLoading: boolean;
}

export default function OccupancyHeatmapTab({ dashboard, conflictsCount, isLoading }: OccupancyHeatmapTabProps) {
  const [selectedCell, setSelectedCell] = useState<{ day: number; hour: number } | null>(null);

  const assignedCount = dashboard ? dashboard.classrooms.filter(c => c.schedules.length > 0).length : 0;

  const hours = useMemo(() => {
    if (!dashboard?.heatmap?.length) return [8, 9, 10, 11, 12, 13, 14, 15, 16, 17];
    const unique = [...new Set(dashboard.heatmap.map(h => h.hour))].filter(h => h >= 6 && h <= 22).sort((a, b) => a - b);
    return unique.length ? unique : [8, 9, 10, 11, 12, 13, 14, 15, 16, 17];
  }, [dashboard]);

  const cellMap = useMemo(() => {
    const map = new Map<string, ClassroomHeatmapCell>();
    for (const cell of dashboard?.heatmap ?? []) {
      map.set(`${cell.day}-${cell.hour}`, cell);
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

  const selectedHeatmapCell = selectedCell ? cellMap.get(`${selectedCell.day}-${selectedCell.hour}`) : null;

  return (
    <div className="space-y-4">
      <ClassroomSummaryCards
        totalClassrooms={dashboard?.totalClassrooms ?? 0}
        occupancyRate={dashboard?.occupancyRate ?? 0}
        conflictsCount={conflictsCount}
        assignedCount={assignedCount}
      />

      <div className="bg-white border rounded-xl p-5">
        <h3 className="text-sm font-semibold mb-4">Doluluk Haritası</h3>

        <div className="overflow-x-auto">
          <table className="w-full border-collapse">
            <thead>
              <tr>
                <th className="w-14 text-xs text-muted-foreground font-medium text-left py-1">Saat</th>
                {DAY_LABELS.map(d => (
                  <th key={d} className="text-xs text-muted-foreground font-medium text-center py-1">{d}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {hours.map(hour => (
                <tr key={hour}>
                  <td className="text-xs text-muted-foreground font-mono py-0.5 pr-2">
                    {String(hour).padStart(2, '0')}:00
                  </td>
                  {DAY_LABELS.map((_, dayIdx) => {
                    const cell = cellMap.get(`${dayIdx}-${hour}`);
                    const occ = cell?.occupancy ?? 0;
                    const style = getOccupancyStyle(occ);
                    const isSelected = selectedCell?.day === dayIdx && selectedCell?.hour === hour;

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
                                onClick={() => setSelectedCell(isSelected ? null : { day: dayIdx, hour })}
                              >
                                {(cell?.usedClassrooms ?? 0) > 0 ? cell!.usedClassrooms : ''}
                              </div>
                            </TooltipTrigger>
                            <TooltipContent side="top" className="text-xs max-w-xs">
                              <p className="font-semibold">{DAY_LABELS[dayIdx]} {String(hour).padStart(2, '0')}:00</p>
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
                  {DAY_FULL[selectedCell.day] ?? DAY_LABELS[selectedCell.day]} — {String(selectedCell.hour).padStart(2, '0')}:00
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
              <p className="text-sm text-muted-foreground text-center py-6">Bu saatte ders yok.</p>
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
