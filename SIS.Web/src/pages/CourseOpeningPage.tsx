import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
import { AlertCircle } from 'lucide-react';
import { CurriculumPanel } from '../components/course-opening/CurriculumPanel';
import { CurriculumPanelFilters } from '../components/course-opening/CurriculumPanelFilters';
import { CourseDetailModal } from '../components/course-opening/CourseDetailModal';
import { OpenedCoursesPanel } from '../components/course-opening/OpenedCoursesPanel';
import { OpenedCoursesPanelFilters } from '../components/course-opening/OpenedCoursesPanelFilters';
import { FreeCourseOpenDialog } from '../components/course-opening/FreeCourseOpenDialog';
import { ExternalCoursesDrawer } from '../components/course-opening/ExternalCoursesDrawer';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useAdminTerms } from '../hooks/useAdminTerms';
import {
  useAdminFaculties,
  useAdminDepartmentsByStructure,
  useAdminProgramsByStructure,
} from '../hooks/useAdminAcademicStructure';
import {
  useBulkOpenCourses,
  useCloseSection,
  useCloseAllSections,
  useReactivateSection,
  useCurriculumCoursesForOpening,
  useDepartmentInstructors,
  useExternalCourses,
  useInstructorWorkload,
  useOpenCourse,
  useOpenedCourses,
  useUpdateOpenedSection,
} from '../hooks/useCourseOpening';
import { useDepartmentForecast } from '../hooks/useForecast';
import type { CourseDemandForecastDto } from '../types/forecast';
import type { CurriculumCourseForOpeningDto } from '../types/courseOpening';

