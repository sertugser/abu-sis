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
  useOpenedCourses,
  useUpdateOpenedSection,
} from '../hooks/useCourseOpening';

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
  const instructorsQuery = useDepartmentInstructors(deptId, termId);
  const bulkAssignMut = useBulkAssignInstructor(deptId ?? 0, termId ?? 0);
  const updateMut = useUpdateOpenedSection(deptId ?? 0, termId ?? 0);

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

  useEffect(() => {
    if (!toast) return;
    const duration = toast.type === 'error' ? 8000 : 4000;
    const t = setTimeout(() => setToast(null), duration);
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

  const toggleAll = useCallback(
    (visibleIds: number[]) => {
      setSelectedSectionIds(prev => {
        const allChecked = visibleIds.every(id => prev.has(id));
        if (allChecked) {
          const next = new Set(prev);
          visibleIds.forEach(id => next.delete(id));
          return next;
        }
        const next = new Set(prev);
        visibleIds.forEach(id => next.add(id));
        return next;
      });
    },
    [],
  );

  const handleInlineAssign = useCallback(
    (sectionId: number, instructorId: number) => {
      updateMut.mutate(
        { sectionId, instructorId },
        {
          onSuccess: () => {
            setInlineAssignments(prev => new Map(prev).set(sectionId, instructorId));
            setToast({ type: 'success', message: 'Öğretim üyesi atandı.' });
          },
          onError: (err: unknown) => {
            setInlineAssignments(prev => {
              const next = new Map(prev);
              next.delete(sectionId);
              return next;
            });
            setToast({
              type: 'error',
              message: err instanceof Error ? err.message : 'Atama başarısız.',
            });
          },
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
              ? `${data.successCount} başarılı, ${data.failedCount} başarısız: ${data.errors.join('; ')}`
              : `${data.successCount} şube başarıyla atandı.`;
          setToast({ type: data.failedCount > 0 ? 'error' : 'success', message: msg });
          setSelectedSectionIds(new Set());
        },
        onError: (err: unknown) =>
          setToast({ type: 'error', message: err instanceof Error ? err.message : 'Toplu atama başarısız.' }),
      },
    );
  }, [selectedInstructorId, selectedSectionIds, bulkAssignMut]);

  const handleClearInvisibleSelections = useCallback((visibleIds: number[]) => {
    const visibleSet = new Set(visibleIds);
    setSelectedSectionIds(prev => {
      const next = new Set<number>();
      prev.forEach(id => { if (visibleSet.has(id)) next.add(id); });
      return next;
    });
  }, []);

  const handleCopyPreviousTerm = useCallback(() => {
    setToast({ type: 'error', message: 'Önceki dönemden kopyalama henüz desteklenmiyor.' });
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
    <div className="flex flex-col h-[calc(100vh-120px)] space-y-3 overflow-hidden">
      {/* Toast */}
      {toast && (
        <Alert variant={toast.type === 'error' ? 'destructive' : 'default'} className="py-2 shrink-0">
          {toast.type === 'error' ? (
            <AlertCircle className="h-4 w-4" />
          ) : (
            <CheckCircle2 className="h-4 w-4" />
          )}
          <AlertDescription className="text-xs">{toast.message}</AlertDescription>
        </Alert>
      )}

      {/* Split Panel — fills remaining height */}
      <div className="flex-1 flex flex-col lg:flex-row gap-3 min-h-0">
        {/* Left: Instructor List (38%) */}
        <div className="w-full lg:w-[38%] min-w-0 lg:min-w-[280px] flex flex-col min-h-0">
          <InstructorListPanel
            instructors={instructors}
            selectedId={selectedInstructorId}
            onSelect={setSelectedInstructorId}
            isLoading={instructorsQuery.isLoading}
            faculties={availableFaculties}
            selectedFacultyId={selectedFacultyId}
            onFacultyChange={(id) => {
              setSelectedFacultyId(id);
              setSelectedDeptId(null);
            }}
            departments={filteredDepartments}
            selectedDeptId={deptId}
            onDeptChange={setSelectedDeptId}
          />
        </div>

        {/* Right: Section Table (62%) */}
        <div className="flex-1 flex flex-col min-h-0">
          <SectionAssignmentTable
            sections={sections}
            instructors={instructors}
            selectedInstructorId={selectedInstructorId}
            inlineAssignments={inlineAssignments}
            onInlineAssign={handleInlineAssign}
            selectedSectionIds={selectedSectionIds}
            onClearInvisibleSelections={handleClearInvisibleSelections}
            onToggleSection={toggleSection}
            onToggleAll={toggleAll}
            onBulkAssign={handleBulkAssign}
            onCopyPreviousTerm={handleCopyPreviousTerm}
            bulkAssigning={bulkAssignMut.isPending}
            isLoading={openedQuery.isLoading}
          />
        </div>
      </div>
    </div>
  );
}
