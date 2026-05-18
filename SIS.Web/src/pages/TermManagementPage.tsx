import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Plus, Edit2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
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
import { DelayedConfirmDialog } from '@/components/ui/delayed-confirm-dialog';
import {
  useAdminTerms,
  useCreateTerm,
  useUpdateTerm,
  useCancelTerm,
  useUncancelTerm,
  type AdminTermDto,
  type CreateTermDto,
  type UpdateTermDto,
} from '../hooks/useAdminTerms';
import { ROUTES } from '@/lib/constants';

// ── Constants ────────────────────────────────────────────────────────────────

const TERM_TYPE_OPTIONS = [
  { value: 'Fall', label: 'Güz' },
  { value: 'Spring', label: 'Bahar' },
  { value: 'Summer', label: 'Yaz' },
  { value: 'FallResit', label: 'Güz Tekders' },
  { value: 'SpringResit', label: 'Bahar Tekders' },
  { value: 'SummerResit', label: 'Yaz Tekders' },
  { value: 'SpringOther', label: 'Bahar Diğer' },
  { value: 'SummerOther', label: 'Yaz Diğer' },
  { value: 'Exemption', label: 'Muafiyet' },
] as const;

const YOKSIS_STATUS_OPTIONS = [
  { value: 'Inactive', label: 'Pasif' },
  { value: 'Active', label: 'Aktif' },
] as const;

const TERM_STATUS_LABELS: Record<string, string> = {
  Inactive:  'Pasif',
  Active:    'Aktif',
  Cancelled: 'İptal',
};

const TERM_STATUS_COLORS: Record<string, string> = {
  Inactive:  'bg-slate-100 text-slate-700 border-slate-300',
  Active:    'bg-emerald-100 text-emerald-700 border-emerald-300',
  Cancelled: 'bg-red-100 text-red-700 border-red-300',
};

const CURRENT_YEAR = new Date().getFullYear();

// ── Helpers ──────────────────────────────────────────────────────────────────

function toDateInput(isoString: string) {
  if (!isoString) return '';
  return isoString.substring(0, 10);
}

function toIsoString(dateInput: string) {
  if (!dateInput) return '';
  return new Date(dateInput).toISOString();
}

function fmt(iso: string) {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('tr-TR', { day: 'numeric', month: 'short', year: 'numeric' });
}


// ── Form State ───────────────────────────────────────────────────────────────

interface TermFormState {
  name: string;
  shortName: string;
  englishName: string;
  shortEnglishName: string;
  otherName: string;
  year: number;
  termType: string;
  yoksisStatus: 'Inactive' | 'Active';
  termStart: string;
  termEnd: string;
  overrideStart: string;
  overrideEnd: string;
}

function emptyForm(): TermFormState {
  return {
    name: '',
    shortName: '',
    englishName: '',
    shortEnglishName: '',
    otherName: '',
    year: CURRENT_YEAR,
    termType: 'Fall',
    yoksisStatus: 'Inactive',
    termStart: '',
    termEnd: '',
    overrideStart: '',
    overrideEnd: '',
  };
}

function termFromDto(term: AdminTermDto): TermFormState {
  return {
    name: term.name,
    shortName: term.shortName,
    englishName: term.englishName,
    shortEnglishName: term.shortEnglishName,
    otherName: term.otherName ?? '',
    year: term.year,
    termType: term.termType,
    yoksisStatus: term.yoksisStatus,
    termStart: toDateInput(term.termStart),
    termEnd: toDateInput(term.termEnd),
    overrideStart: toDateInput(term.overrideStart ?? ''),
    overrideEnd: toDateInput(term.overrideEnd ?? ''),
  };
}


// ── Main Page ─────────────────────────────────────────────────────────────────

