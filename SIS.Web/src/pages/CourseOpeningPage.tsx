import { useMemo, useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import {
  AlertCircle,
  AlertTriangle,
  Layers,
  Package,
  Plus,
  TrendingUp,
  Users,
} from 'lucide-react';
import { AddCourseDialog } from '../components/course-opening/AddCourseDialog';
import { BulkOpenDialog } from '../components/course-opening/BulkOpenDialog';
import { SemesterSectionGroup } from '../components/course-opening/SemesterSectionGroup';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useOpenedCourses } from '../hooks/useCourseOpening';
import { useDepartmentForecast } from '../hooks/useForecast';
import type { CourseDemandForecastDto } from '../types/forecast';

function getSemesterLabel(semester: number) {
  if (semester === 0) return 'Diğer Dersler';
  const year = Math.ceil(semester / 2);
  const isFall = semester % 2 === 1;
  return `${year}. Sınıf ${isFall ? 'Güz' : 'Bahar'}`;
}

export default function CourseOpeningPage() {
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();
  const departments = contextQuery.data?.accessibleDepartments ?? [];
  const [selectedDeptId, setSelectedDeptId] = useState<number | null>(null);
  const [showAddDialog, setShowAddDialog] = useState(false);
  const [showBulkDialog, setShowBulkDialog] = useState(false);

  const deptId = selectedDeptId ?? departments[0]?.departmentId ?? null;
  const termId = activeTerm?.id ?? null;

  const openedQuery = useOpenedCourses(deptId, termId);
  const { data: forecastData } = useDepartmentForecast(deptId);

  const forecastMap = useMemo(() => {
    const map = new Map<number, CourseDemandForecastDto>();
    if (forecastData?.courseSummaries) {
      for (const summary of forecastData.courseSummaries) {
        map.set(summary.courseId, summary);
      }
    }
    return map;
  }, [forecastData]);

  const openedSections = openedQuery.data?.sections;
  const sectionsBySemester = useMemo(() => {
    if (!openedSections) return [];

    const map = new Map<number, typeof openedSections>();
    for (const section of openedSections) {
      const semester = section.suggestedSemester || 0;
      if (!map.has(semester)) map.set(semester, []);
      map.get(semester)!.push(section);
    }

    return [...map.entries()].sort((a, b) => a[0] - b[0]);
  }, [openedSections]);

  const totalSections = openedQuery.data?.totalSections ?? 0;
  const totalCapacity = (openedSections ?? []).reduce((sum, section) => sum + section.capacity, 0);
  const capSummary = forecastData?.capacitySummary;

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
    <div className="space-y-5">
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex items-center gap-2">
          <label className="text-sm font-medium">Bölüm:</label>
          <select
            className="h-9 rounded-md border border-input bg-background px-3 text-sm"
            value={deptId ?? ''}
            onChange={event => setSelectedDeptId(Number(event.target.value))}
          >
            {departments.map(department => (
              <option key={department.departmentId} value={department.departmentId}>
                {department.departmentCode} — {department.departmentName}
              </option>
            ))}
          </select>
        </div>
        <div className="ml-auto flex gap-2">
          <Button size="sm" onClick={() => setShowAddDialog(true)} disabled={!deptId || !termId}>
            <Plus className="h-4 w-4 mr-1" /> Ekle
          </Button>
          <Button size="sm" variant="outline" onClick={() => setShowBulkDialog(true)} disabled={!deptId || !termId}>
            <Package className="h-4 w-4 mr-1" /> Toplu Ders Aç
          </Button>
        </div>
      </div>

      {!openedQuery.isLoading && !openedQuery.isError && (
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
          <div className="bg-white border rounded-xl p-4 flex items-center gap-3">
            <div className="bg-blue-50 p-2 rounded-lg"><Layers className="h-4 w-4 text-blue-600" /></div>
            <div>
              <p className="text-xl font-bold">{totalSections}</p>
              <p className="text-[11px] text-muted-foreground">Toplam Şube</p>
            </div>
          </div>
          <div className="bg-white border rounded-xl p-4 flex items-center gap-3">
            <div className="bg-emerald-50 p-2 rounded-lg"><Users className="h-4 w-4 text-emerald-600" /></div>
            <div>
              <p className="text-xl font-bold">{totalCapacity}</p>
              <p className="text-[11px] text-muted-foreground">Toplam Kontenjan</p>
            </div>
          </div>
          <div className="bg-white border rounded-xl p-4 flex items-center gap-3">
            <div className={`p-2 rounded-lg ${capSummary && capSummary.sectionDelta > 0 ? 'bg-red-50' : capSummary && capSummary.sectionDelta < 0 ? 'bg-amber-50' : 'bg-green-50'}`}>
              <TrendingUp className={`h-4 w-4 ${capSummary && capSummary.sectionDelta > 0 ? 'text-red-600' : capSummary && capSummary.sectionDelta < 0 ? 'text-amber-600' : 'text-green-600'}`} />
            </div>
            <div>
              <p className="text-xl font-bold">
                {capSummary ? (capSummary.sectionDelta > 0 ? `+${capSummary.sectionDelta}` : capSummary.sectionDelta) : '—'}
              </p>
              <p className="text-[11px] text-muted-foreground">Tahmin Farkı (şube)</p>
            </div>
          </div>
          <div className="bg-white border rounded-xl p-4 flex items-center gap-3">
            <div className={`p-2 rounded-lg ${capSummary && capSummary.coursesNeedingMoreSections > 0 ? 'bg-red-50' : 'bg-green-50'}`}>
              <AlertTriangle className={`h-4 w-4 ${capSummary && capSummary.coursesNeedingMoreSections > 0 ? 'text-red-600' : 'text-green-600'}`} />
            </div>
            <div>
              <p className="text-xl font-bold">{capSummary?.coursesNeedingMoreSections ?? '—'}</p>
              <p className="text-[11px] text-muted-foreground">Dikkat Gerektiren</p>
            </div>
          </div>
        </div>
      )}

      {openedQuery.isLoading ? (
        <Skeleton className="h-64 rounded-xl" />
      ) : openedQuery.isError ? (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Veriler yüklenemedi</AlertTitle>
          <AlertDescription>
            {(openedQuery.error as { response?: { status?: number } })?.response?.status === 403
              ? 'Bu bölüm için yetkiniz bulunmuyor.'
              : 'Açılan ders verileri alınamadı.'}
          </AlertDescription>
        </Alert>
      ) : sectionsBySemester.length === 0 ? (
        <div className="text-center py-16 bg-white border rounded-xl">
          <Package className="h-12 w-12 mx-auto text-gray-300 mb-3" />
          <p className="text-muted-foreground">Henüz ders açılmamış.</p>
          <Button variant="outline" className="mt-4" onClick={() => setShowBulkDialog(true)}>
            <Package className="h-4 w-4 mr-1" /> Toplu Ders Aç
          </Button>
        </div>
      ) : (
        <div className="space-y-4">
          {sectionsBySemester.map(([semester, sections]) => (
            <SemesterSectionGroup
              key={semester}
              semesterLabel={getSemesterLabel(semester)}
              sections={sections}
              deptId={deptId!}
              termId={termId}
              forecastMap={forecastMap}
            />
          ))}
        </div>
      )}

      {showAddDialog && deptId && (
        <AddCourseDialog
          departmentId={deptId}
          termId={termId}
          forecastMap={forecastMap}
          onClose={() => setShowAddDialog(false)}
        />
      )}

      {showBulkDialog && deptId && (
        <BulkOpenDialog
          departmentId={deptId}
          termId={termId}
          forecastMap={forecastMap}
          onClose={() => setShowBulkDialog(false)}
        />
      )}
    </div>
  );
}
