import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { DndContext, PointerSensor, useSensor, useSensors } from '@dnd-kit/core';
import type { DragEndEvent } from '@dnd-kit/core';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { ScrollArea } from '@/components/ui/scroll-area';
import {
  AlertCircle, CheckCircle2, ChevronDown, ChevronRight,
  GripHorizontal, GripVertical, Minus, Search, X,
  BookOpen, FlaskConical,
} from 'lucide-react';
import { useDraggable } from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';
import {
  useAssignSection, useAvailableClassrooms, useFacultyPlanningSections,
  useFacultyPlanningTimeSlots, useUnassignSection,
} from '../../hooks/useFacultyPlanning';
import { useDepartmentInstructors } from '../../hooks/useCourseOpening';
import { buildManualScheduleOptions } from './sectionUtils';
import type { FacultyPlanningSection, FacultyPlanningTimeSlot } from '../../types/facultyPlanning';
import type { AssignmentResult } from '../../types/facultyPlanning';
import { CalendarGrid } from './CalendarGrid';
import { cn } from '@/lib/utils';

/* ── Tipler ── */
type AssignPhase = 'theory' | 'lab';

interface AssignState {
  section: FacultyPlanningSection;
  day: number;
  startTime: string;
  endTime: string;
  phase: AssignPhase;
}

interface ActionFeedback {
  kind: 'success' | 'error';
  message: string;
}

interface PanelPosition {
  x: number;
  y: number;
}

const WEEKDAY_LABELS: Record<number, string> = { 1: 'Pzt', 2: 'Sal', 3: 'Çar', 4: 'Per', 5: 'Cum' };

/** Ders için gerekli slot sayısını ve classroom tipini belirle */
function getPhaseInfo(section: FacultyPlanningSection, phase: AssignPhase) {
  if (phase === 'theory') {
    return {
      slotCount: section.theoryHours || 1,
      classroomType: 'Lecture' as const,
      label: 'Teori',
    };
  }
  return {
    slotCount: section.labHours || 1,
    classroomType: 'Lab' as const,
    label: 'Lab',
  };
}

/** Dersin hangi fazlarla atanması gerektiğini belirle */
function getSectionPhases(section: FacultyPlanningSection): AssignPhase[] {
  const phases: AssignPhase[] = [];
  if (section.theoryHours > 0) phases.push('theory');
  if (section.labHours > 0) phases.push('lab');
  if (phases.length === 0) phases.push('theory'); // fallback
  return phases;
}

/* ── Draggable Section Item ── */
function DraggableSectionItem({
  section, isSelected, onSelect,
}: {
  section: FacultyPlanningSection;
  isSelected: boolean;
  onSelect: (section: FacultyPlanningSection) => void;
}) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: `section-${section.sectionId}`,
    data: { section },
  });

  const style = transform ? { transform: CSS.Translate.toString(transform) } : undefined;

  return (
    <div
      ref={setNodeRef}
      style={style}
      {...listeners}
      {...attributes}
      onClick={() => onSelect(section)}
      className={cn(
        'flex items-center gap-2 rounded-lg px-2.5 py-2 text-sm transition-all cursor-grab active:cursor-grabbing select-none',
        'hover:bg-slate-100 border border-transparent',
        isSelected && 'bg-blue-50 border-blue-200 ring-1 ring-blue-300/50',
        isDragging && 'opacity-50 shadow-lg z-50',
      )}
    >
      <GripVertical className="h-4 w-4 text-slate-300 flex-shrink-0" />
      <div className="flex-1 min-w-0">
        <span className="text-slate-700 font-medium">Şube {section.sectionIndex}</span>
      </div>
      <span className="text-xs text-slate-400">{section.capacity} kişi</span>
    </div>
  );
}

