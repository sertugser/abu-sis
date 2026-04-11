import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { useFacultyPlanningConflicts } from '../../hooks/useFacultyPlanning';

export function ConflictBar({ facultyId, termId }: { facultyId: number; termId: number | null }) {
  const [showConflicts, setShowConflicts] = useState(false);
  const conflictsQuery = useFacultyPlanningConflicts(facultyId, termId);

  if (!termId || conflictsQuery.isLoading) return <Skeleton className="h-8 rounded-lg" />;

  const conflicts = conflictsQuery.data ?? [];
  if (conflicts.length === 0) return null;

  return (
    <div className="border-t bg-red-50 px-4 py-2">
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium text-red-700">{conflicts.length} çakışma</span>
        <Button size="sm" variant="outline" onClick={() => setShowConflicts(prev => !prev)}>
          {showConflicts ? 'Gizle' : 'Göster'}
        </Button>
      </div>
      {showConflicts && (
        <div className="mt-2 max-h-48 overflow-y-auto space-y-1">
          {conflicts.map((c, i) => (
            <div key={i} className="text-xs flex gap-2">
              <Badge variant="destructive" className="text-[9px]">{c.conflictType}</Badge>
              <span className="font-mono">{c.courseCode}</span>
              <span className="text-muted-foreground">{c.message}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
