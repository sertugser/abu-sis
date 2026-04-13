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
  /** Aynı courseCode'lu şubeler arasındaki sıra (1-based), ayırt etmek için */
  groupIndex?: number;
}

export function ScheduleListItem({ data, isSelected, onClick, groupIndex }: ScheduleListItemProps) {
  const { kind, item } = data;

  const sectionNum = kind === 'unscheduled' ? item.sectionIndex : item.sectionNumber;
  const displayNum = sectionNum || groupIndex || 0;
  const code = displayNum
    ? `${item.courseCode}-${String(displayNum).padStart(2, '0')}`
    : item.courseCode;

  // Kısa bilgi: başarılı → derslik+saat, başarısız → constraint tipi, zamanlanmamış → kapasite
  const meta = kind === 'success'
    ? `${item.classroomName} · ${item.scheduleSummary}`
    : kind === 'failure'
      ? TYPE_SHORT[item.suggestions?.[0]?.suggestionType ?? ''] ?? 'Atanamadı'
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
      className={`w-full text-left px-3 py-2 border-b border-border/30 cursor-pointer transition-colors ${
        isSelected
          ? 'bg-indigo-500/10 border-l-[3px] border-l-indigo-500'
          : 'hover:bg-muted/40'
      }`}
    >
      <div className="flex items-center justify-between gap-2">
        <div className="min-w-0 flex items-center gap-2">
          <div className={`w-1.5 h-1.5 rounded-full shrink-0 ${dotColor}`} />
          <span className="text-[13px] font-semibold text-foreground truncate">{code}</span>
          <span className="text-[11px] text-muted-foreground truncate">{item.courseName}</span>
        </div>
        <span className={`text-[10px] shrink-0 ${kind === 'failure' ? 'text-red-500' : 'text-muted-foreground'}`}>
          {meta}
        </span>
      </div>
    </button>
  );
}

const TYPE_SHORT: Record<string, string> = {
  CapacityIncrease: 'Kapasite',
  ClassroomAdd: 'Derslik yok',
  TimeChange: 'Çakışma',
  InstructorReassign: 'Hoca çakışması',
  ConstraintRelax: 'Kısıt',
};

export type { ListItemData };
