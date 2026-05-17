import { useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { AlertCircle, CheckCircle2, Zap } from 'lucide-react';
import {
  useBulkOpenCourses,
  useCurriculumCoursesForOpening,
  useDepartmentInstructors,
} from '../../hooks/useCourseOpening';
import type { CourseDemandForecastDto } from '../../types/forecast';

interface CourseSettings {
  sectionCount: number;
  capacity: number;
  instructorId: string;
}

export function BulkOpenDialog({
  departmentId,
  termId,
  forecastMap,
  onClose,
}: {
  departmentId: number;
  termId: number;
  forecastMap: Map<number, CourseDemandForecastDto>;
  onClose: () => void;
}) {
  const [showAll, setShowAll] = useState(false);
  const curriculumQuery = useCurriculumCoursesForOpening(departmentId, termId, showAll);
  const instructorsQuery = useDepartmentInstructors(departmentId);
  const bulkMutation = useBulkOpenCourses(departmentId, termId);

  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [courseSettings, setCourseSettings] = useState<Map<number, CourseSettings>>(new Map());

  const courses = curriculumQuery.data ?? [];
  const unopenedCourses = courses.filter(course => !course.alreadyOpened && course.canOpen);
  const blockedCourses = courses.filter(course => !course.alreadyOpened && !course.canOpen);
  const blockedUnopenedCount = blockedCourses.length;
  const blockedReasonBreakdown = (() => {
    const counts: Record<string, number> = {};
    for (const c of blockedCourses) {
      const key = c.blockReason ?? 'Other';
      counts[key] = (counts[key] ?? 0) + 1;
    }
    const labels: Record<string, string> = {
      TermNotInPlanning: 'Dönem planlamada değil',
      CourseUnassigned: 'Sahip bölüm atanmamış',
      NotOwner: 'Başka bölümün dersi',
      Other: 'Diğer',
    };
    return Object.entries(counts)
      .map(([key, count]) => `${labels[key] ?? key}: ${count}`)
      .join(' • ');
  })();

  const [initialized, setInitialized] = useState(false);
  if (!initialized && unopenedCourses.length > 0) {
    const compulsoryIds = new Set(unopenedCourses.filter(course => course.isCompulsory).map(course => course.courseId));
    setSelectedIds(compulsoryIds);

    const settings = new Map<number, CourseSettings>();
    for (const course of unopenedCourses) {
      const forecast = forecastMap.get(course.courseId);
      settings.set(course.courseId, {
        sectionCount: Math.max(1, Math.min(20, forecast?.recommendedSectionCount ?? 20)),
        capacity: forecast?.sectionDistribution?.effectiveCapacityPerSection ?? course.recommendedCapacity ?? 40,
        instructorId: '',
      });
    }
    setCourseSettings(settings);
    setInitialized(true);
  }

  const getSettings = (courseId: number): CourseSettings => {
    return courseSettings.get(courseId) ?? { sectionCount: 1, capacity: 40, instructorId: '' };
  };

  const updateSetting = (courseId: number, field: keyof CourseSettings, value: number | string) => {
    setCourseSettings(prev => {
      const next = new Map(prev);
      const current = next.get(courseId) ?? { sectionCount: 1, capacity: 40, instructorId: '' };
      next.set(courseId, {
        ...current,
        [field]: field === 'sectionCount'
          ? Math.max(1, Math.min(20, Number(value)))
          : value,
      });
      return next;
    });
  };

  const toggleCourse = (courseId: number) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(courseId)) next.delete(courseId);
      else next.add(courseId);
      return next;
    });
  };

  const toggleAll = () => {
    if (selectedIds.size === unopenedCourses.length) setSelectedIds(new Set());
    else setSelectedIds(new Set(unopenedCourses.map(course => course.courseId)));
  };

  const fillFromForecast = () => {
    setCourseSettings(prev => {
      const next = new Map(prev);
      for (const courseId of selectedIds) {
        const forecast = forecastMap.get(courseId);
        const course = unopenedCourses.find(item => item.courseId === courseId);
        if (forecast) {
          next.set(courseId, {
            sectionCount: Math.max(1, Math.min(20, forecast.recommendedSectionCount ?? 20)),
            capacity: forecast.sectionDistribution?.effectiveCapacityPerSection ?? course?.recommendedCapacity ?? 40,
            instructorId: next.get(courseId)?.instructorId ?? '',
          });
        }
      }
      return next;
    });
  };

  const selectedCourses = unopenedCourses.filter(course => selectedIds.has(course.courseId));
  const totalSelectedSections = selectedCourses.reduce((sum, course) => sum + getSettings(course.courseId).sectionCount, 0);
  const totalSelectedCapacity = selectedCourses.reduce((sum, course) => {
    const settings = getSettings(course.courseId);
    return sum + settings.sectionCount * settings.capacity;
  }, 0);

  const handleSubmit = () => {
    if (selectedIds.size === 0) return;
    bulkMutation.mutate({
      courses: Array.from(selectedIds).map(courseId => {
        const settings = getSettings(courseId);
        return {
          courseId,
          sectionCount: settings.sectionCount,
          capacityPerSection: settings.capacity,
          instructorId: settings.instructorId ? Number(settings.instructorId) : undefined,
        };
      }),
    });
  };

  return (
    <Dialog open onOpenChange={onClose}>
      <DialogContent className="max-w-4xl max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Toplu Ders Aç</DialogTitle>
          <DialogDescription>Seçilen müfredat derslerini tek seferde açın. Her ders için şube sayısı ve kontenjan ayrı ayarlanabilir.</DialogDescription>
        </DialogHeader>

        {curriculumQuery.isLoading ? <Skeleton className="h-48" /> : (
          <>
            <div className="flex items-center gap-3 mb-3 flex-wrap">
              <Button size="sm" variant="outline" onClick={toggleAll}>
                {selectedIds.size === unopenedCourses.length ? 'Hiçbirini Seçme' : 'Tümünü Seç'}
              </Button>
              <Button size="sm" variant="outline" onClick={fillFromForecast} disabled={selectedIds.size === 0}>
                <Zap className="h-3.5 w-3.5 mr-1" /> Tahminle Doldur
              </Button>
              <span className="text-sm text-muted-foreground">{selectedIds.size} ders seçili</span>
              {blockedUnopenedCount > 0 && (
                <span
                  className="text-xs text-amber-700 cursor-help"
                  title={blockedReasonBreakdown || undefined}
                >
                  {blockedUnopenedCount} ders toplu açmaya dahil edilmedi
                </span>
              )}
              <label className="flex items-center gap-1.5 text-sm text-gray-600 whitespace-nowrap cursor-pointer select-none ml-auto">
                <input type="checkbox" checked={showAll} onChange={event => setShowAll(event.target.checked)} className="rounded border-gray-300" />
                Tüm dönemler
              </label>
            </div>

            <div className="max-h-72 overflow-y-auto border rounded-lg">
              <Table>
                <TableHeader>
                  <TableRow className="bg-gray-50/50">
                    <TableHead className="w-8" />
                    <TableHead className="font-semibold">Kod</TableHead>
                    <TableHead className="font-semibold">Ders Adı</TableHead>
                    <TableHead className="font-semibold text-center">AKTS</TableHead>
                    <TableHead className="font-semibold text-center">Sınıf</TableHead>
                    <TableHead className="font-semibold text-center">Talep</TableHead>
                    <TableHead className="font-semibold text-center w-20">Şube</TableHead>
                    <TableHead className="font-semibold text-center w-20">Kont.</TableHead>
                    <TableHead className="font-semibold w-48">Öğretim Elemanı</TableHead>
                    <TableHead className="font-semibold">Tür</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {unopenedCourses.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={10} className="text-center text-muted-foreground py-6">
                        Tüm müfredat dersleri zaten açılmış.
                      </TableCell>
                    </TableRow>
                  ) : (
                    unopenedCourses.map(course => {
                      const isSelected = selectedIds.has(course.courseId);
                      const settings = getSettings(course.courseId);
                      const forecast = forecastMap.get(course.courseId);

                      return (
                        <TableRow key={course.courseId} className={`cursor-pointer ${isSelected ? 'bg-primary/5' : ''}`} onClick={() => toggleCourse(course.courseId)}>
                          <TableCell onClick={event => event.stopPropagation()}>
                            <input type="checkbox" checked={isSelected} onChange={() => toggleCourse(course.courseId)} />
                          </TableCell>
                          <TableCell><span className="font-mono text-xs font-semibold">{course.courseCode}</span></TableCell>
                          <TableCell className="text-sm">{course.courseName}</TableCell>
                          <TableCell className="text-center text-xs">{course.ects}</TableCell>
                          <TableCell className="text-center text-xs">{course.suggestedSemester > 0 ? Math.ceil(course.suggestedSemester / 2) : '—'}</TableCell>
                          <TableCell className="text-center text-xs font-semibold">
                            {forecast ? (
                              <TooltipProvider delayDuration={150}>
                                <Tooltip>
                                  <TooltipTrigger asChild>
                                    <div className="cursor-default">
                                      <div>{forecast.totalPredictedDemand}</div>
                                      {forecast.recommendedSectionCount > 0 && (
                                        <div className="text-[10px] text-blue-600 font-normal">→ {forecast.recommendedSectionCount} şube</div>
                                      )}
                                    </div>
                                  </TooltipTrigger>
                                  <TooltipContent side="top" className="max-w-xs text-xs space-y-1">
                                    {course.previousTermEnrollment != null && (
                                      <p>Geçen aynı dönemde kayıt: {course.previousTermEnrollment}</p>
                                    )}
                                    {forecast.historicalEnrollments.length > 0 ? (
                                      <>
                                        <p className="font-medium pt-1 border-t">Geçmiş dönemler</p>
                                        {forecast.historicalEnrollments.slice(-3).reverse().map(history => (
                                          <p key={history.termId}>{history.termName}: {history.actualEnrollment} öğrenci, {history.sectionCount} şube</p>
                                        ))}
                                      </>
                                    ) : (
                                      <p>Tarihsel kayıt verisi bulunmuyor.</p>
                                    )}
                                  </TooltipContent>
                                </Tooltip>
                              </TooltipProvider>
                            ) : '—'}
                          </TableCell>
                          <TableCell className="text-center" onClick={event => event.stopPropagation()}>
                            <Input
                              type="number"
                              min={1}
                              max={20}
                              className="h-7 w-14 text-xs text-center mx-auto"
                              value={settings.sectionCount}
                              onChange={event => updateSetting(course.courseId, 'sectionCount', Number(event.target.value))}
                            />
                          </TableCell>
                          <TableCell className="text-center" onClick={event => event.stopPropagation()}>
                            <Input
                              type="number"
                              min={1}
                              max={500}
                              className="h-7 w-14 text-xs text-center mx-auto"
                              value={settings.capacity}
                              onChange={event => updateSetting(course.courseId, 'capacity', Math.max(1, Number(event.target.value)))}
                            />
                          </TableCell>
                          <TableCell onClick={event => event.stopPropagation()}>
                            <select
                              className="h-8 w-full rounded border border-input bg-background px-2 text-xs"
                              value={settings.instructorId}
                              onChange={event => updateSetting(course.courseId, 'instructorId', event.target.value)}
                            >
                              <option value="">Seçiniz</option>
                              {(instructorsQuery.data ?? []).map(instructor => (
                                <option key={instructor.instructorId} value={instructor.instructorId}>{instructor.fullName}</option>
                              ))}
                            </select>
                          </TableCell>
                          <TableCell>
                            <Badge variant={course.isCompulsory ? 'default' : 'outline'} className="text-[10px]">
                              {course.isCompulsory ? 'Zorunlu' : 'Seçmeli'}
                            </Badge>
                          </TableCell>
                        </TableRow>
                      );
                    })
                  )}
                </TableBody>
              </Table>
            </div>

            {selectedIds.size > 0 && (
              <div className="flex items-center gap-4 text-sm bg-gray-50 rounded-lg px-4 py-2.5 mt-3">
                <span className="font-medium">Toplam:</span>
                <Badge variant="outline" className="py-1">{selectedIds.size} ders</Badge>
                <Badge variant="outline" className="py-1">{totalSelectedSections} şube</Badge>
                <Badge variant="outline" className="py-1">{totalSelectedCapacity} kontenjan</Badge>
              </div>
            )}
          </>
        )}

        {bulkMutation.data && (
          <Alert variant={bulkMutation.data.errors.length > 0 ? 'destructive' : 'default'} className="mt-3">
            <CheckCircle2 className="h-4 w-4" />
            <AlertTitle>Sonuç</AlertTitle>
            <AlertDescription>
              <p>{bulkMutation.data.totalCoursesOpened} ders açıldı, {bulkMutation.data.totalSectionsCreated} şube oluşturuldu.</p>
              {bulkMutation.data.errors.map((error, index) => (
                <p key={index} className="text-xs text-destructive">{error}</p>
              ))}
            </AlertDescription>
          </Alert>
        )}

        {bulkMutation.isError && (
          <Alert variant="destructive" className="mt-2">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              {(() => {
                const e = bulkMutation.error as {
                  response?: { data?: { error?: string; detail?: string; title?: string } };
                  message?: string;
                };
                return (
                  e?.response?.data?.error ??
                  e?.response?.data?.detail ??
                  e?.response?.data?.title ??
                  e?.message ??
                  'Toplu ders açma işlemi başarısız oldu.'
                );
              })()}
            </AlertDescription>
          </Alert>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Kapat</Button>
          <Button onClick={handleSubmit} disabled={selectedIds.size === 0 || bulkMutation.isPending}>
            {bulkMutation.isPending ? 'Açılıyor...' : `Seçilen Dersleri Aç (${selectedIds.size})`}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
