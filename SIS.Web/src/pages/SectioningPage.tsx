import { useCallback, useEffect, useMemo, useState, type ChangeEvent } from 'react';
import { AlertCircle } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
import { getAcademicOperationsErrorMessage } from '@/api/academicOperations.api';
import {
  useAcademicOperationsContext,
  useDepartmentClassLists,
} from '@/hooks/useAcademicOperations';
import { useActiveTerm } from '@/hooks/useActiveTerm';
import { useAdminDepartmentsByStructure } from '@/hooks/useAdminAcademicStructure';
import { useFacultyLookup } from '@/hooks/useAdminLookup';
import {
  useUpdateOpenedSection,
  useBulkAssignInstructor,
  useCloseSection,
  useReactivateSection,
  useDeleteAllSectionsHard,
  useTransferStudentsBetweenSections,
  useAutoSplitCourse,
  useOpenCourse,
} from '@/hooks/useCourseOpening';
import { usePermissions } from '@/hooks/usePermissions';
import type { AutoSplitStrategy } from '@/types/courseOpening';
import type { DepartmentSectionClassList } from '@/types/academicOperations';
import { useCourseGroups } from './sectioning/hooks/useCourseGroups';
import { FilterPanel } from './sectioning/components/FilterPanel';
import { SectionsPanel } from './sectioning/components/SectionsPanel';
import { CoursesPanel } from './sectioning/components/CoursesPanel';
import { StudentsPanel } from './sectioning/components/StudentsPanel';
import { BottomToolbar } from './sectioning/components/BottomToolbar';
import { AssignInstructorDialog } from './sectioning/components/AssignInstructorDialog';
import { BulkAssignInstructorDialog } from './sectioning/components/BulkAssignInstructorDialog';
import { EditCapacityDialog, type CapacityUpdate } from './sectioning/components/EditCapacityDialog';
import { ConfirmActionDialog } from './sectioning/components/ConfirmActionDialog';
import { AutoSplitConfirmDialog } from './sectioning/components/AutoSplitConfirmDialog';
import { AddSectionDialog, type AddSectionPayload } from './sectioning/components/AddSectionDialog';
import type { SplitStrategy } from './sectioning/constants';
import type { CourseGroup } from './sectioning/types';

type ConfirmAction =
  | { type: 'closeSection'; section: DepartmentSectionClassList }
  | { type: 'reactivateSection'; section: DepartmentSectionClassList }
  | { type: 'deleteAllSections'; course: CourseGroup };

