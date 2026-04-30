import { useMemo, useState } from 'react';
import { Dialog, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { AccessPolicyPopover } from './AccessPolicyPopover';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { cn } from '@/lib/utils';
import {
  AlertCircle,
  CheckCircle2,
  Copy,
  Edit2,
  Plus,
  RotateCcw,
  Trash2,
  X,
} from 'lucide-react';
import type { OpenedCourseSectionDto, InstructorOptionDto, InstructorWorkloadDto } from '../../types/courseOpening';
import { WorkloadBar } from './WorkloadBar';

type Mode = 'list' | 'add' | 'edit' | 'duplicate' | 'closeConfirm';

interface CourseMetaSummary {
  courseId: number;
  courseCode: string;
  courseName: string;
  /** İlk şubeden alınmış ortak ders bilgileri — şube bağımsız Course alanlarıdır */
  theoryHours?: number;
  labHours?: number;
  credits?: number;
  ects?: number;
  suggestedSemester?: number;
  departmentName?: string | null;
}

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  course: CourseMetaSummary | null;
  sections: OpenedCourseSectionDto[];
  instructors: InstructorOptionDto[];
  termId: number;
  onAddSection: (
    courseId: number,
    capacity: number,
    instructorId: number | undefined,
    internalQuota?: number,
    externalQuota?: number,
  ) => void;
  onUpdateSection: (
    sectionId: number,
    capacity: number,
    instructorId: number | undefined,
    internalQuota?: number | null,
    externalQuota?: number | null,
  ) => void;
  onCloseSection: (sectionId: number) => void;
  onReactivateSection: (sectionId: number) => Promise<unknown>;
  updatePending: boolean;
  closePending: boolean;
  closeError: Error | null;
  workloadData: InstructorWorkloadDto | undefined;
  onInstructorSelect: (id: number | null) => void;
  readOnly?: boolean;
}

/**
 * Proliz tarzı tek dialog: ders meta bilgileri + şubeler tablosu + tek-şube aksiyonları
 * (Yeni Şube / Düzenle / Çoğalt / Kapat / Yeniden Aç).
 *
 * Alt bar'daki "Erişim" ve "Tüm Şubeleri Sil" ders seviyesinde — bu dialog şube seviyesinde.
 */
