import { Button } from '@/components/ui/button';
import { ClipboardList, School, BarChart3, Zap } from 'lucide-react';
import type { ScheduleRunSummary } from '../../../types/facultyPlanning';
import { RunChipBar } from './RunChipBar';

interface ScheduleTopBarProps {
  unscheduledCount: number;
  availableClassroomCount: number | undefined;
  occupancyPct: number;
  isPending: boolean;
  onRun: () => void;
  runs: ScheduleRunSummary[];
  activeRunId: number | null;
  onRunSelect: (id: number) => void;
}

export function ScheduleTopBar({
  unscheduledCount,
  availableClassroomCount,
  occupancyPct,
  isPending,
  onRun,
  runs,
  activeRunId,
  onRunSelect,
}: ScheduleTopBarProps) {
  return (
    <div className="bg-muted/30 border border-border/50 rounded-lg px-4 py-3 space-y-2.5">
      {/* Row 1: Metrics + Run button */}
      <div className="flex items-center gap-3">
        <div className="grid grid-cols-3 gap-2 flex-1">
          {/* Metric 1: Zamanlanmamış */}
          <div className="bg-background rounded-lg px-3 py-2 flex items-center gap-2.5">
            <div className="w-8 h-8 bg-amber-500/10 rounded-lg flex items-center justify-center">
              <ClipboardList className="h-4 w-4 text-amber-500" />
            </div>
            <div>
              <div className="text-lg font-bold text-amber-600">{unscheduledCount}</div>
              <div className="text-[10px] text-muted-foreground">Zamanlanmamış</div>
            </div>
          </div>

          {/* Metric 2: Müsait derslik */}
          <div className="bg-background rounded-lg px-3 py-2 flex items-center gap-2.5">
            <div className="w-8 h-8 bg-emerald-500/10 rounded-lg flex items-center justify-center">
              <School className="h-4 w-4 text-emerald-500" />
            </div>
            <div>
              <div className="text-lg font-bold text-emerald-600">{availableClassroomCount ?? '—'}</div>
              <div className="text-[10px] text-muted-foreground">Müsait Derslik</div>
            </div>
          </div>

          {/* Metric 3: Doluluk */}
          <div className="bg-background rounded-lg px-3 py-2 flex items-center gap-2.5">
            <div className="w-8 h-8 bg-indigo-500/10 rounded-lg flex items-center justify-center">
              <BarChart3 className="h-4 w-4 text-indigo-500" />
            </div>
            <div>
              <div className="text-lg font-bold text-indigo-600">%{occupancyPct}</div>
              <div className="text-[10px] text-muted-foreground">Doluluk</div>
            </div>
          </div>
        </div>

        <Button onClick={onRun} disabled={isPending} className="shrink-0">
          <Zap className="h-4 w-4 mr-1.5" />
          {isPending ? 'Hesaplanıyor...' : 'Otomatik Çalıştır'}
        </Button>
      </div>

      {/* Row 2: Chip bar */}
      <RunChipBar runs={runs} activeRunId={activeRunId} onSelect={onRunSelect} />
    </div>
  );
}