export default function SectioningPage() {
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();
  const { data: faculties } = useFacultyLookup();
  const { data: structureDepartments } = useAdminDepartmentsByStructure();

  const accessibleDepartments = contextQuery.data?.accessibleDepartments;
  const departments = useMemo(() => accessibleDepartments ?? [], [accessibleDepartments]);

  // ── Filter state ──
  const [selectedFacultyId, setSelectedFacultyId] = useState<number | null>(null);
  const [selectedDeptId, setSelectedDeptId] = useState<number | null>(null);
  const [courseCodeFilter, setCourseCodeFilter] = useState('');
  const [courseNameFilter, setCourseNameFilter] = useState('');
  const [instructorFilter, setInstructorFilter] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');

  // ── Selection state ──
  const [selectedCourseCode, setSelectedCourseCode] = useState<string | null>(null);
  const [selectedSectionId, setSelectedSectionId] = useState<number | null>(null);
  const [selectedStudentIds, setSelectedStudentIds] = useState<Set<number>>(() => new Set());

  // ── Bulk toolbar state ──
  const [splitCount, setSplitCount] = useState('3');
  const [splitStrategy, setSplitStrategy] = useState<SplitStrategy>('Equal');
  const [moveToSectionNumber, setMoveToSectionNumber] = useState('1');
  const [autoSplitConfirmOpen, setAutoSplitConfirmOpen] = useState(false);
  const [addSectionOpen, setAddSectionOpen] = useState(false);

  // ── Dialog state ──
  const [assignInstructorTarget, setAssignInstructorTarget] = useState<DepartmentSectionClassList | null>(null);
  const [bulkAssignOpen, setBulkAssignOpen] = useState(false);
  const [editCapacityTarget, setEditCapacityTarget] = useState<DepartmentSectionClassList | null>(null);
  const [confirmAction, setConfirmAction] = useState<ConfirmAction | null>(null);

  useEffect(() => {
    const t = setTimeout(() => {
      const combined = [courseCodeFilter, courseNameFilter, instructorFilter]
        .map(s => s.trim())
        .filter(Boolean)
        .join(' ');
      setDebouncedQuery(combined);
    }, 350);
    return () => clearTimeout(t);
  }, [courseCodeFilter, courseNameFilter, instructorFilter]);

  const accessibleDeptIds = useMemo(() => new Set(departments.map(d => d.departmentId)), [departments]);

  const availableFaculties = useMemo(() => {
    if (!faculties || !structureDepartments) return [];
    const facultyIds = new Set(
      structureDepartments
        .filter(sd => accessibleDeptIds.has(sd.id))
        .map(sd => sd.facultyId),
    );
    return faculties.filter(f => facultyIds.has(f.id));
  }, [faculties, structureDepartments, accessibleDeptIds]);

  const filteredDepartments = useMemo(() => {
    if (!selectedFacultyId || !structureDepartments) return departments;
    const deptIdsInFaculty = new Set(
      structureDepartments
        .filter(sd => sd.facultyId === selectedFacultyId)
        .map(sd => sd.id),
    );
    return departments.filter(d => deptIdsInFaculty.has(d.departmentId));
  }, [departments, structureDepartments, selectedFacultyId]);

  const currentDeptId = useMemo(() => {
    if (selectedDeptId && filteredDepartments.some(d => d.departmentId === selectedDeptId)) {
      return selectedDeptId;
    }
    return filteredDepartments[0]?.departmentId ?? null;
  }, [selectedDeptId, filteredDepartments]);

  const handleFacultyChange = useCallback((event: ChangeEvent<HTMLSelectElement>) => {
    const value = event.target.value ? Number(event.target.value) : null;
    setSelectedFacultyId(value);
    setSelectedDeptId(null);
    setSelectedCourseCode(null);
    setSelectedSectionId(null);
  }, []);

  const handleDeptChange = useCallback((event: ChangeEvent<HTMLSelectElement>) => {
    const value = event.target.value ? Number(event.target.value) : null;
    setSelectedDeptId(value);
    setSelectedCourseCode(null);
    setSelectedSectionId(null);
  }, []);

  const classListsQuery = useDepartmentClassLists(currentDeptId, activeTerm?.id, true, debouncedQuery || undefined);
  const sections = useMemo(() => classListsQuery.data?.sections ?? [], [classListsQuery.data]);
  const courseGroups = useCourseGroups(sections);

  // ── Sync derived selection state ──
  useEffect(() => {
    if (selectedCourseCode && !courseGroups.some(g => g.courseCode === selectedCourseCode)) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setSelectedCourseCode(null);
      setSelectedSectionId(null);
    }
  }, [courseGroups, selectedCourseCode]);

  const selectedCourse = courseGroups.find(g => g.courseCode === selectedCourseCode) ?? null;

  useEffect(() => {
    if (
      selectedSectionId !== null &&
      selectedCourse &&
      !selectedCourse.sections.some(s => s.sectionId === selectedSectionId)
    ) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setSelectedSectionId(null);
    }
  }, [selectedCourse, selectedSectionId]);

  const selectedSection = selectedCourse?.sections.find(s => s.sectionId === selectedSectionId) ?? null;

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setSelectedStudentIds(new Set());
  }, [selectedSectionId]);

  const toggleStudent = (studentId: number) => {
    setSelectedStudentIds(prev => {
      const next = new Set(prev);
      if (next.has(studentId)) next.delete(studentId);
      else next.add(studentId);
      return next;
    });
  };

  const toggleAllStudents = (checked: boolean) => {
    if (!selectedSection) return;
    setSelectedStudentIds(checked ? new Set(selectedSection.students.map(s => s.studentId)) : new Set());
  };

  // ── Mutations (Faz 2-4) ──
  const updateSection = useUpdateOpenedSection(currentDeptId ?? 0, activeTerm?.id ?? 0);
  const bulkAssign = useBulkAssignInstructor(currentDeptId ?? 0, activeTerm?.id ?? 0);
  const closeSection = useCloseSection(currentDeptId ?? 0, activeTerm?.id ?? 0);
  const reactivateSection = useReactivateSection(currentDeptId ?? 0, activeTerm?.id ?? 0);
  const deleteAllSections = useDeleteAllSectionsHard(currentDeptId ?? 0, activeTerm?.id ?? 0);
  const transferStudents = useTransferStudentsBetweenSections(currentDeptId ?? 0, activeTerm?.id ?? 0);
  const autoSplit = useAutoSplitCourse(currentDeptId ?? 0, activeTerm?.id ?? 0);
  const openCourse = useOpenCourse(currentDeptId ?? 0, activeTerm?.id ?? 0);

  const { hasPermission } = usePermissions();
  const canTransfer = hasPermission('menu.subelendirme-islemleri.transfer');
  const canAutoSplit = hasPermission('menu.subelendirme-islemleri.auto-split');

  const handleAssignInstructor = (instructorId: number) => {
    if (!assignInstructorTarget) return;
    updateSection.mutate(
      { sectionId: assignInstructorTarget.sectionId, instructorId },
      { onSuccess: () => setAssignInstructorTarget(null) },
    );
  };

  const handleBulkAssignInstructor = (instructorId: number) => {
    if (!selectedCourse) return;
    const assignments = selectedCourse.sections.map(s => ({ sectionId: s.sectionId, instructorId }));
    bulkAssign.mutate(
      { assignments },
      { onSuccess: () => setBulkAssignOpen(false) },
    );
  };

  const handleEditCapacity = (update: CapacityUpdate) => {
    if (!editCapacityTarget) return;
    updateSection.mutate(
      {
        sectionId: editCapacityTarget.sectionId,
        capacity: update.capacity,
        internalQuota: update.internalQuota,
        externalQuota: update.externalQuota,
      },
      { onSuccess: () => setEditCapacityTarget(null) },
    );
  };

  const handleAddSection = (payload: AddSectionPayload) => {
    if (!selectedCourse) return;
    openCourse.mutate(
      {
        courseId: selectedCourse.courseId,
        sectionCount: 1,
        capacityPerSection: payload.capacityPerSection,
        internalQuota: payload.internalQuota ?? undefined,
        externalQuota: payload.externalQuota ?? undefined,
        instructorId: payload.instructorId ?? undefined,
        includeCrossLists: false,
      },
      { onSuccess: () => setAddSectionOpen(false) },
    );
  };

  const handleConfirmAutoSplit = () => {
    if (!selectedCourse || !activeTerm) return;
    const targetCount = Number(splitCount);
    if (!Number.isInteger(targetCount) || targetCount < 1) return;
    autoSplit.mutate(
      {
        courseId: selectedCourse.courseId,
        termId: activeTerm.id,
        targetSectionCount: targetCount,
        strategy: splitStrategy as AutoSplitStrategy,
      },
      {
        onSuccess: () => {
          setAutoSplitConfirmOpen(false);
          setSelectedStudentIds(new Set());
        },
      },
    );
  };

  const handleTransferStudents = (targetSectionId: number) => {
    if (!selectedSectionId || selectedStudentIds.size === 0) return;
    transferStudents.mutate(
      {
        sourceSectionId: selectedSectionId,
        targetSectionId,
        studentIds: Array.from(selectedStudentIds),
      },
      {
        onSuccess: () => {
          setSelectedStudentIds(new Set());
        },
      },
    );
  };

  const handleConfirmAction = () => {
    if (!confirmAction) return;
    switch (confirmAction.type) {
      case 'closeSection':
        closeSection.mutate(
          {
            sectionId: confirmAction.section.sectionId,
            label: `${confirmAction.section.courseCode} — ${confirmAction.section.sectionNumber}. şube`,
          },
          { onSuccess: () => setConfirmAction(null) },
        );
        break;
      case 'reactivateSection':
        reactivateSection.mutate(confirmAction.section.sectionId, { onSuccess: () => setConfirmAction(null) });
        break;
      case 'deleteAllSections':
        deleteAllSections.mutate(confirmAction.course.courseId, { onSuccess: () => setConfirmAction(null) });
        break;
    }
  };

  // ── Loading / Error ──
  if (contextQuery.isLoading) {
    return (
      <div className="space-y-3">
        <Skeleton className="h-[280px] rounded-lg" />
        <Skeleton className="h-[460px] rounded-lg" />
        <Skeleton className="h-[80px] rounded-lg" />
      </div>
    );
  }

  if (contextQuery.isError || !contextQuery.data) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Sayfa yüklenemedi</AlertTitle>
        <AlertDescription>
          {getAcademicOperationsErrorMessage(contextQuery.error, 'Yetkili kapsam bilgileri alınamadı.')}
        </AlertDescription>
      </Alert>
    );
  }

  const isConfirming =
    confirmAction?.type === 'closeSection' ? closeSection.isPending :
    confirmAction?.type === 'reactivateSection' ? reactivateSection.isPending :
    confirmAction?.type === 'deleteAllSections' ? deleteAllSections.isPending :
    false;

  return (
    <div className="space-y-3">
      {classListsQuery.isError && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Hata</AlertTitle>
          <AlertDescription>
            {getAcademicOperationsErrorMessage(classListsQuery.error, 'Şube listesi yüklenemedi.')}
          </AlertDescription>
        </Alert>
      )}

      {/* ── ÜST: Filtreler + Şubeler ── */}
      <div className="grid gap-3 lg:grid-cols-2">
        <FilterPanel
          activeTermName={activeTerm?.name ?? '—'}
          availableFaculties={availableFaculties}
          selectedFacultyId={selectedFacultyId}
          onFacultyChange={handleFacultyChange}
          filteredDepartments={filteredDepartments}
          currentDeptId={currentDeptId}
          onDeptChange={handleDeptChange}
          courseCodeFilter={courseCodeFilter}
          setCourseCodeFilter={setCourseCodeFilter}
          courseNameFilter={courseNameFilter}
          setCourseNameFilter={setCourseNameFilter}
          instructorFilter={instructorFilter}
          setInstructorFilter={setInstructorFilter}
          onClearAll={() => {
            setCourseCodeFilter('');
            setCourseNameFilter('');
            setInstructorFilter('');
          }}
        />

        <SectionsPanel
          course={selectedCourse}
          selectedSectionId={selectedSectionId}
          onSelectSection={setSelectedSectionId}
          isLoading={classListsQuery.isLoading}
          onAddSection={() => setAddSectionOpen(true)}
          onBulkAssignInstructor={() => setBulkAssignOpen(true)}
          onDeleteAllSections={() =>
            selectedCourse && setConfirmAction({ type: 'deleteAllSections', course: selectedCourse })
          }
          onAssignInstructor={setAssignInstructorTarget}
          onEditCapacity={setEditCapacityTarget}
          onCloseSection={section => setConfirmAction({ type: 'closeSection', section })}
          onReactivateSection={section => setConfirmAction({ type: 'reactivateSection', section })}
        />
      </div>

      {/* ── ALT: Dersler + Şube Öğrencileri ── */}
      <div className="grid gap-3 lg:grid-cols-2">
        <CoursesPanel
          courses={courseGroups}
          selectedCourseCode={selectedCourseCode}
          onSelectCourse={code => {
            setSelectedCourseCode(code);
            setSelectedSectionId(null);
          }}
          isLoading={classListsQuery.isLoading}
        />

        <StudentsPanel
          section={selectedSection}
          selectedStudentIds={selectedStudentIds}
          onToggleStudent={toggleStudent}
          onToggleAll={toggleAllStudents}
        />
      </div>

      {/* ── Alt toolbar ── */}
      <BottomToolbar
        course={selectedCourse}
        splitCount={splitCount}
        setSplitCount={setSplitCount}
        splitStrategy={splitStrategy}
        setSplitStrategy={value => setSplitStrategy(value as SplitStrategy)}
        moveToSectionNumber={moveToSectionNumber}
        setMoveToSectionNumber={setMoveToSectionNumber}
        selectedStudentCount={selectedStudentIds.size}
        canAutoSplit={canAutoSplit}
        isAutoSplitting={autoSplit.isPending}
        onAutoSplit={() => setAutoSplitConfirmOpen(true)}
        sourceSectionId={selectedSectionId}
        canTransfer={canTransfer}
        isTransferring={transferStudents.isPending}
        onTransferStudents={handleTransferStudents}
      />

      {/* ── Dialogs (Faz 2 operasyonları) ── */}
      <AssignInstructorDialog
        open={!!assignInstructorTarget}
        section={assignInstructorTarget}
        departmentId={currentDeptId}
        termId={activeTerm?.id ?? null}
        isSubmitting={updateSection.isPending}
        onClose={() => setAssignInstructorTarget(null)}
        onSubmit={handleAssignInstructor}
      />

      <BulkAssignInstructorDialog
        open={bulkAssignOpen}
        course={selectedCourse}
        departmentId={currentDeptId}
        termId={activeTerm?.id ?? null}
        isSubmitting={bulkAssign.isPending}
        onClose={() => setBulkAssignOpen(false)}
        onSubmit={handleBulkAssignInstructor}
      />

      <EditCapacityDialog
        open={!!editCapacityTarget}
        section={editCapacityTarget}
        isSubmitting={updateSection.isPending}
        onClose={() => setEditCapacityTarget(null)}
        onSubmit={handleEditCapacity}
      />

      <ConfirmActionDialog
        open={confirmAction?.type === 'closeSection'}
        title="Şubeyi Kapat"
        description={
          confirmAction?.type === 'closeSection' ? (
            <>
              <span className="font-mono font-semibold text-blue-700">{confirmAction.section.courseCode}</span> dersinin{' '}
              <strong>Şube {confirmAction.section.sectionNumber}</strong> kapatılacak.
              {confirmAction.section.activeStudentCount > 0 && (
                <p className="mt-2 text-amber-700">
                  ⚠ Bu şubede <strong>{confirmAction.section.activeStudentCount} kayıtlı öğrenci</strong> var. Kayıtları iptal edilebilir.
                </p>
              )}
            </>
          ) : null
        }
        confirmLabel="Kapat"
        tone="destructive"
        isSubmitting={isConfirming}
        onCancel={() => setConfirmAction(null)}
        onConfirm={handleConfirmAction}
      />

      <ConfirmActionDialog
        open={confirmAction?.type === 'reactivateSection'}
        title="Şubeyi Yeniden Aç"
        description={
          confirmAction?.type === 'reactivateSection' ? (
            <>
              <span className="font-mono font-semibold text-blue-700">{confirmAction.section.courseCode}</span> dersinin{' '}
              <strong>Şube {confirmAction.section.sectionNumber}</strong> yeniden aktifleştirilecek.
            </>
          ) : null
        }
        confirmLabel="Yeniden Aç"
        isSubmitting={isConfirming}
        onCancel={() => setConfirmAction(null)}
        onConfirm={handleConfirmAction}
      />

      <AddSectionDialog
        open={addSectionOpen}
        course={selectedCourse}
        departmentId={currentDeptId}
        termId={activeTerm?.id ?? null}
        isSubmitting={openCourse.isPending}
        onClose={() => setAddSectionOpen(false)}
        onSubmit={handleAddSection}
      />

      <AutoSplitConfirmDialog
        open={autoSplitConfirmOpen}
        course={selectedCourse}
        targetCount={Number(splitCount)}
        strategy={splitStrategy}
        isSubmitting={autoSplit.isPending}
        onCancel={() => setAutoSplitConfirmOpen(false)}
        onConfirm={handleConfirmAutoSplit}
      />

      <ConfirmActionDialog
        open={confirmAction?.type === 'deleteAllSections'}
        title="Tüm Şubelerini Sil"
        description={
          confirmAction?.type === 'deleteAllSections' ? (
            <>
              <span className="font-mono font-semibold text-blue-700">{confirmAction.course.courseCode}</span> dersinin{' '}
              <strong>{confirmAction.course.sections.length} şubesi</strong> bu dönemden tamamen silinecek.
              <p className="mt-2 text-red-700">
                ⚠ Bu işlem geri alınamaz. Aktif kayıt, not veya yoklama varsa backend tarafından engellenebilir.
              </p>
            </>
          ) : null
        }
        confirmLabel="Tümünü Sil"
        tone="destructive"
        isSubmitting={isConfirming}
        onCancel={() => setConfirmAction(null)}
        onConfirm={handleConfirmAction}
      />
    </div>
  );
}
