import { useMemo, useState } from 'react';
import { Search } from 'lucide-react';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet';
import { Input } from '@/components/ui/input';
import { ScrollArea } from '@/components/ui/scroll-area';
import { useAssignSection, useAvailableClassrooms } from '../../hooks/useFacultyPlanning';
import type { FacultyPlanningSection } from '../../types/facultyPlanning';
import { AssignableSectionRow } from './AssignableSectionRow';

const DAY_NAMES: Record<number, string> = {
  1: 'Pazartesi',
  2: 'Salı',
  3: 'Çarşamba',
  4: 'Perşembe',
  5: 'Cuma',
};

interface SlotAssignSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  day: number;
  slotOrder: number;
  startTime: string;
  endTime: string;
  termId: number;
  sections: FacultyPlanningSection[];
}

interface SectionWithReason {
  section: FacultyPlanningSection;
  available: boolean;
  reason?: string;
}

function classifySection(
  section: FacultyPlanningSection,
  _day: number,
): SectionWithReason {
  if (section.isPinned) {
    return { section, available: false, reason: 'Sabitlenmiş' };
  }
  if (section.status !== 'Unscheduled') {
    return { section, available: false, reason: 'Zaten atandı' };
  }
  if (!section.instructorId) {
    return { section, available: false, reason: 'Öğretim üyesi yok' };
  }
  return { section, available: true };
}

/** Renders the classroom-fetching row for an assignable section */
function AssignableRowWrapper({
  section,
  termId,
  day,
  startTime,
  endTime,
  onAssign,
  isAssigning,
}: {
  section: FacultyPlanningSection;
  termId: number;
  day: number;
  startTime: string;
  endTime: string;
  onAssign: (sectionId: number, classroomId: number) => void;
  isAssigning: boolean;
}) {
  const classroomsQuery = useAvailableClassrooms(
    termId,
    day,
    startTime,
    endTime,
    section.capacity,
  );

  return (
    <AssignableSectionRow
      section={{
        sectionId: section.sectionId,
        courseCode: section.courseCode,
        courseName: section.courseName,
        sectionIndex: section.sectionIndex,
        instructorName: section.instructorName,
        capacity: section.capacity,
        theoryHours: section.theoryHours,
        labHours: section.labHours,
      }}
      available={true}
      classrooms={classroomsQuery.data ?? []}
      classroomsLoading={classroomsQuery.isLoading}
      onAssign={onAssign}
      isAssigning={isAssigning}
    />
  );
}

export function SlotAssignSheet({
  open,
  onOpenChange,
  day,
  slotOrder: _slotOrder,
  startTime,
  endTime,
  termId,
  sections,
}: SlotAssignSheetProps) {
  const [search, setSearch] = useState('');
  const assignMutation = useAssignSection();

  // Only consider unscheduled + non-pinned sections for this panel
  const classified = useMemo<SectionWithReason[]>(() => {
    return sections
      .filter(s => s.status === 'Unscheduled' || s.isPinned)
      .map(s => classifySection(s, day));
  }, [sections, day]);

  const filtered = useMemo<SectionWithReason[]>(() => {
    if (!search.trim()) return classified;
    const q = search.toLowerCase();
    return classified.filter(
      ({ section: s }) =>
        s.courseCode.toLowerCase().includes(q) ||
        s.courseName.toLowerCase().includes(q) ||
        (s.instructorName?.toLowerCase().includes(q) ?? false),
    );
  }, [classified, search]);

  const assignable = useMemo(
    () => filtered.filter(r => r.available),
    [filtered],
  );
  const unassignable = useMemo(
    () => filtered.filter(r => !r.available),
    [filtered],
  );

  const timeLabel = `${startTime.slice(0, 5)}–${endTime.slice(0, 5)}`;
  const dayName = DAY_NAMES[day] ?? `Gün ${day}`;

  const handleAssign = (sectionId: number, classroomId: number) => {
    assignMutation.mutate(
      {
        sectionId,
        classroomId,
        scheduleSlots: [
          { day, startTime: startTime.slice(0, 5), endTime: endTime.slice(0, 5), classroomId },
        ],
      },
      {
        onSuccess: () => {
          onOpenChange(false);
        },
      },
    );
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="right" className="w-[400px] sm:w-[440px] p-0 flex flex-col">
        <SheetHeader className="px-4 pt-5 pb-3 border-b border-slate-100 shrink-0">
          <SheetTitle className="text-base font-semibold text-[#003366]">
            {dayName} {timeLabel} — Şube Ata
          </SheetTitle>
          <div className="relative mt-2">
            <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400 pointer-events-none" />
            <Input
              placeholder="Ders kodu, ad veya öğretim üyesi ara…"
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="pl-8 h-8 text-sm"
            />
          </div>
        </SheetHeader>

        <ScrollArea className="flex-1 min-h-0">
          {/* Assignable sections */}
          {assignable.length > 0 && (
            <div>
              <div className="px-4 py-2 bg-emerald-50/60 border-b border-slate-100">
                <span className="text-xs font-semibold text-emerald-700 uppercase tracking-wider">
                  Atanabilir ({assignable.length})
                </span>
              </div>
              <div>
                {assignable.map(({ section }) => (
                  <AssignableRowWrapper
                    key={section.sectionId}
                    section={section}
                    termId={termId}
                    day={day}
                    startTime={startTime}
                    endTime={endTime}
                    onAssign={handleAssign}
                    isAssigning={assignMutation.isPending}
                  />
                ))}
              </div>
            </div>
          )}

          {/* Unassignable sections */}
          {unassignable.length > 0 && (
            <div>
              <div className="px-4 py-2 bg-slate-50 border-b border-slate-100">
                <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">
                  Atanamaz ({unassignable.length})
                </span>
              </div>
              <div>
                {unassignable.map(({ section, reason }) => (
                  <AssignableSectionRow
                    key={section.sectionId}
                    section={{
                      sectionId: section.sectionId,
                      courseCode: section.courseCode,
                      courseName: section.courseName,
                      sectionIndex: section.sectionIndex,
                      instructorName: section.instructorName,
                      capacity: section.capacity,
                      theoryHours: section.theoryHours,
                      labHours: section.labHours,
                    }}
                    available={false}
                    reason={reason}
                    classrooms={[]}
                    classroomsLoading={false}
                    onAssign={() => undefined}
                    isAssigning={false}
                  />
                ))}
              </div>
            </div>
          )}

          {/* Empty state */}
          {filtered.length === 0 && (
            <div className="flex flex-col items-center justify-center h-40 text-slate-400">
              <p className="text-sm">
                {search ? 'Arama sonucu bulunamadı' : 'Atanabilir şube yok'}
              </p>
            </div>
          )}
        </ScrollArea>

        {/* Error feedback */}
        {assignMutation.isError && (
          <div className="px-4 py-2 bg-red-50 border-t border-red-200 shrink-0">
            <p className="text-xs text-red-600">Atama başarısız. Lütfen tekrar deneyin.</p>
          </div>
        )}
      </SheetContent>
    </Sheet>
  );
}
