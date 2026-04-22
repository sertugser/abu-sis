import { useCallback, useMemo, useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { AlertCircle, Plus } from 'lucide-react';
import { CourseFilterBar } from '../components/course-opening/CourseFilterBar';
import { CurriculumPanel } from '../components/course-opening/CurriculumPanel';
import { CourseDetailModal } from '../components/course-opening/CourseDetailModal';
import { OpenedCoursesPanel } from '../components/course-opening/OpenedCoursesPanel';
import { FreeCourseOpenDialog } from '../components/course-opening/FreeCourseOpenDialog';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useAdminFaculties, useAdminDepartmentsByStructure } from '../hooks/useAdminAcademicStructure';
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
  const { data: faculties } = useAdminFaculties();
  const { data: structureDepartments } = useAdminDepartmentsByStructure();
  const departments = contextQuery.data?.accessibleDepartments ?? [];
  const [selectedFacultyId, setSelectedFacultyId] = useState<number | null>(null);
  const [selectedDeptId, setSelectedDeptId] = useState<number | null>(null);
  const [semesterFilter, setSemesterFilter] = useState<number | null>(null);
  const [typeFilter, setTypeFilter] = useState<'all' | 'compulsory' | 'elective'>('all');
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [selectedInstructorId, setSelectedInstructorId] = useState<number | null>(null);
  const [detailCourse, setDetailCourse] = useState<CurriculumCourseForOpeningDto | null>(null);
  const [freeCourseDialogOpen, setFreeCourseDialogOpen] = useState(false);

  const deptId = selectedDeptId ?? departments[0]?.departmentId ?? null;
  const termId = activeTerm?.id ?? null;

  const openedQuery = useOpenedCourses(deptId, termId);
  const curriculumQuery = useCurriculumCoursesForOpening(deptId, termId, true);
  const { data: forecastData } = useDepartmentForecast(deptId);
  const instructorsQuery = useDepartmentInstructors(deptId);
  const workloadQuery = useInstructorWorkload(selectedInstructorId, termId);
  const externalCoursesQuery = useExternalCourses(deptId, termId);

  const openMutation = useOpenCourse(deptId ?? 0, termId ?? 0);
  const bulkMutation = useBulkOpenCourses(deptId ?? 0, termId ?? 0);
  const updateMutation = useUpdateOpenedSection(deptId ?? 0, termId ?? 0);
  const closeMutation = useCloseSection(deptId ?? 0, termId ?? 0);
  const closeAllMutation = useCloseAllSections(deptId ?? 0, termId ?? 0);
  const reactivateMutation = useReactivateSection(deptId ?? 0, termId ?? 0);

  const forecastMap = useMemo(() => {
    const map = new Map<number, CourseDemandForecastDto>();
    if (forecastData?.courseSummaries) {
      for (const s of forecastData.courseSummaries) map.set(s.courseId, s);
    }
    return map;
  }, [forecastData]);

  const stats = useMemo(() => {
    const courses = curriculumQuery.data ?? [];
    const sections = openedQuery.data?.sections ?? [];
    const openedCourseIds = new Set(sections.map(s => s.courseId));
    return {
      curriculumCount: courses.length,
      openedCourseCount: openedCourseIds.size,
      openedSectionCount: sections.length,
      pendingCount: courses.filter(c => !c.alreadyOpened && c.canOpen).length,
      totalCapacity: sections.reduce((s, sec) => s + sec.capacity, 0),
      totalDemand: forecastData?.totalPredictedDemand ?? 0,
    };
  }, [curriculumQuery.data, openedQuery.data, forecastData]);

  const handleFacultyChange = useCallback((facultyId: number | null) => {
    setSelectedFacultyId(facultyId);
    // Reset department when faculty changes
    setSelectedDeptId(null);
  }, []);

  const toggleSelect = useCallback((courseId: number) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(courseId)) next.delete(courseId); else next.add(courseId);
      return next;
    });
  }, []);

  const handleOpenSelected = useCallback(async () => {
    if (!deptId || !termId || selectedIds.size === 0) return;
    const courses = curriculumQuery.data ?? [];

    // Madde 14: Seçilen derslerin herhangi biri cross-list'liyse onay sor
    let includeCrossLists = false;
    try {
      const { default: apiClient } = await import('../api/axios');
      const crossListChecks = await Promise.all(
        [...selectedIds].map(async cid => {
          try {
            const res = await apiClient.get<Array<{ code: string }>>(`/admin/courses/${cid}/cross-lists`);
            return { courseId: cid, links: res.data };
          } catch {
            return { courseId: cid, links: [] };
          }
        })
      );
      const withLinks = crossListChecks.filter(x => x.links.length > 0);
      if (withLinks.length > 0) {
        const details = withLinks
          .map(x => {
            const code = courses.find(c => c.courseId === x.courseId)?.courseCode ?? `#${x.courseId}`;
            const linked = x.links.map(l => l.code).join(', ');
            return `${code} ↔ ${linked}`;
          })
          .join('\n');
        includeCrossLists = window.confirm(
          `Seçilen derslerin bazıları cross-list eşlemeli:\n\n${details}\n\n` +
          `Cross-list'li dersler için paralel şubeler de otomatik açılsın mı?\n\n` +
          `Evet: Aynı hoca/kapasite ile her iki kod için de şube açılır (ortak grup).\n` +
          `Hayır: Sadece seçtiğiniz dersler açılır.`
        );
      }
    } catch { /* network/permission errors → default false */ }

    bulkMutation.mutate({
      courses: [...selectedIds].map(courseId => {
        const c = courses.find(x => x.courseId === courseId);
        return {
          courseId,
          sectionCount: c?.recommendedSectionCount ?? 1,
          capacityPerSection: c?.recommendedCapacity ?? 40,
          includeCrossLists,
        };
      }),
    }, { onSuccess: () => setSelectedIds(new Set()) });
  }, [deptId, termId, selectedIds, curriculumQuery.data, bulkMutation]);

  const handleUpdateSection = useCallback(
    (sectionId: number, capacity: number, instructorId: number | undefined, internalQuota?: number | null, externalQuota?: number | null) =>
      updateMutation.mutate({ sectionId, capacity, instructorId, internalQuota, externalQuota }), [updateMutation]);

  const handleCloseSection = useCallback(
    (sectionId: number) => closeMutation.mutate(sectionId), [closeMutation]);

  const handleCloseAllSections = useCallback(
    (courseId: number) => closeAllMutation.mutateAsync(courseId), [closeAllMutation]);

  const handleReactivateSection = useCallback(
    (sectionId: number) => reactivateMutation.mutateAsync(sectionId), [reactivateMutation]);

  const handleAddSection = useCallback(
    (courseId: number, capacity: number, instructorId: number | undefined, internalQuota?: number, externalQuota?: number) => {
      if (deptId && termId) openMutation.mutate({ courseId, sectionCount: 1, capacityPerSection: capacity, instructorId, internalQuota, externalQuota });
    }, [deptId, termId, openMutation]);

  const exportCsv = useCallback(() => {
    const secs = openedQuery.data?.sections ?? [];
    if (!secs.length) return;
    const csv = 'Ders Kodu,Ders Adı,Şube,Kontenjan,Kayıtlı,Hoca\n' +
      secs.map(s => `${s.courseCode},"${s.courseName}",${s.sectionNumber},${s.capacity},${s.enrolledCount},"${s.instructorName ?? ''}"`).join('\n');
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
  if (!termId) {
    return (
      <Alert>
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Aktif dönem bulunamadı</AlertTitle>
        <AlertDescription>Aktif bir akademik dönem tanımlanmamış.</AlertDescription>
      </Alert>
    );
  }

  const termNotInPreparing = !!activeTerm && activeTerm.status !== 'Preparing';

  return (
    <div className="flex flex-col h-[calc(100vh-120px)]">
      {termNotInPreparing && (
        <Alert variant="warning" className="mb-3 shrink-0">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>
            {activeTerm?.name} dönemi hazırlık fazında değil (durum: {activeTerm?.status})
          </AlertTitle>
          <AlertDescription>
            Yeni ders/şube açma işlemi yalnızca <b>Hazırlık</b> statüsündeki dönemlerde yapılabilir.
            Ders açmak için Dönem Yönetimi sayfasından yeni bir dönem oluşturup <b>Hazırlık</b> durumuna alın,
            ya da mevcut dönemi geri alın. Bu sayfadaki müfredat dersleri şu an salt okunur moddadır.
          </AlertDescription>
        </Alert>
      )}
      <div className="shrink-0 pb-3 flex items-stretch gap-2">
        <div className="flex-1 min-w-0">
          <CourseFilterBar
            departments={departments}
            faculties={faculties ?? []}
            structureDepartments={structureDepartments ?? []}
            selectedFacultyId={selectedFacultyId}
            onFacultyChange={handleFacultyChange}
            selectedDeptId={deptId}
            onDeptChange={setSelectedDeptId}
            semesterFilter={semesterFilter}
            onSemesterChange={setSemesterFilter}
            typeFilter={typeFilter}
            onTypeChange={setTypeFilter}
            stats={stats}
          />
        </div>
        <Button
          variant="outline"
          size="sm"
          className="shrink-0 h-auto gap-1.5"
          onClick={() => setFreeCourseDialogOpen(true)}
          disabled={!deptId || termNotInPreparing}
          title={termNotInPreparing ? 'Dönem hazırlık fazında değil' : 'Müfredatta olmayan bir dersi serbest aç'}
        >
          <Plus className="h-3.5 w-3.5" />
          Serbest Ders Aç
        </Button>
      </div>
      <div className="flex-1 min-h-0 grid grid-cols-1 lg:grid-cols-[1fr_1px_1fr] rounded-xl border border-gray-200 shadow-sm bg-white" style={{ overflow: 'hidden' }}>
        <CurriculumPanel
          courses={curriculumQuery.data}
          isLoading={curriculumQuery.isLoading}
          forecastMap={forecastMap}
          semesterFilter={semesterFilter}
          typeFilter={typeFilter}
          selectedIds={selectedIds}
          onToggleSelect={toggleSelect}
          onOpenSelected={handleOpenSelected}
          onOpenCourseDetail={setDetailCourse}
        />
        <div className="bg-gray-200 w-px hidden lg:block" />
        <OpenedCoursesPanel
          sections={openedQuery.data?.sections}
          isLoading={openedQuery.isLoading}
          termId={termId}
          forecastMap={forecastMap}
          instructors={instructorsQuery.data ?? []}
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
      {freeCourseDialogOpen && deptId && termId && (
        <FreeCourseOpenDialog
          departmentId={deptId}
          termId={termId}
          onClose={() => setFreeCourseDialogOpen(false)}
        />
      )}
    </div>
  );
}
