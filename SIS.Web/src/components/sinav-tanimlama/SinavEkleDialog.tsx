import { useState } from 'react';
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Loader2 } from 'lucide-react';
import type { SectionExam } from '../../types/sectionExam';
import type { GradeComponentType } from '../../types/grade';
import { useExamConfig, useSaveSectionExam, useUpsertComponent } from '../../hooks/useSectionExams';
import { ClassroomMultiSelect } from './ClassroomMultiSelect';

// ── datetime-local helpers ──────────────────────────────────────
function toLocalInput(isoUtc: string): string {
  // ISO UTC → "YYYY-MM-DDTHH:mm" in local time (input[type=datetime-local])
  const d = new Date(isoUtc);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function toUtcIso(localString: string): string {
  // "YYYY-MM-DDTHH:mm" (local) → UTC ISO string
  return new Date(localString).toISOString();
}

// ── types ──────────────────────────────────────────────────────
type ExamType = 'Midterm' | 'Final';

interface FormState {
  examType: ExamType;
  name: string;
  shortName: string;
  weight: string;         // string to allow empty input
  dateLocal: string;      // "YYYY-MM-DDTHH:mm"
  durationMinutes: string;
  classroomIds: number[];
  description: string;
  isAnnouncedToStudents: boolean;
}

interface SinavEkleDialogProps {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  sectionId: number;
  editTarget?: SectionExam | null;
}

const emptyForm = (examType: ExamType = 'Midterm'): FormState => ({
  examType,
  name: '',
  shortName: '',
  weight: '',
  dateLocal: '',
  durationMinutes: '',
  classroomIds: [],
  description: '',
  isAnnouncedToStudents: false,
});

function examTypeToGradeType(t: ExamType): GradeComponentType {
  return t === 'Midterm' ? 'Midterm' : 'Final';
}

function gradeTypeToExamType(t: GradeComponentType): ExamType {
  return t === 'Midterm' ? 'Midterm' : 'Final';
}

function extractErrorMessage(err: unknown): string {
  if (err && typeof err === 'object') {
    // Try { response: { data: { error: string } } } (conflict/400)
    const response = (err as { response?: { data?: { error?: string; message?: string } } }).response;
    if (response?.data?.error) return response.data.error;
    if (response?.data?.message) return response.data.message;
  }
  return 'Kaydedilemedi.';
}

export function SinavEkleDialog({
  open, onOpenChange, sectionId, editTarget,
}: SinavEkleDialogProps) {
  const config = useExamConfig(open ? sectionId : null);
  const saveSectionExamMut = useSaveSectionExam(sectionId);
  const upsertComponentMut = useUpsertComponent(sectionId);

  const [form, setForm] = useState<FormState>(emptyForm());
  const [prevKey, setPrevKey] = useState<string | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);

  // prev-key reset pattern (same as ExamRegulationFormDialog)
  const key = `${open}-${editTarget?.id ?? 'new'}`;
  if (open && key !== prevKey) {
    setPrevKey(key);
    setSubmitError(null);
    if (editTarget) {
      setForm({
        examType: gradeTypeToExamType(editTarget.componentType),
        name: editTarget.componentName,
        shortName: '', // shortName not carried in SectionExam list; user may re-enter
        weight: String(editTarget.weight),
        dateLocal: toLocalInput(editTarget.examDate),
        durationMinutes: editTarget.durationMinutes != null ? String(editTarget.durationMinutes) : '',
        classroomIds: editTarget.classrooms.map((c) => c.classroomId),
        description: editTarget.description ?? '',
        isAnnouncedToStudents: editTarget.isAnnouncedToStudents,
      });
    } else {
      setForm(emptyForm());
    }
  }

  const set = <K extends keyof FormState>(k: K, v: FormState[K]) =>
    setForm((f) => ({ ...f, [k]: v }));

  // ── Derived limits from config ─────────────────────────────
  const cfg = config.data;
  const allComponents = cfg?.components ?? [];
  const existingOfType = allComponents.filter((c) => c.type === examTypeToGradeType(form.examType));
  const editingComponentId = editTarget?.gradeComponentId ?? null;
  // Exclude the component being edited from "existing" count
  const otherOfType = existingOfType.filter((c) => c.id !== editingComponentId);

  const limitCount = form.examType === 'Midterm' ? (cfg?.midtermCount ?? 0) : (cfg?.finalCount ?? 0);
  const isAtLimit = cfg?.hasRegulation && limitCount > 0 && otherOfType.length >= limitCount;

  const existingWeightSum = allComponents
    .filter((c) => c.id !== editingComponentId)
    .reduce((sum, c) => sum + c.weight, 0);
  const remainingWeight = 100 - existingWeightSum;

  const minBound = form.examType === 'Midterm' ? (cfg?.minMidtermWeight ?? null) : (cfg?.minFinalWeight ?? null);
  const maxBound = form.examType === 'Midterm' ? (cfg?.maxMidtermWeight ?? null) : (cfg?.maxFinalWeight ?? null);

  const weightNum = Number(form.weight);
  const weightValid = !isNaN(weightNum) && weightNum > 0 && weightNum <= 100;
  const weightOutOfBounds = cfg?.hasRegulation && weightValid && (
    (minBound != null && weightNum < minBound) ||
    (maxBound != null && weightNum > maxBound)
  );

  const canSubmit =
    form.name.trim() !== '' &&
    form.dateLocal !== '' &&
    weightValid &&
    !saveSectionExamMut.isPending &&
    !upsertComponentMut.isPending;

  // ── next order for new component ──
  const nextOrder = allComponents.length > 0
    ? Math.max(...allComponents.map((c) => c.order)) + 1
    : 1;

  const handleSubmit = async () => {
    if (!canSubmit) return;
    setSubmitError(null);
    try {
      // Step 1: upsert component
      const componentResult = await upsertComponentMut.mutateAsync({
        id: editingComponentId,
        name: form.name.trim(),
        shortName: form.shortName.trim(),
        weight: weightNum,
        order: editingComponentId != null
          ? (allComponents.find((c) => c.id === editingComponentId)?.order ?? nextOrder)
          : nextOrder,
        type: examTypeToGradeType(form.examType),
      });

      // Step 2: save section exam
      await saveSectionExamMut.mutateAsync({
        id: editTarget?.id ?? null,
        gradeComponentId: componentResult.id,
        examDate: toUtcIso(form.dateLocal),
        durationMinutes: form.durationMinutes !== '' ? Number(form.durationMinutes) : null,
        isAnnouncedToStudents: form.isAnnouncedToStudents,
        description: form.description.trim() || null,
        classroomIds: form.classroomIds,
      });

      onOpenChange(false);
    } catch (err: unknown) {
      setSubmitError(extractErrorMessage(err));
    }
  };

  const isPending = upsertComponentMut.isPending || saveSectionExamMut.isPending;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{editTarget ? 'Sınav Düzenle' : 'Sınav Ekle'}</DialogTitle>
          {cfg && (
            <p className="text-xs text-muted-foreground">
              {cfg.courseCode} — {cfg.courseName}
            </p>
          )}
        </DialogHeader>

        {config.isLoading ? (
          <div className="flex items-center gap-2 py-6 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" /> Yükleniyor…
          </div>
        ) : (
          <div className="space-y-4">
            {/* Sınav Tipi */}
            <div className="space-y-1.5">
              <Label className="text-sm font-medium">Sınav Tipi</Label>
              <Select
                value={form.examType}
                onValueChange={(v) => set('examType', v as ExamType)}
              >
                <SelectTrigger className="h-9 text-sm">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Midterm">
                    Ara Sınav
                    {cfg?.hasRegulation && cfg.midtermCount > 0 && (
                      <span className="ml-1.5 text-xs text-muted-foreground">
                        (maks. {cfg.midtermCount})
                      </span>
                    )}
                  </SelectItem>
                  <SelectItem value="Final">
                    Yarıyıl Sonu
                    {cfg?.hasRegulation && cfg.finalCount > 0 && (
                      <span className="ml-1.5 text-xs text-muted-foreground">
                        (maks. {cfg.finalCount})
                      </span>
                    )}
                  </SelectItem>
                </SelectContent>
              </Select>
              {isAtLimit && (
                <p className="text-xs text-amber-600">
                  Bu tür için izin verilen sınav sayısına ({limitCount}) ulaşıldı. Devam edebilirsiniz ancak yönerge sınırını aşacaksınız.
                </p>
              )}
            </div>

            {/* Sınav Adı */}
            <div className="space-y-1.5">
              <Label className="text-sm font-medium">Sınav Adı</Label>
              <Input
                value={form.name}
                onChange={(e) => set('name', e.target.value)}
                placeholder="örn. 1. Ara Sınav"
                className="h-9 text-sm"
              />
            </div>

            {/* Kısa Ad */}
            <div className="space-y-1.5">
              <Label className="text-sm font-medium">Kısa Ad</Label>
              <Input
                value={form.shortName}
                onChange={(e) => set('shortName', e.target.value)}
                placeholder="örn. V1"
                className="h-9 text-sm"
                maxLength={20}
              />
            </div>

            {/* Etki Oranı */}
            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <Label className="text-sm font-medium">Etki Oranı (%)</Label>
                <span className="text-xs text-muted-foreground">
                  Kalan: %{Math.max(0, remainingWeight)}
                  {minBound != null && maxBound != null && (
                    <> · Yönerge: %{minBound}–{maxBound}</>
                  )}
                </span>
              </div>
              <Input
                type="number"
                min={1}
                max={100}
                value={form.weight}
                onChange={(e) => set('weight', e.target.value)}
                className="h-9 text-sm"
                placeholder="örn. 40"
              />
              {weightOutOfBounds && (
                <p className="text-xs text-amber-600">
                  Bu değer yönerge sınırlarının (%{minBound}–%{maxBound}) dışında.
                </p>
              )}
            </div>

            {/* Tarih + Saat */}
            <div className="space-y-1.5">
              <Label className="text-sm font-medium">Tarih ve Saat</Label>
              <Input
                type="datetime-local"
                value={form.dateLocal}
                onChange={(e) => set('dateLocal', e.target.value)}
                className="h-9 text-sm"
              />
            </div>

            {/* Süre */}
            <div className="space-y-1.5">
              <Label className="text-sm font-medium">Süre (dakika)</Label>
              <Input
                type="number"
                min={1}
                value={form.durationMinutes}
                onChange={(e) => set('durationMinutes', e.target.value)}
                placeholder="örn. 90 (opsiyonel)"
                className="h-9 text-sm"
              />
            </div>

            {/* Derslikler */}
            <div className="space-y-1.5">
              <Label className="text-sm font-medium">Derslikler</Label>
              <ClassroomMultiSelect
                classrooms={cfg?.classrooms ?? []}
                value={form.classroomIds}
                onChange={(ids) => set('classroomIds', ids)}
              />
            </div>

            {/* Açıklama */}
            <div className="space-y-1.5">
              <Label className="text-sm font-medium">Açıklama</Label>
              <textarea
                value={form.description}
                onChange={(e) => set('description', e.target.value)}
                placeholder="Opsiyonel açıklama"
                rows={2}
                className="flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 resize-none"
              />
            </div>

            {/* İlan */}
            <div className="flex items-center gap-3 rounded-lg border p-3">
              <Switch
                checked={form.isAnnouncedToStudents}
                onCheckedChange={(v) => set('isAnnouncedToStudents', v)}
              />
              <div>
                <Label className="text-sm font-medium">Öğrencilere ilan et</Label>
                <p className="text-xs text-muted-foreground">
                  Açılırsa öğrenciler sınav bilgisini görebilir.
                </p>
              </div>
            </div>
          </div>
        )}

        {submitError && (
          <p className="text-sm text-destructive">{submitError}</p>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
            İptal
          </Button>
          <Button onClick={handleSubmit} disabled={!canSubmit || isPending}>
            {isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Kaydet
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
