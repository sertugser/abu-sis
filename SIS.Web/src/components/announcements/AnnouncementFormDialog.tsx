import { useEffect, useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { AlertCircle, Info } from 'lucide-react';
import {
  useCreateAnnouncement,
  useUpdateAnnouncement,
  useAnnouncementCreateContext,
} from '../../hooks/useAnnouncements';
import type {
  AnnouncementAudience,
  AnnouncementDetail,
} from '../../types/announcements';
import { ANNOUNCEMENT_AUDIENCE_LABELS } from '../../types/announcements';

interface AnnouncementFormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  editing: AnnouncementDetail | null;
}

interface FormState {
  title: string;
  content: string;
  targetAudience: AnnouncementAudience;
  targetDepartmentId: number | null;
  targetFacultyId: number | null;
  targetSectionId: number | null;
  isPinned: boolean;
  startsAt: string;
  expiresAt: string;
}

function emptyForm(defaultAudience: AnnouncementAudience = 'All'): FormState {
  return {
    title: '',
    content: '',
    targetAudience: defaultAudience,
    targetDepartmentId: null,
    targetFacultyId: null,
    targetSectionId: null,
    isPinned: false,
    startsAt: '',
    expiresAt: '',
  };
}

function toInputDateTime(iso: string | null | undefined): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '';
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

// Hangi hedef kitle için hangi FK dolu olacak
function resolveTargetIds(audience: AnnouncementAudience, form: FormState) {
  const isDept    = ['Department', 'DepartmentStudents', 'DepartmentInstructors'].includes(audience);
  const isFaculty = ['Faculty', 'FacultyStudents', 'FacultyInstructors', 'FacultyManagement'].includes(audience);
  const isSection = audience === 'CourseSection';
  return {
    targetDepartmentId: isDept    ? form.targetDepartmentId : null,
    targetFacultyId:    isFaculty ? form.targetFacultyId    : null,
    targetSectionId:    isSection ? form.targetSectionId    : null,
  };
}

