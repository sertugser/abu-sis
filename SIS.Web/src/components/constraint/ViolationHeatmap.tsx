import { useState, useMemo } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import type { HeatmapDto, HeatmapRowDto } from '@/types/constraintAnalytics';

interface Props {
  heatmap: HeatmapDto | undefined;
  isLoading: boolean;
}

const DAY_LABELS = ['Pzt', 'Sal', 'Car', 'Per', 'Cum', 'Cmt'];
const DAY_MAP: Record<number, number> = { 1: 0, 2: 1, 3: 2, 4: 3, 5: 4, 6: 5 };

const SEVERITY_COLORS: Record<string, string> = {
  none: 'bg-muted/30',
  low: 'bg-yellow-200 dark:bg-yellow-900/50',
  medium: 'bg-orange-300 dark:bg-orange-800/60',
  high: 'bg-red-400 dark:bg-red-700/70',
};

function buildSlotLabels(): string[] {
  const labels: string[] = [];
  for (let h = 8; h <= 20; h++) {
    labels.push(`${String(h).padStart(2, '0')}:00`);
  }
  return labels;
}

export default function ViolationHeatmap({ heatmap, isLoading }: Props) {
  const [selectedRow, setSelectedRow] = useState<string | null>(null);
  const slotLabels = useMemo(() => buildSlotLabels(), []);

  const aggregated = useMemo(() => {
    if (!heatmap) return null;

    const grid: Record<string, number> = {};
    for (const row of heatmap.rows) {
      for (const cell of row.cells) {
        const dayIdx = DAY_MAP[cell.dayOfWeek] ?? -1;
        if (dayIdx < 0) continue;
        const slotIdx = cell.slotIndex - 8;
        if (slotIdx < 0 || slotIdx >= slotLabels.length) continue;
        const key = `${dayIdx}-${slotIdx}`;
        grid[key] = (grid[key] ?? 0) + cell.violationCount;
      }
    }
    return grid;
  }, [heatmap, slotLabels]);

  if (isLoading) {
    return (
      <Card>
        <CardContent className="pt-4">
          <div className="h-48 bg-muted animate-pulse rounded" />
        </CardContent>
      </Card>
    );
  }

  if (!heatmap || heatmap.rows.length === 0) return null;

  const activeRow: HeatmapRowDto | undefined = selectedRow
    ? heatmap.rows.find(r => r.constraintCode === selectedRow)
    : undefined;

  return (
    <Card>
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between">
          <CardTitle className="text-sm font-medium">Ihlal Isi Haritasi</CardTitle>
          <select
            className="h-7 rounded border bg-background px-2 text-xs"
            value={selectedRow ?? '__all__'}
            onChange={e => setSelectedRow(e.target.value === '__all__' ? null : e.target.value)}
          >
            <option value="__all__">Tumu</option>
            {heatmap.rows.map(r => (
              <option key={r.constraintCode} value={r.constraintCode}>
                {r.constraintName}
              </option>
            ))}
          </select>
        </div>
      </CardHeader>
      <CardContent>
        <div className="overflow-x-auto">
          <div
            className="grid gap-[2px]"
            style={{
              gridTemplateColumns: `64px repeat(${DAY_LABELS.length}, 1fr)`,
            }}
          >
            {/* Header row */}
            <div />
            {DAY_LABELS.map(d => (
              <div key={d} className="text-center text-[10px] font-medium text-muted-foreground pb-1">
                {d}
              </div>
            ))}

            {/* Data rows */}
            {slotLabels.map((label, slotIdx) => (
              <>
                <div key={`label-${slotIdx}`} className="text-[10px] text-muted-foreground flex items-center">
                  {label}
                </div>
                {DAY_LABELS.map((_, dayIdx) => {
                  const count = getCount(activeRow, aggregated, dayIdx, slotIdx);
                  const severity = getSeverity(count);
                  const cellKey = `${dayIdx}-${slotIdx}`;

                  return (
                    <Popover key={cellKey}>
                      <PopoverTrigger asChild>
                        <button
                          className={`h-6 w-full rounded-sm transition-colors ${SEVERITY_COLORS[severity]} hover:ring-1 hover:ring-foreground/30`}
                        />
                      </PopoverTrigger>
                      <PopoverContent side="top" className="w-auto p-2 text-xs">
                        <div className="font-medium">{DAY_LABELS[dayIdx]} {label}</div>
                        <div className="text-muted-foreground">{count} ihlal</div>
                      </PopoverContent>
                    </Popover>
                  );
                })}
              </>
            ))}
          </div>
        </div>

        {/* Legend */}
        <div className="mt-3 flex items-center gap-3 text-[10px] text-muted-foreground">
          <span>Az</span>
          <div className="flex gap-[2px]">
            {['none', 'low', 'medium', 'high'].map(s => (
              <div key={s} className={`h-3 w-6 rounded-sm ${SEVERITY_COLORS[s]}`} />
            ))}
          </div>
          <span>Cok</span>
        </div>
      </CardContent>
    </Card>
  );
}

function getCount(
  activeRow: HeatmapRowDto | undefined,
  aggregated: Record<string, number> | null,
  dayIdx: number,
  slotIdx: number,
): number {
  if (activeRow) {
    return activeRow.cells
      .filter(c => (DAY_MAP[c.dayOfWeek] ?? -1) === dayIdx && c.slotIndex - 8 === slotIdx)
      .reduce((sum, c) => sum + c.violationCount, 0);
  }
  if (aggregated) {
    return aggregated[`${dayIdx}-${slotIdx}`] ?? 0;
  }
  return 0;
}

function getSeverity(count: number): string {
  if (count === 0) return 'none';
  if (count === 1) return 'low';
  if (count <= 3) return 'medium';
  return 'high';
}
