import { useState, useMemo } from 'react';
import { CheckSquare, Square, AlertCircle, CheckCircle2, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useAdminTerms } from '../hooks/useAdminTerms';
import { usePreviousTermCourses, useTransferFromTerm } from '../hooks/useCourseOpening';
import type { PreviousTermCourseDto, TransferFromTermResultDto } from '../types/courseOpening';

export default function TermProgramTransferPage() {
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();
  const { data: allTerms } = useAdminTerms();
  const departments = contextQuery.data?.accessibleDepartments ?? [];

  const [selectedDeptId, setSelectedDeptId] = useState<number | null>(null);
  const [sourceTermId, setSourceTermId] = useState<number | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [transferResult, setTransferResult] = useState<TransferFromTermResultDto | null>(null);

  const deptId = selectedDeptId ?? departments[0]?.departmentId ?? null;
  const targetTermId = activeTerm?.id ?? null;
  const sourceTerms = useMemo(() => (allTerms ?? []).filter((t) => t.id !== targetTermId), [allTerms, targetTermId]);

  const coursesQuery = usePreviousTermCourses(deptId, sourceTermId, targetTermId);
  const transferMutation = useTransferFromTerm(deptId ?? 0, targetTermId ?? 0);
  const courses = coursesQuery.data ?? [];
  const transferable = courses.filter((c) => !c.alreadyOpened);

  const toggle = (c: PreviousTermCourseDto) => {
    if (c.alreadyOpened) return;
    setSelectedIds((prev) => { const n = new Set(prev); if (n.has(c.courseId)) { n.delete(c.courseId); } else { n.add(c.courseId); } return n; });
  };

  const handleTransfer = async () => {
    if (!deptId || !sourceTermId || !targetTermId || selectedIds.size === 0) return;
    setTransferResult(null);
    const result = await transferMutation.mutateAsync({ departmentId: deptId, sourceTermId, targetTermId, courseIds: [...selectedIds] });
    setTransferResult(result);
    setSelectedIds(new Set());
  };

  const changeDept = (v: string) => { setSelectedDeptId(Number(v)); setSelectedIds(new Set()); setTransferResult(null); };
  const changeSource = (v: string) => { setSourceTermId(Number(v)); setSelectedIds(new Set()); setTransferResult(null); };

  if (contextQuery.isLoading) return <div className="p-6 space-y-3"><Skeleton className="h-8 w-64" /><Skeleton className="h-64 w-full" /></div>;

  return (
    <div className="p-6 space-y-6 max-w-6xl mx-auto">
      <div>
        <h1 className="text-2xl font-bold">Dönem Programı Aktarım</h1>
        <p className="text-muted-foreground text-sm mt-1">Önceki dönem ders açma ayarlarını aktif döneme kopyalayın.</p>
      </div>

      {/* Filter Bar */}
      <div className="flex flex-wrap gap-3 items-end bg-muted/40 p-4 rounded-lg">
        {departments.length > 1 && (
          <div className="space-y-1">
            <label className="text-xs font-medium text-muted-foreground">Bölüm</label>
            <Select value={String(deptId ?? '')} onValueChange={changeDept}>
              <SelectTrigger className="w-52"><SelectValue placeholder="Bölüm seçin" /></SelectTrigger>
              <SelectContent>{departments.map((d) => <SelectItem key={d.departmentId} value={String(d.departmentId)}>{d.departmentName}</SelectItem>)}</SelectContent>
            </Select>
          </div>
        )}
        <div className="space-y-1">
          <label className="text-xs font-medium text-muted-foreground">Kaynak Dönem</label>
          <Select value={String(sourceTermId ?? '')} onValueChange={changeSource}>
            <SelectTrigger className="w-52"><SelectValue placeholder="Kaynak dönem seçin" /></SelectTrigger>
            <SelectContent>{sourceTerms.map((t) => <SelectItem key={t.id} value={String(t.id)}>{t.name}</SelectItem>)}</SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <label className="text-xs font-medium text-muted-foreground">Hedef Dönem</label>
          <div className="h-9 px-3 flex items-center border rounded-md bg-background text-sm text-muted-foreground w-52">
            {activeTerm?.name ?? 'Aktif dönem yok'}
          </div>
        </div>
      </div>

      {/* Transfer result */}
      {transferResult && (
        <Alert>
          <CheckCircle2 className="h-4 w-4" />
          <AlertDescription>
            <span className="font-medium">{transferResult.successCount} ders aktarıldı ({transferResult.totalSectionsCreated} şube){transferResult.failedCount > 0 && `, ${transferResult.failedCount} başarısız`}</span>
            {transferResult.results.filter((r) => !r.success).map((r) => (
              <div key={r.courseId} className="flex items-start gap-1 text-xs mt-1 text-destructive">
                <XCircle className="h-3 w-3 mt-0.5 shrink-0" /><span><strong>{r.courseCode}</strong>: {r.error}</span>
              </div>
            ))}
          </AlertDescription>
        </Alert>
      )}

      {/* Content */}
      {!sourceTermId ? (
        <div className="text-center py-16 text-muted-foreground">Ders listesini görmek için kaynak dönem seçin.</div>
      ) : coursesQuery.isLoading ? (
        <Skeleton className="h-64 w-full" />
      ) : courses.length === 0 ? (
        <Alert><AlertCircle className="h-4 w-4" /><AlertDescription>Seçilen dönemde bu bölüme ait açılmış ders bulunamadı.</AlertDescription></Alert>
      ) : (
        <div className="space-y-3">
          <div className="flex gap-2 items-center flex-wrap">
            <Button variant="outline" size="sm" onClick={() => setSelectedIds(new Set(transferable.map((c) => c.courseId)))}>Tümünü Seç</Button>
            <Button variant="outline" size="sm" onClick={() => setSelectedIds(new Set(transferable.filter((c) => c.isCompulsory).map((c) => c.courseId)))}>Zorunluları Seç</Button>
            {selectedIds.size > 0 && <Button variant="ghost" size="sm" onClick={() => setSelectedIds(new Set())}>Temizle</Button>}
            <span className="text-sm text-muted-foreground ml-auto">{selectedIds.size} seçili</span>
          </div>

          <div className="border rounded-lg overflow-hidden overflow-x-auto">
            <table className="w-full text-sm min-w-[700px]">
              <thead className="bg-muted/50">
                <tr>
                  {['', 'Ders Kodu', 'Ders Adı', 'AKTS', 'Şube', 'Kontenjan', 'Hoca', 'Durum'].map((h, i) => (
                    <th key={i} className={`p-3 font-medium ${i === 0 ? 'w-10' : i < 2 || i > 5 ? 'text-left' : 'text-center'}`}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {courses.map((c) => {
                  const sel = selectedIds.has(c.courseId);
                  return (
                    <tr key={c.courseId} onClick={() => toggle(c)}
                      className={`border-t transition-colors ${c.alreadyOpened ? 'opacity-50' : 'hover:bg-muted/30 cursor-pointer'} ${sel ? 'bg-blue-50 dark:bg-blue-950/20' : ''}`}>
                      <td className="p-3 text-center">{sel ? <CheckSquare className="h-4 w-4 text-primary mx-auto" /> : <Square className="h-4 w-4 text-muted-foreground mx-auto" />}</td>
                      <td className="p-3 font-mono font-medium">{c.courseCode}</td>
                      <td className="p-3">{c.courseName}</td>
                      <td className="p-3 text-center">{c.ects}</td>
                      <td className="p-3 text-center">{c.sectionCount}</td>
                      <td className="p-3 text-center">{c.totalCapacity}</td>
                      <td className="p-3 text-xs text-muted-foreground">{c.instructorName ?? '—'}</td>
                      <td className="p-3 text-center">
                        {c.alreadyOpened
                          ? <Badge variant="secondary">Zaten Açık</Badge>
                          : <Badge variant="outline" className="text-green-600 border-green-400">Aktarılabilir</Badge>}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          <div className="flex justify-end">
            <Button onClick={handleTransfer} disabled={selectedIds.size === 0 || transferMutation.isPending || !targetTermId}>
              {transferMutation.isPending ? 'Aktarılıyor...' : `Seçilenleri Aktar (${selectedIds.size})`}
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
