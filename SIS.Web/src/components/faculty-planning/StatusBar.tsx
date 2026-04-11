import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { useFacultyPlanningOverview } from '../../hooks/useFacultyPlanning';

export function StatusBar({ facultyId, termId }: { facultyId: number; termId: number | null }) {
  const overviewQuery = useFacultyPlanningOverview(facultyId, termId);

  if (!termId || overviewQuery.isLoading) return <Skeleton className="h-10 rounded-lg" />;
  if (overviewQuery.isError || !overviewQuery.data) return null;

  const ov = overviewQuery.data;
  const progressPct = ov.totalSections > 0
    ? Math.round((ov.scheduledSections / ov.totalSections) * 100)
    : 0;

  return (
    <div className="flex flex-wrap items-center gap-4 bg-white border rounded-lg px-4 py-2 text-sm">
      <span>
        Programlanan: <strong>{ov.scheduledSections}/{ov.totalSections}</strong>
      </span>
      <Progress value={progressPct} className="w-32 h-2" />
      {ov.conflictCount > 0 && (
        <Badge variant="destructive">{ov.conflictCount} çakışma</Badge>
      )}
      <div className="ml-auto flex flex-wrap gap-1">
        {ov.departmentProgress.map(dp => (
          <Badge
            key={dp.departmentId}
            variant={dp.scheduledSections === dp.totalSections ? 'success' : 'outline'}
            className="text-[10px]"
          >
            {dp.departmentCode}: {dp.scheduledSections}/{dp.totalSections}
          </Badge>
        ))}
      </div>
    </div>
  );
}
