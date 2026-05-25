import { useMemo, useState } from 'react';
import {
  CalendarClock, Copy, FolderOpen, Loader2, Lock, Plus, RotateCcw, ScrollText, Trash2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { useFacultyLookup, useAdminPrograms } from '../hooks/useAdminLookup';
import { useAdminDepartmentsByStructure, useAdminProgramsByStructure } from '../hooks/useAdminAcademicStructure';
import {
  useExamRegulations, useDeleteExamRegulation, useCloneExamRegulation, useAttendancePolicy,
} from '../hooks/useExamRegulations';
import { ExamRegulationFormDialog } from '../components/exam-regulation/ExamRegulationFormDialog';
import type { ExamRegulation } from '../types/examRegulation';
import { TERM_LABELS, LEVEL_LABELS } from '../lib/examRegulationLabels';

const ATTENDANCE_METHOD_LABELS: Record<string, string> = {
  Separate: 'Teori/Uygulama Ayrı',
  TotalHours: 'Toplam Saate Göre',
  Passive: 'Pasif',
  MarkOnly: 'Sadece İşaretle',
};

const LEVEL_BADGE_CLASS: Record<string, string> = {
  Associate: 'bg-sky-50 text-sky-700 border-sky-200',
  Bachelor: 'bg-indigo-50 text-indigo-700 border-indigo-200',
  Master: 'bg-violet-50 text-violet-700 border-violet-200',
  Doctorate: 'bg-fuchsia-50 text-fuchsia-700 border-fuchsia-200',
};

const labelClass = 'text-xs font-medium text-muted-foreground';
const triggerClass = 'h-9 text-sm w-full bg-white';

// ─── Çoğalt (clone) dialog — window.prompt yerine ────────────────────
function CloneDialog({
  target, onOpenChange, onConfirm, pending,
}: {
  target: ExamRegulation | null;
  onOpenChange: (v: boolean) => void;
  onConfirm: (year: number) => void;
  pending: boolean;
}) {
  const [year, setYear] = useState('');
  const open = target !== null;

  // target değişince yıl alanını hedefin yılı+1 ile doldur (prev-key deseni).
  const [prevId, setPrevId] = useState<number | null>(null);
  if (target && target.id !== prevId) {
    setPrevId(target.id);
    setYear(String(target.year + 1));
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Yönergeyi Çoğalt</DialogTitle>
        </DialogHeader>
        <p className="text-sm text-muted-foreground">
          <span className="font-medium text-foreground">{target?.name}</span> yönergesinin tüm
          ayarları yeni bir yıla kopyalanır.
        </p>
        <div className="space-y-1.5">
          <Label className="text-sm font-medium">Hedef Yıl</Label>
          <Input
            type="number"
            min={0}
            value={year}
            onChange={(e) => setYear(e.target.value)}
            autoFocus
          />
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>İptal</Button>
          <Button
            onClick={() => { const y = Number(year); if (y) onConfirm(y); }}
            disabled={pending || !Number(year)}
          >
            {pending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Çoğalt
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default function ExamRegulationManagementPage() {
  const [facultyId, setFacultyId] = useState<number | undefined>(undefined);
  const [departmentId, setDepartmentId] = useState<number | undefined>(undefined);
  const [programId, setProgramId] = useState<number | undefined>(undefined);

  const { data: faculties, isLoading: loadingFaculties } = useFacultyLookup();
  const { data: departments, isLoading: loadingDepts } = useAdminDepartmentsByStructure(facultyId);
  const { data: programs, isLoading: loadingPrograms } = useAdminProgramsByStructure(departmentId);
  const { data: allPrograms } = useAdminPrograms();

  // Program seçiliyse server-side filtre; değilse hepsini çekip fakülte/bölüme göre süz.
  const regulations = useExamRegulations(programId);
  const deleteMut = useDeleteExamRegulation();
  const cloneMut = useCloneExamRegulation();
  const attendancePolicy = useAttendancePolicy();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<ExamRegulation | null>(null);
  const [cloneTarget, setCloneTarget] = useState<ExamRegulation | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ExamRegulation | null>(null);

  const hasFilter = facultyId !== undefined || departmentId !== undefined || programId !== undefined;

  // Fakülte/bölüm seçiliyken (program değilken) listeyi programId üyeliğine göre süz.
  const allowedProgramIds = useMemo<Set<number> | null>(() => {
    if (programId) return null;
    if (departmentId) return new Set((programs ?? []).map((p) => p.id));
    if (facultyId) {
      const deptIds = new Set((departments ?? []).map((d) => d.id));
      return new Set((allPrograms ?? []).filter((p) => deptIds.has(p.departmentId)).map((p) => p.id));
    }
    return null;
  }, [programId, departmentId, facultyId, programs, departments, allPrograms]);

  const list = useMemo(() => {
    const all = regulations.data ?? [];
    return allowedProgramIds == null ? all : all.filter((r) => allowedProgramIds.has(r.programId));
  }, [regulations.data, allowedProgramIds]);

  const openNew = () => { setEditTarget(null); setDialogOpen(true); };
  const openEdit = (r: ExamRegulation) => { setEditTarget(r); setDialogOpen(true); };

  const resetFilters = () => {
    setFacultyId(undefined);
    setDepartmentId(undefined);
    setProgramId(undefined);
  };

  return (
    <div className="p-6 space-y-5">
      {/* ── Başlık ── */}
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10 text-primary">
            <ScrollText className="h-5 w-5" />
          </div>
          <div>
            <h1 className="text-xl font-semibold leading-tight">Sınav Müfredatı</h1>
            <p className="text-sm text-muted-foreground">
              Program bazında sınav yapısı, ağırlıklar ve değerlendirme kuralları.
            </p>
          </div>
        </div>
        <Button onClick={openNew}>
          <Plus className="mr-1.5 h-4 w-4" /> Yeni Yönerge
        </Button>
      </div>

      {/* ── Devamsızlık politikası bilgi şeridi ── */}
      <div className="flex items-start gap-2.5 rounded-lg border border-border/80 bg-muted/40 px-4 py-2.5 text-sm">
        <CalendarClock className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
        <p className="text-muted-foreground">
          {attendancePolicy.data ? (
            <>
              <span className="font-medium text-foreground">Devamsızlık:</span>{' '}
              {ATTENDANCE_METHOD_LABELS[attendancePolicy.data.attendanceMethod] ?? attendancePolicy.data.attendanceMethod}
              {' · '}{attendancePolicy.data.termWeekCount} hafta
              {' · '}Teori %{attendancePolicy.data.theoryAbsenceLimitPercent}
              {' · '}Uygulama %{attendancePolicy.data.labAbsenceLimitPercent}
              {' — '}tüm üniversite için ortaktır, <span className="font-medium text-foreground">Sistem Ayarları</span>'ndan yönetilir.
            </>
          ) : (
            <>
              Devamsızlık eşikleri (hafta sayısı, teori/uygulama oranı) tüm üniversite için ortak olup{' '}
              <span className="font-medium text-foreground">Sistem Ayarları</span> sayfasından yönetilir.
            </>
          )}
        </p>
      </div>

      {/* ── Cascade filtre ── */}
      <Card>
        <CardContent className="py-4">
          <div className="grid grid-cols-1 gap-x-4 gap-y-3 md:grid-cols-4">
            <div className="space-y-1">
              <Label className={labelClass}>Fakülte</Label>
              <Select
                value={facultyId?.toString() ?? ''}
                onValueChange={(v) => { setFacultyId(Number(v)); setDepartmentId(undefined); setProgramId(undefined); }}
              >
                <SelectTrigger className={triggerClass}>
                  {loadingFaculties ? <Loader2 className="h-4 w-4 animate-spin" /> : <SelectValue placeholder="Tümü" />}
                </SelectTrigger>
                <SelectContent>
                  {(faculties ?? []).map((f) => (
                    <SelectItem key={f.id} value={f.id.toString()}>{f.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-1">
              <Label className={labelClass}>Bölüm</Label>
              <Select
                value={departmentId?.toString() ?? ''}
                onValueChange={(v) => { setDepartmentId(Number(v)); setProgramId(undefined); }}
                disabled={!facultyId}
              >
                <SelectTrigger className={triggerClass}>
                  {loadingDepts ? <Loader2 className="h-4 w-4 animate-spin" /> : <SelectValue placeholder="Tümü" />}
                </SelectTrigger>
                <SelectContent>
                  {(departments ?? []).map((d) => (
                    <SelectItem key={d.id} value={d.id.toString()}>{d.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-1">
              <Label className={labelClass}>Program</Label>
              <Select
                value={programId?.toString() ?? ''}
                onValueChange={(v) => setProgramId(Number(v))}
                disabled={!departmentId}
              >
                <SelectTrigger className={triggerClass}>
                  {loadingPrograms ? <Loader2 className="h-4 w-4 animate-spin" /> : <SelectValue placeholder="Tümü" />}
                </SelectTrigger>
                <SelectContent>
                  {(programs ?? []).map((p) => (
                    <SelectItem key={p.id} value={p.id.toString()}>{p.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex items-end">
              {hasFilter && (
                <Button
                  variant="ghost"
                  className="h-9 w-full text-sm text-muted-foreground hover:bg-primary/5 hover:text-primary"
                  onClick={resetFilters}
                >
                  <RotateCcw className="mr-1.5 h-4 w-4" /> Sıfırla
                </Button>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* ── Tablo ── */}
      <Card>
        <CardContent className="overflow-x-auto p-0">
          <table className="w-full min-w-[920px]">
            <thead className="border-b border-border/80 bg-muted/40">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Adı</th>
                <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Program</th>
                <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Öğr. Tipi</th>
                <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Dönem / Yıl</th>
                <th className="px-4 py-3 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">Sınav</th>
                <th className="px-4 py-3 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">Ağırlık</th>
                <th className="px-4 py-3 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">Geçme</th>
                <th className="px-4 py-3 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">Değerlendirme</th>
                <th className="px-4 py-3 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">Durum</th>
                <th className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">İşlem</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border/80">
              {regulations.isLoading && (
                <tr>
                  <td colSpan={10} className="px-4 py-12 text-center text-sm text-muted-foreground">
                    <Loader2 className="mx-auto mb-2 h-5 w-5 animate-spin" /> Yükleniyor...
                  </td>
                </tr>
              )}

              {!regulations.isLoading && list.map((r) => (
                <tr
                  key={r.id}
                  className="cursor-pointer transition-colors hover:bg-primary/5"
                  onClick={() => openEdit(r)}
                >
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1.5 font-medium">
                      {r.name || <span className="text-muted-foreground">(adsız)</span>}
                      {!r.letterRangesEditable && (
                        <span title="Harf aralıkları kilitli" className="inline-flex">
                          <Lock className="h-3.5 w-3.5 text-muted-foreground" />
                        </span>
                      )}
                    </div>
                  </td>
                  <td className="px-4 py-3 text-sm text-muted-foreground">{r.programName}</td>
                  <td className="px-4 py-3">
                    <Badge variant="outline" className={LEVEL_BADGE_CLASS[r.degreeLevel] ?? ''}>
                      {LEVEL_LABELS[r.degreeLevel] ?? r.degreeLevel}
                    </Badge>
                  </td>
                  <td className="px-4 py-3 text-sm">
                    {TERM_LABELS[r.termType] ?? r.termType} <span className="text-muted-foreground">· {r.year}</span>
                  </td>
                  <td className="px-4 py-3 text-center text-sm whitespace-nowrap">
                    {r.midtermCount} Vize <span className="text-muted-foreground">·</span> {r.finalCount} Final
                  </td>
                  <td className="px-4 py-3 text-center text-xs whitespace-nowrap text-muted-foreground">
                    V %{r.minMidtermWeightPercent}–{r.maxMidtermWeightPercent}
                    {' · '}F %{r.minFinalWeightPercent}–{r.maxFinalWeightPercent}
                  </td>
                  <td className="px-4 py-3 text-center text-sm font-medium">{r.passingGrade}</td>
                  <td className="px-4 py-3 text-center">
                    <Badge
                      variant="outline"
                      className={r.evaluationType === 'Relative'
                        ? 'bg-amber-50 text-amber-700 border-amber-200'
                        : 'bg-slate-50 text-slate-600 border-slate-200'}
                    >
                      {r.evaluationType === 'Relative' ? 'Bağıl' : 'Mutlak'}
                    </Badge>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <Badge
                      variant="outline"
                      className={r.isActive
                        ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                        : 'bg-red-50 text-red-600 border-red-200'}
                    >
                      {r.isActive ? 'Aktif' : 'Pasif'}
                    </Badge>
                  </td>
                  <td className="px-4 py-3 text-right" onClick={(e) => e.stopPropagation()}>
                    <div className="flex items-center justify-end gap-1">
                      <Button
                        variant="ghost" size="icon"
                        className="h-8 w-8 hover:bg-primary/5 hover:text-primary"
                        title="Çoğalt"
                        onClick={() => setCloneTarget(r)}
                      >
                        <Copy className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost" size="icon"
                        className="h-8 w-8 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
                        title="Sil"
                        onClick={() => setDeleteTarget(r)}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </td>
                </tr>
              ))}

              {!regulations.isLoading && list.length === 0 && (
                <tr>
                  <td colSpan={10} className="px-4 py-16 text-center">
                    <FolderOpen className="mx-auto mb-3 h-10 w-10 text-muted-foreground/30" />
                    <p className="text-sm font-medium text-foreground">
                      {hasFilter ? 'Bu seçimle eşleşen yönerge yok' : 'Henüz sınav yönergesi tanımlanmamış'}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {hasFilter
                        ? 'Farklı bir fakülte/bölüm/program seçebilir veya filtreleri sıfırlayabilirsiniz.'
                        : 'Sağ üstteki "Yeni Yönerge" butonuyla ilk yönergeyi oluşturun.'}
                    </p>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </CardContent>
      </Card>

      {/* ── Form (oluştur/düzenle) ── */}
      <ExamRegulationFormDialog open={dialogOpen} onOpenChange={setDialogOpen} editTarget={editTarget} />

      {/* ── Çoğalt ── */}
      <CloneDialog
        target={cloneTarget}
        onOpenChange={(v) => { if (!v) setCloneTarget(null); }}
        pending={cloneMut.isPending}
        onConfirm={(year) => {
          if (!cloneTarget) return;
          cloneMut.mutate({ id: cloneTarget.id, payload: { year } }, { onSuccess: () => setCloneTarget(null) });
        }}
      />

      {/* ── Sil onayı ── */}
      <Dialog open={deleteTarget !== null} onOpenChange={(v) => { if (!v) setDeleteTarget(null); }}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Yönergeyi Sil</DialogTitle>
          </DialogHeader>
          <p className="text-sm text-muted-foreground">
            <span className="font-medium text-foreground">{deleteTarget?.name}</span> yönergesi
            silinecek. Bu işlem geri alınamaz.
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteTarget(null)}>İptal</Button>
            <Button
              variant="destructive"
              disabled={deleteMut.isPending}
              onClick={() => {
                if (!deleteTarget) return;
                deleteMut.mutate(deleteTarget.id, { onSuccess: () => setDeleteTarget(null) });
              }}
            >
              {deleteMut.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Sil
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
