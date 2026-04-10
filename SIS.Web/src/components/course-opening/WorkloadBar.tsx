import type { InstructorWorkloadDto } from '../../types/courseOpening';

export function WorkloadBar({ workload }: { workload: InstructorWorkloadDto }) {
  const pct = workload.maxWeeklyHours > 0
    ? Math.round((workload.assignedWeeklyHours / workload.maxWeeklyHours) * 100)
    : 0;
  const color = pct >= 100 ? 'bg-red-500' : pct >= 80 ? 'bg-amber-500' : 'bg-emerald-500';

  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between text-xs">
        <span className="text-muted-foreground">{workload.title}</span>
        <span className="font-medium">
          {workload.assignedWeeklyHours}/{workload.maxWeeklyHours} saat
          <span className="text-muted-foreground ml-1">({workload.remainingCapacity} boş)</span>
        </span>
      </div>
      <div className="h-1.5 bg-gray-100 rounded-full overflow-hidden">
        <div className={`h-full rounded-full ${color}`} style={{ width: `${Math.min(pct, 100)}%` }} />
      </div>
    </div>
  );
}
