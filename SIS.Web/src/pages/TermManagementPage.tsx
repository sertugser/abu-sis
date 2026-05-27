import { useState } from 'react';
import { Plus, Edit2, MoreHorizontal, Power, PowerOff, RotateCcw, Trash2, ChevronRight, AlertTriangle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
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
import { ListToolbar } from '../components/academic-structure/ListToolbar';
import { SortHeader } from '../components/academic-structure/shared';
import { useListController } from '../hooks/useListController';
import { TermForm, type TermFormState } from '@/components/term-management/TermForm';
import { TermDetailView } from '@/components/term-management/TermDetailView';
import { toDateInput, toIsoString, fmt } from '@/components/term-management/termDate';
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

// Sayfa başına varsayılan 15 dönem; sonrası pagination ile.
const DEFAULT_PAGE_SIZE = 15;
const PAGE_SIZE_OPTIONS = [15, 30, 50];

// ── Types ─────────────────────────────────────────────────────────────────────

type SortKey = 'name' | 'status' | 'yoksis' | 'termStart' | 'calendar';

// ── Liste mantığı (useListController'a verilen saf fonksiyonlar) ──────────────

function matchesSearch(t: AdminTermDto, q: string): boolean {
  return t.name.toLowerCase().includes(q)
    || t.shortName.toLowerCase().includes(q)
    || String(t.year).includes(q)
    || t.termTypeLabel.toLowerCase().includes(q);
}

function compareTerm(a: AdminTermDto, b: AdminTermDto, key: SortKey): number {
  if (key === 'name') return a.name.localeCompare(b.name, 'tr');
  if (key === 'status') return a.status.localeCompare(b.status, 'tr');
  if (key === 'yoksis') return a.yoksisStatus.localeCompare(b.yoksisStatus, 'tr');
  if (key === 'calendar') return Number(a.hasAcademicCalendar) - Number(b.hasAcademicCalendar);
  return new Date(a.termStart).getTime() - new Date(b.termStart).getTime();
}

// ── Form State ───────────────────────────────────────────────────────────────

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
  const [viewingTerm, setViewingTerm] = useState<AdminTermDto | null>(null);
  const [form, setForm] = useState<TermFormState>(emptyForm());
  const [formError, setFormError] = useState('');
  const [successMessage, setSuccessMessage] = useState('');
  const [actionError, setActionError] = useState('');
  const [confirmCancelTarget, setConfirmCancelTarget] = useState<AdminTermDto | null>(null);
  // Hafif (geri-dönüşlü) durum aksiyonları için onay: kullanıma aç/kapat, iptali geri al.
  const [confirmAction, setConfirmAction] = useState<{ term: AdminTermDto; kind: 'activate' | 'deactivate' | 'uncancel' } | null>(null);

  const { data: terms = [], isLoading, isError, refetch } = useAdminTerms();
  const createTerm = useCreateTerm();
  const updateTerm = useUpdateTerm();
  const cancelTerm = useCancelTerm();
  const uncancelTerm = useUncancelTerm();
  const activateTerm = useActivateTerm();
  const deactivateTerm = useDeactivateTerm();

  const list = useListController<AdminTermDto, SortKey>({
    items: terms,
    initialSortKey: 'termStart',
    initialSortDir: 'desc',
    defaultPageSize: DEFAULT_PAGE_SIZE,
    matchesSearch,
    compare: compareTerm,
  });

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
      setActionError(extractApiError(err, 'İptal işlemi başarısız.'));
    }
  }

  async function handleUncancel(termId: number) {
    try {
      await uncancelTerm.mutateAsync(termId);
      showSuccess('Dönem iptali geri alındı.');
    } catch (err: unknown) {
      setActionError(extractApiError(err, 'İptal geri alma başarısız.'));
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
    <div className="flex flex-1 flex-col gap-3 min-h-0">
      {actionError && (
        <div className="p-3 bg-destructive/10 border border-destructive/30 rounded-lg text-sm text-destructive flex items-start gap-2">
          <span className="shrink-0 font-semibold">Hata:</span>
          <span className="flex-1">{actionError}</span>
          <button
            type="button"
            onClick={() => setActionError('')}
            className="shrink-0 text-destructive hover:text-destructive/80 font-bold"
            aria-label="Hatayı kapat"
          >
            &times;
          </button>
        </div>
      )}

      {/* ── Merged card: toolbar + table + footer ── */}
      <div className="bg-white border border-slate-200 rounded-xl overflow-hidden flex flex-1 flex-col min-h-0">
        <ListToolbar
          searchPlaceholder="Dönem adı, yıl veya tür ara..."
          search={list.search}
          onSearchChange={list.setSearch}
          hasAnyConstraint={list.hasAnyConstraint}
          onClear={list.clearAll}
          successMessage={successMessage}
          actions={
            <Button size="sm" className="h-8 shrink-0" onClick={openCreate}>
              <Plus className="h-4 w-4 mr-1" /> Yeni Dönem Oluştur
            </Button>
          }
        />

        <div className="flex-1 min-h-0 overflow-auto">
          <table className="w-full min-w-[820px] text-sm">
            <thead className="bg-slate-50 border-b border-slate-200 sticky top-0 z-10">
              <tr>
                <th className="text-left px-4 py-3 font-semibold text-slate-600">
                  <SortHeader label="Dönem" active={list.sortKey === 'name'} dir={list.sortDir} onClick={() => list.toggleSort('name')} />
                </th>
                <th className="text-center px-4 py-3 font-semibold text-slate-600 w-40">
                  <SortHeader label="Durum" active={list.sortKey === 'status'} dir={list.sortDir} onClick={() => list.toggleSort('status')} />
                </th>
                <th className="text-center px-4 py-3 font-semibold text-slate-600 w-40">
                  <SortHeader label="YÖKSİS" active={list.sortKey === 'yoksis'} dir={list.sortDir} onClick={() => list.toggleSort('yoksis')} />
                </th>
                <th className="text-center px-4 py-3 font-semibold text-slate-600 w-40">
                  <SortHeader label="Dönem Tarihi" active={list.sortKey === 'termStart'} dir={list.sortDir} onClick={() => list.toggleSort('termStart')} />
                </th>
                <th className="text-center px-4 py-3 font-semibold text-slate-600 w-40">
                  <SortHeader label="Akademik Takvim" active={list.sortKey === 'calendar'} dir={list.sortDir} onClick={() => list.toggleSort('calendar')} />
                </th>
                <th className="px-4 py-3 w-40" />
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                <tr>
                  <td colSpan={6} className="px-4 py-10 text-center text-muted-foreground">Yükleniyor...</td>
                </tr>
              ) : isError ? (
                <tr>
                  <td colSpan={6} className="px-4 py-14">
                    <div className="flex flex-col items-center gap-3 text-center">
                      <AlertTriangle className="h-6 w-6 text-destructive" />
                      <div>
                        <p className="font-medium text-foreground">Dönem listesi yüklenemedi.</p>
                        <p className="text-xs text-muted-foreground mt-1">Bağlantı veya sunucu hatası olabilir.</p>
                      </div>
                      <Button size="sm" variant="outline" className="h-8" onClick={() => refetch()}>
                        Tekrar dene
                      </Button>
                    </div>
                  </td>
                </tr>
              ) : list.visible.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-4 py-14">
                    <div className="flex flex-col items-center gap-3 text-center">
                      <div>
                        <p className="font-medium text-foreground">
                          {list.hasAnyConstraint ? 'Kriterle eşleşen dönem bulunamadı.' : 'Henüz dönem oluşturulmamış.'}
                        </p>
                        <p className="text-xs text-muted-foreground mt-1">
                          {list.hasAnyConstraint
                            ? 'Arama kriterini değiştirebilirsiniz.'
                            : 'İlk dönemi oluşturarak akademik takvime başlayın.'}
                        </p>
                      </div>
                      {!list.hasAnyConstraint && (
                        <Button size="sm" className="h-8" onClick={openCreate}>
                          <Plus className="h-4 w-4 mr-1" /> İlk Dönemi Oluştur
                        </Button>
                      )}
                    </div>
                  </td>
                </tr>
              ) : (
                list.paged.map(term => (
                  <tr
                    key={term.id}
                    role="button"
                    tabIndex={0}
                    onClick={() => setViewingTerm(term)}
                    onKeyDown={e => {
                      if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        setViewingTerm(term);
                      }
                    }}
                    title={`${term.name} — detayları göster`}
                    className={`group cursor-pointer transition-colors border-b border-slate-200 border-l-2 focus:outline-none focus:bg-slate-50/80 ${
                      term.isActive
                        ? 'border-l-emerald-500 bg-emerald-50/50 hover:bg-emerald-50/80'
                        : 'border-l-transparent hover:bg-slate-50/60'
                    }`}
                  >
                    {/* Ad */}
                    <td className="px-4 py-3">
                      <span className="font-semibold text-foreground">{term.name}</span>
                    </td>

                    {/* Durum */}
                    <td className="px-4 py-3 text-center">
                      <Badge className={`text-sm ${TERM_STATUS_COLORS[term.status] ?? 'bg-gray-100 text-gray-700 border-gray-300'}`}>
                        {term.statusLabel || TERM_STATUS_LABELS[term.status] || term.status}
                      </Badge>
                    </td>

                    {/* YÖKSİS */}
                    <td className="px-4 py-3 text-center">
                      <Badge className={`text-sm ${term.yoksisStatus === 'Active' ? 'bg-blue-50 text-blue-700 border-blue-200' : 'bg-slate-50 text-slate-500 border-slate-200'}`}>
                        {term.yoksisStatus === 'Active' ? 'Aktif' : 'Pasif'}
                      </Badge>
                    </td>

                    {/* Dönem Tarihi */}
                    <td className="px-4 py-3 text-center">
                      <div>
                        <p className="text-sm font-medium text-foreground">{fmt(term.termStart)}</p>
                        <p className="text-xs text-muted-foreground">{fmt(term.termEnd)}</p>
                      </div>
                    </td>

                    {/* Akademik Takvim */}
                    <td className="px-4 py-3 text-center">
                      {term.hasAcademicCalendar
                        ? <Badge className="text-sm bg-emerald-100 text-emerald-700 border-emerald-300">Takvim Tanımlı</Badge>
                        : <Badge className="text-sm bg-amber-100 text-amber-700 border-amber-300">Takvim Eksik</Badge>}
                    </td>

                    {/* İşlemler */}
                    <td className="px-4 py-3" onClick={e => e.stopPropagation()}>
                      <div className="flex items-center gap-2 justify-end">
                        <Button variant="ghost" size="sm" className="h-8 w-8 p-0 transition-colors hover:bg-blue-50 hover:text-blue-600" title="Düzenle" onClick={() => openEdit(term)}>
                          <Edit2 className="h-3.5 w-3.5" />
                        </Button>
                        <div className="w-px h-5 bg-slate-200" />
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="sm" className="h-8 w-8 p-0 transition-colors hover:bg-slate-200/60" title="İşlemler">
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
                        <ChevronRight className="h-4 w-4 ml-1 shrink-0 text-muted-foreground/60 transition-all duration-150 group-hover:translate-x-0.5 group-hover:text-foreground" />
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {!isLoading && list.visible.length > 0 && (
          <TablePagination
            totalCount={list.visible.length}
            page={list.safePage}
            pageSize={list.pageSize}
            onPageChange={list.setPage}
            onPageSizeChange={list.changePageSize}
            itemLabel="dönem"
            pageSizeOptions={PAGE_SIZE_OPTIONS}
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

      {/* Detay Modal (satıra tıkla ile açılır) */}
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
