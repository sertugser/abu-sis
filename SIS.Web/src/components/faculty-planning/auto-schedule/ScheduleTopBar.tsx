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
  const metrics = [
    { icon: ClipboardList, value: unscheduledCount, label: 'Zamanlanmamış', color: 'text-amber-600', bg: 'bg-amber-500/10' },
    { icon: School, value: availableClassroomCount ?? '—', label: 'Müsait Derslik', color: 'text-emerald-600', bg: 'bg-emerald-500/10' },
    { icon: BarChart3, value: `%${occupancyPct}`, label: 'Doluluk', color: 'text-indigo-600', bg: 'bg-indigo-500/10' },
  ];

  return (
    <div className="border border-border/50 rounded-lg overflow-hidden">
      {/* Row 1: Metrics + Run button */}
      <div className="flex items-stretch">
        {metrics.map((m, i) => {
          const Icon = m.icon;
          return (
            <div key={i} className={`flex-1 flex items-center gap-3 px-4 py-3 ${i < metrics.length - 1 ? 'border-r border-border/40' : ''}`}>
              <div className={`w-9 h-9 ${m.bg} rounded-lg flex items-center justify-center shrink-0`}>
                <Icon className={`h-4 w-4 ${m.color}`} />
              </div>
              <div>
                <div className={`text-xl font-bold tabular-nums ${m.color}`}>{m.value}</div>
                <div className="text-[10px] text-muted-foreground">{m.label}</div>
              </div>
            </div>
          );
        })}
        <div className="flex items-center px-4 border-l border-border/40">
          <Button onClick={onRun} disabled={isPending} size="lg">
            <Zap className="h-4 w-4 mr-1.5" />
            {isPending ? 'Hesaplanıyor...' : 'Otomatik Programla'}
          </Button>
        </div>
      </div>

      {/* Row 2: Chip bar */}
      {runs.length > 0 && (
        <div className="px-4 py-2 border-t border-border/40 bg-muted/20">
          <RunChipBar runs={runs} activeRunId={activeRunId} onSelect={onRunSelect} />
        </div>
      )}
    </div>
  );
}
