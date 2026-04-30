import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Badge } from '@/components/ui/badge';
import { Loader2, Trash2 } from 'lucide-react';
import { useElectiveGroupDetail } from '../../hooks/useElectiveGroups';
import { useUpdatePool, useDeletePool } from '../../hooks/useCurriculumManagement';
import type { CurriculumElectivePoolDto } from '../../types/curriculumManagement';

interface Props {
  curriculumId: number;
  pool: CurriculumElectivePoolDto;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

interface FormState {
  slotName: string;
  semesterStart: string;
  semesterEnd: string;
  requiredCourseCount: string;
  requiredEcts: string;
  ectsPerCourse: string;
}

function poolToForm(pool: CurriculumElectivePoolDto): FormState {
  return {
    slotName: pool.slotName,
    semesterStart: String(pool.suggestedSemesterStart),
    semesterEnd: String(pool.suggestedSemesterEnd),
    requiredCourseCount: String(pool.requiredCourseCount),
    requiredEcts: String(pool.requiredEcts),
    ectsPerCourse: pool.ectsPerCourse !== null ? String(pool.ectsPerCourse) : '',
  };
}

export default function PoolDetailDialog({ curriculumId, pool, open, onOpenChange }: Props) {
  const [form, setForm] = useState<FormState>(() => poolToForm(pool));
  const [validationError, setValidationError] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);

  const { data: groupDetail, isLoading: groupLoading } = useElectiveGroupDetail(pool.electiveGroupId);
  const updateMutation = useUpdatePool();
  const deleteMutation = useDeletePool();

  // Pool prop değişince formu yeniden yükle — adjust-state-during-render
  // (React 19+: useEffect içinde setState yasaklandı, prev-tracking ile render'da update)
  const trackKey = open ? `${pool.id}` : 'closed';
  const [prevTrackKey, setPrevTrackKey] = useState(trackKey);
  if (trackKey !== prevTrackKey) {
    setPrevTrackKey(trackKey);
    if (open) {
      setForm(poolToForm(pool));
      setValidationError(null);
    }
  }

  const ectsCleared = form.ectsPerCourse.trim() === '' && pool.ectsPerCourse !== null;

  function validate(): string | null {
    if (!form.slotName.trim()) return 'Slot adı boş olamaz.';
    const start = Number(form.semesterStart);
    const end = Number(form.semesterEnd);
    if (Number.isNaN(start) || Number.isNaN(end)) return 'Yarıyıl değerleri sayı olmalı.';
    if (start > end) return 'Başlangıç yarıyılı bitişten büyük olamaz.';
    const reqCount = Number(form.requiredCourseCount);
    const reqEcts = Number(form.requiredEcts);
    if (reqCount <= 0 && reqEcts <= 0) {
      return 'Gerekli ders sayısı veya AKTS en az biri 0\'dan büyük olmalıdır.';
    }
    if (form.ectsPerCourse.trim() !== '') {
      const epc = Number(form.ectsPerCourse);
      if (Number.isNaN(epc) || epc < 0) return 'Ders başına AKTS negatif olamaz.';
    }
    return null;
  }

  function handleSave() {
    const err = validate();
    if (err) {
      setValidationError(err);
      return;
    }
    setValidationError(null);

    const epcRaw = form.ectsPerCourse.trim();
    const epcParsed = epcRaw === '' ? undefined : Number(epcRaw);

    updateMutation.mutate(
      {
        curriculumId,
        poolId: pool.id,
        slotName: form.slotName.trim(),
        suggestedSemesterStart: Number(form.semesterStart),
        suggestedSemesterEnd: Number(form.semesterEnd),
        requiredCourseCount: Number(form.requiredCourseCount),
        requiredEcts: Number(form.requiredEcts),
        ...(epcParsed !== undefined ? { ectsPerCourse: epcParsed } : {}),
        ...(ectsCleared ? { clearEctsPerCourse: true } : {}),
      },
      {
        onSuccess: () => onOpenChange(false),
      },
    );
  }

  function handleDelete() {
    deleteMutation.mutate(
      { curriculumId, poolId: pool.id },
      {
        onSuccess: () => {
          setConfirmDelete(false);
          onOpenChange(false);
        },
      },
    );
  }