export default function CourseOpeningPage() {
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();
  const { data: allTerms } = useAdminTerms();
  const { data: faculties } = useAdminFaculties();
  const { data: structureDepartments } = useAdminDepartmentsByStructure();
  const accessibleDepartments = contextQuery.data?.accessibleDepartments ?? [];

  // ─── Akademik hiyerarşi (her iki panel paylaşır) ─────────────
  const [facultyId, setFacultyId] = useState<number | null>(null);
  const [deptId, setDeptId] = useState<number | null>(null);
  const [programId, setProgramId] = useState<number | null>(null);
  const [curriculumId, setCurriculumId] = useState<number | null>(null);

  // ─── Sol panel filtreleri ────────────────────────────────────
  const [sourceClassYear, setSourceClassYear] = useState<number | null>(null);
  const [sourceSemester, setSourceSemester] = useState<'fall' | 'spring' | null>(null);
  const [includeUnmapped, setIncludeUnmapped] = useState(false);
  const [sourceCodeSearch, setSourceCodeSearch] = useState('');
  const [sourceNameSearch, setSourceNameSearch] = useState('');

  // ─── Sağ panel filtreleri ────────────────────────────────────
  const [targetTermId, setTargetTermId] = useState<number | null>(null);
  const [targetClassYear, setTargetClassYear] = useState<number | null>(null);
  const [targetCodeSearch, setTargetCodeSearch] = useState('');
  const [targetNameSearch, setTargetNameSearch] = useState('');

  // ─── Diğer state ─────────────────────────────────────────────
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [selectedInstructorId, setSelectedInstructorId] = useState<number | null>(null);
  const [detailCourse, setDetailCourse] = useState<CurriculumCourseForOpeningDto | null>(null);
  const [freeCourseDialogOpen, setFreeCourseDialogOpen] = useState(false);
  const [externalDrawerOpen, setExternalDrawerOpen] = useState(false);

  // İlk açılışta erişilebilir ilk bölümü ve aktif dönemi varsayılan seç
  useEffect(() => {
    if (deptId == null && accessibleDepartments.length > 0) {
      setDeptId(accessibleDepartments[0].departmentId);
    }
  }, [accessibleDepartments, deptId]);

  useEffect(() => {
    if (targetTermId == null && activeTerm?.id) {
      setTargetTermId(activeTerm.id);
    }
  }, [activeTerm?.id, targetTermId]);

  // Bölüm seçilince fakülteyi de senkronize et
  useEffect(() => {
    if (deptId != null && facultyId == null && structureDepartments) {
      const sd = structureDepartments.find((d) => d.id === deptId);
      if (sd) setFacultyId(sd.facultyId);
    }
  }, [deptId, facultyId, structureDepartments]);

  const { data: programs } = useAdminProgramsByStructure(deptId ?? undefined);

  const openedQuery = useOpenedCourses(deptId, targetTermId);
  const curriculumQuery = useCurriculumCoursesForOpening(
    deptId,
    targetTermId,
    true, // showAll — sınıf/dönem filtresi frontend'de
    curriculumId,
    includeUnmapped,
  );
  const { data: forecastData } = useDepartmentForecast(deptId);
  const instructorsQuery = useDepartmentInstructors(deptId);
  const workloadQuery = useInstructorWorkload(selectedInstructorId, targetTermId);
  const externalCoursesQuery = useExternalCourses(deptId, targetTermId);

  const openMutation = useOpenCourse(deptId ?? 0, targetTermId ?? 0);
  const bulkMutation = useBulkOpenCourses(deptId ?? 0, targetTermId ?? 0);
  const updateMutation = useUpdateOpenedSection(deptId ?? 0, targetTermId ?? 0);
  const closeMutation = useCloseSection(deptId ?? 0, targetTermId ?? 0);
  const closeAllMutation = useCloseAllSections(deptId ?? 0, targetTermId ?? 0);
  const reactivateMutation = useReactivateSection(deptId ?? 0, targetTermId ?? 0);

  const forecastMap = useMemo(() => {
    const map = new Map<number, CourseDemandForecastDto>();
    if (forecastData?.courseSummaries) {
      for (const s of forecastData.courseSummaries) map.set(s.courseId, s);
    }
    return map;
  }, [forecastData]);

  // Hedef dönem aktif mi? (geçmiş veya iptal edilmişse read-only)
  const targetTerm = useMemo(
    () => (allTerms ?? []).find((t) => t.id === targetTermId) ?? null,
    [allTerms, targetTermId],
  );
  const isTargetTermHistorical = useMemo(() => {
    if (!targetTerm) return false;
    return targetTerm.status !== 'Active';
  }, [targetTerm]);
  const targetTermIsCancelled = targetTerm?.status === 'Cancelled';

  const toggleSelect = useCallback((courseId: number) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(courseId)) next.delete(courseId);
      else next.add(courseId);
      return next;
    });
  }, []);

  const handleOpenSelected = useCallback(async () => {
    if (!deptId || !targetTermId || selectedIds.size === 0) return;
    const courses = curriculumQuery.data ?? [];

    let includeCrossLists = false;
    try {
      const { default: apiClient } = await import('../api/axios');
      const crossListChecks = await Promise.all(
        [...selectedIds].map(async (cid) => {
          try {
            const res = await apiClient.get<Array<{ code: string }>>(`/admin/courses/${cid}/cross-lists`);
            return { courseId: cid, links: res.data };
          } catch {
            return { courseId: cid, links: [] };
          }
        }),
      );
      const withLinks = crossListChecks.filter((x) => x.links.length > 0);
      if (withLinks.length > 0) {
        const details = withLinks
          .map((x) => {
            const code = courses.find((c) => c.courseId === x.courseId)?.courseCode ?? `#${x.courseId}`;
            const linked = x.links.map((l) => l.code).join(', ');
            return `${code} ↔ ${linked}`;
          })
          .join('\n');
        includeCrossLists = window.confirm(
          `Seçilen derslerin bazıları cross-list eşlemeli:\n\n${details}\n\n` +
            `Cross-list'li dersler için paralel şubeler de otomatik açılsın mı?\n\n` +
            `Evet: Aynı hoca/kapasite ile her iki kod için de şube açılır (ortak grup).\n` +
            `Hayır: Sadece seçtiğiniz dersler açılır.`,
        );
      }
    } catch {
      /* network/permission errors → default false */
    }

    bulkMutation.mutate(
      {
        courses: [...selectedIds].map((courseId) => {
          const c = courses.find((x) => x.courseId === courseId);
          return {
            courseId,
            sectionCount: c?.recommendedSectionCount ?? 1,
            capacityPerSection: c?.recommendedCapacity ?? 40,
            includeCrossLists,
          };
        }),
      },
      { onSuccess: () => setSelectedIds(new Set()) },
    );
  }, [deptId, targetTermId, selectedIds, curriculumQuery.data, bulkMutation]);

  const handleUpdateSection = useCallback(
    (
      sectionId: number,
      capacity: number,
      instructorId: number | undefined,
      internalQuota?: number | null,
      externalQuota?: number | null,
    ) => updateMutation.mutate({ sectionId, capacity, instructorId, internalQuota, externalQuota }),
    [updateMutation],
  );

  const handleCloseSection = useCallback((sectionId: number) => closeMutation.mutate(sectionId), [closeMutation]);
  const handleCloseAllSections = useCallback(
    (courseId: number) => closeAllMutation.mutateAsync(courseId),
    [closeAllMutation],
  );
  const handleReactivateSection = useCallback(
    (sectionId: number) => reactivateMutation.mutateAsync(sectionId),
    [reactivateMutation],
  );
  const handleAddSection = useCallback(
    (
      courseId: number,
      capacity: number,
      instructorId: number | undefined,
      internalQuota?: number,
      externalQuota?: number,
    ) => {
      if (deptId && targetTermId)
        openMutation.mutate({
          courseId,
          sectionCount: 1,
          capacityPerSection: capacity,
          instructorId,
          internalQuota,
          externalQuota,
        });
    },
    [deptId, targetTermId, openMutation],
  );

  const exportCsv = useCallback(() => {
    const secs = openedQuery.data?.sections ?? [];
    if (!secs.length) return;
    const csv =
      'Ders Kodu,Ders Adı,Şube,Kontenjan,Kayıtlı,Hoca\n' +
      secs
        .map(
          (s) =>
            `${s.courseCode},"${s.courseName}",${s.sectionNumber},${s.capacity},${s.enrolledCount},"${s.instructorName ?? ''}"`,
        )
        .join('\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8;' }));
    Object.assign(document.createElement('a'), { href: url, download: 'acilan-dersler.csv' }).click();
    URL.revokeObjectURL(url);
  }, [openedQuery.data]);

  if (contextQuery.isLoading) return <Skeleton className="h-96 rounded-xl" />;

  if (contextQuery.isError) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Sayfa yüklenemedi</AlertTitle>
        <AlertDescription>Yetki bilgileri alınamadı.</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="flex flex-col h-[calc(100vh-120px)]">
      {targetTermIsCancelled && (
        <Alert variant="destructive" className="mb-3 shrink-0">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>{targetTerm?.name} dönemi iptal edilmiş</AlertTitle>
          <AlertDescription>
            İptal edilmiş dönemde yeni ders veya şube açma işlemi yapılamaz.
          </AlertDescription>
        </Alert>
      )}

      <div
        className="flex-1 min-h-0 grid grid-cols-1 lg:grid-cols-[1fr_1px_1fr] rounded-xl border border-gray-200 shadow-sm bg-white"
        style={{ overflow: 'hidden' }}
      >
        <CurriculumPanel
          courses={curriculumQuery.data}
          isLoading={curriculumQuery.isLoading}
          forecastMap={forecastMap}
          classYear={sourceClassYear}
          semester={sourceSemester}
          codeSearch={sourceCodeSearch}
          nameSearch={sourceNameSearch}
          selectedIds={selectedIds}
          onToggleSelect={toggleSelect}
          onOpenSelected={handleOpenSelected}
          onOpenCourseDetail={setDetailCourse}
          filtersSlot={
            <CurriculumPanelFilters
              faculties={faculties ?? []}
              structureDepartments={structureDepartments ?? []}
              accessibleDepartments={accessibleDepartments}
              programs={programs ?? []}
              facultyId={facultyId}
              onFacultyChange={setFacultyId}
              deptId={deptId}
              onDeptChange={setDeptId}
              programId={programId}
              onProgramChange={setProgramId}
              curriculumId={curriculumId}
              onCurriculumChange={setCurriculumId}
              classYear={sourceClassYear}
              onClassYearChange={setSourceClassYear}
              semester={sourceSemester}
              onSemesterChange={setSourceSemester}
              includeUnmapped={includeUnmapped}
              onIncludeUnmappedChange={setIncludeUnmapped}
              codeSearch={sourceCodeSearch}
              onCodeSearchChange={setSourceCodeSearch}
              nameSearch={sourceNameSearch}
              onNameSearchChange={setSourceNameSearch}
            />
          }
          onOpenFreeCourseDialog={() => setFreeCourseDialogOpen(true)}
          freeDialogDisabled={!deptId || !targetTermId || isTargetTermHistorical}
        />
        <div className="bg-gray-200 w-px hidden lg:block" />
        <OpenedCoursesPanel
          sections={openedQuery.data?.sections}
          isLoading={openedQuery.isLoading}
          termId={targetTermId ?? 0}
          forecastMap={forecastMap}
          instructors={instructorsQuery.data ?? []}
          classYear={targetClassYear}
          codeSearch={targetCodeSearch}
          nameSearch={targetNameSearch}
          onUpdateSection={handleUpdateSection}
          onCloseSection={handleCloseSection}
          onCloseAllSections={handleCloseAllSections}
          onReactivateSection={handleReactivateSection}
          onAddSection={handleAddSection}
          updatePending={updateMutation.isPending}
          closePending={closeMutation.isPending}
          closeError={closeMutation.error}
          workloadData={workloadQuery.data}
          onInstructorSelect={setSelectedInstructorId}
          onExportCsv={exportCsv}
          externalCourses={externalCoursesQuery.data}
          onOpenExternalDrawer={() => setExternalDrawerOpen(true)}
          readOnly={isTargetTermHistorical}
          filtersSlot={
            <OpenedCoursesPanelFilters
              faculties={faculties ?? []}
              structureDepartments={structureDepartments ?? []}
              accessibleDepartments={accessibleDepartments}
              programs={programs ?? []}
              facultyId={facultyId}
              onFacultyChange={setFacultyId}
              deptId={deptId}
              onDeptChange={setDeptId}
              programId={programId}
              onProgramChange={setProgramId}
              termId={targetTermId}
              onTermChange={setTargetTermId}
              availableTerms={allTerms ?? []}
              classYear={targetClassYear}
              onClassYearChange={setTargetClassYear}
              codeSearch={targetCodeSearch}
              onCodeSearchChange={setTargetCodeSearch}
              nameSearch={targetNameSearch}
              onNameSearchChange={setTargetNameSearch}
              isHistoricalTerm={isTargetTermHistorical}
            />
          }
        />
      </div>

      {detailCourse && (
        <CourseDetailModal
          open={!!detailCourse}
          onClose={() => setDetailCourse(null)}
          course={detailCourse}
          forecast={forecastMap.get(detailCourse.courseId)}
          instructors={instructorsQuery.data ?? []}
          onOpenCourse={(request) => {
            openMutation.mutate(request, {
              onSuccess: () => setDetailCourse(null),
            });
          }}
          isOpening={openMutation.isPending}
        />
      )}
      {freeCourseDialogOpen && deptId && targetTermId && (
        <FreeCourseOpenDialog
          departmentId={deptId}
          termId={targetTermId}
          onClose={() => setFreeCourseDialogOpen(false)}
        />
      )}

      <ExternalCoursesDrawer
        open={externalDrawerOpen}
        onOpenChange={setExternalDrawerOpen}
        externalCourses={externalCoursesQuery.data}
        departmentName={accessibleDepartments.find((d) => d.departmentId === deptId)?.departmentName}
      />
    </div>
  );
}
