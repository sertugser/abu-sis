import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
import { AlertCircle, CheckCircle2 } from 'lucide-react';
import { InstructorListPanel } from '../components/instructor-assignment/InstructorListPanel';
import { SectionAssignmentTable } from '../components/instructor-assignment/SectionAssignmentTable';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useAdminFaculties, useAdminDepartmentsByStructure } from '../hooks/useAdminAcademicStructure';
import {
  useBulkAssignInstructor,
  useDepartmentInstructors,
  useInstructorWorkload,
  useOpenedCourses,
  useUpdateOpenedSection,
} from '../hooks/useCourseOpening';
import type { InstructorWorkloadDto } from '../types/courseOpening';

export default function InstructorAssignmentPage() {
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();
  const { data: faculties } = useAdminFaculties();
  const { data: structureDepartments } = useAdminDepartmentsByStructure();
  const departments = contextQuery.data?.accessibleDepartments ?? [];

  const [selectedFacultyId, setSelectedFacultyId] = useState<number | null>(null);
  const [selectedDeptId, setSelectedDeptId] = useState<number | null>(null);
  const [selectedInstructorId, setSelectedInstructorId] = useState<number | null>(null);
  const [selectedSectionIds, setSelectedSectionIds] = useState<Set<number>>(new Set());
  const [inlineAssignments, setInlineAssignments] = useState<Map<number, number>>(new Map());
  const [toast, setToast] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  const deptId = selectedDeptId ?? departments[0]?.departmentId ?? null;
  const termId = activeTerm?.id ?? null;

  const prevDeptIdRef = useRef(deptId);
  if (prevDeptIdRef.current !== deptId) {
    prevDeptIdRef.current = deptId;
    setSelectedSectionIds(new Set());
    setInlineAssignments(new Map());
    setSelectedInstructorId(null);
  }

  const openedQuery = useOpenedCourses(deptId, termId);
  const instructorsQuery = useDepartmentInstructors(deptId);
  const workloadQuery = useInstructorWorkload(selectedInstructorId, termId);
  const bulkAssignMut = useBulkAssignInstructor(deptId ?? 0, termId ?? 0);
  const updateMut = useUpdateOpenedSection(deptId ?? 0, termId ?? 0);

  // Collect workloads: we only have detailed workload for the selected instructor
  const workloads = useMemo(() => {
    const map = new Map<number, InstructorWorkloadDto>();
    if (workloadQuery.data) map.set(workloadQuery.data.instructorId, workloadQuery.data);
    return map;
  }, [workloadQuery.data]);

  // Filter departments by faculty
  const availableFaculties = useMemo(() => {
    if (!faculties || !structureDepartments) return [];
    const accessibleDeptIds = new Set(departments.map(d => d.departmentId));
    const facultyIds = new Set(
      structureDepartments.filter(sd => accessibleDeptIds.has(sd.id)).map(sd => sd.facultyId),
    );
    return faculties.filter(f => facultyIds.has(f.id));
  }, [faculties, structureDepartments, departments]);

  const filteredDepartments = useMemo(() => {
    if (!selectedFacultyId || !structureDepartments) return departments;
    const deptIdsInFaculty = new Set(
      structureDepartments.filter(sd => sd.facultyId === selectedFacultyId).map(sd => sd.id),
    );
    return departments.filter(d => deptIdsInFaculty.has(d.departmentId));
  }, [departments, structureDepartments, selectedFacultyId]);

  // Auto-hide toast
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 4000);
    return () => clearTimeout(t);
  }, [toast]);

  const sections = openedQuery.data?.sections ?? [];
  const instructors = instructorsQuery.data ?? [];

  const toggleSection = useCallback((id: number) => {
    setSelectedSectionIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  const toggleAll = useCallback(() => {
    setSelectedSectionIds(prev => {
      if (prev.size === sections.length) return new Set();
      return new Set(sections.map(s => s.sectionId));
    });
  }, [sections]);

  const handleInlineAssign = useCallback(
    (sectionId: number, instructorId: number) => {
      setInlineAssignments(prev => new Map(prev).set(sectionId, instructorId));
      // Immediately apply single inline assignment
      updateMut.mutate(
        { sectionId, instructorId },
        {
          onSuccess: () => setToast({ type: 'success', message: 'Ogretim uyesi atandi.' }),
          onError: (err: unknown) =>
            setToast({
              type: 'error',
              message: err instanceof Error ? err.message : 'Atama basarisiz.',
            }),
        },
      );
    },
    [updateMut],
  );

  const handleBulkAssign = useCallback(() => {
    if (!selectedInstructorId || selectedSectionIds.size === 0) return;
    const assignments = [...selectedSectionIds].map(sectionId => ({
      sectionId,
      instructorId: selectedInstructorId,
    }));
    bulkAssignMut.mutate(
      { assignments },
      {
        onSuccess: data => {
          const msg =
            data.failedCount > 0
              ? `${data.successCount} basarili, ${data.failedCount} basarisiz: ${data.errors.join('; ')}`
              : `${data.successCount} sube basariyla atandi.`;
          setToast({ type: data.failedCount > 0 ? 'error' : 'success', message: msg });
          setSelectedSectionIds(new Set());
        },
        onError: (err: unknown) =>
          setToast({ type: 'error', message: err instanceof Error ? err.message : 'Toplu atama basarisiz.' }),
      },
    );
  }, [selectedInstructorId, selectedSectionIds, bulkAssignMut]);

  const handleCopyPreviousTerm = useCallback(() => {
    setToast({ type: 'error', message: 'Onceki donemden kopyalama henuz desteklenmiyor.' });
  }, []);

  if (contextQuery.isLoading) {
    return (
      <div className="p-6 space-y-4">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-[500px] w-full" />
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full p-4 space-y-3">
      {/* Page Title + Filter */}
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-lg font-bold">Ogretim Elemani Atama</h2>
        <div className="flex items-center gap-1.5 text-xs">
          <label className="font-medium">Fakulte:</label>
          <select
            className="h-7 rounded-md border border-input bg-background px-2 text-xs"
            value={selectedFacultyId ?? ''}
            onChange={e => {
              setSelectedFacultyId(e.target.value ? Number(e.target.value) : null);
              setSelectedDeptId(null);
            }}
          >
            <option value="">Tumu</option>
            {availableFaculties.map(f => (
              <option key={f.id} value={f.id}>
                {f.name}
              </option>
            ))}
          </select>
        </div>
        <div className="flex items-center gap-1.5 text-xs">
          <label className="font-medium">Bolum:</label>
          <select
            className="h-7 rounded-md border border-input bg-background px-2 text-xs"
            value={selectedDeptId ?? ''}
            onChange={e => setSelectedDeptId(Number(e.target.value))}
          >
            {filteredDepartments.map(d => (
              <option key={d.departmentId} value={d.departmentId}>
                {d.departmentCode} - {d.departmentName}
              </option>
            ))}
          </select>
        </div>
        {activeTerm && (
          <span className="text-xs text-muted-foreground ml-auto">
            Donem: <strong>{activeTerm.name}</strong>
          </span>
        )}
      </div>

      {/* Toast */}
      {toast && (
        <Alert variant={toast.type === 'error' ? 'destructive' : 'default'} className="py-2">
          {toast.type === 'error' ? (
            <AlertCircle className="h-4 w-4" />
          ) : (
            <CheckCircle2 className="h-4 w-4" />
          )}
          <AlertDescription className="text-xs">{toast.message}</AlertDescription>
        </Alert>
      )}

      {/* Split Panel */}
      <div className="flex-1 flex flex-col lg:flex-row gap-3 min-h-0">
        {/* Left: Instructor List (40%) */}
        <div className="w-full lg:w-[40%] min-w-0 lg:min-w-[280px]">
          <InstructorListPanel
            instructors={instructors}
            workloads={workloads}
            selectedId={selectedInstructorId}
            onSelect={setSelectedInstructorId}
          />
        </div>

        {/* Right: Section Table (60%) */}
        <div className="flex-1">
          <SectionAssignmentTable
            sections={sections}
            instructors={instructors}
            selectedInstructorId={selectedInstructorId}
            inlineAssignments={inlineAssignments}
            onInlineAssign={handleInlineAssign}
            selectedSectionIds={selectedSectionIds}
            onToggleSection={toggleSection}
            onToggleAll={toggleAll}
            onBulkAssign={handleBulkAssign}
            onCopyPreviousTerm={handleCopyPreviousTerm}
            bulkAssigning={bulkAssignMut.isPending}
          />
        </div>
      </div>
    </div>
  );
}
