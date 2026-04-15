import { useState, useMemo, useCallback } from 'react';
import { CheckSquare, Square, AlertCircle, CheckCircle2, XCircle, Search, AlertTriangle, Copy, ArrowRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useAdminTerms } from '../hooks/useAdminTerms';
import { usePreviousTermCourses, useTransferFromTerm, useDepartmentInstructors } from '../hooks/useCourseOpening';
import type { PreviousTermCourseDto, TransferFromTermResultDto, TransferCourseItem } from '../types/courseOpening';

interface CourseOverride {
  sectionCount: number;
  capacityPerSection: number;
  instructorId: number | null;
}

type FilterType = 'all' | 'compulsory' | 'elective';

export default function TermProgramTransferPage() {
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();
  const { data: allTerms } = useAdminTerms();
  const departments = contextQuery.data?.accessibleDepartments ?? [];

  const [selectedDeptId, setSelectedDeptId] = useState<number | null>(null);
  const [sourceTermId, setSourceTermId] = useState<number | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [transferResult, setTransferResult] = useState<TransferFromTermResultDto | null>(null);
  const [overrides, setOverrides] = useState<Record<number, CourseOverride>>({});
  const [searchQuery, setSearchQuery] = useState('');
  const [filterType, setFilterType] = useState<FilterType>('all');
  const [showConfirmDialog, setShowConfirmDialog] = useState(false);

  const deptId = selectedDeptId ?? departments[0]?.departmentId ?? null;
  const targetTermId = activeTerm?.id ?? null;
  const termStatus = activeTerm?.status;
  const isTermSuitable = termStatus === 'Planning' || termStatus === 'Draft';
  const sourceTerms = useMemo(() => (allTerms ?? []).filter((t) => t.id !== targetTermId), [allTerms, targetTermId]);
  const selectedSourceTerm = sourceTerms.find((t) => t.id === sourceTermId);

  const coursesQuery = usePreviousTermCourses(deptId, sourceTermId, targetTermId);
  const transferMutation = useTransferFromTerm(deptId ?? 0, targetTermId ?? 0);
  const instructorsQuery = useDepartmentInstructors(deptId, targetTermId);
  const instructors = instructorsQuery.data ?? [];
  const courses = coursesQuery.data ?? [];
  const transferable = courses.filter((c) => !c.alreadyOpened);

  const filteredCourses = useMemo(() => {
    let result = courses;
    if (filterType === 'compulsory') result = result.filter((c) => c.isCompulsory);
    if (filterType === 'elective') result = result.filter((c) => !c.isCompulsory);
    if (searchQuery.trim()) {
      const q = searchQuery.trim().toLowerCase();
      result = result.filter(
        (c) =>
          c.courseCode.toLowerCase().includes(q) ||
          c.courseName.toLowerCase().includes(q) ||
          (c.instructorName && c.instructorName.toLowerCase().includes(q)),
      );
    }
    return result;
  }, [courses, filterType, searchQuery]);

  const getOverride = useCallback(
    (c: PreviousTermCourseDto): CourseOverride =>
      overrides[c.courseId] ?? {
        sectionCount: c.sectionCount,
        capacityPerSection: c.sectionCount > 0 ? Math.round(c.totalCapacity / c.sectionCount) : 40,
        instructorId: c.instructorId,
      },
    [overrides],
  );

  const updateOverride = (courseId: number, field: keyof CourseOverride, value: number | null) => {
    setOverrides((prev) => {
      const course = courses.find((c) => c.courseId === courseId);
      if (!course) return prev;
      const current = prev[courseId] ?? {
        sectionCount: course.sectionCount,
        capacityPerSection: course.sectionCount > 0 ? Math.round(course.totalCapacity / course.sectionCount) : 40,
        instructorId: course.instructorId,
      };
      return { ...prev, [courseId]: { ...current, [field]: value } };
    });
  };

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

  const handleTransfer = async () => {
    if (!deptId || !sourceTermId || !targetTermId || selectedIds.size === 0) return;
    setShowConfirmDialog(false);
    setTransferResult(null);

    const courseItems: TransferCourseItem[] = selectedCourses.map((c) => {
      const ov = getOverride(c);
      return {
        courseId: c.courseId,
        sectionCount: ov.sectionCount,
        capacityPerSection: ov.capacityPerSection,
        instructorId: ov.instructorId,
      };
    });

    const result = await transferMutation.mutateAsync({
      departmentId: deptId,
      sourceTermId,
      targetTermId,
      courses: courseItems,
    });
    setTransferResult(result);
    setSelectedIds(new Set());
  };

  const changeDept = (v: string) => {
    setSelectedDeptId(Number(v));
    setSelectedIds(new Set());
    setTransferResult(null);
    setOverrides({});
    setSearchQuery('');
  };
  const changeSource = (v: string) => {
    setSourceTermId(Number(v));
    setSelectedIds(new Set());
    setTransferResult(null);
    setOverrides({});
    setSearchQuery('');
  };

  if (contextQuery.isLoading)
    return (
      <div className="p-6 space-y-3">
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    );

  return (
    <div className="space-y-4 p-4">
      {/* Term status warning */}
      {activeTerm && !isTermSuitable && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Dönem durumu uygun değil</AlertTitle>
          <AlertDescription>
            Aktif dönem şu anda <strong>{termStatus}</strong> durumunda. Aktarım yapabilmek için dönemin{' '}
            <strong>Planning</strong> veya <strong>Draft</strong> durumunda olması gerekiyor.
          </AlertDescription>
        </Alert>
      )}

      {/* Filter Bar — Card style */}
      <Card>
        <CardContent className="pt-3 pb-3">
          <div className="flex items-center gap-3 flex-wrap">
            {departments.length > 1 && (
              <>
                <Select value={String(deptId ?? '')} onValueChange={changeDept}>
                  <SelectTrigger className="h-8 w-[180px] text-xs">
                    <SelectValue placeholder="Bölüm seçin" />
                  </SelectTrigger>
                  <SelectContent>
                    {departments.map((d) => (
                      <SelectItem key={d.departmentId} value={String(d.departmentId)}>
                        {d.departmentName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <div className="h-5 w-px bg-border" />
              </>
            )}

            <Select value={String(sourceTermId ?? '')} onValueChange={changeSource}>
              <SelectTrigger className="h-8 w-[170px] text-xs">
                <SelectValue placeholder="Kaynak dönem seçin" />
              </SelectTrigger>
              <SelectContent>
                {sourceTerms.map((t) => (
                  <SelectItem key={t.id} value={String(t.id)}>
                    {t.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <ArrowRight className="h-4 w-4 text-muted-foreground shrink-0" />

            <div className="h-8 px-3 flex items-center border rounded-md bg-muted/50 text-xs text-muted-foreground">
              {activeTerm?.name ?? 'Aktif dönem yok'}
            </div>

            {sourceTermId && courses.length > 0 && (
              <>
                <div className="h-5 w-px bg-border" />
                <span className="text-xs text-muted-foreground whitespace-nowrap">
                  {transferable.length} aktarılabilir / {courses.length} ders
                </span>
              </>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Transfer result */}
      {transferResult && (
        <Alert variant={transferResult.failedCount > 0 ? 'destructive' : 'default'}>
          {transferResult.failedCount > 0 ? <AlertCircle className="h-4 w-4" /> : <CheckCircle2 className="h-4 w-4" />}
          <AlertTitle>
            {transferResult.failedCount === 0 ? 'Aktarım tamamlandı' : 'Aktarım kısmen tamamlandı'}
          </AlertTitle>
          <AlertDescription className="space-y-1">
            <p>
              <strong>{transferResult.successCount}</strong> ders başarıyla aktarıldı (
              {transferResult.totalSectionsCreated} şube oluşturuldu).
              {transferResult.failedCount > 0 && (
                <span className="text-destructive">
                  {' '}
                  <strong>{transferResult.failedCount}</strong> ders aktarılamadı.
                </span>
              )}
            </p>
            {transferResult.results
              .filter((r) => !r.success)
              .map((r) => (
                <div key={r.courseId} className="flex items-start gap-1.5 text-sm mt-1 text-destructive">
                  <XCircle className="h-3.5 w-3.5 mt-0.5 shrink-0" />
                  <span>
                    <strong>{r.courseCode} — {r.courseName}:</strong> {r.error}
                  </span>
                </div>
              ))}
          </AlertDescription>
        </Alert>
      )}

      {/* Content */}
      {!sourceTermId ? (
        <Card>
          <CardContent className="p-0">
            <div className="py-16 text-center">
              <Copy className="h-10 w-10 text-muted-foreground/30 mx-auto mb-3" />
              <p className="text-muted-foreground text-sm">Kaynak dönem seçin</p>
              <p className="text-xs text-muted-foreground/60 mt-1">
                Kopyalanacak dersleri görmek için bir önceki dönem seçin.
              </p>
            </div>
          </CardContent>
        </Card>
      ) : coursesQuery.isLoading ? (
        <Card>
          <CardContent className="p-0">
            <div className="py-16 text-center text-muted-foreground text-sm">Yükleniyor...</div>
          </CardContent>
        </Card>
      ) : courses.length === 0 ? (
        <Card>
          <CardContent className="p-0">
            <div className="py-16 text-center">
              <AlertCircle className="h-10 w-10 text-muted-foreground/30 mx-auto mb-3" />
              <p className="text-muted-foreground text-sm">Ders bulunamadı</p>
              <p className="text-xs text-muted-foreground/60 mt-1">
                {selectedSourceTerm?.name} döneminde bu bölüme ait açılmış ders yok.
              </p>
            </div>
          </CardContent>
        </Card>
      ) : (
        <>
          {/* Search & selection toolbar */}
          <Card>
            <CardContent className="pt-3 pb-3">
              <div className="flex items-center gap-2.5 flex-wrap">
                <div className="relative w-[220px]">
                  <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
                  <Input
                    placeholder="Kod, isim veya hoca..."
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    className="pl-8 h-8 text-sm"
                  />
                </div>

                <div className="h-5 w-px bg-border" />

                <div className="flex items-center gap-0.5 bg-muted rounded-md p-0.5">
                  {([
                    ['all', 'Tümü'],
                    ['compulsory', 'Zorunlu'],
                    ['elective', 'Seçmeli'],
                  ] as const).map(([key, label]) => (
                    <button
                      key={key}
                      onClick={() => setFilterType(key)}
                      className={`px-2.5 py-1 text-xs rounded transition-all ${
                        filterType === key
                          ? 'bg-background text-foreground shadow-sm font-medium'
                          : 'text-muted-foreground hover:text-foreground'
                      }`}
                    >
                      {label}
                    </button>
                  ))}
                </div>

                <div className="h-5 w-px bg-border" />

                <Button variant="outline" size="sm" className="h-8 text-xs" onClick={() => setSelectedIds(new Set(transferable.map((c) => c.courseId)))}>
                  Tümünü Seç
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  className="h-8 text-xs"
                  onClick={() =>
                    setSelectedIds(new Set(transferable.filter((c) => c.isCompulsory).map((c) => c.courseId)))
                  }
                >
                  Zorunluları Seç
                </Button>
                {selectedIds.size > 0 && (
                  <Button variant="ghost" size="sm" className="h-8 text-xs" onClick={() => setSelectedIds(new Set())}>
                    Temizle
                  </Button>
                )}

                <div className="ml-auto flex items-center gap-3">
                  <span className="text-xs text-muted-foreground whitespace-nowrap">{selectedIds.size} seçili</span>
                  <Button
                    size="sm"
                    className="h-8 text-xs"
                    onClick={() => setShowConfirmDialog(true)}
                    disabled={selectedIds.size === 0 || transferMutation.isPending || !targetTermId || !isTermSuitable}
                  >
                    {transferMutation.isPending ? 'Aktarılıyor...' : `Aktar (${selectedIds.size})`}
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Course table */}
          <Card>
            <CardContent className="p-0">
              <div className="overflow-x-auto">
                <table className="w-full text-sm min-w-[900px]">
                  <thead className="bg-muted/50">
                    <tr>
                      <th className="p-3 w-10" />
                      <th className="p-3 text-left font-medium text-xs">Ders Kodu</th>
                      <th className="p-3 text-left font-medium text-xs">Ders Adı</th>
                      <th className="p-3 text-center font-medium text-xs">AKTS</th>
                      <th className="p-3 text-center font-medium text-xs w-20">Şube</th>
                      <th className="p-3 text-center font-medium text-xs w-24">Kontenjan</th>
                      <th className="p-3 text-left font-medium text-xs w-48">Hoca</th>
                      <th className="p-3 text-center font-medium text-xs">Durum</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredCourses.map((c) => {
                      const sel = selectedIds.has(c.courseId);
                      const ov = getOverride(c);
                      return (
                        <tr
                          key={c.courseId}
                          className={`border-t transition-colors ${c.alreadyOpened ? 'opacity-50' : 'hover:bg-muted/30'} ${sel ? 'bg-blue-50 dark:bg-blue-950/20' : ''}`}
                        >
                          <td className="p-3 text-center cursor-pointer" onClick={() => toggle(c)}>
                            {sel ? (
                              <CheckSquare className="h-4 w-4 text-primary mx-auto" />
                            ) : (
                              <Square className="h-4 w-4 text-muted-foreground mx-auto" />
                            )}
                          </td>
                          <td className="p-3 font-mono font-medium cursor-pointer" onClick={() => toggle(c)}>
                            {c.courseCode}
                          </td>
                          <td className="p-3 cursor-pointer" onClick={() => toggle(c)}>
                            {c.courseName}
                          </td>
                          <td className="p-3 text-center cursor-pointer" onClick={() => toggle(c)}>
                            {c.ects}
                          </td>
                          <td className="p-3 text-center">
                            {c.alreadyOpened ? (
                              c.sectionCount
                            ) : (
                              <Input
                                type="number"
                                min={1}
                                max={20}
                                value={ov.sectionCount}
                                onChange={(e) =>
                                  updateOverride(c.courseId, 'sectionCount', Math.max(1, Math.min(20, Number(e.target.value) || 1)))
                                }
                                className="h-7 w-16 text-center text-xs mx-auto"
                              />
                            )}
                          </td>
                          <td className="p-3 text-center">
                            {c.alreadyOpened ? (
                              c.totalCapacity
                            ) : (
                              <Input
                                type="number"
                                min={1}
                                value={ov.capacityPerSection}
                                onChange={(e) =>
                                  updateOverride(c.courseId, 'capacityPerSection', Math.max(1, Number(e.target.value) || 1))
                                }
                                className="h-7 w-20 text-center text-xs mx-auto"
                              />
                            )}
                          </td>
                          <td className="p-3">
                            {c.alreadyOpened ? (
                              <span className="text-xs text-muted-foreground">{c.instructorName ?? '—'}</span>
                            ) : (
                              <Select
                                value={ov.instructorId != null ? String(ov.instructorId) : 'none'}
                                onValueChange={(v) =>
                                  updateOverride(c.courseId, 'instructorId', v === 'none' ? null : Number(v))
                                }
                              >
                                <SelectTrigger className="h-7 w-44 text-xs">
                                  <SelectValue placeholder="Hoca seçin" />
                                </SelectTrigger>
                                <SelectContent>
                                  <SelectItem value="none">— Atanmamış —</SelectItem>
                                  {instructors.map((inst) => (
                                    <SelectItem key={inst.instructorId} value={String(inst.instructorId)}>
                                      {inst.fullName}
                                    </SelectItem>
                                  ))}
                                </SelectContent>
                              </Select>
                            )}
                          </td>
                          <td className="p-3 text-center cursor-pointer" onClick={() => toggle(c)}>
                            {c.alreadyOpened ? (
                              <Badge variant="secondary" className="text-xs">Zaten Açık</Badge>
                            ) : (
                              <Badge variant="outline" className="text-green-600 border-green-400 text-xs">
                                Aktarılabilir
                              </Badge>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>

              {filteredCourses.length === 0 && (
                <div className="py-12 text-center">
                  <Search className="h-8 w-8 text-muted-foreground/30 mx-auto mb-3" />
                  <p className="text-muted-foreground text-sm">Aramanızla eşleşen ders bulunamadı.</p>
                  <p className="text-xs text-muted-foreground/60 mt-1">Filtrelerinizi değiştirmeyi deneyin.</p>
                </div>
              )}
            </CardContent>
          </Card>
        </>
      )}

      {/* Confirmation Dialog */}
      <AlertDialog open={showConfirmDialog} onOpenChange={setShowConfirmDialog}>
        <AlertDialogContent className="max-w-lg">
          <AlertDialogHeader>
            <AlertDialogTitle>Aktarımı Onayla</AlertDialogTitle>
            <AlertDialogDescription asChild>
              <div className="space-y-3">
                <p>
                  Aşağıdaki <strong>{selectedCourses.length} ders</strong> hedef döneme aktarılacak:
                </p>
                <div className="max-h-48 overflow-y-auto space-y-1 border rounded-md p-2 bg-muted/30">
                  {selectedCourses.map((c) => {
                    const ov = getOverride(c);
                    const inst = instructors.find((i) => i.instructorId === ov.instructorId);
                    return (
                      <div key={c.courseId} className="flex items-center justify-between text-sm py-1">
                        <span className="font-mono font-medium">{c.courseCode}</span>
                        <span className="text-muted-foreground text-xs">
                          {ov.sectionCount} şube × {ov.capacityPerSection} kişi
                          {inst ? ` — ${inst.fullName}` : ''}
                        </span>
                      </div>
                    );
                  })}
                </div>
                <p className="text-sm text-muted-foreground">
                  Toplam{' '}
                  <strong>
                    {selectedCourses.reduce((sum, c) => sum + getOverride(c).sectionCount, 0)} şube
                  </strong>{' '}
                  oluşturulacak. Bu işlem geri alınamaz.
                </p>
              </div>
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <AlertDialogAction onClick={handleTransfer}>Aktarımı Başlat</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