export default function TermManagementPage() {
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [editingTerm, setEditingTerm] = useState<AdminTermDto | null>(null);
  const [viewingTerm, setViewingTerm] = useState<AdminTermDto | null>(null);
  const [form, setForm] = useState<TermFormState>(emptyForm());
  const [formError, setFormError] = useState('');
  const [successMessage, setSuccessMessage] = useState('');
  const [actionError, setActionError] = useState('');
  const [confirmCancelTarget, setConfirmCancelTarget] = useState<AdminTermDto | null>(null);

  const { data: terms = [], isLoading } = useAdminTerms();
  const createTerm = useCreateTerm();
  const updateTerm = useUpdateTerm();
  const cancelTerm = useCancelTerm();
  const uncancelTerm = useUncancelTerm();

  const sortedTerms = [...terms].sort((a, b) => b.year - a.year || a.termType.localeCompare(b.termType));

  function openCreate() { setForm(emptyForm()); setFormError(''); setCreateModalOpen(true); }
  function openEdit(term: AdminTermDto) { setForm(termFromDto(term)); setFormError(''); setEditingTerm(term); }

  function validateForm(): string | null {
    const name = form.name.trim();
    const shortName = form.shortName.trim();
    const shortEnglishName = form.shortEnglishName.trim();

    if (name.length < 3) return 'Dönem adı en az 3 karakter olmalıdır.';
    if (shortEnglishName.length < 3) return 'Kısa İngilizce ad en az 3 karakter olmalıdır.';
    if (shortName && shortName.length < 3) return 'Kısa ad boş olabilir ancak girildiyse en az 3 karakter olmalıdır.';

    if (!form.termStart || !form.termEnd) return 'Dönem başlangıç/bitiş tarihleri zorunludur.';
    const start = new Date(form.termStart);
    const end = new Date(form.termEnd);
    if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return 'Geçersiz tarih.';
    if (start >= end) return 'Başlangıç tarihi bitiş tarihinden önce olmalıdır.';

    // Override validation
    if (form.overrideStart) {
      const ovStart = new Date(form.overrideStart);
      if (Number.isNaN(ovStart.getTime())) return 'Erken aktivasyon tarihi geçersiz.';
      if (ovStart >= start)
        return 'Erken aktivasyon tarihi dönem başlangıcından ÖNCE olmalı. Dönemi geciktirmek için TermStart\'ı değiştirin.';
    }
    if (form.overrideEnd) {
      const ovEnd = new Date(form.overrideEnd);
      if (Number.isNaN(ovEnd.getTime())) return 'Uzatma tarihi geçersiz.';
      if (ovEnd <= end)
        return 'Uzatma tarihi dönem bitişinden SONRA olmalı. Kısaltma için TermEnd\'i değiştirin.';
    }
    if (form.overrideStart && form.overrideEnd) {
      const ovStart = new Date(form.overrideStart);
      const ovEnd = new Date(form.overrideEnd);
      if (ovEnd <= ovStart)
        return 'Uzatma tarihi, erken aktivasyon tarihinden sonra olmalı.';
    }

    return null;
  }

  function extractApiError(err: unknown, fallback: string): string {
    const axiosErr = err as { response?: { data?: { error?: string; message?: string; title?: string } } };
    return (
      axiosErr?.response?.data?.error
      ?? axiosErr?.response?.data?.message
      ?? axiosErr?.response?.data?.title
      ?? (err instanceof Error ? err.message : fallback)
    );
  }

  async function handleCreate() {
    const error = validateForm();
    if (error) { setFormError(error); return; }
    const dto: CreateTermDto = {
      name: form.name.trim(),
      shortName: form.shortName.trim(),
      englishName: form.englishName.trim(),
      shortEnglishName: form.shortEnglishName.trim(),
      otherName: form.otherName.trim() || null,
      year: form.year,
      termType: form.termType as CreateTermDto['termType'],
      yoksisStatus: form.yoksisStatus,
      termStart: toIsoString(form.termStart),
      termEnd: toIsoString(form.termEnd),
      overrideStart: form.overrideStart ? toIsoString(form.overrideStart) : null,
      overrideEnd: form.overrideEnd ? toIsoString(form.overrideEnd) : null,
    };
    try {
      await createTerm.mutateAsync(dto);
      setCreateModalOpen(false);
      showSuccess('Dönem oluşturuldu.');
    } catch (err: unknown) {
      setFormError(extractApiError(err, 'Dönem oluşturulamadı.'));
    }
  }

  async function handleUpdate() {
    if (!editingTerm) return;
    const error = validateForm();
    if (error) { setFormError(error); return; }
    const dto: UpdateTermDto = {
      name: form.name.trim(),
      shortName: form.shortName.trim(),
      englishName: form.englishName.trim(),
      shortEnglishName: form.shortEnglishName.trim(),
      otherName: form.otherName.trim() || null,
      termType: form.termType as UpdateTermDto['termType'],
      yoksisStatus: form.yoksisStatus,
      termStart: toIsoString(form.termStart),
      termEnd: toIsoString(form.termEnd),
      overrideStart: form.overrideStart ? toIsoString(form.overrideStart) : null,
      overrideEnd: form.overrideEnd ? toIsoString(form.overrideEnd) : null,
    };
    try {
      await updateTerm.mutateAsync({ id: editingTerm.id, dto });
      setEditingTerm(null);
      showSuccess('Dönem güncellendi.');
    } catch (err: unknown) {
      setFormError(extractApiError(err, 'Dönem güncellenemedi.'));
    }
  }

  async function handleConfirmCancel() {
    if (!confirmCancelTarget) return;
    const termId = confirmCancelTarget.id;
    setConfirmCancelTarget(null);
    try {
      await cancelTerm.mutateAsync(termId);
      showSuccess('Dönem iptal edildi.');
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { error?: string; message?: string; title?: string } } };
      const msg = axiosErr?.response?.data?.error
        ?? axiosErr?.response?.data?.message
        ?? axiosErr?.response?.data?.title
        ?? (err instanceof Error ? err.message : 'İptal işlemi başarısız.');
      setActionError(msg);
    }
  }

  async function handleUncancel(termId: number) {
    try {
      await uncancelTerm.mutateAsync(termId);
      showSuccess('Dönem iptali geri alındı.');
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { error?: string; message?: string; title?: string } } };
      const msg = axiosErr?.response?.data?.error
        ?? axiosErr?.response?.data?.message
        ?? axiosErr?.response?.data?.title
        ?? (err instanceof Error ? err.message : 'İptal geri alma başarısız.');
      setActionError(msg);
    }
  }

  function showSuccess(msg: string) {
    setSuccessMessage(msg);
    setTimeout(() => setSuccessMessage(''), 3000);
  }

  return (
    <div className="space-y-5">
      {/* Table */}
      <div className="bg-background border border-border rounded-xl overflow-hidden">
        {/* Toolbar */}
        <div className="flex items-center justify-between px-4 py-3 border-b border-border bg-muted/40">
          <p className="text-sm text-muted-foreground">
            {isLoading ? '' : `${sortedTerms.length} dönem`}
          </p>
          <div className="flex items-center gap-3">
            {successMessage && (
              <span className="text-sm font-medium text-success">{successMessage}</span>
            )}
            <Button size="sm" onClick={openCreate}>
              <Plus className="h-4 w-4 mr-1" />
              Yeni Dönem Oluştur
            </Button>
          </div>
        </div>

        {actionError && (
          <div className="mx-4 mt-3 p-3 bg-destructive/10 border border-destructive/30 rounded-lg text-sm text-destructive flex items-start gap-2">
            <span className="shrink-0 font-semibold">Hata:</span>
            <span>{actionError}</span>
            <button onClick={() => setActionError('')} className="ml-auto shrink-0 text-destructive hover:text-destructive/80 font-bold">&times;</button>
          </div>
        )}

        <table className="w-full text-sm">
          <thead className="bg-muted/20 border-b border-border">
            <tr>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Dönem</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Durum</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">YÖKSİS</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Dönem Tarihi</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Akademik Takvim</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading ? (
              <tr>
                <td colSpan={6} className="px-4 py-10 text-center text-muted-foreground">Yükleniyor...</td>
              </tr>
            ) : sortedTerms.length === 0 ? (
              <tr>
                <td colSpan={6} className="px-4 py-10 text-center text-muted-foreground">Henüz dönem oluşturulmamış.</td>
              </tr>
            ) : sortedTerms.map(term => (
              <tr
                key={term.id}
                onDoubleClick={() => setViewingTerm(term)}
                title="Detayları görmek için çift tıklayın"
                className={`transition-colors hover:bg-muted/20 cursor-pointer select-none ${term.isActive ? 'border-l-4 border-l-[var(--color-success)]' : 'border-l-4 border-l-transparent'}`}
              >
                  {/* Ad */}
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <div>
                        <p className="font-semibold text-foreground">{term.name}</p>
                        <p className="text-xs text-muted-foreground">
                          {term.year} · {term.termTypeLabel}
                        </p>
                      </div>
                      {term.isActive && (
                        <Badge variant="success" className="text-xs shrink-0">Aktif</Badge>
                      )}
                    </div>
                  </td>

                  {/* Durum */}
                  <td className="px-4 py-3">
                    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold border ${TERM_STATUS_COLORS[term.status] ?? 'bg-gray-100 text-gray-700 border-gray-300'}`}>
                      {term.statusLabel || TERM_STATUS_LABELS[term.status] || term.status}
                    </span>
                  </td>

                  {/* YÖKSİS */}
                  <td className="px-4 py-3">
                    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium border ${term.yoksisStatus === 'Active' ? 'bg-blue-50 text-blue-700 border-blue-200' : 'bg-slate-50 text-slate-500 border-slate-200'}`}>
                      {term.yoksisStatus === 'Active' ? 'Aktif' : 'Pasif'}
                    </span>
                  </td>

                  {/* Dönem Tarihi */}
                  <td className="px-4 py-3">
                    <div>
                      <p className="text-sm font-medium text-foreground">{fmt(term.termStart)}</p>
                      <p className="text-xs text-muted-foreground">{fmt(term.termEnd)}</p>
                    </div>
                  </td>

                  {/* Akademik Takvim */}
                  <td className="px-4 py-3">
                    {term.hasAcademicCalendar
                      ? <Badge className="bg-emerald-100 text-emerald-700">Takvim Tanımlı</Badge>
                      : <Badge className="bg-amber-100 text-amber-700">Takvim Eksik</Badge>}
                  </td>

                  {/* İşlemler */}
                  <td className="px-4 py-3" onDoubleClick={e => e.stopPropagation()}>
                    <div className="flex items-center gap-1 justify-end">
                      <Button variant="ghost" size="sm" onClick={() => openEdit(term)}>
                        <Edit2 className="h-3.5 w-3.5" />
                      </Button>
                      {term.status === 'Cancelled' ? (
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={uncancelTerm.isPending}
                          onClick={() => handleUncancel(term.id)}
                        >
                          İptali Geri Al
                        </Button>
                      ) : (
                        <Button
                          size="sm"
                          variant="destructive"
                          disabled={cancelTerm.isPending}
                          onClick={() => setConfirmCancelTarget(term)}
                        >
                          İptal Et
                        </Button>
                      )}
                    </div>
                  </td>
              </tr>
            ))}
          </tbody>
        </table>

        <div className="px-4 py-3 border-t border-border bg-muted/10">
          <p className="text-sm text-muted-foreground italic">
            Akademik takvim üretimi için →{' '}
            <Link to={ROUTES.ACADEMIC_CALENDAR_GENERATOR} className="underline text-blue-600">
              Takvim Oluşturucu
            </Link>
          </p>
        </div>
      </div>

      {/* Cancel Confirmation Dialog — yıkıcı (cascade: section'lar Cancelled olur), 5sn delay. */}
      <DelayedConfirmDialog
        open={!!confirmCancelTarget}
        onOpenChange={(open) => !open && setConfirmCancelTarget(null)}
        title={`Dönemi iptal et: ${confirmCancelTarget?.name ?? ''}`}
        description="Bu dönemin tüm section'ları otomatik olarak 'Cancelled' durumuna geçecek. Aktif kayıtlar, atamalar ve programlar etkilenecek."
        footnote="İptal sonradan geri alınabilir (Uncancel), ancak aktif öğrenci kayıtları/atamalar bu süre içinde dondurulur."
        confirmLabel="Dönemi iptal et"
        delaySeconds={5}
        severity="danger"
        onConfirm={handleConfirmCancel}
      />

      {/* Create Modal */}
      <Dialog open={createModalOpen} onOpenChange={setCreateModalOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader><DialogTitle>Yeni Dönem Oluştur</DialogTitle></DialogHeader>
          <TermForm form={form} setForm={setForm} showYearTermType formError={formError} />
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateModalOpen(false)}>İptal</Button>
            <Button onClick={handleCreate} disabled={createTerm.isPending}>
              {createTerm.isPending ? 'Oluşturuluyor...' : 'Oluştur'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Edit Modal */}
      <Dialog open={!!editingTerm} onOpenChange={open => !open && setEditingTerm(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader><DialogTitle>Düzenle — {editingTerm?.name}</DialogTitle></DialogHeader>
          <TermForm form={form} setForm={setForm} showYearTermType={false} formError={formError} />
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditingTerm(null)}>İptal</Button>
            <Button onClick={handleUpdate} disabled={updateTerm.isPending}>
              {updateTerm.isPending ? 'Kaydediliyor...' : 'Kaydet'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Detay Modal (çift tık ile açılır) */}
      <Dialog open={!!viewingTerm} onOpenChange={open => !open && setViewingTerm(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Dönem Detayları — {viewingTerm?.name}</DialogTitle>
          </DialogHeader>
          {viewingTerm && (
            <TermDetailView
              term={viewingTerm}
              onEdit={() => {
                const t = viewingTerm;
                setViewingTerm(null);
                openEdit(t);
              }}
            />
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setViewingTerm(null)}>Kapat</Button>
            <Button
              onClick={() => {
                const t = viewingTerm;
                setViewingTerm(null);
                if (t) openEdit(t);
              }}
            >
              Düzenle
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

// ── Term Detail View ─────────────────────────────────────────────────────────

function TermDetailView({ term, onEdit }: { term: AdminTermDto; onEdit: () => void }) {
  const today = new Date();
  const start = new Date(term.termStart);
  const end = new Date(term.termEnd);
  const withinDates = today >= start && today <= end;

  const hasOverrideStart = !!term.overrideStart;
  const hasOverrideEnd = !!term.overrideEnd;

  // "Neden Aktif değil?" açıklaması — kullanıcı sık karşılaştığı için görünür yap
  const inactiveReason =
    term.status === 'Active' ? null
    : term.isCancelled ? 'Bu dönem iptal edilmiş. Aktif olabilmesi için önce iptal geri alınmalı.'
    : !withinDates ? `Bugün (${fmt(today.toISOString())}) dönem tarih aralığında değil. Aktif olabilmesi için bugünün ${fmt(term.effectiveStart)} ve ${fmt(term.effectiveEnd)} arasında olması gerekir.`
    : null;

  return (
    <div className="space-y-3 py-2 text-sm">
      <DetailRow label="Ad" value={term.name} />
      <DetailRow label="Kısa Ad" value={term.shortName || '—'} />
      <DetailRow label="İngilizce Ad" value={term.englishName || '—'} />
      <DetailRow label="Kısa İngilizce Ad" value={term.shortEnglishName || '—'} />
      <DetailRow label="Diğer Ad" value={term.otherName || '—'} />
      <DetailRow label="Yıl" value={String(term.year)} />
      <DetailRow label="Tür" value={term.termTypeLabel} />
      <DetailRow label="YÖKSİS Durumu" value={term.yoksisStatus === 'Active' ? 'Aktif' : 'Pasif'} />
      <DetailRow
        label="Durum"
        value={
          <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold border ${
            term.status === 'Active' ? 'bg-emerald-100 text-emerald-700 border-emerald-300' :
            term.status === 'Cancelled' ? 'bg-red-100 text-red-700 border-red-300' :
            'bg-slate-100 text-slate-700 border-slate-300'
          }`}>
            {term.statusLabel || term.status}
          </span>
        }
      />
      <DetailRow label="Başlangıç" value={fmt(term.termStart)} />
      <DetailRow label="Bitiş" value={fmt(term.termEnd)} />

      {(hasOverrideStart || hasOverrideEnd) && (
        <>
          {hasOverrideStart && (
            <DetailRow
              label="Erken Aktivasyon"
              value={
                <span className="inline-flex items-center gap-1.5">
                  <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold border bg-sky-100 text-sky-700 border-sky-300">
                    🔵 {fmt(term.overrideStart!)}
                  </span>
                  <span className="text-xs text-muted-foreground">(dönem başlamadan aktif edildi)</span>
                </span>
              }
            />
          )}
          {hasOverrideEnd && (
            <DetailRow
              label="Uzatma"
              value={
                <span className="inline-flex items-center gap-1.5">
                  <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold border bg-amber-100 text-amber-700 border-amber-300">
                    🟡 {fmt(term.overrideEnd!)}
                  </span>
                  <span className="text-xs text-muted-foreground">(dönem sonrası uzatıldı)</span>
                </span>
              }
            />
          )}
        </>
      )}

      <DetailRow
        label="Akademik Takvim"
        value={term.hasAcademicCalendar ? 'Tanımlı' : 'Eksik'}
      />

      {inactiveReason && (
        <div className="mt-4 p-3 rounded-lg bg-amber-50 border border-amber-200 text-xs text-amber-900">
          <p className="font-semibold mb-1">Neden Aktif değil?</p>
          <p className="mb-2">{inactiveReason}</p>
          {!term.isCancelled && (
            <div className="flex items-center gap-2 pt-1 border-t border-amber-200/60">
              <Button size="sm" variant="outline" onClick={onEdit} className="h-7 text-xs">
                Tarihleri Düzenle
              </Button>
              <span className="text-[11px] text-amber-800/80">
                veya Erken Aktivasyon / Uzatma alanını doldurarak override ekleyin.
              </span>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function DetailRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="grid grid-cols-[140px_1fr] gap-3 items-center">
      <span className="text-xs font-medium text-muted-foreground">{label}</span>
      <span className="text-sm text-foreground">{value}</span>
    </div>
  );
}

// ── Term Form ─────────────────────────────────────────────────────────────────

function TermForm({
  form, setForm, showYearTermType, formError,
}: {
  form: TermFormState;
  setForm: React.Dispatch<React.SetStateAction<TermFormState>>;
  showYearTermType: boolean;
  formError: string;
}) {
  return (
    <div className="space-y-4 py-2 max-h-[65vh] overflow-y-auto pr-1">
      <div className="space-y-1">
        <label className="text-sm font-medium">Dönem Adı *</label>
        <Input value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} placeholder="örn. 2024-2025 Güz Dönemi" />
      </div>

      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1">
          <label className="text-sm font-medium">Kısa Ad</label>
          <Input value={form.shortName} onChange={e => setForm(f => ({ ...f, shortName: e.target.value }))} placeholder="örn. 24-25 Güz" />
        </div>
        <div className="space-y-1">
          <label className="text-sm font-medium">İngilizce Ad</label>
          <Input value={form.englishName} onChange={e => setForm(f => ({ ...f, englishName: e.target.value }))} placeholder="örn. Fall 2024-2025" />
        </div>
      </div>

      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1">
          <label className="text-sm font-medium">Kısa İngilizce Ad *</label>
          <Input value={form.shortEnglishName} onChange={e => setForm(f => ({ ...f, shortEnglishName: e.target.value }))} placeholder="örn. Fall 24-25" />
        </div>
        <div className="space-y-1">
          <label className="text-sm font-medium">Diğer Ad</label>
          <Input value={form.otherName} onChange={e => setForm(f => ({ ...f, otherName: e.target.value }))} placeholder="Opsiyonel" />
        </div>
      </div>

      {showYearTermType ? (
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1">
            <label className="text-sm font-medium">Yıl *</label>
            <Input type="number" value={form.year} onChange={e => setForm(f => ({ ...f, year: Number(e.target.value) }))} min={2000} max={2100} />
          </div>
          <div className="space-y-1">
            <label className="text-sm font-medium">Tür *</label>
            <select
              value={form.termType}
              onChange={e => setForm(f => ({ ...f, termType: e.target.value }))}
              className="w-full border border-border rounded-lg px-3 py-2 text-sm bg-background text-foreground"
            >
              {TERM_TYPE_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </div>
        </div>
      ) : (
        <div className="space-y-1">
          <label className="text-sm font-medium">Tür</label>
          <select
            value={form.termType}
            onChange={e => setForm(f => ({ ...f, termType: e.target.value }))}
            className="w-full border border-border rounded-lg px-3 py-2 text-sm bg-background text-foreground"
          >
            {TERM_TYPE_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
          </select>
        </div>
      )}

      <div className="space-y-1">
        <label className="text-sm font-medium">YÖKSİS Durumu</label>
        <select
          value={form.yoksisStatus}
          onChange={e => setForm(f => ({ ...f, yoksisStatus: e.target.value as 'Inactive' | 'Active' }))}
          className="w-full border border-border rounded-lg px-3 py-2 text-sm bg-background text-foreground"
        >
          {YOKSIS_STATUS_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
      </div>

      <div className="border-t border-border pt-3 space-y-2">
        <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">Dönem Tarihleri</p>
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1">
            <label className="text-sm font-medium">Başlangıç *</label>
            <Input type="date" value={form.termStart} onChange={e => setForm(f => ({ ...f, termStart: e.target.value }))} />
          </div>
          <div className="space-y-1">
            <label className="text-sm font-medium">Bitiş *</label>
            <Input type="date" value={form.termEnd} onChange={e => setForm(f => ({ ...f, termEnd: e.target.value }))} />
          </div>
        </div>
      </div>

      <div className="border-t border-border pt-3 space-y-2">
        <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
          Override Tarihleri
          <span className="ml-2 font-normal text-muted-foreground normal-case">
            (Opsiyonel — admin dönem tarihi dışında manuel kontrol için)
          </span>
        </p>
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1">
            <label className="text-sm font-medium">Erken Aktivasyon</label>
            <Input type="date" value={form.overrideStart}
              onChange={e => setForm(f => ({ ...f, overrideStart: e.target.value }))} />
            <p className="text-[11px] text-muted-foreground leading-tight">
              Dönem başlangıcından önce aktif et. TermStart'tan <strong>önce</strong> bir tarih girilmeli.
            </p>
          </div>
          <div className="space-y-1">
            <label className="text-sm font-medium">Uzatma</label>
            <Input type="date" value={form.overrideEnd}
              onChange={e => setForm(f => ({ ...f, overrideEnd: e.target.value }))} />
            <p className="text-[11px] text-muted-foreground leading-tight">
              Dönem bitişinden sonra aktif tut. TermEnd'den <strong>sonra</strong> bir tarih girilmeli.
            </p>
          </div>
        </div>
      </div>

      {formError && <p className="text-sm text-destructive">{formError}</p>}
    </div>
  );
}