export default function AnnouncementFormDialog({
  open,
  onOpenChange,
  editing,
}: AnnouncementFormDialogProps) {
  const createMutation  = useCreateAnnouncement();
  const updateMutation  = useUpdateAnnouncement();
  const { data: ctx, isLoading: ctxLoading } = useAnnouncementCreateContext(open && !editing);

  const [form,  setForm]  = useState<FormState>(() => emptyForm());
  const [error, setError] = useState<string | null>(null);

  // ctx yüklenince default audience belirle
  useEffect(() => {
    if (!open || editing || !ctx) return;
    const defaultAudience = ctx.allowedAudiences[0] ?? 'All';
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setForm({
      ...emptyForm(defaultAudience),
      // Dept/Faculty scoped roller için kendi scope'unu otomatik doldur
      targetDepartmentId: ctx.departmentId,
      targetFacultyId:    ctx.facultyId,
    });
    setError(null);
  }, [open, editing, ctx]);

  // Düzenleme modunda mevcut verileri yükle
  useEffect(() => {
    if (!open || !editing) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setForm({
      title:              editing.title,
      content:            editing.content,
      targetAudience:     editing.targetAudience,
      targetDepartmentId: editing.targetDepartmentId,
      targetFacultyId:    editing.targetFacultyId,
      targetSectionId:    editing.targetSectionId,
      isPinned:           editing.isPinned,
      startsAt:           toInputDateTime(editing.startsAt),
      expiresAt:          toInputDateTime(editing.expiresAt),
    });
    setError(null);
  }, [open, editing]);

  const isPending = createMutation.isPending || updateMutation.isPending;

  // Audience seçeneği değişince scope FK'larını güncelle
  function handleAudienceChange(audience: AnnouncementAudience) {
    setForm(prev => ({
      ...prev,
      targetAudience:     audience,
      targetDepartmentId: ctx?.departmentId ?? prev.targetDepartmentId,
      targetFacultyId:    ctx?.facultyId    ?? prev.targetFacultyId,
      targetSectionId:    null,
    }));
  }

  async function handleSubmit() {
    setError(null);

    if (!form.title.trim()) { setError('Başlık zorunludur.'); return; }
    if (!form.content.trim()) { setError('İçerik zorunludur.'); return; }
    if (form.targetAudience === 'CourseSection' && !form.targetSectionId) {
      setError('Hedef ders şubesi seçilmelidir.');
      return;
    }
    if (form.startsAt && form.expiresAt) {
      if (new Date(form.expiresAt) <= new Date(form.startsAt)) {
        setError('Bitiş tarihi başlangıçtan sonra olmalıdır.');
        return;
      }
    }

    const startsAt  = form.startsAt  ? new Date(form.startsAt).toISOString()  : null;
    const expiresAt = form.expiresAt ? new Date(form.expiresAt).toISOString() : null;
    const ids = resolveTargetIds(form.targetAudience, form);

    try {
      if (editing) {
        await updateMutation.mutateAsync({
          id: editing.id,
          payload: {
            title:           form.title.trim(),
            content:         form.content.trim(),
            targetAudience:  form.targetAudience,
            ...ids,
            isPinned:        form.isPinned,
            startsAt:        startsAt ?? new Date().toISOString(),
            expiresAt,
          },
        });
      } else {
        await createMutation.mutateAsync({
          title:          form.title.trim(),
          content:        form.content.trim(),
          targetAudience: form.targetAudience,
          ...ids,
          isPinned:       form.isPinned,
          startsAt,
          expiresAt,
        });
      }
      onOpenChange(false);
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        'Duyuru kaydedilemedi.';
      setError(message);
    }
  }

  // Context yoksa (düzenleme modu ya da henüz yüklenmediyse) tüm audience seçeneklerini göster
  const audienceOptions: AnnouncementAudience[] = editing
    ? (Object.keys(ANNOUNCEMENT_AUDIENCE_LABELS) as AnnouncementAudience[])
    : (ctx?.allowedAudiences ?? []);

  const showSectionSelector  = form.targetAudience === 'CourseSection';
  const showDeptInfo         = ['Department', 'DepartmentStudents', 'DepartmentInstructors'].includes(form.targetAudience);
  const showFacultyInfo      = ['Faculty', 'FacultyStudents', 'FacultyInstructors', 'FacultyManagement'].includes(form.targetAudience);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{editing ? 'Duyuruyu Düzenle' : 'Yeni Duyuru'}</DialogTitle>
        </DialogHeader>

        {ctxLoading && !editing && (
          <p className="text-sm text-muted-foreground">Yükleniyor...</p>
        )}

        <div className="space-y-4">
          {/* Başlık */}
          <div>
            <Label htmlFor="title">Başlık</Label>
            <Input
              id="title"
              value={form.title}
              onChange={(e) => setForm({ ...form, title: e.target.value })}
              maxLength={200}
              placeholder="Duyuru başlığı"
            />
          </div>

          {/* İçerik */}
          <div>
            <Label htmlFor="content">İçerik</Label>
            <Textarea
              id="content"
              value={form.content}
              onChange={(e) => setForm({ ...form, content: e.target.value })}
              rows={6}
              maxLength={5000}
              placeholder="Duyuru içeriği"
            />
            <p className="text-xs text-muted-foreground mt-1">{form.content.length} / 5000</p>
          </div>

          {/* Hedef Kitle */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <Label htmlFor="audience">Hedef Kitle</Label>
              <select
                id="audience"
                value={form.targetAudience}
                onChange={(e) => handleAudienceChange(e.target.value as AnnouncementAudience)}
                className="w-full bg-card border border-input rounded-md px-3 py-2 text-sm"
              >
                {audienceOptions.map((key) => (
                  <option key={key} value={key}>
                    {ANNOUNCEMENT_AUDIENCE_LABELS[key]}
                  </option>
                ))}
              </select>
            </div>

            {/* Ders Şubesi Seçici (Instructor) */}
            {showSectionSelector && (
              <div>
                <Label htmlFor="section">Ders Şubesi</Label>
                {(ctx?.sections ?? []).length === 0 ? (
                  <p className="text-sm text-muted-foreground mt-2">
                    Aktif ders şubeniz bulunmuyor.
                  </p>
                ) : (
                  <select
                    id="section"
                    value={form.targetSectionId ?? ''}
                    onChange={(e) =>
                      setForm({ ...form, targetSectionId: e.target.value ? Number(e.target.value) : null })
                    }
                    className="w-full bg-card border border-input rounded-md px-3 py-2 text-sm"
                  >
                    <option value="">— Şube seçin —</option>
                    {ctx?.sections.map((s) => (
                      <option key={s.id} value={s.id}>
                        {s.courseCode} {s.courseName} — Şube {s.sectionNumber} ({s.termName})
                      </option>
                    ))}
                  </select>
                )}
              </div>
            )}

            {/* Bölüm bilgisi (otomatik - sadece gösterim) */}
            {showDeptInfo && ctx?.departmentName && (
              <div className="flex items-center gap-2 text-sm text-muted-foreground pt-5">
                <Info className="h-4 w-4 shrink-0" />
                <span>Bölüm: <strong>{ctx.departmentName}</strong></span>
              </div>
            )}

            {/* Fakülte bilgisi (otomatik - sadece gösterim) */}
            {showFacultyInfo && ctx?.facultyName && (
              <div className="flex items-center gap-2 text-sm text-muted-foreground pt-5">
                <Info className="h-4 w-4 shrink-0" />
                <span>Fakülte: <strong>{ctx.facultyName}</strong></span>
              </div>
            )}
          </div>

          {/* Tarih aralığı */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <Label htmlFor="startsAt">Başlangıç</Label>
              <Input
                id="startsAt"
                type="datetime-local"
                value={form.startsAt}
                onChange={(e) => setForm({ ...form, startsAt: e.target.value })}
              />
              <p className="text-[11px] text-muted-foreground mt-1">
                Boş bırakılırsa şu andan itibaren yayınlanır.
              </p>
            </div>
            <div>
              <Label htmlFor="expiresAt">Bitiş (Opsiyonel)</Label>
              <Input
                id="expiresAt"
                type="datetime-local"
                value={form.expiresAt}
                onChange={(e) => setForm({ ...form, expiresAt: e.target.value })}
              />
            </div>
          </div>

          {/* Pin */}
          <div className="flex items-center gap-3 pt-2">
            <Switch
              id="pinned"
              checked={form.isPinned}
              onCheckedChange={(v) => setForm({ ...form, isPinned: v })}
            />
            <Label htmlFor="pinned" className="cursor-pointer">Üste Sabitle</Label>
          </div>

          {error && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
            İptal
          </Button>
          <Button onClick={handleSubmit} disabled={isPending || (ctxLoading && !editing)}>
            {isPending ? 'Kaydediliyor...' : editing ? 'Güncelle' : 'Oluştur'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
