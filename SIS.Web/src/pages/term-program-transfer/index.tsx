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
import type { CourseOverride } from './types';
import { TransferFilterBar } from './components/TransferFilterBar';
import { SourcePanelHeader } from './components/SourcePanelHeader';
import { TransferTable } from './components/TransferTable';
import { TransferConfirmDialog } from './components/TransferConfirmDialog';
import { TransferResultBanner } from './components/TransferResultBanner';
import { TargetTermPanel } from './components/TargetTermPanel';
import { RowEditSheet } from './components/RowEditSheet';
import { SectionDetailDialog } from './components/SectionDetailDialog';
import { SectionEditDialog } from './components/SectionEditDialog';
import { DeleteSectionDialog } from './components/DeleteSectionDialog';
import { AddSectionDialog } from './components/AddSectionDialog';
import type { OpenedCourseSectionDto } from '../../types/courseOpening';

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
  const [transferException, setTransferException] = useState<string | null>(null);
  const [overrides, setOverrides] = useState<Record<number, CourseOverride>>({});
  const [searchCode, setSearchCode] = useState('');
  const [searchName, setSearchName] = useState('');
  const [showConfirmDialog, setShowConfirmDialog] = useState(false);
  const [includeSchedule, setIncludeSchedule] = useState(false);
  const [editingCourseId, setEditingCourseId] = useState<number | null>(null);
  const [detailSection, setDetailSection] = useState<OpenedCourseSectionDto | null>(null);
  const [editSection, setEditSection] = useState<OpenedCourseSectionDto | null>(null);
  const [addSectionFor, setAddSectionFor] = useState<OpenedCourseSectionDto | null>(null);
  const [deleteSection, setDeleteSection] = useState<OpenedCourseSectionDto | null>(null);

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
    if (searchCode.trim()) {
      const q = searchCode.trim().toLowerCase();
      result = result.filter((c) => c.courseCode.toLowerCase().includes(q));
    }
    if (searchName.trim()) {
      const q = searchName.trim().toLowerCase();
      result = result.filter((c) => c.courseName.toLowerCase().includes(q));
    }
    return result;
  }, [courses, searchCode, searchName]);

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
  const totalCredits = selectedCourses.reduce((sum, c) => sum + c.credits, 0);
  const totalEcts = selectedCourses.reduce((sum, c) => sum + c.ects, 0);

  const handleTransfer = async () => {
    if (!deptId || !sourceTermId || !targetTermId || selectedIds.size === 0) return;
    setShowConfirmDialog(false);
    setTransferResult(null);
    setTransferException(null);

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

    try {
      const result = await transferMutation.mutateAsync({
        departmentId: deptId,
        sourceTermId,
        targetTermId,
        courses: courseItems,
      });
      setTransferResult(result);
      setSelectedIds(new Set());
    } catch (err) {
      const e = err as {
        response?: { data?: { error?: string; detail?: string; title?: string } };
        message?: string;
      };
      setTransferException(
        e?.response?.data?.error ??
          e?.response?.data?.detail ??
          e?.response?.data?.title ??
          e?.message ??
          'Aktarım sırasında beklenmeyen bir hata oluştu.',
      );
    }
  };

  const resetSelection = () => {
    setSelectedIds(new Set());
    setTransferResult(null);
    setTransferException(null);
    setOverrides({});
    setSearchCode('');
    setSearchName('');
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
    <div className="space-y-3">
      <TransferFilterBar termStatus={termStatus} isTermSuitable={isTermSuitable} />

      {transferException && (
        <div className="rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3">
          <div className="flex items-start gap-2">
            <AlertCircle className="h-4 w-4 mt-0.5 text-destructive shrink-0" />
            <div className="flex-1 text-sm">
              <div className="font-semibold text-destructive">Aktarım yapılamadı</div>
              <div className="text-destructive/90">{transferException}</div>
            </div>
            <button
              type="button"
              className="text-xs text-destructive/70 hover:text-destructive"
              onClick={() => setTransferException(null)}
              aria-label="Kapat"
            >
              ✕
            </button>
          </div>
        </div>
      )}

      {transferResult && <TransferResultBanner result={transferResult} />}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-3 items-start">
        {/* SOL: Kaynak (Referans) panel — kendi filtreleri ve aksiyonları */}
        <Card className="overflow-hidden">
          <SourcePanelHeader
            departments={departments}
            departmentId={deptId}
            onDepartmentChange={changeDept}
            sourceTerms={sourceTerms}
            sourceTermId={sourceTermId}
            onSourceChange={changeSource}
            programs={programs}
            programId={programId}
            onProgramChange={setProgramId}
            classLevel={classLevel}
            onClassLevelChange={setClassLevel}
            searchCode={searchCode}
            onSearchCodeChange={setSearchCode}
            searchName={searchName}
            onSearchNameChange={setSearchName}
            selectedCount={selectedIds.size}
            totalCount={courses.length}
            transferableCount={transferable.length}
            totalCredits={totalCredits}
            totalEcts={totalEcts}
            includeSchedule={includeSchedule}
            onIncludeScheduleChange={setIncludeSchedule}
            targetDepartmentLabel={departments.find((d) => d.departmentId === deptId)?.departmentName ?? null}
            onTransferClick={() => setShowConfirmDialog(true)}
            isPending={transferMutation.isPending}
            canTransfer={Boolean(targetTermId) && isTermSuitable}
          />

          <CardContent className="p-0">
            {!sourceTermId ? (
              <div className="py-12 text-center">
                <Copy className="h-8 w-8 text-muted-foreground/30 mx-auto mb-2" />
                <p className="text-xs text-muted-foreground">Kaynak dönem seçin</p>
                <p className="text-[10px] text-muted-foreground/60 mt-1">
                  Üstteki filtreden bir önceki dönemi seçin.
                </p>
              </div>
            ) : coursesQuery.isLoading ? (
              <div className="py-12 text-center text-xs text-muted-foreground">Yükleniyor...</div>
            ) : courses.length === 0 ? (
              <div className="py-12 text-center">
                <AlertCircle className="h-8 w-8 text-muted-foreground/30 mx-auto mb-2" />
                <p className="text-xs text-muted-foreground">Ders bulunamadı</p>
                <p className="text-[10px] text-muted-foreground/60 mt-1">
                  {selectedSourceTerm?.name} döneminde bu bölüme ait açılmış ders yok.
                </p>
              </div>
            ) : (
              <TransferTable
                courses={filteredCourses}
                selectedIds={selectedIds}
                onToggle={toggle}
                onEdit={(c) => setEditingCourseId(c.courseId)}
              />
            )}
          </CardContent>
        </Card>

        {/* SAĞ: Hedef dönem — Bölüm+Program sol ile çift yönlü, dönem/sınıf/arama bağımsız */}
        <TargetTermPanel
          departments={departments}
          departmentId={deptId}
          onDepartmentChange={changeDept}
          programId={programId}
          onProgramChange={setProgramId}
          defaultTermId={targetTermId}
          availableTerms={allTerms ?? []}
          onDetail={setDetailSection}
          onEdit={setEditSection}
          onAddSection={setAddSectionFor}
          onDelete={setDeleteSection}
        />
      </div>

      <TransferConfirmDialog
        open={showConfirmDialog}
        onOpenChange={setShowConfirmDialog}
        selectedCourses={selectedCourses}
        getOverride={getOverride}
        instructors={instructors}
        targetDepartmentName={departments.find((d) => d.departmentId === deptId)?.departmentName ?? null}
        targetTermName={activeTerm?.name ?? null}
        onConfirm={handleTransfer}
      />

      <RowEditSheet
        course={editingCourseId != null ? (courses.find((c) => c.courseId === editingCourseId) ?? null) : null}
        override={editingCourseId != null ? getOverride(courses.find((c) => c.courseId === editingCourseId)!) : null}
        instructors={instructors}
        maxSections={maxSections}
        open={editingCourseId != null}
        onOpenChange={(open) => !open && setEditingCourseId(null)}
        onUpdate={(field, value) => {
          if (editingCourseId != null) updateOverride(editingCourseId, field, value);
        }}
      />

      {/* Sağ panel aksiyon dialogları */}
      <SectionDetailDialog
        section={detailSection}
        open={detailSection != null}
        onOpenChange={(o) => !o && setDetailSection(null)}
      />
      <SectionEditDialog
        section={editSection}
        departmentId={deptId ?? 0}
        termId={targetTermId ?? 0}
        instructors={instructors}
        open={editSection != null}
        onOpenChange={(o) => !o && setEditSection(null)}
      />
      <AddSectionDialog
        section={addSectionFor}
        departmentId={deptId ?? 0}
        termId={targetTermId ?? 0}
        open={addSectionFor != null}
        onOpenChange={(o) => !o && setAddSectionFor(null)}
      />
      <DeleteSectionDialog
        section={deleteSection}
        departmentId={deptId ?? 0}
        termId={targetTermId ?? 0}
        open={deleteSection != null}
        onOpenChange={(o) => !o && setDeleteSection(null)}
      />
    </div>
  );
}
