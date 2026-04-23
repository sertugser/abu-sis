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
  };
}


// ── Main Page ─────────────────────────────────────────────────────────────────

export default function TermManagementPage() {
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [editingTerm, setEditingTerm] = useState<AdminTermDto | null>(null);
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
    if (!form.name.trim()) return 'Dönem adı zorunludur.';
    if (!form.shortEnglishName.trim()) return 'Kısa İngilizce ad zorunludur.';
    if (!form.termStart || !form.termEnd) return 'Dönem başlangıç/bitiş tarihleri zorunludur.';
    return null;
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
    };
    try {
      await createTerm.mutateAsync(dto);
      setCreateModalOpen(false);
      showSuccess('Dönem oluşturuldu.');
    } catch (err: unknown) {
      setFormError(err instanceof Error ? err.message : 'Dönem oluşturulamadı.');
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
    };
    try {
      await updateTerm.mutateAsync({ id: editingTerm.id, dto });
      setEditingTerm(null);
      showSuccess('Dönem güncellendi.');
    } catch (err: unknown) {
      setFormError(err instanceof Error ? err.message : 'Dönem güncellenemedi.');
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
          <div>
            <h1 className="text-base font-semibold">Akademik Dönem Tanımları</h1>
            <p className="text-sm text-muted-foreground">
              {isLoading ? '' : `${sortedTerms.length} dönem`}
            </p>
          </div>
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
                className={`transition-colors hover:bg-muted/20 ${term.isActive ? 'border-l-4 border-l-[var(--color-success)]' : 'border-l-4 border-l-transparent'}`}
              >
                  {/* Ad */}
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <div>
                        <p className="font-semibold text-foreground">{term.name}</p>
                        <p className="text-xs text-muted-foreground">
                          {term.shortName} · {term.termTypeLabel}
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
                  <td className="px-4 py-3">
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
            Faz tarihleri ve override ayarları için →{' '}
            <Link to={ROUTES.ACADEMIC_CALENDAR_ADMIN} className="underline text-blue-600">
              Akademik Takvim Yönetimi
            </Link>
          </p>
        </div>
      </div>

      {/* Cancel Confirmation Dialog */}
      <AlertDialog open={!!confirmCancelTarget} onOpenChange={(open) => !open && setConfirmCancelTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Dönemi İptal Et</AlertDialogTitle>
            <AlertDialogDescription>
              <strong>{confirmCancelTarget?.name}</strong> dönemini iptal etmek istediğinize emin misiniz?
              Bu işlem geri alınabilir, ancak aktif kayıtları etkileyebilir.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Vazgeç</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleConfirmCancel}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              İptal Et
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

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

      {formError && <p className="text-sm text-destructive">{formError}</p>}
    </div>
  );
}
