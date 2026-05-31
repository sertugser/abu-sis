import { useMemo, useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  DraggableDialog,
  DraggableDialogContent,
  DraggableDialogTitleBar,
} from '@/components/ui/draggable-dialog';
import { Loader2, Lock, X } from 'lucide-react';
import { cn } from '@/lib/utils';
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
// ExamType is used only for CREATE mode select options (Midterm | Final).
// In EDIT mode we preserve the full GradeComponentType from the server so
// 'Other' is never silently coerced to 'Final'.
type ExamType = 'Midterm' | 'Final';

interface FormState {
  // componentType holds the authoritative grade-component type.
  // In CREATE mode it is always Midterm or Final (user-chosen).
  // In EDIT mode it is the original type from the server (may be 'Other').
  componentType: GradeComponentType;
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
  componentType: examType,
  name: '',
  shortName: '',
  weight: '',
  dateLocal: '',
  durationMinutes: '',
  classroomIds: [],
  description: '',
  isAnnouncedToStudents: false,
});

// Only used for CREATE mode weight-limit lookups (Midterm vs Final bounds).
// 'Other' falls back to Final bounds as a best-effort — irrelevant in
// practice since Other components are only edited, never created here.
function componentTypeToExamType(t: GradeComponentType): ExamType {
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

// ── Exam-name templates derived from admin counts (no new entity) ────
interface ExamTemplate {
  name: string;
  shortName: string;
}

const GENERIC_TEMPLATES: ExamTemplate[] = [
  { name: '1. Ara Sınav', shortName: 'V1' },
  { name: '2. Ara Sınav', shortName: 'V2' },
  { name: 'Yarıyıl Sonu Sınavı', shortName: 'F' },
];

function buildTemplates(
  examType: ExamType,
  hasRegulation: boolean,
  midtermCount: number,
  finalCount: number,
): ExamTemplate[] {
  if (!hasRegulation || (midtermCount === 0 && finalCount === 0)) {
    return GENERIC_TEMPLATES;
  }
  if (examType === 'Midterm') {
    return Array.from({ length: midtermCount }, (_, i) => ({
      name: `${i + 1}. Ara Sınav`,
      shortName: `V${i + 1}`,
    }));
  }
  // Final
  if (finalCount > 1) {
    return Array.from({ length: finalCount }, (_, i) => ({
      name: `${i + 1}. Yarıyıl Sonu Sınavı`,
      shortName: `F${i + 1}`,
    }));
  }
  return [{ name: 'Yarıyıl Sonu Sınavı', shortName: 'F' }];
}

export function SinavEkleDialog({
  open, onOpenChange, sectionId, editTarget,
}: SinavEkleDialogProps) {
  const config = useExamConfig(open ? sectionId : null);
  const saveSectionExamMut = useSaveSectionExam(sectionId);
  const upsertComponentMut = useUpsertComponent(sectionId);

  const [form, setForm] = useState<FormState>(emptyForm());
  const [prevKey, setPrevKey] = useState<string | null>(null);
  // Whether the edit target's shortName has been seeded from config yet. Reset
  // with the dialog key so a re-open re-seeds; flipped to true once config
  // resolves (config may be cached → seeds in the same pass, or arrive later).
  const [shortNameSeeded, setShortNameSeeded] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  // prev-key reset pattern (same as ExamRegulationFormDialog). Seeds the form
  // from the edit target (or empties it) whenever the dialog opens for a new
  // target. shortName is seeded from config if it is already cached.
  const key = `${open}-${editTarget?.id ?? 'new'}`;
  if (open && key !== prevKey) {
    setPrevKey(key);
    setSubmitError(null);
    if (editTarget) {
      const cachedShortName =
        config.data?.components.find((c) => c.id === editTarget.gradeComponentId)?.shortName ?? '';
      setShortNameSeeded(config.data != null);
      setForm({
        componentType: editTarget.componentType,
        name: editTarget.componentName,
        shortName: cachedShortName,
        weight: String(editTarget.weight),
        dateLocal: toLocalInput(editTarget.examDate),
        durationMinutes: editTarget.durationMinutes != null ? String(editTarget.durationMinutes) : '',
        classroomIds: editTarget.classrooms.map((c) => c.classroomId),
        description: editTarget.description ?? '',
        isAnnouncedToStudents: editTarget.isAnnouncedToStudents,
      });
    } else {
      setShortNameSeeded(true);
      setForm(emptyForm());
    }
  }

  // Late-load path: config resolved after the dialog opened for an edit target.
  // Seed shortName if the user has not typed one yet. Render-phase (no effect)
  // to avoid a cascading setState-in-effect render.
  if (open && editTarget && config.data && !shortNameSeeded) {
    const matched = config.data.components.find((c) => c.id === editTarget.gradeComponentId);
    setShortNameSeeded(true);
    if (matched) {
      setForm((f) => (f.shortName === '' ? { ...f, shortName: matched.shortName } : f));
    }
  }

  const set = <K extends keyof FormState>(k: K, v: FormState[K]) =>
    setForm((f) => ({ ...f, [k]: v }));

  // ── Derived limits from config ─────────────────────────────
  const cfg = config.data;
  const allComponents = useMemo(() => cfg?.components ?? [], [cfg]);
  // For count/weight limits, map componentType to the Midterm|Final axis.
  const examTypeForLimits = componentTypeToExamType(form.componentType);
  const existingOfType = allComponents.filter((c) => c.type === form.componentType);
  const editingComponentId = editTarget?.gradeComponentId ?? null;
  // Exclude the component being edited from "existing" count
  const otherOfType = existingOfType.filter((c) => c.id !== editingComponentId);

  const limitCount = examTypeForLimits === 'Midterm' ? (cfg?.midtermCount ?? 0) : (cfg?.finalCount ?? 0);
  const isAtLimit = cfg?.hasRegulation && limitCount > 0 && otherOfType.length >= limitCount;

  const existingWeightSum = allComponents
    .filter((c) => c.id !== editingComponentId)
    .reduce((sum, c) => sum + c.weight, 0);
  const remainingWeight = 100 - existingWeightSum;

  const minBound = examTypeForLimits === 'Midterm' ? (cfg?.minMidtermWeight ?? null) : (cfg?.minFinalWeight ?? null);
  const maxBound = examTypeForLimits === 'Midterm' ? (cfg?.maxMidtermWeight ?? null) : (cfg?.maxFinalWeight ?? null);

  // ── Kilitli (admin-pinned weight) ────────────────────────────
  // When the regulation pins min == max for this type the weight cannot be
  // chosen — it is read-only and forced to that value.
  const isWeightLocked =
    !!cfg?.hasRegulation && minBound != null && maxBound != null && minBound === maxBound;

  // Effective weight string: when locked it is forced to the pinned bound,
  // otherwise it is whatever the user typed. Derived (no effect) so there is no
  // cascading render.
  const weightValue = isWeightLocked && minBound != null ? String(minBound) : form.weight;

  const weightNum = Number(weightValue);
  const weightValid = !isNaN(weightNum) && weightNum > 0 && weightNum <= 100;
  const weightOutOfBounds = cfg?.hasRegulation && !isWeightLocked && weightValid && (
    (minBound != null && weightNum < minBound) ||
    (maxBound != null && weightNum > maxBound)
  );

  // Fix 4: Build bounds warning message with correct text for partial bounds.
  function buildBoundsMessage(): string {
    if (minBound != null && maxBound != null) return `Bu değer yönerge sınırlarının (%${minBound}–%${maxBound}) dışında.`;
    if (minBound != null) return `Bu değer yönerge alt sınırının (%${minBound}) altında.`;
    if (maxBound != null) return `Bu değer yönerge üst sınırını (%${maxBound}) aşıyor.`;
    return '';
  }

  // Full hint: En fazla / Kullanılan / Kalan (Proliz parity).
  const usedOfType = otherOfType.reduce((sum, c) => sum + c.weight, 0);

  // ── Sınav Şablon Adı (derive from admin counts) ──────────────
  const templates = useMemo(
    () => buildTemplates(
      examTypeForLimits,
      !!cfg?.hasRegulation,
      cfg?.midtermCount ?? 0,
      cfg?.finalCount ?? 0,
    ),
    [examTypeForLimits, cfg?.hasRegulation, cfg?.midtermCount, cfg?.finalCount],
  );

  // Templates already consumed by another component of the matching name are
  // disabled (Proliz: the same template should not define a second exam).
  // Exclude the component being edited so its own template stays selectable.
  const usedTemplateNames = useMemo(() => {
    const editingName = allComponents.find((c) => c.id === editingComponentId)?.name;
    return new Set(
      allComponents
        .filter((c) => c.id !== editingComponentId && c.name !== editingName)
        .map((c) => c.name),
    );
  }, [allComponents, editingComponentId]);

  const applyTemplate = (templateName: string) => {
    const t = templates.find((tpl) => tpl.name === templateName);
    if (!t) return;
    setForm((f) => ({ ...f, name: t.name, shortName: t.shortName }));
  };

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
      // Step 1: upsert component.
      // Fix 2: pass componentType directly — preserves 'Other' in edit mode.
      const componentResult = await upsertComponentMut.mutateAsync({
        id: editingComponentId,
        name: form.name.trim(),
        shortName: form.shortName.trim(),
        weight: weightNum,
        order: editingComponentId != null
          ? (allComponents.find((c) => c.id === editingComponentId)?.order ?? nextOrder)
          : nextOrder,
        type: form.componentType,
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

  const selectedClassrooms = (cfg?.classrooms ?? []).filter((c) => form.classroomIds.includes(c.id));

  return (
    <DraggableDialog open={open} onOpenChange={onOpenChange}>
      <DraggableDialogContent className="max-w-2xl">
        {/* Colored, draggable title bar */}
        <DraggableDialogTitleBar className="bg-gradient-to-r from-indigo-600 to-violet-600 px-4 py-2.5 text-white">
          <span className="flex items-center gap-2">
            Sınav İşlemleri
            {cfg && (
              <span className="truncate font-normal text-indigo-100">
                — {cfg.courseCode} {cfg.courseName}
              </span>
            )}
          </span>
        </DraggableDialogTitleBar>

        {config.isLoading ? (
          <div className="flex items-center gap-2 px-4 py-10 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" /> Yükleniyor…
          </div>
        ) : (
          <div className="max-h-[75vh] overflow-y-auto px-4 py-3">
            <div className="grid grid-cols-1 gap-x-4 gap-y-3 sm:grid-cols-2">
              {/* Row 1: Sınav Tipi | Sınav Şablon Adı */}
              <Field label="Sınav Tipi">
                {editTarget ? (
                  <div className="flex h-9 items-center gap-1.5 rounded-md border border-input bg-muted/40 px-3 text-sm">
                    <TypeBadge type={form.componentType} />
                    <span className="text-xs text-muted-foreground">(değiştirilemez)</span>
                  </div>
                ) : (
                  <Select
                    value={form.componentType as ExamType}
                    onValueChange={(v) => set('componentType', v as ExamType)}
                  >
                    <SelectTrigger className="h-9 text-sm">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Midterm">
                        Ara Sınav
                        {cfg?.hasRegulation && cfg.midtermCount > 0 && (
                          <span className="ml-1.5 text-xs text-muted-foreground">(maks. {cfg.midtermCount})</span>
                        )}
                      </SelectItem>
                      <SelectItem value="Final">
                        Yarıyıl Sonu
                        {cfg?.hasRegulation && cfg.finalCount > 0 && (
                          <span className="ml-1.5 text-xs text-muted-foreground">(maks. {cfg.finalCount})</span>
                        )}
                      </SelectItem>
                    </SelectContent>
                  </Select>
                )}
              </Field>

              <Field label="Sınav Şablon Adı" hint={editTarget ? 'opsiyonel' : undefined}>
                <Select value="" onValueChange={applyTemplate}>
                  <SelectTrigger className="h-9 text-sm">
                    <SelectValue placeholder="Şablondan seç…" />
                  </SelectTrigger>
                  <SelectContent>
                    {templates.map((t) => {
                      const used = usedTemplateNames.has(t.name);
                      return (
                        <SelectItem key={t.name} value={t.name} disabled={used}>
                          {t.name}
                          <span className="ml-1.5 text-xs text-muted-foreground">{t.shortName}</span>
                          {used && <span className="ml-1.5 text-xs text-amber-600">(kullanıldı)</span>}
                        </SelectItem>
                      );
                    })}
                  </SelectContent>
                </Select>
              </Field>

              {/* Row 2: Sınav Adı | Kısa Ad */}
              <Field label="Sınav Adı">
                <Input
                  value={form.name}
                  onChange={(e) => set('name', e.target.value)}
                  placeholder="örn. 1. Ara Sınav"
                  className="h-9 text-sm"
                />
              </Field>
              <Field label="Kısa Ad">
                <Input
                  value={form.shortName}
                  onChange={(e) => set('shortName', e.target.value)}
                  placeholder="örn. V1"
                  className="h-9 text-sm"
                  maxLength={20}
                />
              </Field>

              {/* Row 3: Tarih ve Saat | Süre */}
              <Field label="Tarih ve Saat">
                <Input
                  type="datetime-local"
                  value={form.dateLocal}
                  onChange={(e) => set('dateLocal', e.target.value)}
                  className="h-9 text-sm"
                />
              </Field>
              <Field label="Süre (dakika)">
                <Input
                  type="number"
                  min={1}
                  value={form.durationMinutes}
                  onChange={(e) => set('durationMinutes', e.target.value)}
                  placeholder="örn. 90 (opsiyonel)"
                  className="h-9 text-sm"
                />
              </Field>

              {/* Row 4: Etki Oranı + Kilitli + full hint (spans both columns) */}
              <div className="sm:col-span-2">
                <Label className="mb-1 block text-xs font-medium text-muted-foreground">Etki Oranı (%)</Label>
                <div className="flex flex-wrap items-center gap-2">
                  <Input
                    type="number"
                    min={1}
                    max={100}
                    value={weightValue}
                    onChange={(e) => set('weight', e.target.value)}
                    className="h-9 w-28 text-sm"
                    placeholder="örn. 40"
                    readOnly={isWeightLocked}
                    disabled={isWeightLocked}
                  />
                  {isWeightLocked && (
                    <span className="inline-flex items-center gap-1 rounded-md bg-amber-100 px-2 py-1 text-xs font-medium text-amber-700">
                      <Lock className="h-3 w-3" /> Kilitli
                    </span>
                  )}
                  <span className="ml-auto text-xs text-muted-foreground">
                    En fazla %{maxBound ?? '—'} · Kullanılan %{usedOfType} · Kalan %{Math.max(0, remainingWeight)}
                  </span>
                </div>
                {weightOutOfBounds && (
                  <p className="mt-1 text-xs text-amber-600">{buildBoundsMessage()}</p>
                )}
                {isAtLimit && (
                  <p className="mt-1 text-xs text-amber-600">
                    Bu tür için izin verilen sınav sayısına ({limitCount}) ulaşıldı. Devam edebilirsiniz ancak yönerge sınırını aşacaksınız.
                  </p>
                )}
              </div>

              {/* Row 5 (full width): Derslikler */}
              <div className="sm:col-span-2">
                <Label className="mb-1 block text-xs font-medium text-muted-foreground">Derslikler</Label>
                <ClassroomMultiSelect
                  classrooms={cfg?.classrooms ?? []}
                  value={form.classroomIds}
                  onChange={(ids) => set('classroomIds', ids)}
                />
                {selectedClassrooms.length > 0 && (
                  <div className="mt-1.5 flex flex-wrap gap-1.5">
                    {selectedClassrooms.map((c) => (
                      <span
                        key={c.id}
                        className="inline-flex items-center gap-1 rounded-full bg-indigo-50 px-2 py-0.5 text-xs font-medium text-indigo-700"
                      >
                        {c.name}
                        <button
                          type="button"
                          aria-label={`${c.name} kaldır`}
                          onClick={() => set('classroomIds', form.classroomIds.filter((id) => id !== c.id))}
                          className="text-indigo-400 hover:text-indigo-700"
                        >
                          <X className="h-3 w-3" />
                        </button>
                      </span>
                    ))}
                  </div>
                )}
              </div>

              {/* Row 6 (full width): Açıklama */}
              <div className="sm:col-span-2">
                <Label className="mb-1 block text-xs font-medium text-muted-foreground">Açıklama</Label>
                <Textarea
                  value={form.description}
                  onChange={(e) => set('description', e.target.value)}
                  placeholder="Opsiyonel açıklama"
                  rows={2}
                  className="min-h-0 resize-none text-sm"
                />
              </div>
            </div>

            {/* Announce switch — single inline row, not a card */}
            <div className="mt-3 flex items-center gap-2.5 border-t pt-3">
              <Switch
                checked={form.isAnnouncedToStudents}
                onCheckedChange={(v) => set('isAnnouncedToStudents', v)}
                className="data-[state=checked]:bg-emerald-500"
              />
              <Label className="text-sm font-medium">Öğrencilere ilan et</Label>
              <span className="text-xs text-muted-foreground">Açılırsa öğrenciler sınav bilgisini görebilir.</span>
            </div>

            {submitError && (
              <p className="mt-3 text-sm text-destructive">{submitError}</p>
            )}

            {/* Footer */}
            <div className="mt-4 flex justify-end gap-2">
              <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
                İptal
              </Button>
              <Button onClick={handleSubmit} disabled={!canSubmit || isPending}>
                {isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Kaydet
              </Button>
            </div>
          </div>
        )}
      </DraggableDialogContent>
    </DraggableDialog>
  );
}

// ── small presentational helpers ────────────────────────────────
interface FieldProps {
  label: string;
  hint?: string;
  children: ReactNode;
}

function Field({ label, hint, children }: FieldProps) {
  return (
    <div>
      <Label className="mb-1 flex items-center justify-between text-xs font-medium text-muted-foreground">
        <span>{label}</span>
        {hint && <span className="font-normal italic">{hint}</span>}
      </Label>
      {children}
    </div>
  );
}

function TypeBadge({ type }: { type: GradeComponentType }) {
  const label = type === 'Midterm' ? 'Ara Sınav' : type === 'Final' ? 'Yarıyıl Sonu' : 'Diğer';
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium',
        type === 'Midterm' && 'bg-indigo-100 text-indigo-700',
        type === 'Final' && 'bg-violet-100 text-violet-700',
        type === 'Other' && 'bg-slate-100 text-slate-700',
      )}
    >
      {label}
    </span>
  );
}
