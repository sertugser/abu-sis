import React, { useState, useMemo } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  Tooltip, TooltipContent, TooltipProvider, TooltipTrigger,
} from '@/components/ui/tooltip';
import {
  AlertCircle, AlertTriangle, CheckCircle2, Edit2, Layers, Package, Plus,
  Trash2, TrendingUp, Users, X, Zap,
} from 'lucide-react';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import {
  useOpenedCourses,
  useCurriculumCoursesForOpening,
  useDepartmentInstructors,
  useInstructorWorkload,
  useOpenCourse,
  useBulkOpenCourses,
  useUpdateOpenedSection,
  useCloseSection,
} from '../hooks/useCourseOpening';
import type {
  OpenedCourseSectionDto,
  CurriculumCourseForOpeningDto,
  InstructorWorkloadDto,
} from '../types/courseOpening';
import { useDepartmentForecast } from '../hooks/useForecast';
import type { CourseDemandForecastDto } from '../types/forecast';

// ── Helpers ────────────────────────────────────────────────────────

function getSemesterLabel(sem: number) {
  if (sem === 0) return 'Diğer Dersler';
  const year = Math.ceil(sem / 2);
  const isGuz = sem % 2 === 1;
  return `${year}. Sınıf ${isGuz ? 'Güz' : 'Bahar'}`;
}

function WorkloadBar({ workload }: { workload: InstructorWorkloadDto }) {
  const pct = workload.maxWeeklyHours > 0
    ? Math.round((workload.assignedWeeklyHours / workload.maxWeeklyHours) * 100) : 0;
  const color = pct >= 100 ? 'bg-red-500' : pct >= 80 ? 'bg-amber-500' : 'bg-emerald-500';
  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between text-xs">
        <span className="text-muted-foreground">{workload.title}</span>
        <span className="font-medium">
          {workload.assignedWeeklyHours}/{workload.maxWeeklyHours} saat
          <span className="text-muted-foreground ml-1">({workload.remainingCapacity} boş)</span>
        </span>
      </div>
      <div className="h-1.5 bg-gray-100 rounded-full overflow-hidden">
        <div className={`h-full rounded-full ${color}`} style={{ width: `${Math.min(pct, 100)}%` }} />
      </div>
    </div>
  );
}

// ── Main Page ──────────────────────────────────────────────────────

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
      for (const cs of forecastData.courseSummaries) map.set(cs.courseId, cs);
    }
    return map;
  }, [forecastData]);

  const openedSections = openedQuery.data?.sections;
  const sectionsBySemester = useMemo(() => {
    if (!openedSections) return [];
    const map = new Map<number, typeof openedSections>();
    for (const section of openedSections) {
      const sem = section.suggestedSemester || 0;
      if (!map.has(sem)) map.set(sem, []);
      map.get(sem)!.push(section);
    }
    return [...map.entries()].sort((a, b) => a[0] - b[0]);
  }, [openedSections]);

  // Summary stats
  const totalSections = openedQuery.data?.totalSections ?? 0;
  const totalCapacity = (openedSections ?? []).reduce((sum, s) => sum + s.capacity, 0);
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
      {/* ── Üst bar ──────────────────────────────────────────────── */}
      <div className="flex flex-wrap items-center gap-3">
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

      {/* ── Summary Kartlar ──────────────────────────────────────── */}
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

      {/* ── Ana tablo ────────────────────────────────────────────── */}
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
          {sectionsBySemester.map(([sem, sections]) => (
            <SemesterSectionGroup
              key={sem}
              semesterLabel={getSemesterLabel(sem)}
              sections={sections}
              deptId={deptId!}
              termId={termId!}
              forecastMap={forecastMap}
            />
          ))}
        </div>
      )}

      {showAddDialog && deptId && termId && (
        <AddCourseDialog departmentId={deptId} termId={termId} forecastMap={forecastMap} onClose={() => setShowAddDialog(false)} />
      )}
      {showBulkDialog && deptId && termId && (
        <BulkOpenDialog departmentId={deptId} termId={termId} forecastMap={forecastMap} onClose={() => setShowBulkDialog(false)} />
      )}
    </div>
  );
}

// ── Semester Section Group ─────────────────────────────────────────

