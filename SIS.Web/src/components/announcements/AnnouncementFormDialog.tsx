import { useEffect, useState } from 'react';
import {
  Dialog,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { AlertCircle, Info, Upload } from 'lucide-react';
import {
  useCreateAnnouncement,
  useDeleteAnnouncementAttachment,
  useUpdateAnnouncement,
  useUploadAnnouncementAttachment,
  useAnnouncementCreateContext,
  useAnnouncementDetail,
} from '../../hooks/useAnnouncements';
import { useAnnouncementCategories } from '../../hooks/useAnnouncementCategories';
import AttachmentList from './AttachmentList';
import type {
  AnnouncementAttachmentSummary,
  AnnouncementAudience,
  AnnouncementContentFormat,
  AnnouncementDetail,
  AnnouncementSettings,
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
  isPopup: boolean;
  requiresAcknowledgement: boolean;
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
    isPopup: false,
    requiresAcknowledgement: false,
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
  const createMutation   = useCreateAnnouncement();
  const updateMutation   = useUpdateAnnouncement();
  const uploadAttachment = useUploadAnnouncementAttachment();
  const deleteAttachment = useDeleteAnnouncementAttachment();
  const { data: ctx, isLoading: ctxLoading } = useAnnouncementCreateContext(open);
  const { data: categories = [] } = useAnnouncementCategories(open);
  const { data: editingDetail }   = useAnnouncementDetail(editing?.id ?? null, !!editing && open);

  const [form,  setForm]  = useState<FormState>(() => emptyForm());
  const [error, setError] = useState<string | null>(null);
  const [pendingFiles, setPendingFiles] = useState<File[]>([]);
  const [uploadProgress, setUploadProgress] = useState<{ name: string; percent: number } | null>(null);
  const [attachmentError, setAttachmentError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) {
      setPendingFiles([]);
      setUploadProgress(null);
      setAttachmentError(null);
      setError(null);
    }
  }, [open]);

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

  useEffect(() => {
    if (!open || !editing) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setForm({
      title:                   editing.title,
      content:                 editing.content,
      contentFormat:           editing.contentFormat,
      targetAudience:          editing.targetAudience,
      targetDepartmentId:      editing.targetDepartmentId,
      targetFacultyId:         editing.targetFacultyId,
      targetSectionId:         editing.targetSectionId,
      targetStudentYear:       editing.targetStudentYear,
      categoryId:              editing.category?.id ?? null,
      isPinned:                editing.isPinned,
      isPopup:                 editing.isPopup,
      requiresAcknowledgement: editing.requiresAcknowledgement,
      startsAt:                toInputDateTime(editing.startsAt),
      expiresAt:               toInputDateTime(editing.expiresAt),
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

    const targetStudentYear = audienceSupportsStudentYearFilter(form.targetAudience)
      ? form.targetStudentYear
      : null;

    try {
      let announcementId: number;
      if (editing) {
        const updated = await updateMutation.mutateAsync({
          id: editing.id,
          payload: {
            title:                   form.title.trim(),
            content:                 form.content.trim(),
            contentFormat:           form.contentFormat,
            targetAudience:          form.targetAudience,
            ...ids,
            targetStudentYear,
            categoryId:              form.categoryId,
            isPinned:                form.isPinned,
            isPopup:                 form.isPopup,
            requiresAcknowledgement: form.isPopup && form.requiresAcknowledgement,
            startsAt:                startsAt ?? new Date().toISOString(),
            expiresAt,
          },
        });
        announcementId = updated.id;
      } else {
        const created = await createMutation.mutateAsync({
          title:                   form.title.trim(),
          content:                 form.content.trim(),
          contentFormat:           form.contentFormat,
          targetAudience:          form.targetAudience,
          ...ids,
          targetStudentYear,
          categoryId:              form.categoryId,
          isPinned:                form.isPinned,
          isPopup:                 form.isPopup,
          requiresAcknowledgement: form.isPopup && form.requiresAcknowledgement,
          startsAt,
          expiresAt,
        });
        announcementId = created.id;
      }

      if (pendingFiles.length > 0) {
        for (const file of pendingFiles) {
          setUploadProgress({ name: file.name, percent: 0 });
          await uploadAttachment.mutateAsync({
            announcementId,
            file,
            onProgress: (percent) => setUploadProgress({ name: file.name, percent }),
          });
        }
        setUploadProgress(null);
        setPendingFiles([]);
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

  const showSectionSelector = form.targetAudience === 'CourseSection';
  const showDeptInfo        = ['Department', 'DepartmentStudents', 'DepartmentInstructors'].includes(form.targetAudience);
  const showFacultyInfo     = ['Faculty', 'FacultyStudents', 'FacultyInstructors', 'FacultyManagement'].includes(form.targetAudience);
  const showYearSelector    = audienceSupportsStudentYearFilter(form.targetAudience);

  const studentYearOptions: number[] = settings
    ? Array.from(
        { length: Math.max(0, settings.studentYearFilterMax - settings.studentYearFilterMin + 1) },
        (_, i) => settings.studentYearFilterMin + i,
      )
    : [];

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <ResizableDialogContent
        defaultWidth={1100}
        defaultHeight={720}
        minWidth={760}
        minHeight={540}
        className="flex flex-col p-0 gap-0"
      >
        {/* Header */}
        <DialogHeader className="px-6 pt-5 pb-3 shrink-0 border-b border-border/60 bg-gradient-to-r from-card via-card to-accent/20">
          <DialogTitle className="text-lg">
            {editing ? 'Duyuruyu Düzenle' : 'Yeni Duyuru'}
          </DialogTitle>
          {ctxLoading && !settings && (
            <p className="text-xs text-muted-foreground mt-1">Ayarlar yükleniyor...</p>
          )}
        </DialogHeader>

        {/* Body — yatay denge: sol (içerik) · sağ (form ayarları sticky) */}
        <div className="flex-1 overflow-y-auto">
          <div className="grid grid-cols-1 lg:grid-cols-[1.5fr_1fr]">
            {/* ── SOL: Başlık + İçerik editörü ────────────────── */}
            <div className="space-y-4 px-6 py-5 lg:border-r lg:border-border/60 min-w-0">
              <div>
                <Label htmlFor="title" className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  Başlık
                </Label>
                <Input
                  id="title"
                  value={form.title}
                  onChange={(e) => setForm({ ...form, title: e.target.value })}
                  maxLength={settings?.maxTitleLength}
                  placeholder="Duyurunuzun başlığı"
                  className="mt-1.5 text-base font-medium h-11"
                />
                {settings && (
                  <p className="text-[11px] text-muted-foreground mt-1 text-right">
                    {form.title.length} / {settings.maxTitleLength}
                  </p>
                )}
              </div>

              <div>
                <div className="flex items-center justify-between mb-1.5">
                  <Label className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                    İçerik
                  </Label>
                  <Tabs
                    value={form.contentFormat}
                    onValueChange={(value) => handleFormatChange(value as AnnouncementContentFormat)}
                  >
                    <TabsList className="h-7">
                      <TabsTrigger value="html" className="text-[11px] px-2.5">Zengin</TabsTrigger>
                      <TabsTrigger value="plain" className="text-[11px] px-2.5">Düz</TabsTrigger>
                    </TabsList>
                  </Tabs>
                </div>

                {form.contentFormat === 'html' && settings ? (
                  <RichTextEditor
                    value={form.content}
                    onChange={(html) => setForm({ ...form, content: html })}
                    placeholder="Duyuru içeriğini buraya yazın..."
                    maxLength={settings.maxContentLength}
                    allowImages={settings.allowImagesInContent}
                    allowTables={settings.allowTablesInContent}
                  />
                ) : (
                  <>
                    <Textarea
                      value={form.content}
                      onChange={(e) => setForm({ ...form, content: e.target.value })}
                      rows={14}
                      maxLength={settings?.maxContentLength}
                      placeholder="Düz metin yazın..."
                      className="resize-none"
                    />
                    {settings && (
                      <p className="text-[11px] text-muted-foreground mt-1 text-right">
                        {form.content.length} / {settings.maxContentLength}
                      </p>
                    )}
                  </>
                )}
              </div>

              {error && (
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}
            </div>

            {/* ── SAĞ: Ayarlar tek akış (kutucuk yok) ─────────── */}
            <div className="px-6 py-5 space-y-5 min-w-0 bg-gradient-to-b from-card to-accent/10">
              <Section title="Hedef">
                <div className="space-y-3">
                  <Field label="Hedef Kitle">
                    <select
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
                  </Field>

                  {showSectionSelector && (
                    <Field label="Ders Şubesi">
                      {(ctx?.sections ?? []).length === 0 ? (
                        <p className="text-sm text-muted-foreground">
                          Aktif ders şubeniz bulunmuyor.
                        </p>
                      ) : (
                        <select
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
                    </Field>
                  )}

                  {showDeptInfo && ctx?.departmentName && (
                    <ScopeInfo label="Bölüm" value={ctx.departmentName} />
                  )}
                  {showFacultyInfo && ctx?.facultyName && (
                    <ScopeInfo label="Fakülte" value={ctx.facultyName} />
                  )}

                  <Field
                    label="Kategori"
                    hint="Opsiyonel"
                  >
                    <select
                      value={form.categoryId ?? ''}
                      onChange={(e) =>
                        setForm({ ...form, categoryId: e.target.value ? Number(e.target.value) : null })
                      }
                      className="w-full bg-card border border-input rounded-md px-3 py-2 text-sm"
                    >
                      <option value="">— Kategorisiz —</option>
                      {categories.map((cat) => (
                        <option key={cat.id} value={cat.id}>{cat.name}</option>
                      ))}
                    </select>
                  </Field>

                  {showYearSelector && settings && (
                    <Field
                      label="Sınıf Filtresi"
                      hint="Opsiyonel"
                    >
                      <select
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
                          <option key={year} value={year}>{year}. Sınıf</option>
                        ))}
                      </select>
                    </Field>
                  )}
                </div>
              </Section>

              <Section title="Zamanlama">
                <div className="grid grid-cols-2 gap-2">
                  <Field label="Başlangıç">
                    <Input
                      type="datetime-local"
                      value={form.startsAt}
                      onChange={(e) => setForm({ ...form, startsAt: e.target.value })}
                    />
                  </Field>
                  <Field label="Bitiş" hint="Ops.">
                    <Input
                      type="datetime-local"
                      value={form.expiresAt}
                      onChange={(e) => setForm({ ...form, expiresAt: e.target.value })}
                    />
                  </Field>
                </div>
                <p className="text-[11px] text-muted-foreground mt-1.5">
                  Boş başlangıç → şu andan itibaren yayınlanır.
                </p>
              </Section>

              <Section title="Davranış">
                <div className="space-y-2.5">
                  <SwitchRow
                    id="pinned"
                    checked={form.isPinned}
                    onCheckedChange={(v) => setForm({ ...form, isPinned: v })}
                    label="Üste Sabitle"
                  />
                  <SwitchRow
                    id="isPopup"
                    checked={form.isPopup}
                    onCheckedChange={(v) =>
                      setForm({
                        ...form,
                        isPopup: v,
                        requiresAcknowledgement: v ? form.requiresAcknowledgement : false,
                      })
                    }
                    label="Pop-up göster"
                    hint="Login sonrası blocking dialog"
                  />
                  {form.isPopup && (
                    <div className="pl-7 border-l-2 border-primary/30">
                      <SwitchRow
                        id="requiresAck"
                        checked={form.requiresAcknowledgement}
                        onCheckedChange={(v) => setForm({ ...form, requiresAcknowledgement: v })}
                        label="Onay zorunlu"
                        hint='"Okudum, Anladım" basılana kadar kapanmaz'
                      />
                    </div>
                  )}
                </div>
              </Section>

              {settings?.attachmentsEnabled && (
                <Section
                  title="Dosya Ekleri"
                  hint={`${(editingDetail?.attachments.length ?? 0) + pendingFiles.length}/${settings.attachmentMaxCount} · maks ${settings.attachmentMaxSizeMB} MB`}
                >
                  <AttachmentSection
                    settings={settings}
                    pendingFiles={pendingFiles}
                    onPendingFilesChange={(files) => {
                      setAttachmentError(null);
                      setPendingFiles(files);
                    }}
                    existingAttachments={editingDetail?.attachments ?? []}
                    announcementId={editing?.id ?? null}
                    onDeleteExisting={(attId) => {
                      if (!editing) return;
                      deleteAttachment.mutate(
                        { announcementId: editing.id, attachmentId: attId },
                        { onError: () => setAttachmentError('Ek silinemedi.') },
                      );
                    }}
                    isDeleting={deleteAttachment.isPending}
                    progress={uploadProgress}
                    error={attachmentError}
                    onError={setAttachmentError}
                  />
                </Section>
              )}
            </div>
          </div>
        </div>

        {/* Footer */}
        <DialogFooter className="px-6 py-4 shrink-0 border-t border-border/60 bg-muted/20">
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
            İptal
          </Button>
          <Button
            onClick={handleSubmit}
            disabled={isPending || !settings}
            className="bg-gradient-to-br from-primary to-primary-light shadow-md hover:shadow-lg transition-shadow"
          >
            {isPending ? 'Kaydediliyor...' : editing ? 'Güncelle' : 'Oluştur'}
          </Button>
        </DialogFooter>
      </ResizableDialogContent>
    </Dialog>
  );
}

// ── Layout yardımcıları ─────────────────────────────────────────────

interface SectionProps {
  title: string;
  hint?: string;
  children: React.ReactNode;
}

/**
 * Sağ kolondaki ayar bölümleri için ince başlıklı wrapper.
 * Kutu/kart yok — yalnızca caps başlık + içerik. Bölümler arası ayrım dikey boşlukla sağlanır.
 */
function Section({ title, hint, children }: SectionProps) {
  return (
    <div>
      <div className="flex items-center justify-between gap-2 mb-2">
        <h4 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
          {title}
        </h4>
        {hint && <span className="text-[11px] text-muted-foreground">{hint}</span>}
      </div>
      {children}
    </div>
  );
}

interface FieldProps {
  label: string;
  hint?: string;
  children: React.ReactNode;
}

function Field({ label, hint, children }: FieldProps) {
  return (
    <div>
      <div className="flex items-center justify-between mb-1">
        <Label className="text-xs font-medium">{label}</Label>
        {hint && <span className="text-[10px] text-muted-foreground">{hint}</span>}
      </div>
      {children}
    </div>
  );
}

interface SwitchRowProps {
  id: string;
  checked: boolean;
  onCheckedChange: (v: boolean) => void;
  label: string;
  hint?: string;
}

function SwitchRow({ id, checked, onCheckedChange, label, hint }: SwitchRowProps) {
  return (
    <div className="flex items-start gap-2.5">
      <Switch id={id} checked={checked} onCheckedChange={onCheckedChange} className="mt-0.5" />
      <Label htmlFor={id} className="cursor-pointer min-w-0">
        <span className="block text-sm font-medium leading-tight">{label}</span>
        {hint && (
          <span className="mt-0.5 block text-[11px] font-normal text-muted-foreground leading-snug">
            {hint}
          </span>
        )}
      </Label>
    </div>
  );
}

function ScopeInfo({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center gap-2 rounded-md bg-accent/40 border border-border/50 px-2.5 py-1.5 text-xs">
      <Info className="h-3 w-3 text-muted-foreground shrink-0" />
      <span className="text-muted-foreground">{label}:</span>
      <strong className="truncate text-foreground">{value}</strong>
    </div>
  );
}

interface AttachmentSectionProps {
  settings: AnnouncementSettings;
  pendingFiles: File[];
  onPendingFilesChange: (files: File[]) => void;
  existingAttachments: AnnouncementAttachmentSummary[];
  announcementId: number | null;
  onDeleteExisting: (attachmentId: number) => void;
  isDeleting: boolean;
  progress: { name: string; percent: number } | null;
  error: string | null;
  onError: (message: string | null) => void;
}

function AttachmentSection({
  settings,
  pendingFiles,
  onPendingFilesChange,
  existingAttachments,
  announcementId,
  onDeleteExisting,
  isDeleting,
  progress,
  error,
  onError,
}: AttachmentSectionProps) {
  const maxBytes = settings.attachmentMaxSizeMB * 1024 * 1024;
  const totalCount = existingAttachments.length + pendingFiles.length;
  const remaining = Math.max(0, settings.attachmentMaxCount - totalCount);

  function handleFilesAdded(fileList: FileList | null) {
    if (!fileList || fileList.length === 0) return;
    const newFiles: File[] = [];
    for (let i = 0; i < fileList.length; i++) {
      const f = fileList.item(i);
      if (!f) continue;
      if (f.size > maxBytes) {
        onError(`"${f.name}" dosyası ${settings.attachmentMaxSizeMB} MB sınırını aşıyor.`);
        return;
      }
      newFiles.push(f);
    }
    const combined = [...pendingFiles, ...newFiles];
    if (existingAttachments.length + combined.length > settings.attachmentMaxCount) {
      onError(`En fazla ${settings.attachmentMaxCount} dosya eklenebilir.`);
      return;
    }
    onError(null);
    onPendingFilesChange(combined);
  }

  function removePending(index: number) {
    const next = pendingFiles.slice();
    next.splice(index, 1);
    onPendingFilesChange(next);
  }

  return (
    <div className="space-y-2">
      {announcementId !== null && existingAttachments.length > 0 && (
        <AttachmentList
          announcementId={announcementId}
          attachments={existingAttachments}
          canDelete
          onDelete={onDeleteExisting}
          isDeleting={isDeleting}
        />
      )}

      {pendingFiles.length > 0 && (
        <ul className="space-y-1.5">
          {pendingFiles.map((f, idx) => (
            <li
              key={`${f.name}-${idx}`}
              className="flex items-center gap-2 rounded-md border border-dashed border-primary/40 bg-primary/5 px-3 py-2 text-sm"
            >
              <Upload className="h-4 w-4 text-primary shrink-0" />
              <div className="min-w-0 flex-1">
                <div className="truncate font-medium">{f.name}</div>
                <div className="text-[11px] text-muted-foreground">
                  {(f.size / 1024).toFixed(1)} KB · kaydedince yüklenir
                </div>
              </div>
              <Button variant="ghost" size="sm" className="h-7" onClick={() => removePending(idx)}>
                Kaldır
              </Button>
            </li>
          ))}
        </ul>
      )}

      {progress && (
        <div className="rounded-md border border-primary/40 bg-primary/5 px-3 py-2 text-sm">
          <div className="flex items-center justify-between mb-1">
            <span className="truncate">{progress.name}</span>
            <span className="text-xs">{progress.percent}%</span>
          </div>
          <div className="h-1.5 w-full overflow-hidden rounded-full bg-muted">
            <div className="h-full bg-primary transition-all" style={{ width: `${progress.percent}%` }} />
          </div>
        </div>
      )}

      {remaining > 0 && (
        <label className="flex flex-col items-center justify-center gap-1 rounded-md border-2 border-dashed border-border/80 bg-muted/20 px-4 py-5 text-sm text-muted-foreground cursor-pointer hover:border-primary/60 hover:bg-primary/5 transition-colors">
          <Upload className="h-5 w-5" />
          <span className="font-medium">Dosya seç</span>
          <span className="text-[11px]">PDF, Word, Excel, resim... (kalan: {remaining})</span>
          <input
            type="file"
            multiple
            className="hidden"
            onChange={(e) => {
              handleFilesAdded(e.target.files);
              e.target.value = '';
            }}
          />
        </label>
      )}

      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  );
}
