import { useCallback, useMemo, useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
import { AlertCircle } from 'lucide-react';
import { CourseFilterBar } from '../components/course-opening/CourseFilterBar';
import { CurriculumPanel } from '../components/course-opening/CurriculumPanel';
import { CourseDetailModal } from '../components/course-opening/CourseDetailModal';
import { OpenedCoursesPanel } from '../components/course-opening/OpenedCoursesPanel';
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

  const deptId = selectedDeptId ?? departments[0]?.departmentId ?? null;
  const termId = activeTerm?.id ?? null;

  const openedQuery = useOpenedCourses(deptId, termId);
  const curriculumQuery = useCurriculumCoursesForOpening(deptId, termId, true);
  const { data: forecastData } = useDepartmentForecast(deptId);
  const instructorsQuery = useDepartmentInstructors(deptId);
  const workloadQuery = useInstructorWorkload(selectedInstructorId, termId);

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

  const handleOpenSelected = useCallback(() => {
    if (!deptId || !termId || selectedIds.size === 0) return;
    const courses = curriculumQuery.data ?? [];
    bulkMutation.mutate({
      courses: [...selectedIds].map(courseId => {
        const c = courses.find(x => x.courseId === courseId);
        return {
          courseId,
          sectionCount: c?.recommendedSectionCount ?? 1,
          capacityPerSection: c?.recommendedCapacity ?? 40,
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

  return (
    <div className="flex flex-col h-[calc(100vh-4rem)]">
      <div className="shrink-0 px-4 pt-4 pb-3">
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
          onExportCsv={exportCsv}
        />
      </div>
      <div className="flex-1 min-h-0 grid grid-cols-1 lg:grid-cols-[1fr_1px_1fr] mx-4 mb-4 rounded-xl border border-gray-200 shadow-sm bg-white" style={{ overflow: 'hidden' }}>
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
    </div>
  );
}
