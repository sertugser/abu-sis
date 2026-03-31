import React, { useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  AlertCircle, CheckCircle2, Edit2, Package, Plus, Trash2, X,
} from 'lucide-react';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import {
  useOpenedCourses,
  useCurriculumCoursesForOpening,
  useDepartmentInstructors,
  useOpenCourse,
  useBulkOpenCourses,
  useUpdateOpenedSection,
  useCloseSection,
} from '../hooks/useCourseOpening';
import type { CurriculumCourseForOpeningDto } from '../types/courseOpening';
import { useDepartmentForecast } from '../hooks/useForecast';
import type { CourseDemandForecastDto } from '../types/forecast';

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

  // Build courseId -> forecast map
  const forecastMap = React.useMemo(() => {
    const map = new Map<number, CourseDemandForecastDto>();
    if (forecastData?.courseSummaries) {
      for (const cs of forecastData.courseSummaries) {
        map.set(cs.courseId, cs);
      }
    }
    return map;
  }, [forecastData]);

  // Group opened sections by semester
  const openedSections = openedQuery.data?.sections;
  const sectionsBySemester = React.useMemo(() => {
    if (!openedSections) return [];
    const map = new Map<number, typeof openedSections>();
    for (const section of openedSections) {
      const sem = section.suggestedSemester || 0;
      if (!map.has(sem)) map.set(sem, []);
      map.get(sem)!.push(section);
    }
    return [...map.entries()].sort((a, b) => a[0] - b[0]);
  }, [openedSections]);

  const getSemesterLabel = (sem: number) => {
    if (sem === 0) return 'Diger Dersler';
    const year = Math.ceil(sem / 2);
    const isGuz = sem % 2 === 1;
    return `${year}. Sinif ${isGuz ? 'Guz' : 'Bahar'}`;
  };

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
    <div className="space-y-6">
      {/* Üst bar - Filtreler */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex items-center gap-2">
          <label className="text-sm font-medium">Dönem:</label>
          <Badge variant="outline">{activeTerm?.name ?? 'Yükleniyor...'}</Badge>
        </div>

        <div className="flex items-center gap-2">
          <label className="text-sm font-medium">Bölüm:</label>
          <select
            className="h-9 rounded-md border border-input bg-background px-3 text-sm"
            value={deptId ?? ''}
            onChange={e => setSelectedDeptId(Number(e.target.value))}
          >
            {departments.map(d => (
              <option key={d.departmentId} value={d.departmentId}>
                {d.departmentCode} — {d.departmentName}
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

      {/* Ana tablo */}
      {openedQuery.isLoading || (!openedQuery.data && !openedQuery.isError) ? (
        <Skeleton className="h-64 rounded-xl" />
      ) : openedQuery.isError ? (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Veriler yüklenemedi</AlertTitle>
          <AlertDescription>
            {(openedQuery.error as { response?: { status?: number } })?.response?.status === 403
              ? 'Bu bölüm için yetkiniz bulunmuyor.'
              : 'Açılan ders verileri alınamadı. Lütfen tekrar deneyin.'}
          </AlertDescription>
        </Alert>
      ) : (
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm">
              {openedQuery.data!.departmentName} — {openedQuery.data!.termName} ({openedQuery.data!.totalSections} sube)
            </CardTitle>
          </CardHeader>
          <CardContent>
            {sectionsBySemester.length > 0 ? (
              sectionsBySemester.map(([sem, sections]) => (
                <SemesterSectionGroup
                  key={sem}
                  semesterLabel={getSemesterLabel(sem)}
                  sections={sections}
                  sectionCount={sections.length}
                  deptId={deptId!}
                  termId={termId!}
                />
              ))
            ) : (
              <div className="text-center py-8 text-slate-500">Henuz ders acilmamis.</div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Tek Ders Ekleme Dialog */}
      {showAddDialog && deptId && termId && (
        <AddCourseDialog
          departmentId={deptId}
          termId={termId}
          forecastMap={forecastMap}
          onClose={() => setShowAddDialog(false)}
        />
      )}

      {/* Toplu Ders Açma Dialog */}
      {showBulkDialog && deptId && termId && (
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

// ── Semester Section Group ──

function SemesterSectionGroup({
  semesterLabel,
  sections,
  sectionCount,
  deptId,
  termId,
}: {
  semesterLabel: string;
  sections: import('../types/courseOpening').OpenedCourseSectionDto[];
  sectionCount: number;
  deptId: number;
  termId: number;
}) {
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editCapacity, setEditCapacity] = useState('');
  const [editInstructorId, setEditInstructorId] = useState('');
  const updateMutation = useUpdateOpenedSection(deptId, termId);
  const closeMutation = useCloseSection(deptId, termId);
  const instructorsQuery = useDepartmentInstructors(deptId);

  const handleEdit = (s: import('../types/courseOpening').OpenedCourseSectionDto) => {
    setEditingId(s.sectionId);
    setEditCapacity(s.capacity.toString());
    setEditInstructorId(s.instructorId?.toString() ?? '');
  };

  const handleSave = () => {
    if (editingId === null) return;
    updateMutation.mutate(
      {
        sectionId: editingId,
        capacity: editCapacity ? Number(editCapacity) : undefined,
        instructorId: editInstructorId ? Number(editInstructorId) : undefined,
      },
      { onSuccess: () => setEditingId(null) },
    );
  };

  const handleClose = (sectionId: number) => {
    if (confirm('Bu subeyi kapatmak istediginize emin misiniz?')) {
      closeMutation.mutate(sectionId);
    }
  };

  return (
    <div className="mb-6">
      <div className="flex items-center justify-between bg-slate-100 px-4 py-2 rounded-t-lg border border-b-0 border-slate-200">
        <h3 className="text-sm font-semibold text-slate-700">
          {semesterLabel}
        </h3>
        <span className="text-xs text-slate-500">{sectionCount} section</span>
      </div>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-12">Sb.</TableHead>
            <TableHead>Ders Kodu</TableHead>
            <TableHead>Ders Adi</TableHead>
            <TableHead className="w-16">T+U</TableHead>
            <TableHead className="w-12">K</TableHead>
            <TableHead className="w-16">AKTS</TableHead>
            <TableHead className="w-12">Snf</TableHead>
            <TableHead className="w-28">KO/DO/K</TableHead>
            <TableHead>Ogretim Elemani</TableHead>
            <TableHead className="w-24">Islemler</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {sections.map(s => (
            <TableRow key={s.sectionId} className={!s.isActive ? 'opacity-50' : ''}>
              <TableCell className="text-center">{s.sectionNumber}</TableCell>
              <TableCell>
                <span className="font-mono text-xs font-semibold text-primary">{s.courseCode}</span>
              </TableCell>
              <TableCell className="text-sm">{s.courseName}</TableCell>
              <TableCell className="text-center">{s.theoryHours}+{s.labHours}</TableCell>
              <TableCell className="text-center">{s.credits}</TableCell>
              <TableCell className="text-center">{s.ects}</TableCell>
              <TableCell className="text-center">{s.suggestedSemester > 0 ? Math.ceil(s.suggestedSemester / 2) : '\u2014'}</TableCell>
              <TableCell className="text-center">
                {editingId === s.sectionId ? (
                  <Input
                    className="h-7 w-20 text-xs"
                    value={editCapacity}
                    onChange={e => setEditCapacity(e.target.value)}
                  />
                ) : (
                  <span>{s.capacity} / {s.enrolledCount} / {s.registeredCount}</span>
                )}
              </TableCell>
              <TableCell>
                {editingId === s.sectionId ? (
                  <select
                    className="h-7 w-full rounded border border-input bg-background px-1 text-xs"
                    value={editInstructorId}
                    onChange={e => setEditInstructorId(e.target.value)}
                  >
                    <option value="">Seciniz</option>
                    {(instructorsQuery.data ?? []).map(inst => (
                      <option key={inst.instructorId} value={inst.instructorId}>{inst.fullName}</option>
                    ))}
                  </select>
                ) : (
                  <span className="text-sm">{s.instructorName ?? '\u2014'}</span>
                )}
              </TableCell>
              <TableCell>
                <div className="flex gap-1">
                  {editingId === s.sectionId ? (
                    <>
                      <Button size="icon" variant="ghost" className="h-7 w-7" onClick={handleSave} disabled={updateMutation.isPending}>
                        <CheckCircle2 className="h-4 w-4 text-green-600" />
                      </Button>
                      <Button size="icon" variant="ghost" className="h-7 w-7" onClick={() => setEditingId(null)}>
                        <X className="h-4 w-4" />
                      </Button>
                    </>
                  ) : (
                    <>
                      <Button size="icon" variant="ghost" className="h-7 w-7" onClick={() => handleEdit(s)} disabled={!s.isActive}>
                        <Edit2 className="h-4 w-4" />
                      </Button>
                      <Button
                        size="icon"
                        variant="ghost"
                        className="h-7 w-7 text-destructive"
                        onClick={() => handleClose(s.sectionId)}
                        disabled={closeMutation.isPending || !s.isActive}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </>
                  )}
                </div>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      {closeMutation.isError && (
        <Alert variant="destructive" className="mt-3">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            {(closeMutation.error as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Sube kapatılamadı.'}
          </AlertDescription>
        </Alert>
      )}
    </div>
  );
}

// ── Tek Ders Ekleme Dialog ──

function AddCourseDialog({
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
  const [instructorId, setInstructorId] = useState<string>('');
  const [search, setSearch] = useState('');

  const handleSelectCourse = (c: CurriculumCourseForOpeningDto) => {
    setSelectedCourse(c);
    if (c.recommendedSectionCount != null && c.recommendedSectionCount > 0) {
      setSectionCount(c.recommendedSectionCount);
    }
    if (c.recommendedCapacity != null && c.recommendedCapacity > 0) {
      setCapacity(c.recommendedCapacity);
    }
  };

  const filteredCourses = (curriculumQuery.data ?? []).filter(
    c => c.courseCode.toLowerCase().includes(search.toLowerCase()) || c.courseName.toLowerCase().includes(search.toLowerCase()),
  );

  const handleSubmit = () => {
    if (!selectedCourse) return;
    openMutation.mutate(
      {
        courseId: selectedCourse.courseId,
        sectionCount,
        capacityPerSection: capacity,
        instructorId: instructorId ? Number(instructorId) : undefined,
      },
      {
        onSuccess: () => {
          setSelectedCourse(null);
          onClose();
        },
      },
    );
  };

  return (
    <Dialog open onOpenChange={onClose}>
      <DialogContent className="max-w-3xl max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Ders Aç</DialogTitle>
          <DialogDescription>Müfredat derslerinden seçim yaparak yeni şube açın.</DialogDescription>
        </DialogHeader>

        {curriculumQuery.isLoading ? (
          <Skeleton className="h-48" />
        ) : (
          <>
            <div className="flex items-center gap-3 mb-3">
              <Input
                placeholder="Ders kodu veya adı ile ara..."
                value={search}
                onChange={e => setSearch(e.target.value)}
                className="flex-1"
              />
              <label className="flex items-center gap-1.5 text-sm text-gray-600 whitespace-nowrap cursor-pointer select-none">
                <input
                  type="checkbox"
                  checked={showAll}
                  onChange={e => setShowAll(e.target.checked)}
                  className="rounded border-gray-300"
                />
                Tüm dersler
              </label>
            </div>

            <div className="max-h-60 overflow-y-auto border rounded-md">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-10" />
                    <TableHead>Ders Kodu</TableHead>
                    <TableHead>Ders Adı</TableHead>
                    <TableHead>AKTS</TableHead>
                    <TableHead>Snf</TableHead>
                    <TableHead>Talep</TableHead>
                    <TableHead>Durum</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {filteredCourses.map(c => (
                    <TableRow
                      key={c.courseId}
                      className={`cursor-pointer ${selectedCourse?.courseId === c.courseId ? 'bg-primary/10' : ''}`}
                      onClick={() => handleSelectCourse(c)}
                    >
                      <TableCell>
                        <input
                          type="radio"
                          checked={selectedCourse?.courseId === c.courseId}
                          onChange={() => handleSelectCourse(c)}
                        />
                      </TableCell>
                      <TableCell>
                        <span className="font-mono text-xs">{c.courseCode}</span>
                      </TableCell>
                      <TableCell>
                        <div className="text-sm">{c.courseName}</div>
                        {c.crossDepartmentDemand && c.crossDepartmentDemand.length > 0 && (
                          <div className="flex gap-1 mt-0.5 flex-wrap">
                            {c.crossDepartmentDemand.map((cd, idx) => (
                              <Badge key={idx} variant="outline" className="text-[10px] px-1 py-0">
                                {cd.departmentName}({cd.estimatedStudentCount})
                              </Badge>
                            ))}
                          </div>
                        )}
                      </TableCell>
                      <TableCell>{c.ects}</TableCell>
                      <TableCell>{Math.ceil(c.suggestedSemester / 2)}</TableCell>
                      <TableCell>
                        <div className="space-y-0.5">
                          {c.predictedDemand != null && (
                            <div className="text-xs text-muted-foreground">Talep: {c.predictedDemand}</div>
                          )}
                          {c.retakeCount != null && c.retakeCount > 0 && (
                            <div className="text-xs text-red-600 font-medium">Alttan: {c.retakeCount}</div>
                          )}
                          {c.recommendedSectionCount != null && (
                            <div className="text-xs text-blue-600">Onerilen: {c.recommendedSectionCount} sube</div>
                          )}
                        </div>
                      </TableCell>
                      <TableCell>
                        {c.alreadyOpened ? (
                          <Badge variant="secondary">{c.existingSectionCount} şube açık</Badge>
                        ) : (
                          <Badge variant="outline">{c.isCompulsory ? 'Zorunlu' : 'Seçmeli'}</Badge>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>

            {selectedCourse && forecastMap.get(selectedCourse.courseId) && (() => {
              const fc = forecastMap.get(selectedCourse.courseId)!;
              return (
                <div className="bg-blue-50 border border-blue-200 rounded-lg p-3 text-sm space-y-1 mt-4 mb-4">
                  <div className="font-medium text-blue-800">Talep Tahmini</div>
                  <div className="grid grid-cols-2 gap-x-4 gap-y-1 text-slate-700">
                    <span>Toplam Talep:</span>
                    <span className="font-semibold">{fc.totalPredictedDemand} ogrenci</span>
                    <span>Zorunlu (sinifta):</span>
                    <span>{fc.demandBreakdown.regularOnSchedule}</span>
                    <span>Tekrar (FF/NA):</span>
                    <span className="text-red-600 font-medium">{fc.demandBreakdown.totalRetake}</span>
                    <span>Ust siniftan:</span>
                    <span>{fc.demandBreakdown.upperCourse}</span>
                    <span>Geride kalan:</span>
                    <span>{fc.demandBreakdown.behindSchedule}</span>
                  </div>
                  <div className="pt-1 border-t border-blue-200 text-blue-700 font-medium">
                    Onerilen: {fc.recommendedSectionCount} section ({fc.sectionDistribution.effectiveCapacityPerSection} kisi/section)
                  </div>
                </div>
              );
            })()}

            {selectedCourse && (
              <div className="grid grid-cols-3 gap-3 mt-4">
                <div className="space-y-1">
                  <label className="text-xs font-medium">Şube Sayısı</label>
                  <Input type="number" min={1} max={10} value={sectionCount} onChange={e => setSectionCount(Number(e.target.value))} />
                </div>
                <div className="space-y-1">
                  <label className="text-xs font-medium">Kontenjan</label>
                  <Input type="number" min={1} max={300} value={capacity} onChange={e => setCapacity(Number(e.target.value))} />
                </div>
                <div className="space-y-1">
                  <label className="text-xs font-medium">Öğretim Elemanı (Opsiyonel)</label>
                  <select
                    className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                    value={instructorId}
                    onChange={e => setInstructorId(e.target.value)}
                  >
                    <option value="">Seçiniz</option>
                    {(instructorsQuery.data ?? []).map(inst => (
                      <option key={inst.instructorId} value={inst.instructorId}>{inst.fullName}</option>
                    ))}
                  </select>
                </div>
              </div>
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
          <Button onClick={handleSubmit} disabled={!selectedCourse || openMutation.isPending}>
            {openMutation.isPending ? 'Açılıyor...' : 'Ders Aç'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── Toplu Ders Açma Dialog ──

function BulkOpenDialog({
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
  const bulkMutation = useBulkOpenCourses(departmentId, termId);

  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [defaultCapacity, setDefaultCapacity] = useState(40);

  const courses = curriculumQuery.data ?? [];
  const unopenedCourses = courses.filter(c => !c.alreadyOpened);

  // Select all compulsory courses by default on first load
  const [initialized, setInitialized] = useState(false);
  if (!initialized && unopenedCourses.length > 0) {
    const compulsoryIds = new Set(unopenedCourses.filter(c => c.isCompulsory).map(c => c.courseId));
    setSelectedIds(compulsoryIds);
    setInitialized(true);
  }

  const toggleCourse = (courseId: number) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(courseId)) next.delete(courseId);
      else next.add(courseId);
      return next;
    });
  };

  const toggleAll = () => {
    if (selectedIds.size === unopenedCourses.length) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(unopenedCourses.map(c => c.courseId)));
    }
  };

  const handleSubmit = () => {
    if (selectedIds.size === 0) return;
    bulkMutation.mutate(
      {
        courses: Array.from(selectedIds).map(courseId => ({
          courseId,
          sectionCount: 1,
          capacityPerSection: defaultCapacity,
        })),
      },
      {
        onSuccess: () => {
          // Don't auto-close — show result
        },
      },
    );
  };

  return (
    <Dialog open onOpenChange={onClose}>
      <DialogContent className="max-w-3xl max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Toplu Ders Aç</DialogTitle>
          <DialogDescription>Seçilen müfredat derslerini tek seferde açın.</DialogDescription>
        </DialogHeader>

        {curriculumQuery.isLoading ? (
          <Skeleton className="h-48" />
        ) : (
          <>
            <div className="flex items-center gap-3 mb-3">
              <div className="space-y-1">
                <label className="text-xs font-medium">Varsayılan Kontenjan</label>
                <Input type="number" min={1} max={300} value={defaultCapacity} onChange={e => setDefaultCapacity(Number(e.target.value))} className="w-24" />
              </div>
              <Button size="sm" variant="outline" onClick={toggleAll} className="mt-5">
                {selectedIds.size === unopenedCourses.length ? 'Hiçbirini Seçme' : 'Tümünü Seç'}
              </Button>
              <span className="text-sm text-muted-foreground mt-5">
                {selectedIds.size} ders seçili
              </span>
              <label className="flex items-center gap-1.5 text-sm text-gray-600 whitespace-nowrap cursor-pointer select-none mt-5 ml-auto">
                <input
                  type="checkbox"
                  checked={showAll}
                  onChange={e => setShowAll(e.target.checked)}
                  className="rounded border-gray-300"
                />
                Tüm dersler
              </label>
            </div>

            <div className="max-h-64 overflow-y-auto border rounded-md">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-10" />
                    <TableHead>Ders Kodu</TableHead>
                    <TableHead>Ders Adı</TableHead>
                    <TableHead>AKTS</TableHead>
                    <TableHead className="px-3 py-2 text-right text-xs font-medium text-slate-500">Talep</TableHead>
                    <TableHead className="px-3 py-2 text-right text-xs font-medium text-slate-500">Onerilen</TableHead>
                    <TableHead>Snf</TableHead>
                    <TableHead>Tur</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {unopenedCourses.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={8} className="text-center text-muted-foreground py-6">
                        Tum mufredat dersleri zaten acilmis.
                      </TableCell>
                    </TableRow>
                  ) : (
                    unopenedCourses.map(c => (
                      <TableRow key={c.courseId} className="cursor-pointer" onClick={() => toggleCourse(c.courseId)}>
                        <TableCell>
                          <input type="checkbox" checked={selectedIds.has(c.courseId)} onChange={() => toggleCourse(c.courseId)} />
                        </TableCell>
                        <TableCell>
                          <span className="font-mono text-xs">{c.courseCode}</span>
                        </TableCell>
                        <TableCell className="text-sm">{c.courseName}</TableCell>
                        <TableCell>{c.ects}</TableCell>
                        <TableCell className="px-3 py-2 text-right text-sm tabular-nums">{forecastMap.get(c.courseId)?.totalPredictedDemand ?? '\u2014'}</TableCell>
                        <TableCell className="px-3 py-2 text-right text-sm font-medium tabular-nums">{forecastMap.get(c.courseId)?.recommendedSectionCount ?? '\u2014'}</TableCell>
                        <TableCell>{c.suggestedSemester > 0 ? Math.ceil(c.suggestedSemester / 2) : '\u2014'}</TableCell>
                        <TableCell>
                          <Badge variant={c.isCompulsory ? 'default' : 'outline'}>
                            {c.isCompulsory ? 'Zorunlu' : 'Secmeli'}
                          </Badge>
                        </TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>
          </>
        )}

        {bulkMutation.data && (
          <Alert variant={bulkMutation.data.errors.length > 0 ? 'destructive' : 'default'} className="mt-3">
            <CheckCircle2 className="h-4 w-4" />
            <AlertTitle>Sonuç</AlertTitle>
            <AlertDescription>
              <p>{bulkMutation.data.totalCoursesOpened} ders açıldı, {bulkMutation.data.totalSectionsCreated} şube oluşturuldu.</p>
              {bulkMutation.data.errors.map((err, i) => (
                <p key={i} className="text-xs text-destructive">{err}</p>
              ))}
            </AlertDescription>
          </Alert>
        )}

        {bulkMutation.isError && (
          <Alert variant="destructive" className="mt-2">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>Toplu ders açma işlemi başarısız oldu.</AlertDescription>
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