/* ── Ders grubu başlığı ── */
function CourseGroupHeader({
  courseCode, courseName, theoryHours, labHours, sectionCount, isExpanded, onToggle,
}: {
  courseCode: string;
  courseName: string;
  theoryHours: number;
  labHours: number;
  sectionCount: number;
  isExpanded: boolean;
  onToggle: () => void;
}) {
  return (
    <button
      onClick={onToggle}
      className="w-full flex items-center gap-2 px-3 py-2 text-left rounded-lg hover:bg-slate-50 transition-colors"
    >
      {isExpanded
        ? <ChevronDown className="h-4 w-4 text-slate-400 flex-shrink-0" />
        : <ChevronRight className="h-4 w-4 text-slate-400 flex-shrink-0" />
      }
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <span className="font-mono font-bold text-sm" style={{ color: '#003366' }}>{courseCode}</span>
          <span className="text-xs text-slate-400">({sectionCount})</span>
        </div>
        <div className="text-xs text-slate-500 truncate leading-snug">{courseName}</div>
      </div>
      <Badge variant="outline" className="text-[10px] flex-shrink-0 border-slate-300 text-slate-500">
        {theoryHours}T+{labHours}L
      </Badge>
    </button>
  );
}

/* ══════════════════════════════════════════════════════════════
   Floating Draggable Panel
   ══════════════════════════════════════════════════════════════ */
function FloatingPanel({
  children, title, badge, defaultPosition, minimized, onMinimize, pointerEventsNone,
}: {
  children: React.ReactNode;
  title: string;
  badge?: string;
  defaultPosition: PanelPosition;
  minimized: boolean;
  onMinimize: () => void;
  pointerEventsNone?: boolean;
}) {
  const [position, setPosition] = useState<PanelPosition>(defaultPosition);
  const [isPanelDragging, setIsPanelDragging] = useState(false);
  const dragOffset = useRef({ x: 0, y: 0 });

  const handleMouseDown = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    setIsPanelDragging(true);
    dragOffset.current = {
      x: e.clientX - position.x,
      y: e.clientY - position.y,
    };
  }, [position]);

  useEffect(() => {
    if (!isPanelDragging) return;

    const handleMouseMove = (e: MouseEvent) => {
      setPosition({
        x: e.clientX - dragOffset.current.x,
        y: e.clientY - dragOffset.current.y,
      });
    };
    const handleMouseUp = () => setIsPanelDragging(false);

    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseup', handleMouseUp);
    return () => {
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseup', handleMouseUp);
    };
  }, [isPanelDragging]);

  if (minimized) {
    return (
      <div
        className="fixed z-50 bg-white border border-slate-200 rounded-xl shadow-xl cursor-pointer hover:shadow-2xl transition-shadow"
        style={{ left: position.x, top: position.y }}
        onClick={onMinimize}
      >
        <div className="flex items-center gap-2 px-3 py-2">
          <GripHorizontal className="h-4 w-4 text-slate-300" />
          <span className="text-sm font-semibold text-slate-700">{title}</span>
          {badge && <Badge variant="outline" className="text-[10px]">{badge}</Badge>}
        </div>
      </div>
    );
  }

  return (
    <div
      className={cn(
        'fixed z-50 bg-white border border-slate-200 rounded-xl shadow-2xl flex flex-col',
        'w-[340px] max-h-[70vh]',
        isPanelDragging && 'opacity-90 cursor-grabbing',
      )}
      style={{
        left: position.x,
        top: position.y,
        pointerEvents: pointerEventsNone ? 'none' : undefined,
      }}
    >
      {/* Başlık — sürüklenebilir */}
      <div
        className="flex items-center gap-2 px-3 py-2.5 border-b border-slate-100 cursor-grab active:cursor-grabbing select-none rounded-t-xl bg-slate-50/80"
        onMouseDown={handleMouseDown}
        style={{ pointerEvents: pointerEventsNone ? 'none' : undefined }}
      >
        <GripHorizontal className="h-4 w-4 text-slate-300" />
        <span className="text-sm font-semibold text-slate-700 flex-1">{title}</span>
        {badge && <Badge variant="outline" className="text-[10px]">{badge}</Badge>}
        <button
          className="p-1 rounded hover:bg-slate-200 transition-colors"
          onClick={e => { e.stopPropagation(); onMinimize(); }}
        >
          <Minus className="h-3.5 w-3.5 text-slate-400" />
        </button>
      </div>

      {/* İçerik */}
      <div className="flex-1 min-h-0 overflow-hidden flex flex-col">
        {children}
      </div>
    </div>
  );
}

