import { useEffect, useMemo, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { AlertCircle, CheckCircle2, Search, X } from 'lucide-react';
import {
  useAssignSection, useAvailableClassrooms, useFacultyPlanningSections,
  useFacultyPlanningTimeSlots, useUnassignSection,
} from '../../hooks/useFacultyPlanning';
import { useDepartmentInstructors } from '../../hooks/useCourseOpening';
import { buildManualScheduleOptions, STATUS_CONFIG } from './sectionUtils';
import type { FacultyPlanningSection, FacultyPlanningTimeSlot } from '../../types/facultyPlanning';
import type { AssignmentResult } from '../../types/facultyPlanning';
import { CalendarGrid } from './CalendarGrid';

interface AssignState {
  section: FacultyPlanningSection;
  day: number;
  startTime: string;
  endTime: string;
}

interface ActionFeedback {
  kind: 'success' | 'error';
  message: string;
}

const WEEKDAY_LABELS: Record<number, string> = { 1: 'Pzt', 2: 'Sal', 3: 'Çar', 4: 'Per', 5: 'Cum' };

export function CalendarViewTab({ facultyId, termId, departments = [] }: {
  facultyId: number;
  termId: number | null;
  departments?: { departmentId: number; departmentCode: string; departmentName: string }[];
}) {
  const [selectedSection, setSelectedSection] = useState<FacultyPlanningSection | null>(null);
  const [assignState, setAssignState] = useState<AssignState | null>(null);
  const [classroomId, setClassroomId] = useState<string>('');
  const [instructorId, setInstructorId] = useState<string>('');
  const [feedback, setFeedback] = useState<ActionFeedback | null>(null);
  const [filterDept, setFilterDept] = useState<number | undefined>();
  const [filterStatus, setFilterStatus] = useState<string | undefined>();
  const [sectionSearch, setSectionSearch] = useState('');
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const [dropdownPos, setDropdownPos] = useState({ top: 0, left: 0, width: 500 });
  const inputRef = useRef<HTMLInputElement>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const sectionsQuery = useFacultyPlanningSections(facultyId, termId, filterDept, filterStatus, 1, 200);
  const timeSlotsQuery = useFacultyPlanningTimeSlots(termId);
  const assignMutation = useAssignSection();
  const unassignMutation = useUnassignSection();

  const sections = sectionsQuery.data?.items ?? [];
  const timeSlots: FacultyPlanningTimeSlot[] = timeSlotsQuery.data ?? [];
  const scheduledCount = sections.filter(s => s.status !== 'Unscheduled').length;
  const unscheduledCount = sections.length - scheduledCount;

  const filteredUnscheduled = useMemo(() => {
    const list = sections.filter(s => s.status === 'Unscheduled');
    if (!sectionSearch.trim()) return list;
    const q = sectionSearch.toLowerCase();
    return list.filter(s => s.courseCode.toLowerCase().includes(q) || s.courseName.toLowerCase().includes(q));
  }, [sections, sectionSearch]);

  // Dropdown pozisyonu
  useEffect(() => {
    if (dropdownOpen && inputRef.current) {
      const rect = inputRef.current.getBoundingClientRect();
      setDropdownPos({ top: rect.bottom + 2, left: rect.left, width: 500 });
    }
  }, [dropdownOpen]);

  // Dropdown dışına tıklayınca kapat
  useEffect(() => {
    if (!dropdownOpen) return;
    const handler = (e: MouseEvent) => {
      const target = e.target as Node;
      if (inputRef.current?.contains(target)) return;
      if (dropdownRef.current?.contains(target)) return;
      setDropdownOpen(false);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [dropdownOpen]);

  const expectedClassroomType = assignState?.section
    ? (assignState.section.labHours > 0 && assignState.section.theoryHours === 0 ? 'Lab' : 'Lecture')
    : undefined;
  const classroomsQuery = useAvailableClassrooms(
    termId, assignState?.day, assignState?.startTime, assignState?.endTime,
    assignState?.section.capacity, expectedClassroomType,
  );
  const instructorsQuery = useDepartmentInstructors(assignState?.section.departmentId ?? null);

  const handleCellClick = (day: number, startTime: string) => {
    if (!selectedSection) return;
    const requiredSlots = selectedSection.theoryHours + selectedSection.labHours || 1;
    const options = buildManualScheduleOptions(timeSlots, requiredSlots);
    const dayOptions = options.filter(o => o.day === day);
    const clicked = dayOptions.find(o => o.startTime.slice(0, 5) === startTime.slice(0, 5)) ?? dayOptions[0];
    if (!clicked) {
      setFeedback({ kind: 'error', message: `${requiredSlots} slot için bu günde uygun aralık bulunamadı.` });
      return;
    }
    setAssignState({ section: selectedSection, day: clicked.day, startTime: clicked.startTime, endTime: clicked.endTime });
    setClassroomId('');
    setInstructorId(selectedSection.instructorId?.toString() ?? '');
  };

  const handleAssign = () => {
    if (!assignState || !classroomId) return;
    setFeedback(null);
    const slots = [{ day: assignState.day, startTime: assignState.startTime, endTime: assignState.endTime }];
    assignMutation.mutate(
      { sectionId: assignState.section.sectionId, classroomId: Number(classroomId), scheduleSlots: slots, instructorId: instructorId ? Number(instructorId) : undefined },
      {
        onSuccess: (result: AssignmentResult) => {
          setFeedback({
            kind: result.conflicts.length > 0 ? 'error' : 'success',
            message: result.conflicts.length > 0 ? `Atandı — ${result.conflicts.length} çakışma var` : 'Şube başarıyla atandı.',
          });
          setAssignState(null);
          setSelectedSection(null);
        },
        onError: () => setFeedback({ kind: 'error', message: 'Şube atanamadı.' }),
      },
    );
  };

  const handleUnassign = (sectionId: number) => {
    setFeedback(null);
    unassignMutation.mutate(sectionId, {
      onSuccess: () => setFeedback({ kind: 'success', message: 'Atama kaldırıldı.' }),
      onError: () => setFeedback({ kind: 'error', message: 'Kaldırılamadı.' }),
    });
  };

  const handleSelectSection = (section: FacultyPlanningSection) => {
    setSelectedSection(section);
    setDropdownOpen(false);
    setSectionSearch('');
    setAssignState(null);
  };

  if (!termId) return <Card><CardContent className="py-8 text-center text-slate-500">Aktif dönem bulunamadı.</CardContent></Card>;
  if (sectionsQuery.isLoading || timeSlotsQuery.isLoading) return <Skeleton className="h-96 rounded-xl" />;

  return (
    <div className="flex flex-col flex-1 min-h-0 gap-2">
      {/* ── Feedback ── */}
      {feedback && (
        <div className={`flex items-center gap-2 rounded-lg border px-3 py-1.5 text-sm shrink-0 ${
          feedback.kind === 'error' ? 'bg-red-50 border-red-200 text-red-800' : 'bg-emerald-50 border-emerald-200 text-emerald-800'
        }`}>
          {feedback.kind === 'error' ? <AlertCircle className="h-3.5 w-3.5 shrink-0" /> : <CheckCircle2 className="h-3.5 w-3.5 shrink-0" />}
          <span>{feedback.message}</span>
          <button className="ml-auto opacity-50 hover:opacity-100" onClick={() => setFeedback(null)}><X className="h-3 w-3" /></button>
        </div>
      )}

      {/* ── Toolbar ── */}
      <div className="flex items-center gap-2 shrink-0">
        {/* Ders seçici */}
        {selectedSection ? (
          <div className="flex items-center gap-1.5 h-9 rounded-md border border-[#003366]/40 bg-[#003366]/5 px-3 text-sm">
            <span className="font-mono font-bold text-[#003366]">{selectedSection.courseCode}</span>
            <span className="text-slate-600 text-xs">{selectedSection.courseName}</span>
            <span className="text-slate-400 text-xs">Ş.{selectedSection.sectionIndex}</span>
            <Badge variant="outline" className="text-[9px]">{selectedSection.theoryHours}T+{selectedSection.labHours}L</Badge>
            <button className="ml-1 p-0.5 rounded hover:bg-slate-200" onClick={() => { setSelectedSection(null); setAssignState(null); }}>
              <X className="h-3 w-3 text-slate-500" />
            </button>
            <span className="text-xs text-blue-600 font-medium ml-2">← Takvimde tıklayın</span>
          </div>
        ) : (
          <div className="relative">
            <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-400" />
            <Input
              ref={inputRef}
              placeholder={`Ders seç (${unscheduledCount} atanmamış)...`}
              value={sectionSearch}
              onChange={e => { setSectionSearch(e.target.value); setDropdownOpen(true); }}
              onFocus={() => setDropdownOpen(true)}
              className="pl-8 h-9 w-72 text-sm border-slate-200"
            />
          </div>
        )}

        <div className="w-px h-6 bg-slate-200" />

        <select className="h-9 rounded-md border border-slate-200 bg-white px-2 text-sm" value={filterDept ?? ''} onChange={e => setFilterDept(e.target.value ? Number(e.target.value) : undefined)}>
          <option value="">Tüm Bölümler</option>
          {departments.map(d => <option key={d.departmentId} value={d.departmentId}>{d.departmentCode}</option>)}
        </select>
        <select className="h-9 rounded-md border border-slate-200 bg-white px-2 text-sm" value={filterStatus ?? ''} onChange={e => setFilterStatus(e.target.value || undefined)}>
          <option value="">Tüm Durumlar</option>
          {Object.entries(STATUS_CONFIG).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
        </select>

        <span className="text-xs text-slate-500 ml-auto tabular-nums">{scheduledCount}/{sections.length}</span>
      </div>

      {/* ── Assign dialog ── */}
      {assignState && (
        <div className="flex items-center gap-3 rounded-lg border border-[#003366]/30 bg-[#003366]/5 px-4 py-2 shrink-0 flex-wrap">
          <span className="text-sm font-medium text-[#003366]">
            <span className="font-mono font-bold">{assignState.section.courseCode}</span> Ş.{assignState.section.sectionIndex}
            {' — '}{WEEKDAY_LABELS[assignState.day]} {assignState.startTime.slice(0, 5)}-{assignState.endTime.slice(0, 5)}
          </span>
          <select className="h-8 rounded border border-slate-200 bg-white px-2 text-sm" value={classroomId} onChange={e => setClassroomId(e.target.value)}>
            <option value="">Derslik seç...</option>
            {(classroomsQuery.data ?? []).map(cr => (
              <option key={cr.classroomId} value={cr.classroomId}>{cr.name} ({cr.capacity})</option>
            ))}
          </select>
          <select className="h-8 rounded border border-slate-200 bg-white px-2 text-sm" value={instructorId} onChange={e => setInstructorId(e.target.value)} disabled={instructorsQuery.isLoading}>
            <option value="">Hoca seç...</option>
            {(instructorsQuery.data ?? []).map(i => (
              <option key={i.instructorId} value={i.instructorId}>{i.fullName}</option>
            ))}
          </select>
          <Button size="sm" disabled={!classroomId || assignMutation.isPending} onClick={handleAssign} style={{ backgroundColor: '#003366' }} className="text-white hover:opacity-90">
            {assignMutation.isPending ? 'Atanıyor...' : 'Ata'}
          </Button>
          <Button size="sm" variant="outline" onClick={() => setAssignState(null)}>İptal</Button>
        </div>
      )}

      {/* ── Takvim — TAM ALAN ── */}
      <div className="flex-1 min-h-0">
        <CalendarGrid
          sections={sections}
          timeSlots={timeSlots}
          onCellClick={handleCellClick}
          onUnassign={handleUnassign}
          isUnassigning={unassignMutation.isPending}
        />
      </div>

      {/* ── Dropdown portal — takvimin ÜSTÜNE biner, takvimi kaydırmaz ── */}
      {dropdownOpen && !selectedSection && (
        <div
          ref={dropdownRef}
          className="fixed max-h-80 overflow-auto bg-white border border-slate-200 rounded-lg shadow-xl z-[100]"
          style={{ top: dropdownPos.top, left: dropdownPos.left, width: dropdownPos.width }}
        >
          {filteredUnscheduled.length === 0 ? (
            <p className="text-sm text-slate-400 text-center py-6">Sonuç yok</p>
          ) : (
            filteredUnscheduled.map(s => (
              <button
                key={s.sectionId}
                className="w-full flex items-center gap-2 px-3 py-2.5 text-left hover:bg-blue-50 border-b border-slate-100 last:border-b-0 transition-colors"
                onClick={() => handleSelectSection(s)}
              >
                <span className="font-mono text-xs font-bold text-[#003366] w-16 shrink-0">{s.courseCode}</span>
                <span className="text-sm text-slate-700 truncate flex-1">{s.courseName}</span>
                <span className="text-[10px] text-slate-400 shrink-0">Ş.{s.sectionIndex}</span>
                <Badge variant="outline" className="text-[9px] shrink-0">{s.departmentCode}</Badge>
                <Badge variant="outline" className="text-[9px] shrink-0">{s.theoryHours}T+{s.labHours}L</Badge>
              </button>
            ))
          )}
        </div>
      )}
    </div>
  );
}
