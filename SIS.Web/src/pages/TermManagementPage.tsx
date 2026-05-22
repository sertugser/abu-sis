import { useState } from 'react';
import { Plus, Edit2, MoreHorizontal, Power, PowerOff, RotateCcw, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { TablePagination } from '@/components/ui/table-pagination';
import { DelayedConfirmDialog } from '@/components/ui/delayed-confirm-dialog';
import {
  useAdminTerms,
  useCreateTerm,
  useUpdateTerm,
  useCancelTerm,
  useUncancelTerm,
  useActivateTerm,
  useDeactivateTerm,
  type AdminTermDto,
  type CreateTermDto,
  type UpdateTermDto,
} from '../hooks/useAdminTerms';

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
  // Hafif (geri-dönüşlü) durum aksiyonları için onay: kullanıma aç/kapat, iptali geri al.
  const [confirmAction, setConfirmAction] = useState<{ term: AdminTermDto; kind: 'activate' | 'deactivate' | 'uncancel' } | null>(null);

  const { data: terms = [], isLoading } = useAdminTerms();
  const createTerm = useCreateTerm();
  const updateTerm = useUpdateTerm();
  const cancelTerm = useCancelTerm();
  const uncancelTerm = useUncancelTerm();
  const activateTerm = useActivateTerm();
  const deactivateTerm = useDeactivateTerm();

  const sortedTerms = [...terms].sort((a, b) => b.year - a.year || a.termType.localeCompare(b.termType));

  // Sayfalama — varsayılan 15 dönem/sayfa.
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(15);
  const totalPages = Math.max(1, Math.ceil(sortedTerms.length / pageSize));
  const safePage = Math.min(Math.max(1, page), totalPages);
  const pagedTerms = sortedTerms.slice((safePage - 1) * pageSize, safePage * pageSize);

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

  async function handleActivate(termId: number) {
    try {
      await activateTerm.mutateAsync(termId);
      showSuccess('Dönem kullanıma açıldı.');
    } catch (err: unknown) {
      setActionError(extractApiError(err, 'Dönem kullanıma açılamadı.'));
    }
  }

  async function handleDeactivate(termId: number) {
    try {
      await deactivateTerm.mutateAsync(termId);
      showSuccess('Dönem kullanıma kapatıldı.');
    } catch (err: unknown) {
      setActionError(extractApiError(err, 'Dönem kullanıma kapatılamadı.'));
    }
  }

  function showSuccess(msg: string) {
    setSuccessMessage(msg);
    setTimeout(() => setSuccessMessage(''), 3000);
  }

  return (
    <div className="flex flex-1 flex-col min-h-0 space-y-5">
      {/* Table */}
      <div className="bg-card border border-border rounded-xl overflow-hidden flex flex-col flex-1 min-h-0">
        {/* Toolbar */}
        <div className="flex items-center justify-end px-4 py-3 border-b border-border bg-card">
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

        <div className="flex-1 min-h-0 overflow-auto">
        <table className="w-full min-w-[820px] text-sm">
          <thead className="bg-card border-b border-border">
            <tr>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground w-[28%]">Dönem</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground w-[13%]">Durum</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground w-[12%]">YÖKSİS</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground w-[16%]">Dönem Tarihi</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground w-[17%]">Akademik Takvim</th>
              <th className="px-4 py-3 text-right font-semibold text-muted-foreground">İşlemler</th>
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
            ) : pagedTerms.map(term => (
              <tr
                key={term.id}
                onDoubleClick={() => setViewingTerm(term)}
                title="Detayları görmek için çift tıklayın"
                className={`transition-colors duration-150 cursor-pointer select-none border-l-4 ${
                  term.isActive
                    ? 'bg-emerald-50/60 border-l-emerald-500 hover:bg-emerald-100/70'
                    : 'border-l-transparent hover:bg-slate-50'
                }`}
              >
                  {/* Ad */}
                  <td className="px-4 py-3">
                    <p className="font-semibold text-foreground">{term.name}</p>
                  </td>

                  {/* Durum */}
                  <td className="px-4 py-3">
                    <Badge className={`text-sm ${TERM_STATUS_COLORS[term.status] ?? 'bg-gray-100 text-gray-700 border-gray-300'}`}>
                      {term.statusLabel || TERM_STATUS_LABELS[term.status] || term.status}
                    </Badge>
                  </td>

                  {/* YÖKSİS */}
                  <td className="px-4 py-3">
                    <Badge className={`text-sm ${term.yoksisStatus === 'Active' ? 'bg-blue-50 text-blue-700 border-blue-200' : 'bg-slate-50 text-slate-500 border-slate-200'}`}>
                      {term.yoksisStatus === 'Active' ? 'Aktif' : 'Pasif'}
                    </Badge>
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
                      ? <Badge className="text-sm bg-emerald-100 text-emerald-700 border-emerald-300">Takvim Tanımlı</Badge>
                      : <Badge className="text-sm bg-amber-100 text-amber-700 border-amber-300">Takvim Eksik</Badge>}
                  </td>

                  {/* İşlemler */}
                  <td className="px-4 py-3" onDoubleClick={e => e.stopPropagation()}>
                    <div className="flex items-center gap-1 justify-end">
                      <Button variant="ghost" size="icon" className="h-8 w-8" title="Düzenle" onClick={() => openEdit(term)}>
                        <Edit2 className="h-4 w-4" />
                      </Button>
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon" className="h-8 w-8" title="İşlemler">
                            <MoreHorizontal className="h-4 w-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end" className="w-48">
                          {!term.isCancelled && (
                            term.isActivated ? (
                              <DropdownMenuItem disabled={deactivateTerm.isPending} onClick={() => setConfirmAction({ term, kind: 'deactivate' })}>
                                <PowerOff className="h-4 w-4 mr-2" /> Kullanıma Kapat
                              </DropdownMenuItem>
                            ) : (
                              <DropdownMenuItem disabled={activateTerm.isPending} onClick={() => setConfirmAction({ term, kind: 'activate' })}>
                                <Power className="h-4 w-4 mr-2" /> Kullanıma Aç
                              </DropdownMenuItem>
                            )
                          )}
                          {term.status === 'Cancelled' ? (
                            <DropdownMenuItem disabled={uncancelTerm.isPending} onClick={() => setConfirmAction({ term, kind: 'uncancel' })}>
                              <RotateCcw className="h-4 w-4 mr-2" /> İptali Geri Al
                            </DropdownMenuItem>
                          ) : (
                            <DropdownMenuItem
                              disabled={cancelTerm.isPending}
                              onClick={() => setConfirmCancelTarget(term)}
                              className="text-red-600 focus:text-red-600"
                            >
                              <Trash2 className="h-4 w-4 mr-2" /> İptal Et
                            </DropdownMenuItem>
                          )}
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </div>
                  </td>
              </tr>
            ))}
          </tbody>
        </table>
        </div>

        {!isLoading && sortedTerms.length > 0 && (
          <TablePagination
            totalCount={sortedTerms.length}
            page={safePage}
            pageSize={pageSize}
            onPageChange={setPage}
            onPageSizeChange={(size) => { setPageSize(size); setPage(1); }}
            itemLabel="dönem"
            pageSizeOptions={[15, 30, 50]}
            size="md"
          />
        )}

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

      {/* Hafif durum aksiyonları onayı — kullanıma aç/kapat, iptali geri al (geri sayımsız). */}
      {(() => {
        const cfg = confirmAction && {
          deactivate: {
            title: 'Dönemi kullanıma kapat',
            description: (
              <><b>{confirmAction.term.name}</b> geçici olarak kullanıma kapatılacak (pasif olacak). Bu döneme ait <b>section/derslere dokunulmaz</b>; istediğin zaman tekrar açabilirsin.</>
            ),
            confirmLabel: 'Evet, kullanıma kapat',
            run: () => handleDeactivate(confirmAction.term.id),
          },
          activate: {
            title: 'Dönemi kullanıma aç',
            description: (
              <><b>{confirmAction.term.name}</b> yeniden kullanıma açılacak; aktif/kullanılabilir hale gelecek.</>
            ),
            confirmLabel: 'Evet, kullanıma aç',
            run: () => handleActivate(confirmAction.term.id),
          },
          uncancel: {
            title: 'İptali geri al',
            description: (
              <><b>{confirmAction.term.name}</b> döneminin iptali kaldırılacak; dönem yeniden kullanılabilir hale gelecek.</>
            ),
            confirmLabel: 'Evet, iptali geri al',
            run: () => handleUncancel(confirmAction.term.id),
          },
        }[confirmAction.kind];
        return (
          <DelayedConfirmDialog
            open={!!confirmAction}
            onOpenChange={(open) => !open && setConfirmAction(null)}
            title={cfg?.title ?? ''}
            description={cfg?.description}
            footnote="Bu işlem geri alınabilir."
            confirmLabel={cfg?.confirmLabel ?? 'Onayla'}
            delaySeconds={0}
            severity="warning"
            onConfirm={() => { cfg?.run(); setConfirmAction(null); }}
          />
        );
      })()}

      {/* Create Modal */}
      <Dialog open={createModalOpen} onOpenChange={setCreateModalOpen}>
        <ResizableDialogContent defaultWidth={560} minWidth={460} minHeight={420} className="flex flex-col p-0 gap-0">
          <DialogHeader className="px-6 pt-6 pb-3 shrink-0">
            <DialogTitle>Yeni Dönem Oluştur</DialogTitle>
            <DialogDescription className="text-xs">
              Dönemin kimlik bilgilerini ve genel başlangıç/bitiş tarihlerini girin.
            </DialogDescription>
          </DialogHeader>
          <div className="flex-1 overflow-y-auto px-6 py-2">
            <TermForm form={form} setForm={setForm} showYearTermType formError={formError} />
          </div>
          <DialogFooter className="shrink-0 px-6 py-4 border-t gap-2">
            <Button variant="outline" onClick={() => setCreateModalOpen(false)}>İptal</Button>
            <Button onClick={handleCreate} disabled={createTerm.isPending}>
              {createTerm.isPending ? 'Oluşturuluyor...' : 'Oluştur'}
            </Button>
          </DialogFooter>
        </ResizableDialogContent>
      </Dialog>

      {/* Edit Modal */}
      <Dialog open={!!editingTerm} onOpenChange={open => !open && setEditingTerm(null)}>
        <ResizableDialogContent defaultWidth={560} minWidth={460} minHeight={420} className="flex flex-col p-0 gap-0">
          <DialogHeader className="px-6 pt-6 pb-3 shrink-0">
            <DialogTitle>Düzenle — {editingTerm?.name}</DialogTitle>
            <DialogDescription className="text-xs">
              Dönemin kimlik bilgilerini ve genel başlangıç/bitiş tarihlerini düzenleyin.
            </DialogDescription>
          </DialogHeader>
          <div className="flex-1 overflow-y-auto px-6 py-2">
            <TermForm form={form} setForm={setForm} showYearTermType={false} formError={formError} />
          </div>
          <DialogFooter className="shrink-0 px-6 py-4 border-t gap-2">
            <Button variant="outline" onClick={() => setEditingTerm(null)}>İptal</Button>
            <Button onClick={handleUpdate} disabled={updateTerm.isPending}>
              {updateTerm.isPending ? 'Kaydediliyor...' : 'Kaydet'}
            </Button>
          </DialogFooter>
        </ResizableDialogContent>
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
  const effectiveStart = new Date(term.effectiveStart);
  const effectiveEnd = new Date(term.effectiveEnd);
  const withinEffectiveDates = today >= effectiveStart && today <= effectiveEnd;

  const hasOverrideStart = !!term.overrideStart;
  const hasOverrideEnd = !!term.overrideEnd;

  // "Neden Aktif değil?" açıklaması — kullanıcı sık karşılaştığı için görünür yap
  const inactiveReason =
    term.status === 'Active' ? null
    : term.isCancelled ? 'Bu dönem iptal edilmiş. Aktif olabilmesi için önce iptal geri alınmalı.'
    : !term.isActivated ? 'Bu dönem kullanıma kapalı. Aktif dönem olarak kullanılabilmesi için önce kullanıma açılmalı.'
    : !withinEffectiveDates ? `Bugün (${fmt(today.toISOString())}) dönem tarih aralığında değil. Aktif olabilmesi için bugünün ${fmt(term.effectiveStart)} ve ${fmt(term.effectiveEnd)} arasında olması gerekir.`
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
      <DetailRow label="Kullanım" value={term.isActivated ? 'Kullanıma açık' : 'Kullanıma kapalı'} />
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
    <div className="space-y-4">
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

      <div className="grid grid-cols-3 gap-3">
        <div className="space-y-1">
          <label className="text-sm font-medium">Yıl *</label>
          <Input
            type="number"
            value={form.year}
            disabled={!showYearTermType}
            title={!showYearTermType ? 'Yıl dönem oluşturulduktan sonra değiştirilemez' : undefined}
            onChange={e => setForm(f => ({ ...f, year: Number(e.target.value) }))}
            min={2000}
            max={2100}
          />
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
