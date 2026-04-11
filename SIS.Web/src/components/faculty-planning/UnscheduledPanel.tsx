import { useDraggable } from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';
import { Badge } from '@/components/ui/badge';
import { ScrollArea } from '@/components/ui/scroll-area';
import type { FacultyPlanningSection } from '../../types/facultyPlanning';
import { cn } from '@/lib/utils';

interface UnscheduledPanelProps {
  sections: FacultyPlanningSection[];
  totalCount: number;
  scheduledCount: number;
  selectedId: number | null;
  onSelect: (section: FacultyPlanningSection) => void;
}

interface DraggableSectionProps {
  section: FacultyPlanningSection;
  isSelected: boolean;
  onSelect: (section: FacultyPlanningSection) => void;
}

function DraggableSection({ section, isSelected, onSelect }: DraggableSectionProps) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: `section-${section.sectionId}`,
    data: { section },
  });

  const style = transform
    ? { transform: CSS.Translate.toString(transform) }
    : undefined;

  return (
    <div
      ref={setNodeRef}
      style={style}
      {...listeners}
      {...attributes}
      onClick={() => onSelect(section)}
      className={cn(
        'w-full text-left rounded-md border p-2 text-xs transition-colors hover:bg-accent cursor-grab active:cursor-grabbing select-none',
        isSelected && 'ring-2 ring-primary bg-accent',
        isDragging && 'opacity-50 shadow-lg',
      )}
    >
      <div className="font-mono font-semibold text-primary">{section.courseCode}</div>
      <div className="text-muted-foreground truncate">{section.courseName}</div>
      <div className="flex items-center gap-1 mt-1">
        <Badge variant="outline" className="text-[10px] px-1 py-0">
          {section.theoryHours}T+{section.labHours}L
        </Badge>
        <span className="text-muted-foreground">{section.departmentCode}</span>
      </div>
    </div>
  );
}

export function UnscheduledPanel({ sections, totalCount, scheduledCount, selectedId, onSelect }: UnscheduledPanelProps) {
  const unscheduled = sections.filter(s => s.status === 'Unscheduled');

  return (
    <div className="w-52 flex-shrink-0 border rounded-lg bg-muted/30 flex flex-col">
      <div className="p-3 border-b">
        <h3 className="text-sm font-semibold">Atanmamis Dersler</h3>
        <p className="text-xs text-muted-foreground mt-1">
          {unscheduled.length} atanmamis / {totalCount} toplam
        </p>
        <div className="mt-2 h-1.5 rounded-full bg-muted overflow-hidden">
          <div
            className="h-full bg-primary rounded-full transition-all"
            style={{ width: totalCount > 0 ? `${(scheduledCount / totalCount) * 100}%` : '0%' }}
          />
        </div>
      </div>
      <ScrollArea className="flex-1 p-2">
        <div className="space-y-1.5">
          {unscheduled.length === 0 && (
            <p className="text-xs text-muted-foreground text-center py-4">
              Tum dersler atanmis
            </p>
          )}
          {unscheduled.map(s => (
            <DraggableSection
              key={s.sectionId}
              section={s}
              isSelected={selectedId === s.sectionId}
              onSelect={onSelect}
            />
          ))}
        </div>
      </ScrollArea>
    </div>
  );
}
