import { useState, useMemo, useCallback } from 'react';
import { AlertCircle, Copy } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { useAcademicOperationsContext } from '../../hooks/useAcademicOperations';
import { useActiveTerm } from '../../hooks/useActiveTerm';
import { useAdminTerms } from '../../hooks/useAdminTerms';
import { useAdminProgramsByStructure } from '../../hooks/useAdminAcademicStructure';
import {
  usePreviousTermCourses,
  useTransferFromTerm,
  useDepartmentInstructors,
  useTransferDefaults,
} from '../../hooks/useCourseOpening';
import type {
  PreviousTermCourseDto,
  TransferFromTermResultDto,
  TransferCourseItem,
} from '../../types/courseOpening';
import type { CourseOverride, FilterType } from './types';
import { TransferFilterBar } from './components/TransferFilterBar';
import { TransferActionToolbar } from './components/TransferActionToolbar';
import { TransferTable } from './components/TransferTable';
import { TransferConfirmDialog } from './components/TransferConfirmDialog';
import { TransferResultBanner } from './components/TransferResultBanner';

export default function TermProgramTransferPage() {
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();
  const { data: allTerms } = useAdminTerms();
  const { data: defaults } = useTransferDefaults();
  const departments = contextQuery.data?.accessibleDepartments ?? [];

  const [selectedDeptId, setSelectedDeptId] = useState<number | null>(null);
  const [sourceTermId, setSourceTermId] = useState<number | null>(null);
  const [programId, setProgramId] = useState<number | null>(null);
  const [classLevel, setClassLevel] = useState<number | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [transferResult, setTransferResult] = useState<TransferFromTermResultDto | null>(null);
  const [overrides, setOverrides] = useState<Record<number, CourseOverride>>({});
  const [searchQuery, setSearchQuery] = useState('');
  const [filterType, setFilterType] = useState<FilterType>('all');
  const [showConfirmDialog, setShowConfirmDialog] = useState(false);
  const [includeSchedule, setIncludeSchedule] = useState(false);

  const deptId = selectedDeptId ?? departments[0]?.departmentId ?? null;
  const targetTermId = activeTerm?.id ?? null;
  const termStatus = activeTerm?.status;
  const isTermSuitable = termStatus === 'Active';
  const sourceTerms = useMemo(
    () => (allTerms ?? []).filter((t) => t.id !== targetTermId),
    [allTerms, targetTermId],
  );
  const selectedSourceTerm = sourceTerms.find((t) => t.id === sourceTermId);

  const coursesQuery = usePreviousTermCourses(deptId, sourceTermId, targetTermId, { programId, classLevel });
  const transferMutation = useTransferFromTerm(deptId ?? 0, targetTermId ?? 0);
  const instructorsQuery = useDepartmentInstructors(deptId, targetTermId);
  const programsQuery = useAdminProgramsByStructure(deptId ?? undefined);
  const programs = useMemo(
    () => (programsQuery.data ?? []).map((p) => ({ id: p.id, name: p.name })),
    [programsQuery.data],
  );
  const instructors = useMemo(() => instructorsQuery.data ?? [], [instructorsQuery.data]);
  const courses = useMemo(() => coursesQuery.data ?? [], [coursesQuery.data]);
  const transferable = useMemo(() => courses.filter((c) => !c.alreadyOpened), [courses]);

  const filteredCourses = useMemo(() => {
    let result = courses;
    if (filterType === 'compulsory') result = result.filter((c) => c.isCompulsory);
    if (filterType === 'elective') result = result.filter((c) => !c.isCompulsory);
    if (searchQuery.trim()) {
      const q = searchQuery.trim().toLowerCase();
      result = result.filter(
        (c) =>
          c.courseCode.toLowerCase().includes(q) ||
          c.courseName.toLowerCase().includes(q) ||
          (c.instructorName && c.instructorName.toLowerCase().includes(q)),
      );
    }
    return result;
  }, [courses, filterType, searchQuery]);

  const fallbackCapacity = defaults?.defaultCapacityPerSection ?? 40;
  const maxSections = defaults?.maxSectionsPerCourse ?? 20;

  const buildDefaultOverride = useCallback(
    (c: PreviousTermCourseDto): CourseOverride => ({
      sectionCount: c.sectionCount,
      capacityPerSection:
        c.sectionCount > 0 ? Math.round(c.totalCapacity / c.sectionCount) : fallbackCapacity,
      instructorId: c.instructorId,
      internalQuota: c.sourceInternalQuota,
      externalQuota: c.sourceExternalQuota,
      accessibleDepartmentIds: c.sourceAccessibleDepartmentIds,
      includeCrossLists: c.hasCrossLists,
    }),
    [fallbackCapacity],
  );

  const getOverride = useCallback(
    (c: PreviousTermCourseDto): CourseOverride => overrides[c.courseId] ?? buildDefaultOverride(c),
    [overrides, buildDefaultOverride],
  );

  function updateOverride<K extends keyof CourseOverride>(
    courseId: number,
    field: K,
    value: CourseOverride[K],
  ) {
    setOverrides((prev) => {
      const course = courses.find((c) => c.courseId === courseId);
      if (!course) return prev;
      const current = prev[courseId] ?? buildDefaultOverride(course);
      return { ...prev, [courseId]: { ...current, [field]: value } };
    });
  }

  const toggle = (c: PreviousTermCourseDto) => {
    if (c.alreadyOpened) return;
    setSelectedIds((prev) => {
      const n = new Set(prev);
      if (n.has(c.courseId)) n.delete(c.courseId);
      else n.add(c.courseId);
      return n;
    });
  };

  const selectedCourses = courses.filter((c) => selectedIds.has(c.courseId));

  const handleTransfer = async () => {
    if (!deptId || !sourceTermId || !targetTermId || selectedIds.size === 0) return;
    setShowConfirmDialog(false);
    setTransferResult(null);

    const courseItems: TransferCourseItem[] = selectedCourses.map((c) => {
      const ov = getOverride(c);
      return {
        courseId: c.courseId,
        sectionCount: ov.sectionCount,
        capacityPerSection: ov.capacityPerSection,
        instructorId: ov.instructorId,
        internalQuota: ov.internalQuota,
        externalQuota: ov.externalQuota,
        accessibleDepartmentIds: ov.accessibleDepartmentIds,
        includeCrossLists: ov.includeCrossLists,
        includeSchedule: includeSchedule && c.hasSchedule,
      };
    });

    const result = await transferMutation.mutateAsync({
      departmentId: deptId,
      sourceTermId,
      targetTermId,
      courses: courseItems,
    });
    setTransferResult(result);
    setSelectedIds(new Set());
  };

  const resetSelection = () => {
    setSelectedIds(new Set());
    setTransferResult(null);
    setOverrides({});
    setSearchQuery('');
  };

  const changeDept = (deptId: number) => {
    setSelectedDeptId(deptId);
    setProgramId(null);
    resetSelection();
  };
  const changeSource = (termId: number) => {
    setSourceTermId(termId);
    resetSelection();
  };

  if (contextQuery.isLoading) {
    return (
      <div className="space-y-3">
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <TransferFilterBar
        departments={departments}
        selectedDeptId={deptId}
        onDeptChange={changeDept}
        sourceTerms={sourceTerms}
        sourceTermId={sourceTermId}
        onSourceChange={changeSource}
        programs={programs}
        programId={programId}
        onProgramChange={setProgramId}
        classLevel={classLevel}
        onClassLevelChange={setClassLevel}
        activeTermName={activeTerm?.name ?? null}
        termStatus={termStatus}
        isTermSuitable={isTermSuitable}
        totalCount={courses.length}
        transferableCount={transferable.length}
      />

      {transferResult && <TransferResultBanner result={transferResult} />}

      {!sourceTermId ? (
        <Card>
          <CardContent className="p-0">
            <div className="py-16 text-center">
              <Copy className="h-10 w-10 text-muted-foreground/30 mx-auto mb-3" />
              <p className="text-muted-foreground text-sm">Kaynak dönem seçin</p>
              <p className="text-xs text-muted-foreground/60 mt-1">
                Kopyalanacak dersleri görmek için bir önceki dönem seçin.
              </p>
            </div>
          </CardContent>
        </Card>
      ) : coursesQuery.isLoading ? (
        <Card>
          <CardContent className="p-0">
            <div className="py-16 text-center text-muted-foreground text-sm">Yükleniyor...</div>
          </CardContent>
        </Card>
      ) : courses.length === 0 ? (
        <Card>
          <CardContent className="p-0">
            <div className="py-16 text-center">
              <AlertCircle className="h-10 w-10 text-muted-foreground/30 mx-auto mb-3" />
              <p className="text-muted-foreground text-sm">Ders bulunamadı</p>
              <p className="text-xs text-muted-foreground/60 mt-1">
                {selectedSourceTerm?.name} döneminde bu bölüme ait açılmış ders yok.
              </p>
            </div>
          </CardContent>
        </Card>
      ) : (
        <>
          <TransferActionToolbar
            searchQuery={searchQuery}
            onSearchChange={setSearchQuery}
            filterType={filterType}
            onFilterChange={setFilterType}
            selectedCount={selectedIds.size}
            onSelectAll={() => setSelectedIds(new Set(transferable.map((c) => c.courseId)))}
            onSelectCompulsory={() =>
              setSelectedIds(new Set(transferable.filter((c) => c.isCompulsory).map((c) => c.courseId)))
            }
            onClearSelection={() => setSelectedIds(new Set())}
            includeSchedule={includeSchedule}
            onIncludeScheduleChange={setIncludeSchedule}
            onTransferClick={() => setShowConfirmDialog(true)}
            isPending={transferMutation.isPending}
            canTransfer={Boolean(targetTermId) && isTermSuitable}
          />

          <TransferTable
            courses={filteredCourses}
            selectedIds={selectedIds}
            onToggle={toggle}
            getOverride={getOverride}
            onUpdateOverride={updateOverride}
            instructors={instructors}
            maxSections={maxSections}
          />
        </>
      )}

      <TransferConfirmDialog
        open={showConfirmDialog}
        onOpenChange={setShowConfirmDialog}
        selectedCourses={selectedCourses}
        getOverride={getOverride}
        instructors={instructors}
        onConfirm={handleTransfer}
      />
    </div>
  );
}
