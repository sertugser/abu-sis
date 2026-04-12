import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
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
import { AlertCircle } from 'lucide-react';
import {
  useCurriculumCoursesForOpening,
  useDepartmentInstructors,
  useInstructorWorkload,
  useOpenCourse,
} from '../../hooks/useCourseOpening';
import type { CurriculumCourseForOpeningDto } from '../../types/courseOpening';
import type { CourseDemandForecastDto } from '../../types/forecast';
import { ForecastSummaryPanel } from './ForecastSummaryPanel';
import { WorkloadBar } from './WorkloadBar';

export function AddCourseDialog({
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
  const openMutation = useOpenCourse(departmentId, termId);

  const [selectedCourse, setSelectedCourse] = useState<CurriculumCourseForOpeningDto | null>(null);
  const [sectionCount, setSectionCount] = useState(1);
  const [capacity, setCapacity] = useState(40);
  const [instructorId, setInstructorId] = useState('');
  const [search, setSearch] = useState('');
  const [selectedDeptIds, setSelectedDeptIds] = useState<number[]>([]);

  const workloadQuery = useInstructorWorkload(instructorId ? Number(instructorId) : null, termId);

  const handleSelectCourse = (course: CurriculumCourseForOpeningDto) => {
    setSelectedCourse(course);
    const suggestedSectionCount = course.recommendedSectionCount ?? 1;
    setSectionCount(Math.max(1, suggestedSectionCount));
    if (course.recommendedCapacity != null && course.recommendedCapacity > 0) {
      setCapacity(course.recommendedCapacity);
    }

    const deptIds = (course.crossDepartmentDemand ?? []).map(item => item.departmentId);
    if (!deptIds.includes(departmentId)) deptIds.push(departmentId);
    setSelectedDeptIds(deptIds);
  };

  const filteredCourses = (curriculumQuery.data ?? []).filter(
    course => course.courseCode.toLowerCase().includes(search.toLowerCase())
      || course.courseName.toLowerCase().includes(search.toLowerCase()),
  );

  const handleSubmit = () => {
    if (!selectedCourse) return;
    openMutation.mutate(
      {
        courseId: selectedCourse.courseId,
        sectionCount,
        capacityPerSection: capacity,
        instructorId: instructorId ? Number(instructorId) : undefined,
        accessibleDepartmentIds: selectedDeptIds,
      },
      { onSuccess: () => { setSelectedCourse(null); onClose(); } },
    );
  };

  const forecast = selectedCourse ? forecastMap.get(selectedCourse.courseId) : null;
  const selectedSectionLimit = 20;

  return (
    <Dialog open onOpenChange={onClose}>
      <DialogContent className="max-w-4xl max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Ders Aç</DialogTitle>
          <DialogDescription>Müfredat derslerinden seçim yaparak yeni şube açın.</DialogDescription>
        </DialogHeader>

        {curriculumQuery.isLoading ? <Skeleton className="h-48" /> : (
          <>
            <div className="flex items-center gap-3 mb-3">
              <Input placeholder="Ders kodu veya adı ile ara..." value={search} onChange={event => setSearch(event.target.value)} className="flex-1" />
              <label className="flex items-center gap-1.5 text-sm text-gray-600 whitespace-nowrap cursor-pointer select-none">
                <input type="checkbox" checked={showAll} onChange={event => setShowAll(event.target.checked)} className="rounded border-gray-300" />
                Tüm dönemler
              </label>
            </div>

            <div className="max-h-60 overflow-y-auto border rounded-lg">
              <Table>
                <TableHeader>
                  <TableRow className="bg-gray-50/50">
                    <TableHead className="w-8" />
                    <TableHead className="font-semibold">Kod</TableHead>
                    <TableHead className="font-semibold">Ders Adı</TableHead>
                    <TableHead className="font-semibold text-center">AKTS</TableHead>
                    <TableHead className="font-semibold text-center">Sınıf</TableHead>
                    <TableHead className="font-semibold text-center">Talep</TableHead>
                    <TableHead className="font-semibold">Durum</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {filteredCourses.map(course => (
                    <TableRow key={course.courseId} className={`cursor-pointer ${selectedCourse?.courseId === course.courseId ? 'bg-primary/5' : ''}`} onClick={() => handleSelectCourse(course)}>
                      <TableCell><input type="radio" checked={selectedCourse?.courseId === course.courseId} onChange={() => handleSelectCourse(course)} /></TableCell>
                      <TableCell><span className="font-mono text-xs font-semibold">{course.courseCode}</span></TableCell>
                      <TableCell>
                        <div className="text-sm">{course.courseName}</div>
                        {course.crossDepartmentDemand && course.crossDepartmentDemand.length > 0 && (
                          <div className="flex gap-1 mt-0.5 flex-wrap">
                            {course.crossDepartmentDemand.map((crossDepartment, index) => (
                              <span key={index} className="text-[10px] px-1.5 py-0 rounded-full bg-sky-100 text-sky-700">
                                {crossDepartment.departmentName}({crossDepartment.estimatedStudentCount})
                              </span>
                            ))}
                          </div>
                        )}
                      </TableCell>
                      <TableCell className="text-center">{course.ects}</TableCell>
                      <TableCell className="text-center">{Math.ceil(course.suggestedSemester / 2)}</TableCell>
                      <TableCell className="text-center">
                        {course.predictedDemand != null ? (
                          <div className="text-xs space-y-0.5">
                            <div className="font-semibold">{course.predictedDemand}</div>
                            {course.retakeCount != null && course.retakeCount > 0 && (
                              <div className="text-red-600">↻ {course.retakeCount}</div>
                            )}
                          </div>
                        ) : '—'}
                      </TableCell>
                      <TableCell>
                        {course.alreadyOpened ? (
                          <Badge variant="secondary" className="text-[10px]">{course.existingSectionCount} şube açık</Badge>
                        ) : course.canOpen ? (
                          <Badge variant="success" className="text-[10px]">Açılabilir</Badge>
                        ) : (
                          <Badge variant="outline" className="text-[10px]">Açılamaz</Badge>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>

            {forecast && <ForecastSummaryPanel forecast={forecast} />}

            {selectedCourse && (
              <>
                <div className="grid grid-cols-3 gap-3 mt-4">
                  <div className="space-y-1.5">
                    <label className="text-sm font-medium">Şube Sayısı</label>
                    <Input
                      type="number"
                      min={1}
                      max={selectedSectionLimit}
                      value={sectionCount}
                      onChange={event => setSectionCount(Math.max(1, Math.min(selectedSectionLimit, Number(event.target.value))))}
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-sm font-medium">Kontenjan</label>
                    <Input type="number" min={1} max={500} value={capacity} onChange={event => setCapacity(Number(event.target.value))} />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-sm font-medium">Öğretim Elemanı</label>
                    <select
                      className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                      value={instructorId}
                      onChange={event => setInstructorId(event.target.value)}
                    >
                      <option value="">Seçiniz (opsiyonel)</option>
                      {(instructorsQuery.data ?? []).map(instructor => (
                        <option key={instructor.instructorId} value={instructor.instructorId}>{instructor.fullName}</option>
                      ))}
                    </select>
                    {workloadQuery.data && (
                      <div className="mt-1"><WorkloadBar workload={workloadQuery.data} /></div>
                    )}
                  </div>
                </div>

                {(selectedCourse.crossDepartmentDemand ?? []).length > 0 && (
                  <div className="mt-3 p-3 bg-blue-50 rounded-lg border border-blue-200">
                    <label className="text-sm font-medium text-blue-900 block mb-2">Dersi Alabilecek Bölümler</label>
                    <div className="grid grid-cols-2 gap-1.5">
                      {(selectedCourse.crossDepartmentDemand ?? []).map(department => (
                        <label key={department.departmentId} className="flex items-center gap-2 text-sm cursor-pointer hover:bg-blue-100 rounded px-1.5 py-1">
                          <input
                            type="checkbox"
                            className="rounded border-gray-300"
                            checked={selectedDeptIds.includes(department.departmentId)}
                            onChange={event => {
                              if (event.target.checked) {
                                setSelectedDeptIds(prev => [...prev, department.departmentId]);
                              } else {
                                setSelectedDeptIds(prev => prev.filter(id => id !== department.departmentId));
                              }
                            }}
                          />
                          <span>{department.departmentName}</span>
                          <span className="text-xs text-gray-500">({department.estimatedStudentCount} öğrenci)</span>
                        </label>
                      ))}
                    </div>
                    <p className="text-xs text-blue-700 mt-1.5">Müfredatına göre otomatik seçildi. İstediğiniz bölümleri kaldırabilir veya ekleyebilirsiniz.</p>
                  </div>
                )}
              </>
            )}
          </>
        )}

        {openMutation.isError && (
          <Alert variant="destructive" className="mt-2">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              {(openMutation.error as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Ders açılamadı.'}
            </AlertDescription>
          </Alert>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>İptal</Button>
          <Button onClick={handleSubmit} disabled={!selectedCourse || !selectedCourse.canOpen || openMutation.isPending}>
            {openMutation.isPending ? 'Açılıyor...' : 'Ders Aç'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