  return (
    <>
      <Dialog open={open} onOpenChange={onOpenChange}>
        <ResizableDialogContent
          defaultWidth={560}
          minWidth={420}
          minHeight={500}
          className="flex flex-col p-0 gap-0"
        >
          <DialogHeader className="px-6 pt-6 pb-3 shrink-0">
            <DialogTitle>Grup Slotu — {pool.groupCode} / {pool.slotName}</DialogTitle>
            <DialogDescription className="text-xs">
              {pool.groupName} · Slot bilgilerini düzenle veya slotu müfredattan sil.
            </DialogDescription>
          </DialogHeader>

          <div className="flex-1 overflow-y-auto px-6 space-y-5 pb-2">
            {/* ── Form ── */}
            <div className="space-y-3 rounded-md border border-border p-3">
              <p className="text-xs font-medium text-muted-foreground">Slot Bilgileri</p>

              <div className="space-y-1">
                <Label className="text-xs">Slot Adı *</Label>
                <Input
                  value={form.slotName}
                  onChange={e => setForm(f => ({ ...f, slotName: e.target.value }))}
                  className="h-8 text-sm"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label className="text-xs">Başlangıç Yarıyılı</Label>
                  <Input
                    type="number"
                    min={1}
                    max={8}
                    value={form.semesterStart}
                    onChange={e => setForm(f => ({ ...f, semesterStart: e.target.value }))}
                    className="h-8 text-sm"
                  />
                </div>
                <div className="space-y-1">
                  <Label className="text-xs">Bitiş Yarıyılı</Label>
                  <Input
                    type="number"
                    min={1}
                    max={8}
                    value={form.semesterEnd}
                    onChange={e => setForm(f => ({ ...f, semesterEnd: e.target.value }))}
                    className="h-8 text-sm"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label className="text-xs">Alınması Gereken Ders Sayısı</Label>
                  <Input
                    type="number"
                    min={0}
                    value={form.requiredCourseCount}
                    onChange={e => setForm(f => ({ ...f, requiredCourseCount: e.target.value }))}
                    className="h-8 text-sm"
                  />
                </div>
                <div className="space-y-1">
                  <Label className="text-xs">Alınması Gereken AKTS</Label>
                  <Input
                    type="number"
                    min={0}
                    value={form.requiredEcts}
                    onChange={e => setForm(f => ({ ...f, requiredEcts: e.target.value }))}
                    className="h-8 text-sm"
                  />
                </div>
              </div>

              <div className="space-y-1">
                <Label className="text-xs">Ders Başına AKTS (opsiyonel)</Label>
                <Input
                  type="number"
                  min={0}
                  value={form.ectsPerCourse}
                  onChange={e => setForm(f => ({ ...f, ectsPerCourse: e.target.value }))}
                  placeholder="boş = ders kataloğu AKTS'i"
                  className="h-8 text-sm"
                />
                {ectsCleared && (
                  <p className="text-[10px] text-amber-700">
                    Override kaldırılacak — ders kataloğu AKTS'i kullanılacak.
                  </p>
                )}
              </div>
            </div>

            {/* ── Adaylar listesi (read-only) ── */}
            <div className="space-y-2 rounded-md border border-border p-3">
              <div className="flex items-center justify-between">
                <p className="text-xs font-medium text-muted-foreground">
                  Aday Dersler ({groupDetail?.courses.length ?? 0})
                </p>
                {groupLoading && <Loader2 className="h-3 w-3 animate-spin text-muted-foreground" />}
              </div>

              {groupDetail && groupDetail.courses.length === 0 && (
                <p className="text-xs text-muted-foreground italic">
                  Bu grupta henüz aday ders yok. "Seçmeli Ders Grupları" sayfasından gruba ders ekleyin.
                </p>
              )}

              {groupDetail && groupDetail.courses.length > 0 && (
                <div className="overflow-y-auto max-h-48 -mx-1">
                  <table className="w-full text-xs">
                    <thead className="text-muted-foreground">
                      <tr className="text-left">
                        <th className="px-2 py-1 font-normal w-20">Kod</th>
                        <th className="px-2 py-1 font-normal">Ad</th>
                        <th className="px-2 py-1 font-normal text-right w-12">AKTS</th>
                      </tr>
                    </thead>
                    <tbody>
                      {groupDetail.courses.map(c => (
                        <tr key={c.courseId} className="border-t border-border/50">
                          <td className="px-2 py-1 font-mono">{c.courseCode}</td>
                          <td className="px-2 py-1">{c.courseName}</td>
                          <td className="px-2 py-1 text-right tabular-nums">{c.ects}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              {pool.ectsPerCourse !== null && (
                <Badge variant="outline" className="text-[10px] bg-blue-50 text-blue-700 border-blue-200">
                  Override: {pool.ectsPerCourse} AKTS/ders
                </Badge>
              )}
            </div>

            {validationError && (
              <p className="text-xs text-destructive">{validationError}</p>
            )}
            {updateMutation.isError && !validationError && (
              <p className="text-xs text-destructive">Slot güncellenemedi.</p>
            )}
            {deleteMutation.isError && (
              <p className="text-xs text-destructive">Slot silinemedi.</p>
            )}
          </div>

          <DialogFooter className="shrink-0 px-6 py-4 border-t flex !justify-between gap-2">
            <Button
              variant="destructive"
              onClick={() => setConfirmDelete(true)}
              disabled={updateMutation.isPending || deleteMutation.isPending}
            >
              <Trash2 className="h-4 w-4 mr-1.5" />
              Sil
            </Button>
            <div className="flex gap-2">
              <Button variant="outline" onClick={() => onOpenChange(false)}>
                İptal
              </Button>
              <Button onClick={handleSave} disabled={updateMutation.isPending}>
                {updateMutation.isPending && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
                Kaydet
              </Button>
            </div>
          </DialogFooter>
        </ResizableDialogContent>
      </Dialog>

      {/* ── Sil onayı ── */}
      <AlertDialog open={confirmDelete} onOpenChange={setConfirmDelete}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Grup slotunu sil?</AlertDialogTitle>
            <AlertDialogDescription>
              <strong>{pool.groupCode} / {pool.slotName}</strong> slotu müfredattan kaldırılacak.
              Bu işlem geri alınamaz; gruba ait aday dersler etkilenmez (sadece müfredattan
              bağlantı kalkar).
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleteMutation.isPending}>İptal</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleDelete}
              disabled={deleteMutation.isPending}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {deleteMutation.isPending && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
              Evet, sil
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
