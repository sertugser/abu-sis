import { useState } from 'react';
import { DndContext, DragOverlay } from '@dnd-kit/core';
import type { DragStartEvent, DragEndEvent } from '@dnd-kit/core';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { AlertCircle, CheckCircle2 } from 'lucide-react';
import {
  useAssignSection, useAvailableClassrooms, useFacultyPlanningSections,
  useFacultyPlanningTimeSlots, useUnassignSection,
} from '../../hooks/useFacultyPlanning';
import { buildManualScheduleOptions } from './sectionUtils';
import type { FacultyPlanningSection, FacultyPlanningTimeSlot } from '../../types/facultyPlanning';
import type { AssignmentResult } from '../../types/facultyPlanning';
import { CalendarGrid } from './CalendarGrid';
import { UnscheduledPanel } from './UnscheduledPanel';
import { SectionDetailPanel } from './SectionDetailPanel';

interface AssignState {
  section: FacultyPlanningSection;
  day: number;
  startTime: string;
  endTime: string;
}

interface ActionFeedback {
  kind: 'success' | 'error';
  title: string;
  message: string;
}

const WEEKDAY_LABELS: Record<number, string> = { 1: 'Pzt', 2: 'Sal', 3: 'Car', 4: 'Per', 5: 'Cum' };

