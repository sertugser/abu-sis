import { cn } from '@/lib/utils';
import { useDepartmentClosingOverview } from '@/hooks/useTermClosing';
import { Loader2 } from 'lucide-react';
import type { DepartmentClosingOverviewDto } from '@/api/termClosing.api';

const STEP_DOTS = [1, 2, 3, 4];

interface DepartmentOverviewProps {
  facultyId?: number;
  selectedDeptId: number | null;
  onSelectDepartment: (deptId: number) => void;
}

export function DepartmentOverview({ facultyId, selectedDeptId, onSelectDepartment }: DepartmentOverviewProps) {
  const { data: departments, isLoading } = useDepartmentClosingOverview(facultyId);

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 py-4 justify-center text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" />
        <span className="text-sm">Fakulte ozeti yukleniyor...</span>
      </div>
    );
  }

  if (!departments?.length) return null;

  return (
    <div className="space-y-2">
      <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider px-1">
        Bolum Kapama Durumu
      </h3>
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-2">
        {departments.map(dept => (
          <DeptCard
            key={dept.departmentId}
            dept={dept}
            isSelected={selectedDeptId === dept.departmentId}
            onClick={() => onSelectDepartment(dept.departmentId)}
          />
        ))}
      </div>
    </div>
  );
}

function DeptCard({ dept, isSelected, onClick }: {
  dept: DepartmentClosingOverviewDto; isSelected: boolean; onClick: () => void;
}) {
  const isDone = dept.completedStepCount === 4;

  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'rounded-lg border p-3 text-left transition-all hover:shadow-sm',
        isSelected && 'ring-2 ring-primary/30 border-primary/40 bg-primary/[0.03]',
        isDone && !isSelected && 'border-emerald-200 bg-emerald-50/30',
        !isDone && !isSelected && 'border-border/80 hover:border-border',
      )}
    >
      <div className="flex items-center justify-between mb-1.5">
        <span className="text-xs font-bold truncate">{dept.departmentCode}</span>
        <span className={cn(
          'text-[10px] font-medium',
          isDone ? 'text-emerald-600' : 'text-muted-foreground',
        )}>
          {dept.completedStepCount}/4
        </span>
      </div>

      {/* Progress dots */}
      <div className="flex gap-1 mb-1.5">
        {STEP_DOTS.map(step => (
          <div
            key={step}
            className={cn(
              'h-1.5 flex-1 rounded-full transition-colors',
              step <= dept.completedStepCount ? 'bg-emerald-500' : 'bg-muted',
            )}
          />
        ))}
      </div>

      <p className="text-[10px] text-muted-foreground truncate" title={dept.departmentName}>
        {dept.departmentName}
      </p>

      {dept.missingGradeCount > 0 && dept.completedStepCount < 1 && (
        <p className="text-[10px] text-amber-600 mt-0.5">
          {dept.missingGradeCount} eksik not
        </p>
      )}

      {dept.lastActivityByName && (
        <p className="text-[9px] text-muted-foreground/70 mt-0.5 truncate">
          {dept.lastActivityByName}
          {dept.lastActivityAt && ` · ${new Date(dept.lastActivityAt).toLocaleString('tr-TR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })}`}
        </p>
      )}
    </button>
  );
}
