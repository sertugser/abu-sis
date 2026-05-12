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
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { AlertCircle, Info } from 'lucide-react';
import {
  useCreateAnnouncement,
  useUpdateAnnouncement,
  useAnnouncementCreateContext,
} from '../../hooks/useAnnouncements';
import { useAnnouncementCategories } from '../../hooks/useAnnouncementCategories';
import type {
  AnnouncementAudience,
  AnnouncementContentFormat,
  AnnouncementDetail,
} from '../../types/announcements';
import {
  ANNOUNCEMENT_AUDIENCE_LABELS,
  audienceSupportsStudentYearFilter,
} from '../../types/announcements';
import RichTextEditor from './RichTextEditor';

interface AnnouncementFormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  editing: AnnouncementDetail | null;
}

interface FormState {
  title: string;
  content: string;
  contentFormat: AnnouncementContentFormat;
  targetAudience: AnnouncementAudience;
  targetDepartmentId: number | null;
  targetFacultyId: number | null;
  targetSectionId: number | null;
  targetStudentYear: number | null;
  categoryId: number | null;
  isPinned: boolean;
  startsAt: string;
  expiresAt: string;
}

const DEFAULT_AUDIENCE: AnnouncementAudience = 'All';

function emptyForm(defaultAudience: AnnouncementAudience = DEFAULT_AUDIENCE): FormState {
  return {
    title: '',
    content: '',
    contentFormat: 'html',
    targetAudience: defaultAudience,
    targetDepartmentId: null,
    targetFacultyId: null,
    targetSectionId: null,
    targetStudentYear: null,
    categoryId: null,
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
  const { data: ctx, isLoading: ctxLoading } = useAnnouncementCreateContext(open);
  const { data: categories = [] } = useAnnouncementCategories(open);

  const [form,  setForm]  = useState<FormState>(() => emptyForm());
  const [error, setError] = useState<string | null>(null);

  // Yeni kayıt: context yüklenince default audience belirle
  useEffect(() => {
    if (!open || editing || !ctx) return;
    const defaultAudience = ctx.allowedAudiences[0] ?? DEFAULT_AUDIENCE;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setForm({
      ...emptyForm(defaultAudience),
      targetDepartmentId: ctx.departmentId,
      targetFacultyId:    ctx.facultyId,
    });
    setError(null);
  }, [open, editing, ctx]);

  // Düzenleme: mevcut kaydın değerlerini yükle
  useEffect(() => {
    if (!open || !editing) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setForm({
      title:              editing.title,
      content:            editing.content,
      contentFormat:      editing.contentFormat,
      targetAudience:     editing.targetAudience,
      targetDepartmentId: editing.targetDepartmentId,
      targetFacultyId:    editing.targetFacultyId,
      targetSectionId:    editing.targetSectionId,
      targetStudentYear:  editing.targetStudentYear,
      categoryId:         editing.category?.id ?? null,
      isPinned:           editing.isPinned,
      startsAt:           toInputDateTime(editing.startsAt),
      expiresAt:          toInputDateTime(editing.expiresAt),
    });
    setError(null);
  }, [open, editing]);

  const isPending = createMutation.isPending || updateMutation.isPending;
  const settings  = ctx?.settings;

  function handleAudienceChange(audience: AnnouncementAudience) {
    setForm((prev) => ({
      ...prev,
      targetAudience:     audience,
      targetDepartmentId: ctx?.departmentId ?? prev.targetDepartmentId,
      targetFacultyId:    ctx?.facultyId    ?? prev.targetFacultyId,
      targetSectionId:    null,
      // Sınıf filtresi yalnızca öğrenci hedefli audience'larda anlamlıdır
      targetStudentYear:  audienceSupportsStudentYearFilter(audience) ? prev.targetStudentYear : null,
    }));
  }

  function handleFormatChange(next: AnnouncementContentFormat) {
    setForm((prev) => ({ ...prev, contentFormat: next }));
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
    if (settings && form.title.length > settings.maxTitleLength) {
      setError(`Başlık en fazla ${settings.maxTitleLength} karakter olabilir.`);
      return;
    }
    if (settings && form.content.length > settings.maxContentLength) {
      setError(`İçerik en fazla ${settings.maxContentLength} karakter olabilir.`);
      return;
    }

    const startsAt  = form.startsAt  ? new Date(form.startsAt).toISOString()  : null;
    const expiresAt = form.expiresAt ? new Date(form.expiresAt).toISOString() : null;
    const ids = resolveTargetIds(form.targetAudience, form);

    // Sınıf filtresi sadece desteklenen audience'larda gönderilir, aksi halde null
    const targetStudentYear = audienceSupportsStudentYearFilter(form.targetAudience)
      ? form.targetStudentYear
      : null;

    try {
      if (editing) {
        await updateMutation.mutateAsync({
          id: editing.id,
          payload: {
            title:             form.title.trim(),
            content:           form.content.trim(),
            contentFormat:     form.contentFormat,
            targetAudience:    form.targetAudience,
            ...ids,
            targetStudentYear,
            categoryId:        form.categoryId,
            isPinned:          form.isPinned,
            startsAt:          startsAt ?? new Date().toISOString(),
            expiresAt,
          },
        });
      } else {
        await createMutation.mutateAsync({
          title:             form.title.trim(),
          content:           form.content.trim(),
          contentFormat:     form.contentFormat,
          targetAudience:    form.targetAudience,
          ...ids,
          targetStudentYear,
          categoryId:        form.categoryId,
          isPinned:          form.isPinned,
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

  const audienceOptions: AnnouncementAudience[] = editing
    ? (Object.keys(ANNOUNCEMENT_AUDIENCE_LABELS) as AnnouncementAudience[])
    : (ctx?.allowedAudiences ?? []);

  const showSectionSelector  = form.targetAudience === 'CourseSection';
  const showDeptInfo         = ['Department', 'DepartmentStudents', 'DepartmentInstructors'].includes(form.targetAudience);
  const showFacultyInfo      = ['Faculty', 'FacultyStudents', 'FacultyInstructors', 'FacultyManagement'].includes(form.targetAudience);
  const showYearSelector     = audienceSupportsStudentYearFilter(form.targetAudience);

  const studentYearOptions: number[] = settings
    ? Array.from(
        { length: Math.max(0, settings.studentYearFilterMax - settings.studentYearFilterMin + 1) },
        (_, i) => settings.studentYearFilterMin + i
      )
    : [];

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{editing ? 'Duyuruyu Düzenle' : 'Yeni Duyuru'}</DialogTitle>
        </DialogHeader>

        {ctxLoading && !settings && (
          <p className="text-sm text-muted-foreground">Ayarlar yükleniyor...</p>
        )}

        <div className="space-y-4">
          {/* Başlık */}
          <div>
            <Label htmlFor="title">Başlık</Label>
            <Input
              id="title"
              value={form.title}
              onChange={(e) => setForm({ ...form, title: e.target.value })}
              maxLength={settings?.maxTitleLength}
              placeholder="Duyuru başlığı"
            />
            {settings && (
              <p className="text-[11px] text-muted-foreground mt-1">
                {form.title.length} / {settings.maxTitleLength}
              </p>
            )}
          </div>

          {/* İçerik biçimi sekmesi */}
          <div>
            <div className="flex items-center justify-between mb-2">
              <Label>İçerik</Label>
              <Tabs
                value={form.contentFormat}
                onValueChange={(value) => handleFormatChange(value as AnnouncementContentFormat)}
              >
                <TabsList className="h-8">
                  <TabsTrigger value="html" className="text-xs px-3">Zengin Metin</TabsTrigger>
                  <TabsTrigger value="plain" className="text-xs px-3">Düz Metin</TabsTrigger>
                </TabsList>
              </Tabs>
            </div>

            {form.contentFormat === 'html' && settings ? (
              <RichTextEditor
                value={form.content}
                onChange={(html) => setForm({ ...form, content: html })}
                placeholder="Zengin içerik yazın..."
                maxLength={settings.maxContentLength}
                allowImages={settings.allowImagesInContent}
                allowTables={settings.allowTablesInContent}
              />
            ) : (
              <>
                <Textarea
                  value={form.content}
                  onChange={(e) => setForm({ ...form, content: e.target.value })}
                  rows={8}
                  maxLength={settings?.maxContentLength}
                  placeholder="Düz metin yazın..."
                />
                {settings && (
                  <p className="text-[11px] text-muted-foreground mt-1">
                    {form.content.length} / {settings.maxContentLength}
                  </p>
                )}
              </>
            )}
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

            {showDeptInfo && ctx?.departmentName && (
              <div className="flex items-center gap-2 text-sm text-muted-foreground pt-5">
                <Info className="h-4 w-4 shrink-0" />
                <span>Bölüm: <strong>{ctx.departmentName}</strong></span>
              </div>
            )}

            {showFacultyInfo && ctx?.facultyName && (
              <div className="flex items-center gap-2 text-sm text-muted-foreground pt-5">
                <Info className="h-4 w-4 shrink-0" />
                <span>Fakülte: <strong>{ctx.facultyName}</strong></span>
              </div>
            )}

            <div>
              <Label htmlFor="category">Kategori (Opsiyonel)</Label>
              <select
                id="category"
                value={form.categoryId ?? ''}
                onChange={(e) =>
                  setForm({ ...form, categoryId: e.target.value ? Number(e.target.value) : null })
                }
                className="w-full bg-card border border-input rounded-md px-3 py-2 text-sm"
              >
                <option value="">— Kategorisiz —</option>
                {categories.map((cat) => (
                  <option key={cat.id} value={cat.id}>
                    {cat.name}
                  </option>
                ))}
              </select>
            </div>

            {showYearSelector && settings && (
              <div>
                <Label htmlFor="studentYear">Sınıf Filtresi (Opsiyonel)</Label>
                <select
                  id="studentYear"
                  value={form.targetStudentYear ?? ''}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      targetStudentYear: e.target.value ? Number(e.target.value) : null,
                    })
                  }
                  className="w-full bg-card border border-input rounded-md px-3 py-2 text-sm"
                >
                  <option value="">— Tüm sınıflar —</option>
                  {studentYearOptions.map((year) => (
                    <option key={year} value={year}>
                      {year}. Sınıf
                    </option>
                  ))}
                </select>
                <p className="text-[11px] text-muted-foreground mt-1">
                  Boş bırakılırsa hedef kitledeki tüm öğrenciler dahil edilir.
                </p>
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
          <Button onClick={handleSubmit} disabled={isPending || !settings}>
            {isPending ? 'Kaydediliyor...' : editing ? 'Güncelle' : 'Oluştur'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
