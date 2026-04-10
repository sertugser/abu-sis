import React, { useMemo, useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
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
import {
  AlertCircle,
  CheckCircle2,
  Edit2,
  Trash2,
  X,
} from 'lucide-react';
import {
  useCloseSection,
  useDepartmentInstructors,
  useInstructorWorkload,
  useUpdateOpenedSection,
} from '../../hooks/useCourseOpening';
import type { OpenedCourseSectionDto } from '../../types/courseOpening';
import type { CourseDemandForecastDto } from '../../types/forecast';
import { WorkloadBar } from './WorkloadBar';

function getCourseStatusBadge(actualSections: number, recommended: number) {
  if (recommended <= 0) return null;
  if (actualSections < recommended) {
    return <span className="text-[10px] px-2 py-0.5 rounded-full bg-red-100 text-red-700 font-medium">Eksik</span>;
  }
  if (actualSections === recommended) {
    return <span className="text-[10px] px-2 py-0.5 rounded-full bg-green-100 text-green-700 font-medium">Uyumlu</span>;
  }
  return <span className="text-[10px] px-2 py-0.5 rounded-full bg-amber-100 text-amber-700 font-medium">Fazla</span>;
}

export function SemesterSectionGroup({
  semesterLabel,
  sections,
  deptId,
  termId,
  forecastMap,
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

  const sectionCountByCourse = useMemo(() => {
    const map = new Map<number, number>();
    for (const section of sections) {
      map.set(section.courseId, (map.get(section.courseId) ?? 0) + 1);
    }
    return map;
  }, [sections]);

  const handleEdit = (section: OpenedCourseSectionDto) => {
    setEditingId(section.sectionId);
    setEditCapacity(section.capacity.toString());
    setEditInstructorId(section.instructorId?.toString() ?? '');
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
              <TableHead className="font-semibold">Öğretim Elemanı</TableHead>
              <TableHead className="w-20 font-semibold text-right">İşlemler</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {sections.map(section => {
              const forecast = forecastMap.get(section.courseId);
              const actualSections = sectionCountByCourse.get(section.courseId) ?? 0;
              const recommended = forecast?.recommendedSectionCount ?? 0;
              const isFirstOfCourse = sections.indexOf(section) === sections.findIndex(item => item.courseId === section.courseId);
              const statusBadge = getCourseStatusBadge(actualSections, recommended);

              return (
                <React.Fragment key={section.sectionId}>
                  {isFirstOfCourse && (
                    <TableRow className="bg-slate-50/70">
                      <TableCell colSpan={10} className="py-2">
                        <div className="flex flex-wrap items-center justify-between gap-2">
                          <div className="flex items-center gap-2">
                            <span className="font-mono text-xs font-semibold text-[#003366]">{section.courseCode}</span>
                            <span className="text-sm font-medium text-slate-900">{section.courseName}</span>
                            {statusBadge}
                          </div>
                          <div className="text-xs text-muted-foreground">
                            {actualSections} açık şube
                            {forecast ? ` • talep ${forecast.totalPredictedDemand} • öneri ${recommended} şube` : ''}
                          </div>
                        </div>
                      </TableCell>
                    </TableRow>
                  )}
                  <TableRow className={`group ${!section.isActive ? 'opacity-50' : ''}`}>
                    <TableCell className="text-center font-mono text-xs">{section.sectionNumber}</TableCell>
                    <TableCell>
                      <span className="font-mono text-xs font-semibold text-[#003366]">{section.courseCode}</span>
                    </TableCell>
                    <TableCell className="text-sm">{section.courseName}</TableCell>
                    <TableCell className="text-center text-xs text-muted-foreground">{section.theoryHours}+{section.labHours}</TableCell>
                    <TableCell className="text-center text-xs">{section.ects}</TableCell>
                    <TableCell className="text-center text-xs">{section.suggestedSemester > 0 ? Math.ceil(section.suggestedSemester / 2) : '—'}</TableCell>
                    <TableCell className="text-center">
                      {editingId === section.sectionId ? (
                        <Input className="h-7 w-20 text-xs mx-auto" value={editCapacity} onChange={event => setEditCapacity(event.target.value)} />
                      ) : (
                        <TooltipProvider delayDuration={200}>
                          <Tooltip>
                            <TooltipTrigger asChild>
                              <span className="text-xs font-mono cursor-default">
                                <span className="font-semibold">{section.capacity}</span>
                                <span className="text-muted-foreground"> / {section.enrolledCount} / {section.registeredCount}</span>
                              </span>
                            </TooltipTrigger>
                            <TooltipContent side="top" className="text-xs">
                              <p>Kontenjan: {section.capacity}</p>
                              <p>Doluluk: {section.enrolledCount}</p>
                              <p>Kayıtlı: {section.registeredCount}</p>
                            </TooltipContent>
                          </Tooltip>
                        </TooltipProvider>
                      )}
                    </TableCell>
                    <TableCell className="text-center">
                      {isFirstOfCourse && forecast ? (
                        <TooltipProvider delayDuration={200}>
                          <Tooltip>
                            <TooltipTrigger asChild>
                              <span className="text-xs font-semibold cursor-default">{forecast.totalPredictedDemand}</span>
                            </TooltipTrigger>
                            <TooltipContent side="top" className="text-xs space-y-0.5">
                              <p>Sınıfta: {forecast.demandBreakdown.regularOnSchedule}</p>
                              <p>Tekrar: {forecast.demandBreakdown.totalRetake}</p>
                              <p>Geride: {forecast.demandBreakdown.behindSchedule}</p>
                              <p>Üst sınıf: {forecast.demandBreakdown.upperCourse}</p>
                              <p className="pt-1 border-t font-medium">Önerilen: {recommended} şube</p>
                            </TooltipContent>
                          </Tooltip>
                        </TooltipProvider>
                      ) : !isFirstOfCourse ? null : (
                        <span className="text-xs text-muted-foreground">—</span>
                      )}
                    </TableCell>
                    <TableCell>
                      {editingId === section.sectionId ? (
                        <div className="space-y-1">
                          <select
                            className="h-7 w-full rounded border border-input bg-background px-1 text-xs"
                            value={editInstructorId}
                            onChange={event => setEditInstructorId(event.target.value)}
                          >
                            <option value="">Seçiniz</option>
                            {(instructorsQuery.data ?? []).map(instructor => (
                              <option key={instructor.instructorId} value={instructor.instructorId}>{instructor.fullName}</option>
                            ))}
                          </select>
                          {workloadQuery.data && <WorkloadBar workload={workloadQuery.data} />}
                        </div>
                      ) : (
                        <span className="text-sm">{section.instructorName ?? '—'}</span>
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex gap-0.5 justify-end opacity-0 group-hover:opacity-100 transition-opacity">
                        {editingId === section.sectionId ? (
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
                            <Button size="icon" variant="ghost" className="h-7 w-7" onClick={() => handleEdit(section)} disabled={!section.isActive}>
                              <Edit2 className="h-3.5 w-3.5" />
                            </Button>
                            <Button size="icon" variant="ghost" className="h-7 w-7 text-destructive hover:text-destructive" onClick={() => setDeleteTarget(section)} disabled={!section.isActive}>
                              <Trash2 className="h-3.5 w-3.5" />
                            </Button>
                          </>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                </React.Fragment>
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

      <Dialog open={deleteTarget !== null} onOpenChange={() => setDeleteTarget(null)}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader><DialogTitle>Şubeyi Kapat</DialogTitle></DialogHeader>
          <div className="space-y-2 text-sm text-muted-foreground">
            <p>
              <strong>{deleteTarget?.courseCode} — Şube {deleteTarget?.sectionNumber}</strong> kapatılacak. Bu işlem geri alınamaz.
            </p>
            {deleteTarget && (
              <p>
                {deleteTarget.draftOrPendingCount > 0
                  ? `${deleteTarget.draftOrPendingCount} adet Draft/Pending kayıt Dropped durumuna düşecek.`
                  : 'Bu şube için düşürülecek Draft/Pending kayıt bulunmuyor.'}
              </p>
            )}
          </div>
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
