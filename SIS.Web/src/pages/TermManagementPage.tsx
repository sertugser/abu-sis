import { useState } from 'react';
import { Plus, Edit2, CheckCircle } from 'lucide-react';
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
  useAdminTerms,
  useCreateTerm,
  useUpdateTerm,
  useActivateTerm,
  useTransitionTermStatus,
  type AdminTermDto,
  type CreateTermDto,
  type UpdateTermDto,
} from '../hooks/useAdminTerms';

// ── Constants ────────────────────────────────────────────────────────────────

const SEMESTER_OPTIONS = [
  { value: 'Fall', label: 'Güz' },
  { value: 'Spring', label: 'Bahar' },
  { value: 'Summer', label: 'Yaz' },
];

const TERM_TYPE_OPTIONS = [
  { value: 'Regular', label: 'Normal' },
  { value: 'Summer', label: 'Yaz' },
  { value: 'Intensive', label: 'Tekders' },
  { value: 'Exemption', label: 'Muafiyet' },
];

const TRANSITION_LABELS: Record<string, string> = {
  Planning: 'Planlamaya Gec',
  Scheduling: 'Programlamaya Gec',
  Published: 'Yayinla',
  RegistrationOpen: 'Kayitlari Ac',
  AddDrop: 'Ders Ekle/Birak',
  WithdrawalPeriod: 'Cekilme Donemi',
  GradeEntry: 'Not Girisi',
  Closed: 'Donemi Kapat',
  Archived: 'Arsivle',
  Draft: 'Taslaga Don',
};

const TERM_STATUS_LABELS: Record<string, string> = {
  Draft: 'Taslak',
  Planning: 'Planlama',
  Scheduling: 'Programlama',
  Published: 'Yayinlandi',
  RegistrationOpen: 'Kayit Acik',
  AddDrop: 'Ekle/Birak',
  WithdrawalPeriod: 'Cekilme Donemi',
  GradeEntry: 'Not Girisi',
  Closed: 'Kapali',
  Archived: 'Arsiv',
};

const TERM_STATUS_COLORS: Record<string, string> = {
  Draft: 'bg-gray-100 text-gray-700 border-gray-300',
  Planning: 'bg-blue-100 text-blue-700 border-blue-300',
  Scheduling: 'bg-indigo-100 text-indigo-700 border-indigo-300',
  Published: 'bg-green-100 text-green-700 border-green-300',
  RegistrationOpen: 'bg-emerald-100 text-emerald-700 border-emerald-300',
  AddDrop: 'bg-yellow-100 text-yellow-700 border-yellow-300',
  WithdrawalPeriod: 'bg-orange-100 text-orange-700 border-orange-300',
  GradeEntry: 'bg-purple-100 text-purple-700 border-purple-300',
  Closed: 'bg-red-100 text-red-700 border-red-300',
  Archived: 'bg-slate-100 text-slate-700 border-slate-300',
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
  year: number;
  semester: string;
  termType: string;
  registrationStart: string;
  registrationEnd: string;
  addDropStart: string;
  addDropEnd: string;
  termStart: string;
  termEnd: string;
  withdrawalDeadline: string;
}

function emptyForm(): TermFormState {
  return {
    name: '',
    shortName: '',
    englishName: '',
    year: CURRENT_YEAR,
    semester: 'Fall',
    termType: 'Regular',
    registrationStart: '',
    registrationEnd: '',
    addDropStart: '',
    addDropEnd: '',
    termStart: '',
    termEnd: '',
    withdrawalDeadline: '',
  };
}

function termFromDto(term: AdminTermDto): TermFormState {
  return {
    name: term.name,
    shortName: term.shortName,
    englishName: term.englishName,
    year: term.year,
    semester: term.semester,
    termType: term.termType,
    registrationStart: toDateInput(term.registrationStart),
    registrationEnd: toDateInput(term.registrationEnd),
    addDropStart: toDateInput(term.addDropStart),
    addDropEnd: toDateInput(term.addDropEnd),
    termStart: toDateInput(term.termStart),
    termEnd: toDateInput(term.termEnd),
    withdrawalDeadline: toDateInput(term.withdrawalDeadline),
  };
}


// ── DateCell ─────────────────────────────────────────────────────────────────

function DateCell({ start, end, active }: { start: string; end: string; active?: boolean }) {
  return (
    <div>
      <p className={`text-sm font-medium ${active ? 'text-success' : 'text-foreground'}`}>
        {fmt(start)}
      </p>
      <p className="text-xs text-muted-foreground">{fmt(end)}</p>
    </div>
  );
}
// ── Main Page ─────────────────────────────────────────────────────────────────