export function CalendarViewTab({ facultyId, termId }: { facultyId: number; termId: number | null }) {
  const [selectedUnscheduled, setSelectedUnscheduled] = useState<FacultyPlanningSection | null>(null);
  const [selectedScheduled, setSelectedScheduled] = useState<FacultyPlanningSection | null>(null);
  const [assignState, setAssignState] = useState<AssignState | null>(null);
  const [classroomId, setClassroomId] = useState<string>('');
  const [feedback, setFeedback] = useState<ActionFeedback | null>(null);
  const [activeDragSection, setActiveDragSection] = useState<FacultyPlanningSection | null>(null);

  const sectionsQuery = useFacultyPlanningSections(facultyId, termId, undefined, undefined, 1, 500);
  const timeSlotsQuery = useFacultyPlanningTimeSlots(termId);
  const assignMutation = useAssignSection();
  const unassignMutation = useUnassignSection();

  const sections = sectionsQuery.data?.items ?? [];
  const timeSlots: FacultyPlanningTimeSlot[] = timeSlotsQuery.data ?? [];
  const scheduledCount = sections.filter(s => s.status !== 'Unscheduled').length;

  // Available classrooms for the assign dialog
  const classroomsQuery = useAvailableClassrooms(
    termId,
    assignState?.day,
    assignState?.startTime,
    assignState?.endTime,
    assignState?.section.capacity,
  );

  const handleCellClick = (day: number, startTime: string) => {
    if (!selectedUnscheduled) return;
    openAssignDialog(selectedUnscheduled, day, startTime);
  };

  const openAssignDialog = (section: FacultyPlanningSection, day: number, startTime: string) => {
    // Build the correct time window for the required slot count
    const requiredSlots = section.theoryHours + section.labHours || 1;
    const options = buildManualScheduleOptions(timeSlots, requiredSlots);
    const dayOptions = options.filter(o => o.day === day);
    // Find an option starting at or closest to the clicked/dropped startTime
    const clicked = dayOptions.find(o => o.startTime.slice(0, 5) === startTime.slice(0, 5)) ?? dayOptions[0];
    if (!clicked) {
      setFeedback({ kind: 'error', title: 'Uygun aralik yok', message: `${requiredSlots} slot icin bu gunde uygun aralik bulunamadi.` });
      return;
    }
    setAssignState({ section, day: clicked.day, startTime: clicked.startTime, endTime: clicked.endTime });
    setClassroomId('');
  };

  const handleDragStart = (event: DragStartEvent) => {
    const section = event.active.data.current?.section as FacultyPlanningSection | undefined;
    if (section) {
      setActiveDragSection(section);
    }
  };

  const handleDragEnd = (event: DragEndEvent) => {
    setActiveDragSection(null);

    const { active, over } = event;
    if (!over) return;

    const section = active.data.current?.section as FacultyPlanningSection | undefined;
    const cellData = over.data.current as { day: number; startTime: string; endTime: string } | undefined;

    if (!section || !cellData) return;

    // Select this section as the active unscheduled one and open assign dialog
    setSelectedUnscheduled(section);
    openAssignDialog(section, cellData.day, cellData.startTime);
  };

  const handleAssign = () => {
    if (!assignState || !classroomId) return;
    setFeedback(null);
    const slots = [{ day: assignState.day, startTime: assignState.startTime, endTime: assignState.endTime }];
    assignMutation.mutate(
      { sectionId: assignState.section.sectionId, classroomId: Number(classroomId), scheduleSlots: slots },
      {
        onSuccess: (result: AssignmentResult) => {
          setFeedback({
            kind: result.conflicts.length > 0 ? 'error' : 'success',
            title: result.conflicts.length > 0 ? 'Atama yapildi, cakisma var' : 'Atama basarili',
            message: result.conflicts.length > 0
              ? `${result.conflicts.length} cakisma tespit edildi.`
              : 'Sube basariyla atandi.',
          });
          setAssignState(null);
          setSelectedUnscheduled(null);
        },
        onError: () => {
          setFeedback({ kind: 'error', title: 'Atama basarisiz', message: 'Sube atanamadi.' });
        },
      },
    );
  };

  const handleUnassign = (sectionId: number) => {
    setFeedback(null);
    unassignMutation.mutate(sectionId, {
      onSuccess: () => {
        setFeedback({ kind: 'success', title: 'Atama kaldirildi', message: 'Sube atamasi kaldirildi.' });
        setSelectedScheduled(null);
      },
      onError: () => {
        setFeedback({ kind: 'error', title: 'Kaldirma basarisiz', message: 'Atama kaldirilamadi.' });
      },
    });
  };

  if (!termId) return <Card><CardContent className="py-8 text-center text-muted-foreground">Aktif donem bulunamadi.</CardContent></Card>;
  if (sectionsQuery.isLoading || timeSlotsQuery.isLoading) return <Skeleton className="h-96 rounded-xl" />;

  return (
    <DndContext onDragStart={handleDragStart} onDragEnd={handleDragEnd}>
      <div className="space-y-3">
        {feedback && (
          <Alert variant={feedback.kind === 'error' ? 'destructive' : 'default'}>
            {feedback.kind === 'error' ? <AlertCircle className="h-4 w-4" /> : <CheckCircle2 className="h-4 w-4" />}
            <AlertTitle>{feedback.title}</AlertTitle>
            <AlertDescription>{feedback.message}</AlertDescription>
          </Alert>
        )}

        {selectedUnscheduled && !assignState && (
          <Alert>
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>
              <span className="font-mono">{selectedUnscheduled.courseCode}</span> secildi
            </AlertTitle>
            <AlertDescription>
              Takvimde bos bir hucreye tiklayarak veya surukleyerek atama yapin.
              <Button variant="ghost" size="sm" className="ml-2" onClick={() => setSelectedUnscheduled(null)}>Iptal</Button>
            </AlertDescription>
          </Alert>
        )}

        {/* Assign dialog inline */}
        {assignState && (
          <Card className="border-primary">
            <CardContent className="py-3 space-y-2">
              <p className="text-sm font-medium">
                <span className="font-mono text-primary">{assignState.section.courseCode}</span>
                {' — '}
                {WEEKDAY_LABELS[assignState.day]} {assignState.startTime.slice(0, 5)}-{assignState.endTime.slice(0, 5)}
              </p>
              <div className="flex flex-wrap items-end gap-3">
                <div className="space-y-1">
                  <label className="text-xs font-medium">Derslik</label>
                  <select
                    className="h-8 w-full sm:w-60 rounded-md border px-2 text-sm"
                    value={classroomId}
                    onChange={e => setClassroomId(e.target.value)}
                  >
                    <option value="">Derslik sec...</option>
                    {(classroomsQuery.data ?? []).map(cr => (
                      <option key={cr.classroomId} value={cr.classroomId}>
                        {cr.name} ({cr.capacity} kisi, {cr.type})
                      </option>
                    ))}
                  </select>
                </div>
                <Button size="sm" disabled={!classroomId || assignMutation.isPending} onClick={handleAssign}>
                  {assignMutation.isPending ? 'Ataniyor...' : 'Ata'}
                </Button>
                <Button size="sm" variant="outline" onClick={() => setAssignState(null)}>Iptal</Button>
              </div>
            </CardContent>
          </Card>
        )}

        <div className="flex flex-col lg:flex-row gap-3" style={{ height: 'auto', minHeight: '400px' }}>
          <UnscheduledPanel
            sections={sections}
            totalCount={sections.length}
            scheduledCount={scheduledCount}
            selectedId={selectedUnscheduled?.sectionId ?? null}
            onSelect={setSelectedUnscheduled}
          />

          <CalendarGrid
            sections={sections}
            timeSlots={timeSlots}
            selectedSectionId={selectedScheduled?.sectionId ?? selectedUnscheduled?.sectionId ?? null}
            onSelectSection={setSelectedScheduled}
            onCellClick={handleCellClick}
            isDraggingSection={activeDragSection !== null}
          />

          <SectionDetailPanel
            section={selectedScheduled}
            onClose={() => setSelectedScheduled(null)}
            onUnassign={handleUnassign}
            isUnassigning={unassignMutation.isPending}
          />
        </div>
      </div>

      <DragOverlay>
        {activeDragSection && (
          <div className="bg-blue-100 border border-blue-400 rounded p-2 shadow-lg text-xs pointer-events-none">
            <div className="font-mono font-bold text-blue-900">{activeDragSection.courseCode}</div>
            <div className="text-blue-700 truncate max-w-[180px]">{activeDragSection.courseName}</div>
            <div className="text-blue-600 text-[10px] mt-0.5">
              {activeDragSection.theoryHours}T+{activeDragSection.labHours}L • {activeDragSection.departmentCode}
            </div>
          </div>
        )}
      </DragOverlay>
    </DndContext>
  );
}