export function CourseManagementDialog({
  open,
  onOpenChange,
  course,
  sections,
  instructors,
  termId,
  onAddSection,
  onUpdateSection,
  onCloseSection,
  onReactivateSection,
  updatePending,
  closePending,
  closeError,
  workloadData,
  onInstructorSelect,
  readOnly = false,
}: Props) {
  void termId;
  const [selectedSectionId, setSelectedSectionId] = useState<number | null>(null);
  const [mode, setMode] = useState<Mode>('list');

  const [formCap, setFormCap] = useState('40');
  const [formInternalQuota, setFormInternalQuota] = useState('');
  const [formExternalQuota, setFormExternalQuota] = useState('');
  const [formInsId, setFormInsId] = useState('');

  const [reactivating, setReactivating] = useState<number | null>(null);
  // Çoğalt modunda kaynak şubenin numarasını sakla — header ve banner için
  const [duplicateSourceNumber, setDuplicateSourceNumber] = useState<number | null>(null);

  const selectedSection = useMemo(
    () => (selectedSectionId == null ? null : sections.find((s) => s.sectionId === selectedSectionId) ?? null),
    [sections, selectedSectionId],
  );

  // Dialog her açılışta liste moduna dön — adjust-state-during-render
  // (React 19+: useEffect içinde setState yasaklandı, prev-tracking ile render'da update)
  const [prevOpen, setPrevOpen] = useState(open);
  if (open !== prevOpen) {
    setPrevOpen(open);
    if (open) {
      setMode('list');
      setDuplicateSourceNumber(null);
      setSelectedSectionId((prev) => prev != null && sections.some((s) => s.sectionId === prev) ? prev : null);
    }
  }

  const startAdd = () => {
    setFormCap('40');
    setFormInternalQuota('');
    setFormExternalQuota('');
    setFormInsId('');
    setMode('add');
  };

  const startEdit = () => {
    if (!selectedSection) return;
    setFormCap(String(selectedSection.capacity));
    setFormInternalQuota(selectedSection.internalQuota != null ? String(selectedSection.internalQuota) : '');
    setFormExternalQuota(selectedSection.externalQuota != null ? String(selectedSection.externalQuota) : '');
    setFormInsId(selectedSection.instructorId?.toString() ?? '');
    onInstructorSelect(selectedSection.instructorId ?? null);
    setMode('edit');
  };

  const startDuplicate = () => {
    if (!selectedSection) return;
    // Çoğalt: seçili şubenin değerlerini default olarak yeni şube formuna yükle.
    // Kaydet basıldığında YENİ bir şube oluşur (mevcut şubeyi değiştirmez).
    setFormCap(String(selectedSection.capacity));
    setFormInternalQuota(selectedSection.internalQuota != null ? String(selectedSection.internalQuota) : '');
    setFormExternalQuota(selectedSection.externalQuota != null ? String(selectedSection.externalQuota) : '');
    setFormInsId(selectedSection.instructorId?.toString() ?? '');
    onInstructorSelect(selectedSection.instructorId ?? null);
    setDuplicateSourceNumber(selectedSection.sectionNumber);
    setMode('duplicate');
  };

  const handleSave = () => {
    if (!course) return;
    const cap = Number(formCap) || 0;
    const intQ = formInternalQuota ? Number(formInternalQuota) : undefined;
    const extQ = formExternalQuota ? Number(formExternalQuota) : undefined;
    const insId = formInsId ? Number(formInsId) : undefined;

    if (mode === 'add' || mode === 'duplicate') {
      // Çoğalt da yeni şube oluşturur (kaynak şubeden bağımsız)
      onAddSection(course.courseId, cap, insId, intQ, extQ);
    } else if (mode === 'edit' && selectedSection) {
      onUpdateSection(
        selectedSection.sectionId,
        cap,
        insId,
        formInternalQuota ? Number(formInternalQuota) : null,
        formExternalQuota ? Number(formExternalQuota) : null,
      );
    }
    setMode('list');
    setDuplicateSourceNumber(null);
  };

  const handleCloseConfirmed = () => {
    if (!selectedSection) return;
    onCloseSection(selectedSection.sectionId);
    setMode('list');
  };

  const handleReactivate = (sectionId: number) => {
    setReactivating(sectionId);
    onReactivateSection(sectionId).finally(() => setReactivating(null));
  };

  const quotaWarning = useMemo(() => {
    const c = Number(formCap) || 0;
    const i = formInternalQuota ? Number(formInternalQuota) : 0;
    const e = formExternalQuota ? Number(formExternalQuota) : 0;
    if ((formInternalQuota || formExternalQuota) && i + e > c) {
      return `İç (${i}) + Dış (${e}) > Kontenjan (${c})`;
    }
    return null;
  }, [formCap, formInternalQuota, formExternalQuota]);

  if (!course) return null;

  const classYear =
    course.suggestedSemester != null && course.suggestedSemester > 0
      ? Math.ceil(course.suggestedSemester / 2)
      : null;
  const totalCapacity = sections.reduce((s, sec) => s + sec.capacity, 0);
  const totalEnrolled = sections.reduce((s, sec) => s + sec.enrolledCount, 0);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <ResizableDialogContent
        defaultWidth={820}
        defaultHeight={640}
        minWidth={600}
        minHeight={520}
        className="flex flex-col p-0 gap-0"
      >
        <DialogHeader className="px-6 pt-5 pb-3 border-b shrink-0">
          <DialogTitle className="flex items-center gap-2">
            <span className="font-mono text-slate-800">{course.courseCode}</span>
            <span className="text-gray-300">—</span>
            <span className="text-gray-800 font-normal">{course.courseName}</span>
          </DialogTitle>
        </DialogHeader>

        <div className="flex-1 overflow-y-auto px-6 py-4 space-y-4">
        {/* ───── BAĞLAM BANNER (read-only, tek satır) ───── */}
        <div className="flex items-center gap-2 px-3 py-1.5 rounded-md bg-slate-100 border border-slate-200 text-xs text-gray-700 flex-wrap">
          {course.departmentName && (
            <>
              <span className="text-gray-700 truncate">{course.departmentName}</span>
              <span className="text-gray-300">·</span>
            </>
          )}
          {classYear != null && (
            <>
              <span className="tabular-nums">{classYear}. Sınıf</span>
              <span className="text-gray-300">·</span>
            </>
          )}
          {course.credits != null && (
            <>
              <span className="tabular-nums">{course.credits} KRD</span>
              <span className="text-gray-300">·</span>
            </>
          )}
          {course.ects != null && (
            <>
              <span className="tabular-nums font-medium">{course.ects} AKTS</span>
              <span className="text-gray-300">·</span>
            </>
          )}
          {course.theoryHours != null && course.labHours != null && (
            <span className="tabular-nums">T+L: {course.theoryHours}+{course.labHours}</span>
          )}
          <span className="ml-auto text-[10px] text-gray-500 tabular-nums">
            {sections.length} şube · {totalCapacity} kont. · {totalEnrolled} kayıt
          </span>
        </div>

        {/* ───── LIST mode: Şubeler tablosu + aksiyonlar ───── */}
        {mode === 'list' && (
          <div className="space-y-3">
            <div className="rounded border border-gray-200 overflow-hidden">
              <div className="grid grid-cols-[28px_60px_1fr_70px_70px_80px_70px] gap-2 px-3 py-2 bg-slate-100 text-xs font-semibold text-gray-700 uppercase tracking-wide border-b">
                <span />
                <span className="text-center">Şube</span>
                <span>Öğretim Üyesi</span>
                <span className="text-center">Kont.</span>
                <span className="text-center">Kayıt</span>
                <span className="text-center">İç/Dış</span>
                <span className="text-center">Durum</span>
              </div>
              {sections.length === 0 ? (
                <div className="px-3 py-8 text-center text-sm text-gray-400">
                  Bu derste henüz şube açılmamış.
                </div>
              ) : (
                sections.map((sec) => (
                  <label
                    key={sec.sectionId}
                    className={cn(
                      'grid grid-cols-[28px_60px_1fr_70px_70px_80px_70px] gap-2 px-3 py-2 text-sm cursor-pointer border-b last:border-b-0 transition-colors',
                      selectedSectionId === sec.sectionId
                        ? 'bg-amber-50 hover:bg-amber-100/60'
                        : 'hover:bg-gray-50',
                      !sec.isActive && 'opacity-60',
                    )}
                  >
                    <input
                      type="radio"
                      name="section-radio"
                      checked={selectedSectionId === sec.sectionId}
                      onChange={() => setSelectedSectionId(sec.sectionId)}
                      className="h-4 w-4 mt-0.5"
                    />
                    <span className="text-center font-mono font-semibold text-slate-800">
                      {sec.sectionNumber}
                    </span>
                    <span className="text-gray-800 truncate">
                      {sec.instructorName ?? <span className="text-gray-400 italic">Atanmadı</span>}
                    </span>
                    <span className="text-center tabular-nums text-gray-700">{sec.capacity}</span>
                    <span
                      className={cn(
                        'text-center tabular-nums',
                        sec.enrolledCount > 0 ? 'text-gray-700 font-medium' : 'text-gray-400',
                      )}
                    >
                      {sec.enrolledCount}
                    </span>
                    <span className="text-center tabular-nums text-gray-500 text-xs">
                      {sec.internalQuota ?? '—'} / {sec.externalQuota ?? '—'}
                    </span>
                    <span
                      className={cn(
                        'text-center text-xs font-medium',
                        sec.isActive ? 'text-emerald-700' : 'text-gray-400',
                      )}
                    >
                      {sec.isActive ? 'Aktif' : 'Pasif'}
                    </span>
                  </label>
                ))
              )}
            </div>

            {closeError && (
              <Alert variant="destructive" className="rounded">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>
                  {(closeError as { response?: { data?: { error?: string } } })?.response?.data?.error ??
                    'Şube kapatılamadı.'}
                </AlertDescription>
              </Alert>
            )}

          </div>
        )}

        {/* ───── ADD / EDIT / DUPLICATE form — 2 kolon Proliz tarzı ───── */}
        {(mode === 'add' || mode === 'edit' || mode === 'duplicate') && (
          <div className="space-y-3">
            <div className="space-y-1.5">
              <h3 className="font-semibold text-sm text-gray-800">
                {mode === 'add' && 'Yeni Şube Ekle'}
                {mode === 'edit' && `Şube ${selectedSection?.sectionNumber} — Düzenle`}
                {mode === 'duplicate' &&
                  `Yeni Şube — Şube ${duplicateSourceNumber}'den Çoğaltılıyor`}
              </h3>
              {mode === 'duplicate' && (
                <div className="flex items-center gap-1.5 text-[11px] text-amber-700 bg-amber-50/80 border border-amber-200 px-2 py-1.5 rounded">
                  <Copy className="h-3 w-3 shrink-0" />
                  <span>
                    Şube <strong>{duplicateSourceNumber}</strong>'in kapasite, kontenjan ve hocası
                    yüklendi. İstediğin alanı değiştirip <strong>Çoğalt</strong>'a basınca yeni
                    şube oluşur.
                  </span>
                </div>
              )}
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {/* SOL KOLON: Şube alanları (editable) */}
              <div className="space-y-3">
                <div className="text-[10px] font-semibold uppercase tracking-wide text-gray-500 border-b pb-1">
                  Şube Bilgileri
                </div>

                <FormField label="Genel Toplam Kontenjan" value={formCap} onChange={setFormCap} />
                <FormField
                  label="Bölüm İçi Kontenjan"
                  value={formInternalQuota}
                  onChange={setFormInternalQuota}
                  placeholder="—"
                />
                <FormField
                  label="Bölüm Dışı Kontenjan"
                  value={formExternalQuota}
                  onChange={setFormExternalQuota}
                  placeholder="—"
                />
                {quotaWarning && (
                  <div className="text-xs text-amber-600 flex items-center gap-1">
                    <AlertCircle className="h-3 w-3 shrink-0" />
                    {quotaWarning}
                  </div>
                )}

                <div className="space-y-1.5">
                  <label className="text-xs font-medium text-gray-600">Öğretim Üyesi</label>
                  <Select
                    value={formInsId === '' ? '__none__' : formInsId}
                    onValueChange={(v) => {
                      const next = v === '__none__' ? '' : v;
                      setFormInsId(next);
                      onInstructorSelect(next ? Number(next) : null);
                    }}
                  >
                    <SelectTrigger className="h-9 text-sm">
                      <SelectValue placeholder="Hoca seçin..." />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="__none__">Hoca atanmadı</SelectItem>
                      {instructors.map((i) => (
                        <SelectItem key={i.instructorId} value={String(i.instructorId)}>
                          {i.fullName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {workloadData && <WorkloadBar workload={workloadData} />}
                </div>
              </div>

              {/* SAĞ KOLON: Ders meta (read-only — sarı arka plan, Proliz tarzı) */}
              <div className="space-y-3">
                <div className="text-[10px] font-semibold uppercase tracking-wide text-gray-500 border-b pb-1">
                  Ders Bilgileri (sadece görüntüleme)
                </div>

                <ReadonlyField label="Ders Kodu" value={course.courseCode} mono />
                <ReadonlyField label="Ders Adı" value={course.courseName} />
                <ReadonlyField
                  label="Sınıf / Yarıyıl"
                  value={
                    classYear != null
                      ? `${classYear}. Sınıf · ${course.suggestedSemester}. yarıyıl`
                      : '—'
                  }
                />
                <ReadonlyField
                  label="Kredi / AKTS"
                  value={
                    course.credits != null && course.ects != null
                      ? `${course.credits} KRD · ${course.ects} AKTS`
                      : '—'
                  }
                />
                <ReadonlyField
                  label="Teori + Lab"
                  value={
                    course.theoryHours != null && course.labHours != null
                      ? `${course.theoryHours} saat + ${course.labHours} saat`
                      : '—'
                  }
                />
                <ReadonlyField label="Sahibi" value={course.departmentName ?? '—'} />
              </div>
            </div>

            <div className="flex items-center gap-2 pt-2 border-t">
              <Button
                variant="outline"
                onClick={() => {
                  setMode('list');
                  setDuplicateSourceNumber(null);
                }}
              >
                <X className="h-4 w-4 mr-1.5" /> Vazgeç
              </Button>
              <Button onClick={handleSave} disabled={updatePending || quotaWarning != null}>
                <CheckCircle2 className="h-4 w-4 mr-1.5" />
                {updatePending
                  ? 'Kaydediliyor...'
                  : mode === 'add'
                  ? 'Ekle'
                  : mode === 'duplicate'
                  ? 'Çoğalt'
                  : 'Kaydet'}
              </Button>
            </div>
          </div>
        )}

        {/* ───── CLOSE CONFIRM ───── */}
        {mode === 'closeConfirm' && selectedSection && (
          <div className="space-y-3">
            <Alert variant="destructive" className="rounded">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>
                <strong>
                  {course.courseCode} — Şube {selectedSection.sectionNumber}
                </strong>{' '}
                kapatılacak.
                {selectedSection.draftOrPendingCount > 0 && (
                  <span> {selectedSection.draftOrPendingCount} adet Draft/Pending kayıt Dropped'a düşecek.</span>
                )}
              </AlertDescription>
            </Alert>
            <div className="flex items-center gap-2">
              <Button variant="outline" onClick={() => setMode('list')}>
                Vazgeç
              </Button>
              <Button variant="destructive" onClick={handleCloseConfirmed} disabled={closePending}>
                {closePending ? 'Kapatılıyor...' : 'Şubeyi Kapat'}
              </Button>
            </div>
          </div>
        )}

        </div>

        <div className="px-6 py-3 border-t shrink-0 flex items-center justify-between gap-3 bg-gray-50/40">
          {/* SOL: list mode aksiyon butonları */}
          {mode === 'list' ? (
            <div className="flex flex-wrap items-center gap-2">
              <Button
                variant="default"
                size="sm"
                className="h-9 gap-1.5"
                onClick={startAdd}
                disabled={readOnly}
              >
                <Plus className="h-4 w-4" />
                Yeni Şube
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="h-9 gap-1.5"
                onClick={startEdit}
                disabled={readOnly || !selectedSection || !selectedSection.isActive}
              >
                <Edit2 className="h-4 w-4" />
                Düzenle
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="h-9 gap-1.5"
                onClick={startDuplicate}
                disabled={readOnly || !selectedSection}
                title="Seçili şubenin kapasite/hoca değerleriyle yeni şube formu açar"
              >
                <Copy className="h-4 w-4" />
                Çoğalt
              </Button>
              {/* Erişim — şubeye/derse hangi bölümlerin kayıt olabileceğini yönetir */}
              <AccessPolicyPopover
                courseId={course.courseId}
                termId={termId}
                courseDepartmentId={sections[0]?.departmentId ?? null}
                courseDepartmentName={sections[0]?.departmentName ?? null}
              />
              {selectedSection && !selectedSection.isActive ? (
                <Button
                  variant="outline"
                  size="sm"
                  className="h-9 gap-1.5 text-emerald-700 border-emerald-200 hover:bg-emerald-50 hover:text-emerald-800"
                  onClick={() => handleReactivate(selectedSection.sectionId)}
                  disabled={readOnly || reactivating === selectedSection.sectionId}
                >
                  <RotateCcw className={cn('h-4 w-4', reactivating === selectedSection.sectionId && 'animate-spin')} />
                  Yeniden Aç
                </Button>
              ) : (
                <Button
                  variant="outline"
                  size="sm"
                  className="h-9 gap-1.5 text-red-600 border-red-200 hover:bg-red-50 hover:text-red-700"
                  onClick={() => setMode('closeConfirm')}
                  disabled={readOnly || !selectedSection || !selectedSection.isActive}
                >
                  <Trash2 className="h-4 w-4" />
                  Şubeyi Kapat
                </Button>
              )}
            </div>
          ) : (
            <span />
          )}

          {/* SAĞ: Kapat */}
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onOpenChange(false)}
            className="h-9 shrink-0 text-gray-600 hover:text-gray-900"
          >
            Kapat
          </Button>
        </div>
      </ResizableDialogContent>
    </Dialog>
  );
}

function FormField({
  label,
  value,
  onChange,
  placeholder,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
}) {
  return (
    <div className="space-y-1">
      <label className="text-xs font-medium text-gray-600">{label}</label>
      <Input
        type="number"
        min={0}
        max={500}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        className="h-9 text-sm"
      />
    </div>
  );
}

function ReadonlyField({
  label,
  value,
  mono,
}: {
  label: string;
  value: string;
  mono?: boolean;
}) {
  return (
    <div className="space-y-1">
      <label className="text-xs font-medium text-gray-600">{label}</label>
      <div
        className={
          'h-9 px-3 flex items-center text-sm rounded-md border border-amber-200 bg-amber-50/60 text-gray-800 truncate' +
          (mono ? ' font-mono font-semibold' : '')
        }
        title={value}
      >
        {value}
      </div>
    </div>
  );
}