function SemesterSectionGroup({
  semesterLabel, sections, deptId, termId, forecastMap,
}: {
  semesterLabel: string;
  sections: OpenedCourseSectionDto[];
  deptId: number;
  termId: number;
  forecastMap: Map<number, CourseDemandForecastDto>;
}) {
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editCapacity, setEditCapacity] = useState('');
  const [editInstructorId, setEditInstructorId] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<OpenedCourseSectionDto | null>(null);
  const updateMutation = useUpdateOpenedSection(deptId, termId);
  const closeMutation = useCloseSection(deptId, termId);
  const instructorsQuery = useDepartmentInstructors(deptId);
  const workloadQuery = useInstructorWorkload(
    editInstructorId ? Number(editInstructorId) : null,
    termId,
  );

  // Section count per course
  const sectionCountByCourse = useMemo(() => {
    const map = new Map<number, number>();
    for (const s of sections) {
      map.set(s.courseId, (map.get(s.courseId) ?? 0) + 1);
    }
    return map;
  }, [sections]);

  const handleEdit = (s: OpenedCourseSectionDto) => {
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

  const handleDelete = () => {
    if (!deleteTarget) return;
    closeMutation.mutate(deleteTarget.sectionId, {
      onSuccess: () => setDeleteTarget(null),
    });
  };

  return (
    <div>
      <div className="flex items-center justify-between bg-gray-50 px-4 py-2.5 rounded-t-xl border border-b-0">
        <h3 className="text-sm font-semibold text-gray-700">{semesterLabel}</h3>
        <span className="text-xs text-muted-foreground">{sections.length} şube</span>
      </div>
      <div className="bg-white border rounded-b-xl overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow className="bg-gray-50/50">
              <TableHead className="w-10 font-semibold">Şb.</TableHead>
              <TableHead className="font-semibold">Ders Kodu</TableHead>
              <TableHead className="font-semibold">Ders Adı</TableHead>
              <TableHead className="w-14 font-semibold text-center">T+U</TableHead>
              <TableHead className="w-14 font-semibold text-center">AKTS</TableHead>
              <TableHead className="w-14 font-semibold text-center">Sınıf</TableHead>
              <TableHead className="w-28 font-semibold text-center">KO / DO / K</TableHead>
              <TableHead className="w-16 font-semibold text-center">Talep</TableHead>
              <TableHead className="w-16 font-semibold text-center">Durum</TableHead>
              <TableHead className="font-semibold">Öğretim Elemanı</TableHead>
              <TableHead className="w-20 font-semibold text-right">İşlemler</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {sections.map(s => {
              const fc = forecastMap.get(s.courseId);
              const actualSections = sectionCountByCourse.get(s.courseId) ?? 0;
              const recommended = fc?.recommendedSectionCount ?? 0;
              const isFirstOfCourse = sections.indexOf(s) === sections.findIndex(x => x.courseId === s.courseId);

              let statusBadge: React.ReactNode = null;
              if (isFirstOfCourse && fc) {
                if (actualSections < recommended) {
                  statusBadge = <span className="text-[10px] px-2 py-0.5 rounded-full bg-red-100 text-red-700 font-medium">Eksik</span>;
                } else if (actualSections === recommended) {
                  statusBadge = <span className="text-[10px] px-2 py-0.5 rounded-full bg-green-100 text-green-700 font-medium">Uyumlu</span>;
                } else {
                  statusBadge = <span className="text-[10px] px-2 py-0.5 rounded-full bg-amber-100 text-amber-700 font-medium">Fazla</span>;
                }
              }

              return (
                <TableRow key={s.sectionId} className={`group ${!s.isActive ? 'opacity-50' : ''}`}>
                  <TableCell className="text-center font-mono text-xs">{s.sectionNumber}</TableCell>
                  <TableCell>
                    <span className="font-mono text-xs font-semibold text-[#003366]">{s.courseCode}</span>
                  </TableCell>
                  <TableCell className="text-sm">{s.courseName}</TableCell>
                  <TableCell className="text-center text-xs text-muted-foreground">{s.theoryHours}+{s.labHours}</TableCell>
                  <TableCell className="text-center text-xs">{s.ects}</TableCell>
                  <TableCell className="text-center text-xs">{s.suggestedSemester > 0 ? Math.ceil(s.suggestedSemester / 2) : '—'}</TableCell>
                  <TableCell className="text-center">
                    {editingId === s.sectionId ? (
                      <Input className="h-7 w-20 text-xs mx-auto" value={editCapacity} onChange={e => setEditCapacity(e.target.value)} />
                    ) : (
                      <TooltipProvider delayDuration={200}>
                        <Tooltip>
                          <TooltipTrigger asChild>
                            <span className="text-xs font-mono cursor-default">
                              <span className="font-semibold">{s.capacity}</span>
                              <span className="text-muted-foreground"> / {s.enrolledCount} / {s.registeredCount}</span>
                            </span>
                          </TooltipTrigger>
                          <TooltipContent side="top" className="text-xs">
                            <p>Kontenjan: {s.capacity}</p>
                            <p>Doluluk: {s.enrolledCount}</p>
                            <p>Kayıtlı: {s.registeredCount}</p>
                          </TooltipContent>
                        </Tooltip>
                      </TooltipProvider>
                    )}
                  </TableCell>
                  <TableCell className="text-center">
                    {isFirstOfCourse && fc ? (
                      <TooltipProvider delayDuration={200}>
                        <Tooltip>
                          <TooltipTrigger asChild>
                            <span className="text-xs font-semibold cursor-default">{fc.totalPredictedDemand}</span>
                          </TooltipTrigger>
                          <TooltipContent side="top" className="text-xs space-y-0.5">
                            <p>Sınıfta: {fc.demandBreakdown.regularOnSchedule}</p>
                            <p>Tekrar: {fc.demandBreakdown.totalRetake}</p>
                            <p>Geride: {fc.demandBreakdown.behindSchedule}</p>
                            <p>Üst sınıf: {fc.demandBreakdown.upperCourse}</p>
                            <p className="pt-1 border-t font-medium">Önerilen: {recommended} şube</p>
                          </TooltipContent>
                        </Tooltip>
                      </TooltipProvider>
                    ) : !isFirstOfCourse ? null : (
                      <span className="text-xs text-muted-foreground">—</span>
                    )}
                  </TableCell>
                  <TableCell className="text-center">{statusBadge}</TableCell>
                  <TableCell>
                    {editingId === s.sectionId ? (
                      <div className="space-y-1">
                        <select
                          className="h-7 w-full rounded border border-input bg-background px-1 text-xs"
                          value={editInstructorId}
                          onChange={e => setEditInstructorId(e.target.value)}
                        >
                          <option value="">Seçiniz</option>
                          {(instructorsQuery.data ?? []).map(inst => (
                            <option key={inst.instructorId} value={inst.instructorId}>{inst.fullName}</option>
                          ))}
                        </select>
                        {workloadQuery.data && <WorkloadBar workload={workloadQuery.data} />}
                      </div>
                    ) : (
                      <span className="text-sm">{s.instructorName ?? '—'}</span>
                    )}
                  </TableCell>
                  <TableCell className="text-right">
                    <div className="flex gap-0.5 justify-end opacity-0 group-hover:opacity-100 transition-opacity">
                      {editingId === s.sectionId ? (
                        <>
                          <Button size="icon" variant="ghost" className="h-7 w-7" onClick={handleSave} disabled={updateMutation.isPending}>
                            <CheckCircle2 className="h-3.5 w-3.5 text-green-600" />
                          </Button>
                          <Button size="icon" variant="ghost" className="h-7 w-7" onClick={() => setEditingId(null)}>
                            <X className="h-3.5 w-3.5" />
                          </Button>
                        </>
                      ) : (
                        <>
                          <Button size="icon" variant="ghost" className="h-7 w-7" onClick={() => handleEdit(s)} disabled={!s.isActive}>
                            <Edit2 className="h-3.5 w-3.5" />
                          </Button>
                          <Button size="icon" variant="ghost" className="h-7 w-7 text-destructive hover:text-destructive" onClick={() => setDeleteTarget(s)} disabled={!s.isActive}>
                            <Trash2 className="h-3.5 w-3.5" />
                          </Button>
                        </>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </div>

      {closeMutation.isError && (
        <Alert variant="destructive" className="mt-2">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            {(closeMutation.error as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Şube kapatılamadı.'}
          </AlertDescription>
        </Alert>
      )}

      {/* Delete confirmation */}
      <Dialog open={deleteTarget !== null} onOpenChange={() => setDeleteTarget(null)}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader><DialogTitle>Şubeyi Kapat</DialogTitle></DialogHeader>
          <p className="text-sm text-muted-foreground">
            <strong>{deleteTarget?.courseCode} — Şube {deleteTarget?.sectionNumber}</strong> kapatılacak. Bu işlem geri alınamaz.
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteTarget(null)}>Vazgeç</Button>
            <Button variant="destructive" onClick={handleDelete} disabled={closeMutation.isPending}>
              {closeMutation.isPending ? 'Kapatılıyor...' : 'Kapat'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

// ── Tek Ders Dialog ────────────────────────────────────────────────

function AddCourseDialog({
  departmentId, termId, forecastMap, onClose,
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

  const workloadQuery = useInstructorWorkload(instructorId ? Number(instructorId) : null, termId);

  const handleSelectCourse = (c: CurriculumCourseForOpeningDto) => {
    setSelectedCourse(c);
    if (c.recommendedSectionCount != null && c.recommendedSectionCount > 0) setSectionCount(c.recommendedSectionCount);
    if (c.recommendedCapacity != null && c.recommendedCapacity > 0) setCapacity(c.recommendedCapacity);
  };

  const filteredCourses = (curriculumQuery.data ?? []).filter(
    c => c.courseCode.toLowerCase().includes(search.toLowerCase()) || c.courseName.toLowerCase().includes(search.toLowerCase()),
  );

  const handleSubmit = () => {
    if (!selectedCourse) return;
    openMutation.mutate(
      { courseId: selectedCourse.courseId, sectionCount, capacityPerSection: capacity, instructorId: instructorId ? Number(instructorId) : undefined },
      { onSuccess: () => { setSelectedCourse(null); onClose(); } },
    );
  };

  const fc = selectedCourse ? forecastMap.get(selectedCourse.courseId) : null;

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
              <Input placeholder="Ders kodu veya adı ile ara..." value={search} onChange={e => setSearch(e.target.value)} className="flex-1" />
              <label className="flex items-center gap-1.5 text-sm text-gray-600 whitespace-nowrap cursor-pointer select-none">
                <input type="checkbox" checked={showAll} onChange={e => setShowAll(e.target.checked)} className="rounded border-gray-300" />
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
                  {filteredCourses.map(c => (
                    <TableRow key={c.courseId} className={`cursor-pointer ${selectedCourse?.courseId === c.courseId ? 'bg-primary/5' : ''}`} onClick={() => handleSelectCourse(c)}>
                      <TableCell><input type="radio" checked={selectedCourse?.courseId === c.courseId} onChange={() => handleSelectCourse(c)} /></TableCell>
                      <TableCell><span className="font-mono text-xs font-semibold">{c.courseCode}</span></TableCell>
                      <TableCell>
                        <div className="text-sm">{c.courseName}</div>
                        {c.crossDepartmentDemand && c.crossDepartmentDemand.length > 0 && (
                          <div className="flex gap-1 mt-0.5 flex-wrap">
                            {c.crossDepartmentDemand.map((cd, idx) => (
                              <span key={idx} className="text-[10px] px-1.5 py-0 rounded-full bg-sky-100 text-sky-700">{cd.departmentName}({cd.estimatedStudentCount})</span>
                            ))}
                          </div>
                        )}
                      </TableCell>
                      <TableCell className="text-center">{c.ects}</TableCell>
                      <TableCell className="text-center">{Math.ceil(c.suggestedSemester / 2)}</TableCell>
                      <TableCell className="text-center">
                        {c.predictedDemand != null ? (
                          <div className="text-xs space-y-0.5">
                            <div className="font-semibold">{c.predictedDemand}</div>
                            {c.retakeCount != null && c.retakeCount > 0 && (
                              <div className="text-red-600">↻ {c.retakeCount}</div>
                            )}
                          </div>
                        ) : '—'}
                      </TableCell>
                      <TableCell>
                        {c.alreadyOpened ? (
                          <Badge variant="secondary" className="text-[10px]">{c.existingSectionCount} şube açık</Badge>
                        ) : (
                          <Badge variant="outline" className="text-[10px]">{c.isCompulsory ? 'Zorunlu' : 'Seçmeli'}</Badge>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>

            {/* ── Forecast Detay Paneli ── */}
            {fc && (
              <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 mt-4 space-y-3">
                <div className="flex items-center gap-2">
                  <TrendingUp className="h-4 w-4 text-blue-700" />
                  <span className="font-semibold text-blue-800 text-sm">Talep Tahmini — {fc.courseCode}</span>
                </div>
                <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                  <div className="bg-white rounded-md p-2.5 text-center">
                    <p className="text-lg font-bold text-blue-700">{fc.totalPredictedDemand}</p>
                    <p className="text-[10px] text-blue-600/70">Toplam Talep</p>
                  </div>
                  <div className="bg-white rounded-md p-2.5 text-center">
                    <p className="text-lg font-bold text-emerald-700">{fc.demandBreakdown.totalEligible}</p>
                    <p className="text-[10px] text-emerald-600/70">İlk Kez Alan</p>
                  </div>
                  <div className="bg-white rounded-md p-2.5 text-center">
                    <p className="text-lg font-bold text-red-700">{fc.demandBreakdown.totalRetake}</p>
                    <p className="text-[10px] text-red-600/70">Tekrar (FF: {fc.demandBreakdown.retakeFF}, NA: {fc.demandBreakdown.retakeNA})</p>
                  </div>
                  <div className="bg-white rounded-md p-2.5 text-center">
                    <p className="text-lg font-bold text-violet-700">{fc.recommendedSectionCount}</p>
                    <p className="text-[10px] text-violet-600/70">Önerilen Şube ({fc.sectionDistribution.effectiveCapacityPerSection} kişi)</p>
                  </div>
                </div>

                {/* Detaylı breakdown */}
                <div className="grid grid-cols-2 gap-x-6 gap-y-1 text-xs text-gray-700 pt-2 border-t border-blue-200">
                  <div className="flex justify-between"><span>Sınıfta (zamanında):</span><span className="font-medium">{fc.demandBreakdown.regularOnSchedule}</span></div>
                  <div className="flex justify-between"><span>Geride kalan:</span><span className="font-medium">{fc.demandBreakdown.behindSchedule}</span></div>
                  <div className="flex justify-between"><span>Üst sınıftan:</span><span className="font-medium">{fc.demandBreakdown.upperCourse}</span></div>
                  <div className="flex justify-between"><span>AKTS kısıtlı:</span><span className="font-medium">{fc.demandBreakdown.ectsLimited}</span></div>
                </div>

                {/* Güven aralığı */}
                {fc.confidence && (
                  <div className="flex items-center gap-3 text-xs pt-2 border-t border-blue-200">
                    <span className="text-blue-700 font-medium">Güven Aralığı:</span>
                    <div className="flex items-center gap-1">
                      <span className="font-mono">{fc.confidence.lowerBound}</span>
                      <div className="w-24 h-1.5 bg-blue-200 rounded-full relative">
                        <div className="absolute h-1.5 bg-blue-500 rounded-full"
                          style={{
                            left: `${Math.max(0, (fc.confidence.lowerBound / (fc.confidence.upperBound * 1.2)) * 100)}%`,
                            width: `${Math.min(100, ((fc.confidence.upperBound - fc.confidence.lowerBound) / (fc.confidence.upperBound * 1.2)) * 100)}%`,
                          }}
                        />
                      </div>
                      <span className="font-mono">{fc.confidence.upperBound}</span>
                    </div>
                    <span className="text-muted-foreground">(±{fc.confidence.margin})</span>
                  </div>
                )}

                {/* Tarihsel kayıtlar */}
                {fc.historicalEnrollments.length > 0 && (
                  <div className="pt-2 border-t border-blue-200">
                    <p className="text-xs font-medium text-blue-700 mb-2">Geçmiş Dönemler</p>
                    <div className="flex items-end gap-1.5 h-16">
                      {fc.historicalEnrollments.map((h, i) => {
                        const maxVal = Math.max(...fc.historicalEnrollments.map(x => x.actualEnrollment), 1);
                        const pct = (h.actualEnrollment / maxVal) * 100;
                        return (
                          <TooltipProvider key={i} delayDuration={100}>
                            <Tooltip>
                              <TooltipTrigger asChild>
                                <div className="flex flex-col items-center flex-1 min-w-0">
                                  <span className="text-[9px] font-mono text-blue-700 mb-0.5">{h.actualEnrollment}</span>
                                  <div className="w-full bg-blue-300 rounded-t" style={{ height: `${Math.max(pct, 8)}%` }} />
                                </div>
                              </TooltipTrigger>
                              <TooltipContent side="top" className="text-xs">
                                <p className="font-medium">{h.termName}</p>
                                <p>Kayıt: {h.actualEnrollment} öğrenci</p>
                                <p>Şube: {h.sectionCount}</p>
                              </TooltipContent>
                            </Tooltip>
                          </TooltipProvider>
                        );
                      })}
                    </div>
                    <div className="flex gap-1.5">
                      {fc.historicalEnrollments.map((h, i) => (
                        <span key={i} className="flex-1 text-[8px] text-center text-muted-foreground truncate">
                          {h.termName.replace(/\d{4}-\d{4}\s*/, '')}
                        </span>
                      ))}
                    </div>
                  </div>
                )}

                {/* Uyarılar */}
                {fc.sectionDistribution.warnings.length > 0 && (
                  <div className="space-y-1 pt-2 border-t border-blue-200">
                    {fc.sectionDistribution.warnings.map((w, i) => (
                      <div key={i} className="flex items-center gap-1.5 text-xs text-amber-700">
                        <AlertTriangle className="h-3 w-3 shrink-0" />
                        <span>{w}</span>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            )}

            {/* ── Açma Formu ── */}
            {selectedCourse && (
              <div className="grid grid-cols-3 gap-3 mt-4">
                <div className="space-y-1.5">
                  <label className="text-sm font-medium">Şube Sayısı</label>
                  <Input type="number" min={1} max={20} value={sectionCount} onChange={e => setSectionCount(Number(e.target.value))} />
                </div>
                <div className="space-y-1.5">
                  <label className="text-sm font-medium">Kontenjan</label>
                  <Input type="number" min={1} max={500} value={capacity} onChange={e => setCapacity(Number(e.target.value))} />
                </div>
                <div className="space-y-1.5">
                  <label className="text-sm font-medium">Öğretim Elemanı</label>
                  <select
                    className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                    value={instructorId}
                    onChange={e => setInstructorId(e.target.value)}
                  >
                    <option value="">Seçiniz (opsiyonel)</option>
                    {(instructorsQuery.data ?? []).map(inst => (
                      <option key={inst.instructorId} value={inst.instructorId}>{inst.fullName}</option>
                    ))}
                  </select>
                  {workloadQuery.data && (
                    <div className="mt-1"><WorkloadBar workload={workloadQuery.data} /></div>
                  )}
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

// ── Toplu Ders Açma Dialog ─────────────────────────────────────────

interface CourseSettings {
  sectionCount: number;
  capacity: number;
}

function BulkOpenDialog({
  departmentId, termId, forecastMap, onClose,
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
  const [courseSettings, setCourseSettings] = useState<Map<number, CourseSettings>>(new Map());

  const courses = curriculumQuery.data ?? [];
  const unopenedCourses = courses.filter(c => !c.alreadyOpened);

  // Default select compulsory courses on first load
  const [initialized, setInitialized] = useState(false);
  if (!initialized && unopenedCourses.length > 0) {
    const compulsoryIds = new Set(unopenedCourses.filter(c => c.isCompulsory).map(c => c.courseId));
    setSelectedIds(compulsoryIds);
    // Initialize settings from forecast
    const settings = new Map<number, CourseSettings>();
    for (const c of unopenedCourses) {
      const fc = forecastMap.get(c.courseId);
      settings.set(c.courseId, {
        sectionCount: fc?.recommendedSectionCount ?? 1,
        capacity: fc?.sectionDistribution?.effectiveCapacityPerSection ?? c.recommendedCapacity ?? 40,
      });
    }
    setCourseSettings(settings);
    setInitialized(true);
  }

  const getSettings = (courseId: number): CourseSettings => {
    return courseSettings.get(courseId) ?? { sectionCount: 1, capacity: 40 };
  };

  const updateSetting = (courseId: number, field: keyof CourseSettings, value: number) => {
    setCourseSettings(prev => {
      const next = new Map(prev);
      const current = next.get(courseId) ?? { sectionCount: 1, capacity: 40 };
      next.set(courseId, { ...current, [field]: value });
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
    else setSelectedIds(new Set(unopenedCourses.map(c => c.courseId)));
  };

  const fillFromForecast = () => {
    setCourseSettings(prev => {
      const next = new Map(prev);
      for (const courseId of selectedIds) {
        const fc = forecastMap.get(courseId);
        const c = unopenedCourses.find(x => x.courseId === courseId);
        if (fc) {
          next.set(courseId, {
            sectionCount: fc.recommendedSectionCount ?? 1,
            capacity: fc.sectionDistribution?.effectiveCapacityPerSection ?? c?.recommendedCapacity ?? 40,
          });
        }
      }
      return next;
    });
  };

  // Summary stats
  const selectedCourses = unopenedCourses.filter(c => selectedIds.has(c.courseId));
  const totalSelectedSections = selectedCourses.reduce((sum, c) => sum + getSettings(c.courseId).sectionCount, 0);
  const totalSelectedCapacity = selectedCourses.reduce((sum, c) => {
    const s = getSettings(c.courseId);
    return sum + s.sectionCount * s.capacity;
  }, 0);

  const handleSubmit = () => {
    if (selectedIds.size === 0) return;
    bulkMutation.mutate(
      {
        courses: Array.from(selectedIds).map(courseId => {
          const s = getSettings(courseId);
          return { courseId, sectionCount: s.sectionCount, capacityPerSection: s.capacity };
        }),
      },
    );
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
              <label className="flex items-center gap-1.5 text-sm text-gray-600 whitespace-nowrap cursor-pointer select-none ml-auto">
                <input type="checkbox" checked={showAll} onChange={e => setShowAll(e.target.checked)} className="rounded border-gray-300" />
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
                    <TableHead className="font-semibold">Tür</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {unopenedCourses.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={9} className="text-center text-muted-foreground py-6">
                        Tüm müfredat dersleri zaten açılmış.
                      </TableCell>
                    </TableRow>
                  ) : (
                    unopenedCourses.map(c => {
                      const isSelected = selectedIds.has(c.courseId);
                      const settings = getSettings(c.courseId);
                      const fc = forecastMap.get(c.courseId);
                      return (
                        <TableRow key={c.courseId} className={`cursor-pointer ${isSelected ? 'bg-primary/5' : ''}`} onClick={() => toggleCourse(c.courseId)}>
                          <TableCell onClick={e => e.stopPropagation()}>
                            <input type="checkbox" checked={isSelected} onChange={() => toggleCourse(c.courseId)} />
                          </TableCell>
                          <TableCell><span className="font-mono text-xs font-semibold">{c.courseCode}</span></TableCell>
                          <TableCell className="text-sm">{c.courseName}</TableCell>
                          <TableCell className="text-center text-xs">{c.ects}</TableCell>
                          <TableCell className="text-center text-xs">{c.suggestedSemester > 0 ? Math.ceil(c.suggestedSemester / 2) : '—'}</TableCell>
                          <TableCell className="text-center text-xs font-semibold">
                            {fc?.totalPredictedDemand ?? '—'}
                            {fc && fc.recommendedSectionCount > 0 && (
                              <div className="text-[10px] text-blue-600 font-normal">→ {fc.recommendedSectionCount} şube</div>
                            )}
                          </TableCell>
                          <TableCell className="text-center" onClick={e => e.stopPropagation()}>
                            <Input
                              type="number" min={1} max={20}
                              className="h-7 w-14 text-xs text-center mx-auto"
                              value={settings.sectionCount}
                              onChange={e => updateSetting(c.courseId, 'sectionCount', Math.max(1, Number(e.target.value)))}
                            />
                          </TableCell>
                          <TableCell className="text-center" onClick={e => e.stopPropagation()}>
                            <Input
                              type="number" min={1} max={500}
                              className="h-7 w-14 text-xs text-center mx-auto"
                              value={settings.capacity}
                              onChange={e => updateSetting(c.courseId, 'capacity', Math.max(1, Number(e.target.value)))}
                            />
                          </TableCell>
                          <TableCell>
                            <Badge variant={c.isCompulsory ? 'default' : 'outline'} className="text-[10px]">
                              {c.isCompulsory ? 'Zorunlu' : 'Seçmeli'}
                            </Badge>
                          </TableCell>
                        </TableRow>
                      );
                    })
                  )}
                </TableBody>
              </Table>
            </div>

            {/* Summary footer */}
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
