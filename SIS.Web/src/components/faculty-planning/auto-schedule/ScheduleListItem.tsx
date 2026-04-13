// SIS.Web/src/components/faculty-planning/auto-schedule/ScheduleListItem.tsx
import type { AutoSchedulePreviewItem, AutoScheduleFailure, FacultyPlanningSection } from '../../../types/facultyPlanning';

type ListItemData =
  | { kind: 'success'; item: AutoSchedulePreviewItem }
  | { kind: 'failure'; item: AutoScheduleFailure }
  | { kind: 'unscheduled'; item: FacultyPlanningSection };

interface ScheduleListItemProps {
  data: ListItemData;
  isSelected: boolean;
  onClick: () => void;
}

export function ScheduleListItem({ data, isSelected, onClick }: ScheduleListItemProps) {
  const { kind, item } = data;

  const code = kind === 'unscheduled'
    ? `${item.courseCode}-${String(item.sectionIndex).padStart(2, '0')}`
    : `${item.courseCode}-${String(item.sectionNumber).padStart(2, '0')}`;

  const name = item.courseName;

  const subtitle = kind === 'success'
    ? `${item.departmentCode} · ${item.scheduleSummary} · ${item.classroomName}`
    : kind === 'failure'
      ? item.suggestions?.[0]?.message ?? item.reason
      : `${item.capacity} kişi`;

  const dotColor = kind === 'success'
    ? 'bg-emerald-500'
    : kind === 'failure'
      ? 'bg-red-500'
      : 'bg-amber-500';

  return (
    <button
      type="button"
      onClick={onClick}
      className={`w-full text-left px-3.5 py-2.5 border-b border-border/30 cursor-pointer transition-colors ${
        isSelected
          ? 'bg-indigo-500/10 border-l-[3px] border-l-indigo-500'
          : 'hover:bg-muted/40'
      } ${kind === 'failure' ? 'bg-red-500/[0.03]' : ''}`}
    >
      <div className="flex items-center justify-between">
        <div className="min-w-0">
          <div className="text-[13px] font-semibold text-foreground truncate">{code}</div>
          <div className="text-[11px] text-muted-foreground truncate">{name}</div>
        </div>
        <div className={`w-2 h-2 rounded-full shrink-0 ml-2 ${dotColor}`} />
      </div>
      <div className={`text-[10px] mt-0.5 truncate ${kind === 'failure' ? 'text-red-500' : 'text-muted-foreground'}`}>
        {kind === 'failure' && '⚠ '}{subtitle}
      </div>
    </button>
  );
}

export type { ListItemData };