export default function TermManagementPage() {
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [editingTerm, setEditingTerm] = useState<AdminTermDto | null>(null);
  const [activateTarget, setActivateTarget] = useState<AdminTermDto | null>(null);
  const [form, setForm] = useState<TermFormState>(emptyForm());
  const [formError, setFormError] = useState('');
  const [successMessage, setSuccessMessage] = useState('');
  const [transitionError, setTransitionError] = useState('');
  const [transitioningTermId, setTransitioningTermId] = useState<number | null>(null);

  const { data: terms = [], isLoading } = useAdminTerms();
  const createTerm = useCreateTerm();
  const updateTerm = useUpdateTerm();
  const activateTerm = useActivateTerm();
  const transitionTerm = useTransitionTermStatus();

  async function handleTransition(termId: number, targetStatus: string) {
    setTransitionError('');
    setTransitioningTermId(termId);
    try {
      await transitionTerm.mutateAsync({ termId, targetStatus });
      showSuccess(`Donem durumu "${TERM_STATUS_LABELS[targetStatus] ?? targetStatus}" olarak guncellendi.`);
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { error?: string; message?: string; title?: string } } };
      const msg = axiosErr?.response?.data?.error
        ?? axiosErr?.response?.data?.message
        ?? axiosErr?.response?.data?.title
        ?? (err instanceof Error ? err.message : 'Gecis basarisiz.');
      setTransitionError(msg);
    } finally {
      setTransitioningTermId(null);
    }
  }

  const sortedTerms = [...terms].sort((a, b) => b.year - a.year || b.semester.localeCompare(a.semester));

  function openCreate() { setForm(emptyForm()); setFormError(''); setCreateModalOpen(true); }
  function openEdit(term: AdminTermDto) { setForm(termFromDto(term)); setFormError(''); setEditingTerm(term); }

  function validateForm(): string | null {
    if (!form.name.trim()) return 'Dönem adı zorunludur.';
    if (!form.registrationStart || !form.registrationEnd) return 'Kayıt tarihleri zorunludur.';
    if (!form.addDropStart || !form.addDropEnd) return 'Ders ekleme/bırakma tarihleri zorunludur.';
    if (!form.termStart || !form.termEnd) return 'Dönem başlangıç/bitiş tarihleri zorunludur.';
    if (!form.withdrawalDeadline) return 'Ders çekilme son tarihi zorunludur.';
    return null;
  }

  async function handleCreate() {
    const error = validateForm();
    if (error) { setFormError(error); return; }
    const dto: CreateTermDto = {
      name: form.name.trim(),
      shortName: form.shortName.trim(),
      englishName: form.englishName.trim(),
      year: form.year,
      semester: form.semester,
      termType: form.termType,
      registrationStart: toIsoString(form.registrationStart),
      registrationEnd: toIsoString(form.registrationEnd),
      addDropStart: toIsoString(form.addDropStart),
      addDropEnd: toIsoString(form.addDropEnd),
      termStart: toIsoString(form.termStart),
      termEnd: toIsoString(form.termEnd),
      withdrawalDeadline: toIsoString(form.withdrawalDeadline),
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
      termType: form.termType,
      registrationStart: toIsoString(form.registrationStart),
      registrationEnd: toIsoString(form.registrationEnd),
      addDropStart: toIsoString(form.addDropStart),
      addDropEnd: toIsoString(form.addDropEnd),
      termStart: toIsoString(form.termStart),
      termEnd: toIsoString(form.termEnd),
      withdrawalDeadline: toIsoString(form.withdrawalDeadline),
    };
    try {
      await updateTerm.mutateAsync({ id: editingTerm.id, dto });
      setEditingTerm(null);
      showSuccess('Dönem güncellendi.');
    } catch (err: unknown) {
      setFormError(err instanceof Error ? err.message : 'Dönem güncellenemedi.');
    }
  }

  async function handleActivate() {
    if (!activateTarget) return;
    try {
      await activateTerm.mutateAsync(activateTarget.id);
      setActivateTarget(null);
      showSuccess(`"${activateTarget.name}" aktif dönem olarak ayarlandı.`);
    } catch {
      setActivateTarget(null);
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

        {transitionError && (
          <div className="mx-4 mt-3 p-3 bg-destructive/10 border border-destructive/30 rounded-lg text-sm text-destructive flex items-start gap-2">
            <span className="shrink-0 font-semibold">Hata:</span>
            <span>{transitionError}</span>
            <button onClick={() => setTransitionError('')} className="ml-auto shrink-0 text-destructive hover:text-destructive/80 font-bold">&times;</button>
          </div>
        )}

        <table className="w-full text-sm">
          <thead className="bg-muted/20 border-b border-border">
            <tr>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Dönem</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Durum</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ders Kaydı</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ekle / Bırak</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Dönem Tarihi</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Çekilme Son</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Geçiş</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading ? (
              <tr>
                <td colSpan={8} className="px-4 py-10 text-center text-muted-foreground">Yükleniyor...</td>
              </tr>
            ) : sortedTerms.length === 0 ? (
              <tr>
                <td colSpan={8} className="px-4 py-10 text-center text-muted-foreground">Henüz dönem oluşturulmamış.</td>
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

                  {/* Kayıt */}
                  <td className="px-4 py-3">
                    <DateCell start={term.registrationStart} end={term.registrationEnd} active={term.isRegistrationOpen} />
                  </td>

                  {/* Ekle/Bırak */}
                  <td className="px-4 py-3">
                    <DateCell start={term.addDropStart} end={term.addDropEnd} active={term.isAddDropOpen} />
                  </td>

                  {/* Dönem Tarihi */}
                  <td className="px-4 py-3">
                    <DateCell start={term.termStart} end={term.termEnd} />
                  </td>

                  {/* Çekilme Son Tarihi */}
                  <td className="px-4 py-3">
                    <p className="text-sm font-medium text-foreground">
                      {fmt(term.withdrawalDeadline)}
                    </p>
                  </td>

                  {/* Geçiş */}
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap gap-1">
                      {(term.allowedTransitions ?? []).length === 0 ? (
                        <span className="text-xs text-muted-foreground">—</span>
                      ) : (
                        term.allowedTransitions.map(target => (
                          <Button
                            key={target}
                            variant="outline"
                            size="sm"
                            className="text-xs h-7 px-2"
                            disabled={transitioningTermId === term.id}
                            onClick={() => handleTransition(term.id, target)}
                          >
                            {transitioningTermId === term.id ? '...' : (TRANSITION_LABELS[target] ?? target)}
                          </Button>
                        ))
                      )}
                    </div>
                  </td>

                  {/* İşlemler */}
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1 justify-end">
                      {!term.isActive && (
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setActivateTarget(term)}
                          className="text-primary hover:text-primary hover:bg-primary/8 text-xs gap-1"
                        >
                          <CheckCircle className="h-3.5 w-3.5" />
                          Aktif Yap
                        </Button>
                      )}
                      <Button variant="ghost" size="sm" onClick={() => openEdit(term)}>
                        <Edit2 className="h-3.5 w-3.5" />
                      </Button>
                    </div>
                  </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Activate Confirmation */}
      <Dialog open={!!activateTarget} onOpenChange={open => !open && setActivateTarget(null)}>
        <DialogContent className="max-w-sm">
          <DialogHeader><DialogTitle>Dönemi Aktif Yap</DialogTitle></DialogHeader>
          <p className="text-sm text-muted-foreground py-2">
            <strong className="text-foreground">{activateTarget?.name}</strong> aktif dönem olarak ayarlanacak.
            Mevcut aktif dönem otomatik olarak pasife alınacak.
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setActivateTarget(null)}>İptal</Button>
            <Button onClick={handleActivate} disabled={activateTerm.isPending}>
              {activateTerm.isPending ? 'Ayarlanıyor...' : 'Aktif Yap'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Create Modal */}
      <Dialog open={createModalOpen} onOpenChange={setCreateModalOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader><DialogTitle>Yeni Dönem Oluştur</DialogTitle></DialogHeader>
          <TermForm form={form} setForm={setForm} showYearSemester formError={formError} />
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
          <TermForm form={form} setForm={setForm} showYearSemester={false} formError={formError} />
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
  form, setForm, showYearSemester, formError,
}: {
  form: TermFormState;
  setForm: React.Dispatch<React.SetStateAction<TermFormState>>;
  showYearSemester: boolean;
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

      {showYearSemester ? (
        <div className="grid grid-cols-3 gap-3">
          <div className="space-y-1">
            <label className="text-sm font-medium">Yıl *</label>
            <Input type="number" value={form.year} onChange={e => setForm(f => ({ ...f, year: Number(e.target.value) }))} min={2000} max={2100} />
          </div>
          <div className="space-y-1">
            <label className="text-sm font-medium">Dönem *</label>
            <select
              value={form.semester}
              onChange={e => setForm(f => ({ ...f, semester: e.target.value }))}
              className="w-full border border-border rounded-lg px-3 py-2 text-sm bg-background text-foreground"
            >
              {SEMESTER_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </div>
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

      <div className="border-t border-border pt-3 space-y-2">
        <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">Kayıt Tarihleri</p>
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1">
            <label className="text-sm font-medium">Başlangıç *</label>
            <Input type="date" value={form.registrationStart} onChange={e => setForm(f => ({ ...f, registrationStart: e.target.value }))} />
          </div>
          <div className="space-y-1">
            <label className="text-sm font-medium">Bitiş *</label>
            <Input type="date" value={form.registrationEnd} onChange={e => setForm(f => ({ ...f, registrationEnd: e.target.value }))} />
          </div>
        </div>
      </div>

      <div className="border-t border-border pt-3 space-y-2">
        <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">Ders Ekle / Bırak Tarihleri</p>
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1">
            <label className="text-sm font-medium">Başlangıç *</label>
            <Input type="date" value={form.addDropStart} onChange={e => setForm(f => ({ ...f, addDropStart: e.target.value }))} />
          </div>
          <div className="space-y-1">
            <label className="text-sm font-medium">Bitiş *</label>
            <Input type="date" value={form.addDropEnd} onChange={e => setForm(f => ({ ...f, addDropEnd: e.target.value }))} />
          </div>
        </div>
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

      <div className="space-y-1">
        <label className="text-sm font-medium">Dersten Çekilme Son Tarihi *</label>
        <Input
          type="date"
          value={form.withdrawalDeadline}
          onChange={e => setForm(f => ({ ...f, withdrawalDeadline: e.target.value }))}
        />
      </div>

      {formError && <p className="text-sm text-destructive">{formError}</p>}
    </div>
  );
}
