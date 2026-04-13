import { useState, useMemo } from 'react';
import { useDraggable } from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { ScrollArea } from '@/components/ui/scroll-area';
import { ChevronDown, ChevronRight, GripVertical, Search } from 'lucide-react';
import type { FacultyPlanningSection } from '../../types/facultyPlanning';
import { cn } from '@/lib/utils';

interface UnscheduledPanelProps {
  sections: FacultyPlanningSection[];
  totalCount: number;
  scheduledCount: number;
  selectedId: number | null;
  onSelect: (section: FacultyPlanningSection) => void;
}

interface CourseGroup {
  courseCode: string;
  courseName: string;
  theoryHours: number;
  labHours: number;
  sections: FacultyPlanningSection[];
}

interface DraggableSectionItemProps {
  section: FacultyPlanningSection;
  isSelected: boolean;
  onSelect: (section: FacultyPlanningSection) => void;
}

function DraggableSectionItem({ section, isSelected, onSelect }: DraggableSectionItemProps) {
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
        'flex items-center gap-1.5 rounded px-1.5 py-1 text-xs transition-colors cursor-grab active:cursor-grabbing select-none',
        'hover:bg-slate-100 border border-transparent',
        isSelected && 'bg-slate-100 border-[#003366]/30 ring-1 ring-[#003366]/20',
        isDragging && 'opacity-50 shadow-lg',
      )}
    >
      <GripVertical className="h-3 w-3 text-slate-400 flex-shrink-0" />
      <div className="flex-1 min-w-0">
        <span className="text-slate-700">Şube {section.sectionIndex}</span>
      </div>
      <span className="text-[10px] text-slate-500">{section.capacity} kişi</span>
    </div>
  );
}

function GroupHeader({
  group,
  isExpanded,
  onToggle,
}: {
  group: CourseGroup;
  isExpanded: boolean;
  onToggle: () => void;
}) {
  return (
    <button
      onClick={onToggle}
      className="w-full flex items-center gap-1.5 px-2 py-1.5 text-left rounded hover:bg-slate-50 transition-colors"
    >
      {isExpanded ? (
        <ChevronDown className="h-3 w-3 text-slate-500 flex-shrink-0" />
      ) : (
        <ChevronRight className="h-3 w-3 text-slate-500 flex-shrink-0" />
      )}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-1.5">
          <span className="font-mono font-bold text-[11px]" style={{ color: '#003366' }}>
            {group.courseCode}
          </span>
          <span className="text-[10px] text-slate-500">({group.sections.length})</span>
        </div>
        <div className="text-[10px] text-slate-500 truncate leading-tight">{group.courseName}</div>
      </div>
      <Badge variant="outline" className="text-[9px] px-1 py-0 flex-shrink-0 border-slate-300 text-slate-600">
        {group.theoryHours}T+{group.labHours}L
      </Badge>
    </button>
  );
}

export function UnscheduledPanel({ sections, totalCount, scheduledCount, selectedId, onSelect }: UnscheduledPanelProps) {
  const [search, setSearch] = useState('');
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(new Set());

  const unscheduled = useMemo(
    () => sections.filter(s => s.status === 'Unscheduled'),
    [sections],
  );

  const filtered = useMemo(() => {
    if (!search.trim()) return unscheduled;
    const q = search.toLowerCase();
    return unscheduled.filter(
      s => s.courseCode.toLowerCase().includes(q) || s.courseName.toLowerCase().includes(q),
    );
  }, [unscheduled, search]);

  const groups = useMemo(() => {
    const map = new Map<string, CourseGroup>();
    for (const s of filtered) {
      let group = map.get(s.courseCode);
      if (!group) {
        group = {
          courseCode: s.courseCode,
          courseName: s.courseName,
          theoryHours: s.theoryHours,
          labHours: s.labHours,
          sections: [],
        };
        map.set(s.courseCode, group);
      }
      group.sections.push(s);
    }
    return Array.from(map.values()).sort((a, b) => a.courseCode.localeCompare(b.courseCode));
  }, [filtered]);

  const toggleGroup = (courseCode: string) => {
    setExpandedGroups(prev => {
      const next = new Set(prev);
      if (next.has(courseCode)) {
        next.delete(courseCode);
      } else {
        next.add(courseCode);
      }
      return next;
    });
  };

  const progressPct = totalCount > 0 ? (scheduledCount / totalCount) * 100 : 0;

  return (
    <div className="w-[220px] flex-shrink-0 border border-slate-200 rounded-lg bg-white flex flex-col overflow-hidden">
      {/* Header with progress */}
      <div className="p-3 border-b border-slate-200">
        <div className="flex items-center justify-between mb-1">
          <h3 className="text-xs font-semibold text-slate-800">Atanmamış Dersler</h3>
          <span className="text-[10px] font-medium text-slate-500">{scheduledCount}/{totalCount}</span>
        </div>
        <div className="h-1.5 rounded-full bg-slate-100 overflow-hidden">
          <div
            className="h-full rounded-full transition-all duration-300"
            style={{
              width: `${progressPct}%`,
              backgroundColor: progressPct === 100 ? '#10b981' : '#003366',
            }}
          />
        </div>
        <p className="text-[10px] text-slate-500 mt-1">
          {unscheduled.length} atanmamış
        </p>
      </div>

      {/* Search */}
      <div className="px-2 py-2 border-b border-slate-100">
        <div className="relative">
          <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-3 w-3 text-slate-400" />
          <Input
            placeholder="Ders ara..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="h-7 pl-7 text-xs bg-slate-50 border-slate-200 focus-visible:ring-[#003366]/30"
          />
        </div>
      </div>

      {/* Grouped list */}
      <ScrollArea className="flex-1">
        <div className="p-1.5 space-y-0.5">
          {groups.length === 0 && (
            <p className="text-[11px] text-slate-400 text-center py-6">
              {search ? 'Sonuç bulunamadı' : 'Tüm dersler atanmış'}
            </p>
          )}
          {groups.map(group => {
            const isExpanded = expandedGroups.has(group.courseCode);
            return (
              <div key={group.courseCode}>
                <GroupHeader
                  group={group}
                  isExpanded={isExpanded}
                  onToggle={() => toggleGroup(group.courseCode)}
                />
                {isExpanded && (
                  <div className="ml-4 space-y-0.5 mb-1">
                    {group.sections
                      .sort((a, b) => a.sectionIndex - b.sectionIndex)
                      .map(s => (
                        <DraggableSectionItem
                          key={s.sectionId}
                          section={s}
                          isSelected={selectedId === s.sectionId}
                          onSelect={onSelect}
                        />
                      ))}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </ScrollArea>
    </div>
  );
}