/* ══════════════════════════════════════════════════════════════
   Ana CalendarViewTab
   ══════════════════════════════════════════════════════════════ */
export function CalendarViewTab({ facultyId, termId, departments = [] }: {
  facultyId: number;
  termId: number | null;
  departments?: { departmentId: number; departmentCode: string; departmentName: string }[];
}) {
  /* State */
  const [selectedSection, setSelectedSection] = useState<FacultyPlanningSection | null>(null);
  const [currentPhase, setCurrentPhase] = useState<AssignPhase>('theory');
  const [assignState, setAssignState] = useState<AssignState | null>(null);
  const [classroomId, setClassroomId] = useState<string>('');
  const [instructorId, setInstructorId] = useState<string>('');
  const [feedback, setFeedback] = useState<ActionFeedback | null>(null);
  const [filterDept, setFilterDept] = useState<number | undefined>();
  const [panelMinimized, setPanelMinimized] = useState(false);
  const [panelSearch, setPanelSearch] = useState('');
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(new Set());
  const [isDraggingSection, setIsDraggingSection] = useState(false);

  /* Queries */
  const sectionsQuery = useFacultyPlanningSections(facultyId, termId, filterDept, undefined, 1, 200);
  const timeSlotsQuery = useFacultyPlanningTimeSlots(termId);
  const assignMutation = useAssignSection();
  const unassignMutation = useUnassignSection();

  const sectionItems = sectionsQuery.data?.items;
  const sections = useMemo(() => sectionItems ?? [], [sectionItems]);
  const timeSlotItems = timeSlotsQuery.data;
  const timeSlots: FacultyPlanningTimeSlot[] = useMemo(() => timeSlotItems ?? [], [timeSlotItems]);
  const scheduledCount = sections.filter(s => s.status !== 'Unscheduled').length;
  const unscheduledSections = useMemo(() => sections.filter(s => s.status === 'Unscheduled'), [sections]);

  /* Panel filtresi */
  const filteredUnscheduled = useMemo(() => {
    let list = unscheduledSections;
    if (panelSearch.trim()) {
      const q = panelSearch.toLowerCase();
      list = list.filter(s => s.courseCode.toLowerCase().includes(q) || s.courseName.toLowerCase().includes(q));
    }
    return list;
  }, [unscheduledSections, panelSearch]);

  /* Grupla */
  const courseGroups = useMemo(() => {
    const map = new Map<string, { courseCode: string; courseName: string; theoryHours: number; labHours: number; sections: FacultyPlanningSection[] }>();
    for (const s of filteredUnscheduled) {
      let group = map.get(s.courseCode);
      if (!group) {
        group = { courseCode: s.courseCode, courseName: s.courseName, theoryHours: s.theoryHours, labHours: s.labHours, sections: [] };
        map.set(s.courseCode, group);
      }
      group.sections.push(s);
    }
    return Array.from(map.values()).sort((a, b) => a.courseCode.localeCompare(b.courseCode));
  }, [filteredUnscheduled]);

  /* Classroom/instructor queries */
  const phaseInfo = assignState ? getPhaseInfo(assignState.section, assignState.phase) : null;
  const classroomsQuery = useAvailableClassrooms(
    termId, assignState?.day, assignState?.startTime, assignState?.endTime,
    assignState?.section.capacity, phaseInfo?.classroomType,
  );
  const instructorsQuery = useDepartmentInstructors(assignState?.section.departmentId ?? null);

  /* DnD sensörleri */
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 8 } }),
  );

  /** Seçili ders için slot hesaplayan yardımcı */
  const openAssignDialog = useCallback((section: FacultyPlanningSection, day: number, startTime: string, phase: AssignPhase) => {
    const info = getPhaseInfo(section, phase);
    const options = buildManualScheduleOptions(timeSlots, info.slotCount);
    const dayOptions = options.filter(o => o.day === day);
    const clicked = dayOptions.find(o => o.startTime.slice(0, 5) === startTime.slice(0, 5)) ?? dayOptions[0];
    if (!clicked) {
      setFeedback({ kind: 'error', message: `${info.label} için ${info.slotCount} ardışık slot bu günde bulunamadı.` });
      return;
    }
    setAssignState({ section, day: clicked.day, startTime: clicked.startTime, endTime: clicked.endTime, phase });
    setClassroomId('');
    setInstructorId(section.instructorId?.toString() ?? '');
  }, [timeSlots]);

  /* Hücre tıklama */
  const handleCellClick = useCallback((day: number, startTime: string) => {
    if (!selectedSection) return;
    openAssignDialog(selectedSection, day, startTime, currentPhase);
  }, [selectedSection, currentPhase, openAssignDialog]);

  /* Atama yap */
  const handleAssign = useCallback(() => {
    if (!assignState || !classroomId) return;
    setFeedback(null);
    const slots = [{ day: assignState.day, startTime: assignState.startTime, endTime: assignState.endTime }];
    assignMutation.mutate(
      {
        sectionId: assignState.section.sectionId,
        classroomId: Number(classroomId),
        scheduleSlots: slots,
        instructorId: instructorId ? Number(instructorId) : undefined,
      },
      {
        onSuccess: (result: AssignmentResult) => {
          const section = assignState.section;
          const finishedPhase = assignState.phase;

          if (result.conflicts.length > 0) {
            setFeedback({ kind: 'error', message: `Atandı — ${result.conflicts.length} çakışma var` });
          } else {
            setFeedback({ kind: 'success', message: `${finishedPhase === 'theory' ? 'Teori' : 'Lab'} başarıyla atandı.` });
          }

          setAssignState(null);

          // Lab fazı var mı kontrol et — teori bitti, lab'a geç
          const phases = getSectionPhases(section);
          if (finishedPhase === 'theory' && phases.includes('lab')) {
            setCurrentPhase('lab');
            setFeedback({ kind: 'success', message: 'Teori atandı. Şimdi Lab için hücre seçin.' });
            // selectedSection'ı koru — lab ataması için
          } else {
            // Tüm fazlar tamamlandı
            setSelectedSection(null);
            setCurrentPhase('theory');
          }
        },
        onError: () => setFeedback({ kind: 'error', message: 'Atama başarısız.' }),
      },
    );
  }, [assignState, classroomId, instructorId, assignMutation]);

  /* Kaldır */
  const handleUnassign = useCallback((sectionId: number) => {
    setFeedback(null);
    unassignMutation.mutate(sectionId, {
      onSuccess: () => setFeedback({ kind: 'success', message: 'Atama kaldırıldı.' }),
      onError: () => setFeedback({ kind: 'error', message: 'Kaldırılamadı.' }),
    });
  }, [unassignMutation]);

  /* Panel'den ders seç */
  const handleSelectSection = useCallback((section: FacultyPlanningSection) => {
    setSelectedSection(section);
    setAssignState(null);
    // İlk faz: teori varsa teori, yoksa lab
    const phases = getSectionPhases(section);
    setCurrentPhase(phases[0]);
  }, []);

  /* DnD: drag başladı → panel pointer-events kapat */
  const handleDragEnd = useCallback((event: DragEndEvent) => {
    setIsDraggingSection(false);
    const { active, over } = event;
    if (!over) return;

    const sectionData = active.data.current?.section as FacultyPlanningSection | undefined;
    const cellData = over.data.current as { day: number; startTime: string; endTime: string } | undefined;
    if (!sectionData || !cellData) return;

    setSelectedSection(sectionData);
    const phases = getSectionPhases(sectionData);
    const phase = phases[0];
    setCurrentPhase(phase);
    openAssignDialog(sectionData, cellData.day, cellData.startTime, phase);
  }, [openAssignDialog]);

  const toggleGroup = useCallback((code: string) => {
    setExpandedGroups(prev => {
      const next = new Set(prev);
      if (next.has(code)) next.delete(code); else next.add(code);
      return next;
    });
  }, []);

  /* Feedback auto-dismiss */
  useEffect(() => {
    if (!feedback) return;
    const t = setTimeout(() => setFeedback(null), 5000);
    return () => clearTimeout(t);
  }, [feedback]);

  /* Guards */
  if (!termId) return <div className="flex-1 flex items-center justify-center text-slate-400 text-base">Aktif dönem bulunamadı.</div>;
  if (sectionsQuery.isLoading || timeSlotsQuery.isLoading) return <Skeleton className="h-full rounded-xl" />;

  const progressPct = sections.length > 0 ? Math.round((scheduledCount / sections.length) * 100) : 0;
  const hasSelection = !!selectedSection;

  // Seçili section bilgisi
  const selectedPhases = selectedSection ? getSectionPhases(selectedSection) : [];
  const hasBothPhases = selectedPhases.length === 2;

  return (
    <DndContext
      sensors={sensors}
      onDragStart={() => setIsDraggingSection(true)}
      onDragEnd={handleDragEnd}
      onDragCancel={() => setIsDraggingSection(false)}
    >
      <div className="flex flex-col flex-1 min-h-0 gap-2 relative">

        {/* ── Feedback toast ── */}
        {feedback && (
          <div className={cn(
            'absolute top-3 right-3 z-[60] flex items-center gap-2 rounded-lg border px-4 py-2.5 text-sm shadow-lg',
            feedback.kind === 'error'
              ? 'bg-red-50 border-red-200 text-red-800'
              : 'bg-emerald-50 border-emerald-200 text-emerald-800',
          )}>
            {feedback.kind === 'error'
              ? <AlertCircle className="h-4 w-4 shrink-0" />
              : <CheckCircle2 className="h-4 w-4 shrink-0" />
            }
            <span className="font-medium">{feedback.message}</span>
            <button className="ml-2 opacity-50 hover:opacity-100" onClick={() => setFeedback(null)}>
              <X className="h-3.5 w-3.5" />
            </button>
          </div>
        )}

        {/* ── Toolbar ── */}
        <div className="flex items-center gap-2 shrink-0 flex-wrap">
          {/* Seçili ders göstergesi */}
          {selectedSection && (
            <div className="flex items-center gap-2 h-10 rounded-lg border-2 border-blue-300 bg-blue-50 px-3 text-sm shadow-sm">
              <span className="font-mono font-bold text-blue-800">{selectedSection.courseCode}</span>
              <span className="text-slate-600 text-xs truncate max-w-[120px]">{selectedSection.courseName}</span>
              <Badge variant="outline" className="text-[10px]">Ş.{selectedSection.sectionIndex}</Badge>

              {/* Faz göstergesi */}
              {hasBothPhases && (
                <div className="flex items-center gap-1 ml-1">
                  <div className={cn(
                    'flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-medium',
                    currentPhase === 'theory' ? 'bg-blue-200 text-blue-800' : 'bg-slate-100 text-slate-400',
                  )}>
                    <BookOpen className="h-3 w-3" />
                    {selectedSection.theoryHours}T
                  </div>
                  <div className={cn(
                    'flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-medium',
                    currentPhase === 'lab' ? 'bg-green-200 text-green-800' : 'bg-slate-100 text-slate-400',
                  )}>
                    <FlaskConical className="h-3 w-3" />
                    {selectedSection.labHours}L
                  </div>
                </div>
              )}

              <button className="p-0.5 rounded hover:bg-blue-100 ml-1" onClick={() => { setSelectedSection(null); setAssignState(null); setCurrentPhase('theory'); }}>
                <X className="h-3.5 w-3.5 text-blue-500" />
              </button>

              {!assignState && (
                <span className="text-xs text-blue-600 font-medium animate-pulse ml-1">
                  {currentPhase === 'lab' ? 'Lab için hücre seçin' : 'Takvimde hücre seçin'}
                </span>
              )}
            </div>
          )}

          {/* Bölüm filtresi */}
          <select
            className="h-9 rounded-lg border border-slate-200 bg-white px-2 text-sm"
            value={filterDept ?? ''}
            onChange={e => setFilterDept(e.target.value ? Number(e.target.value) : undefined)}
          >
            <option value="">Tüm Bölümler</option>
            {departments.map(d => <option key={d.departmentId} value={d.departmentId}>{d.departmentCode}</option>)}
          </select>

          <div className="flex-1" />

          {/* İlerleme */}
          <div className="flex items-center gap-2">
            <div className="h-2 w-24 bg-slate-100 rounded-full overflow-hidden">
              <div
                className="h-full rounded-full transition-all duration-300"
                style={{ width: `${progressPct}%`, backgroundColor: progressPct === 100 ? '#10b981' : '#003366' }}
              />
            </div>
            <span className="text-sm font-semibold text-slate-600 tabular-nums">{scheduledCount}/{sections.length}</span>
          </div>
        </div>

        {/* ── Assign dialog bar ── */}
        {assignState && (
          <div className={cn(
            'flex items-center gap-3 rounded-xl border-2 px-4 py-2.5 shrink-0 flex-wrap shadow-sm',
            assignState.phase === 'lab'
              ? 'border-green-300 bg-green-50/80'
              : 'border-blue-200 bg-blue-50/80',
          )}>
            <div className="flex items-center gap-2">
              <div className={cn(
                'w-2 h-2 rounded-full animate-pulse',
                assignState.phase === 'lab' ? 'bg-green-500' : 'bg-blue-500',
              )} />
              {assignState.phase === 'lab'
                ? <FlaskConical className="h-4 w-4 text-green-700" />
                : <BookOpen className="h-4 w-4 text-blue-700" />
              }
              <span className={cn(
                'text-sm font-semibold',
                assignState.phase === 'lab' ? 'text-green-900' : 'text-blue-900',
              )}>
                <span className="font-mono">{assignState.section.courseCode}</span>
                {' '}Ş.{assignState.section.sectionIndex}
                {' — '}{phaseInfo?.label}
              </span>
              <Badge variant="outline" className="text-xs">
                {WEEKDAY_LABELS[assignState.day]} {assignState.startTime.slice(0, 5)}-{assignState.endTime.slice(0, 5)}
              </Badge>
              <Badge variant="outline" className="text-xs">
                {phaseInfo?.slotCount} slot &middot; {phaseInfo?.classroomType}
              </Badge>
            </div>

            <div className="w-px h-6 bg-slate-200" />

            <select
              className="h-9 rounded-lg border border-slate-200 bg-white px-3 text-sm min-w-[160px]"
              value={classroomId}
              onChange={e => setClassroomId(e.target.value)}
            >
              <option value="">{phaseInfo?.classroomType === 'Lab' ? 'Lab seç...' : 'Derslik seç...'}</option>
              {(classroomsQuery.data ?? []).map(cr => (
                <option key={cr.classroomId} value={cr.classroomId}>{cr.name} ({cr.capacity})</option>
              ))}
            </select>

            <select
              className="h-9 rounded-lg border border-slate-200 bg-white px-3 text-sm min-w-[160px]"
              value={instructorId}
              onChange={e => setInstructorId(e.target.value)}
              disabled={instructorsQuery.isLoading}
            >
              <option value="">Hoca seç...</option>
              {(instructorsQuery.data ?? []).map(i => (
                <option key={i.instructorId} value={i.instructorId}>{i.fullName}</option>
              ))}
            </select>

            <Button
              size="sm"
              disabled={!classroomId || assignMutation.isPending}
              onClick={handleAssign}
              className={cn(
                'text-white px-6',
                assignState.phase === 'lab'
                  ? 'bg-green-600 hover:bg-green-700'
                  : 'bg-blue-600 hover:bg-blue-700',
              )}
            >
              {assignMutation.isPending ? 'Atanıyor...' : `${phaseInfo?.label} Ata`}
            </Button>
            <Button size="sm" variant="outline" onClick={() => setAssignState(null)}>İptal</Button>
          </div>
        )}

        {/* ── TAKVİM ── */}
        <div className="flex-1 min-h-0">
          <CalendarGrid
            sections={sections}
            timeSlots={timeSlots}
            onCellClick={handleCellClick}
            onUnassign={handleUnassign}
            isUnassigning={unassignMutation.isPending}
            isDraggingSection={isDraggingSection}
            hasSelection={hasSelection}
          />
        </div>

        {/* ── Floating ders listesi ── */}
        <FloatingPanel
          title="Atanmamış Dersler"
          badge={`${unscheduledSections.length}`}
          defaultPosition={{ x: 20, y: 160 }}
          minimized={panelMinimized}
          onMinimize={() => setPanelMinimized(prev => !prev)}
          pointerEventsNone={isDraggingSection}
        >
          {/* Arama */}
          <div className="px-3 py-2 border-b border-slate-100">
            <div className="relative">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
              <Input
                placeholder="Ders ara..."
                value={panelSearch}
                onChange={e => setPanelSearch(e.target.value)}
                className="h-8 pl-8 text-sm bg-slate-50 border-slate-200"
              />
            </div>
          </div>

          {/* İlerleme barı */}
          <div className="px-3 py-1.5 border-b border-slate-100 flex items-center gap-2">
            <div className="h-1.5 flex-1 bg-slate-100 rounded-full overflow-hidden">
              <div
                className="h-full rounded-full transition-all duration-300"
                style={{ width: `${progressPct}%`, backgroundColor: progressPct === 100 ? '#10b981' : '#003366' }}
              />
            </div>
            <span className="text-[11px] text-slate-400 tabular-nums whitespace-nowrap">
              {scheduledCount}/{sections.length}
            </span>
          </div>

          {/* Gruplu liste */}
          <ScrollArea className="flex-1">
            <div className="p-2 space-y-0.5">
              {courseGroups.length === 0 && (
                <p className="text-sm text-slate-400 text-center py-8">
                  {panelSearch ? 'Sonuç bulunamadı' : 'Tüm dersler atanmış'}
                </p>
              )}
              {courseGroups.map(group => {
                const isExpanded = expandedGroups.has(group.courseCode);
                return (
                  <div key={group.courseCode}>
                    <CourseGroupHeader
                      courseCode={group.courseCode}
                      courseName={group.courseName}
                      theoryHours={group.theoryHours}
                      labHours={group.labHours}
                      sectionCount={group.sections.length}
                      isExpanded={isExpanded}
                      onToggle={() => toggleGroup(group.courseCode)}
                    />
                    {isExpanded && (
                      <div className="ml-5 space-y-0.5 mb-1">
                        {group.sections
                          .sort((a, b) => a.sectionIndex - b.sectionIndex)
                          .map(s => (
                            <DraggableSectionItem
                              key={s.sectionId}
                              section={s}
                              isSelected={selectedSection?.sectionId === s.sectionId}
                              onSelect={handleSelectSection}
                            />
                          ))}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </ScrollArea>
        </FloatingPanel>
      </div>
    </DndContext>
  );
}
