import { useState } from 'react';
import {
  Plus,
  Edit2,
  Trash2,
  ChevronRight,
  AlertTriangle,
  Upload,
  Building2,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import {
  useAdminFaculties,
  useCreateFaculty,
  useUpdateFaculty,
  useDeleteFaculty,
  useBulkCreateFaculties,
  type FacultyDto,
} from '../hooks/useAdminAcademicStructure';
import { FacultyFormFields, SortHeader } from '../components/academic-structure/shared';
import { FacultyDeleteDialog } from '../components/academic-structure/FacultyDeleteDialog';
import { FacultyDetailPanel } from '../components/academic-structure/FacultyDetailPanel';
import { BulkAddDialog } from '../components/academic-structure/BulkAddDialog';
import { ListToolbar } from '../components/academic-structure/ListToolbar';
import { TablePagination } from '@/components/ui/table-pagination';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { useListController } from '../hooks/useListController';
import {
  emptyFacultyForm,
  extractErrorMessage,
  type FacultyFormState,
} from '../components/academic-structure/shared-constants';

// ── Types ────────────────────────────────────────────────────────────────────

type SortKey = 'name' | 'code' | 'departmentCount' | 'programCount' | 'studentCount';

// Sayfa başına varsayılan 15 fakülte; sonrası pagination ile.
const DEFAULT_PAGE_SIZE = 15;
const PAGE_SIZE_OPTIONS = [15, 30, 50];

// ── Liste mantığı (useListController'a verilen saf fonksiyonlar) ──────────────

function matchesSearch(f: FacultyDto, q: string): boolean {
  return f.name.toLowerCase().includes(q) || f.code.toLowerCase().includes(q);
}

function compareFaculty(a: FacultyDto, b: FacultyDto, key: SortKey): number {
  if (key === 'name') return a.name.localeCompare(b.name, 'tr');
  if (key === 'code') return a.code.localeCompare(b.code, 'tr');
  if (key === 'programCount') return a.programCount - b.programCount;
  if (key === 'studentCount') return a.studentCount - b.studentCount;
  return a.departmentCount - b.departmentCount;
}

/** Liste sütunlarındaki sayı: 0 ise soluk, doluysa vurgulu. */
function Metric({ value }: { value: number }) {
  return (
    <span className={`tabular-nums ${value === 0 ? 'text-slate-400' : 'font-medium text-foreground'}`}>
      {value}
    </span>
  );
}

// ── Main Page ────────────────────────────────────────────────────────────────

export default function FacultyDefinitionsPage() {
  const { data: faculties = [], isLoading, isError, refetch } = useAdminFaculties();
  const createFaculty = useCreateFaculty();
  const updateFaculty = useUpdateFaculty();
  const deleteFaculty = useDeleteFaculty();
  const bulkCreateFaculties = useBulkCreateFaculties();
  const [bulkOpen, setBulkOpen] = useState(false);

  const [createOpen, setCreateOpen] = useState(false);
  const [editItem, setEditItem] = useState<FacultyDto | null>(null);
  const [deleteItem, setDeleteItem] = useState<FacultyDto | null>(null);
  const [detailItem, setDetailItem] = useState<FacultyDto | null>(null);
  const [form, setForm] = useState<FacultyFormState>(emptyFacultyForm());
  const [formError, setFormError] = useState('');

  const [successMessage, setSuccessMessage] = useState('');
  const [actionError, setActionError] = useState('');

  const list = useListController<FacultyDto, SortKey>({
    items: faculties,
    initialSortKey: 'name',
    defaultPageSize: DEFAULT_PAGE_SIZE,
    matchesSearch,
    compare: compareFaculty,
  });

  const dialogOpen = createOpen || !!editItem;
  const isSavePending = createFaculty.isPending || updateFaculty.isPending;

  function showSuccess(msg: string) {
    setSuccessMessage(msg);
    setTimeout(() => setSuccessMessage(''), 3000);
  }

  function openCreate() {
    setForm(emptyFacultyForm());
    setFormError('');
    setEditItem(null);
    setCreateOpen(true);
  }

  function openEdit(f: FacultyDto) {
    setForm({ name: f.name, code: f.code });
    setFormError('');
    setCreateOpen(false);
    setEditItem(f);
  }

  function closeDialog() {
    setCreateOpen(false);
    setEditItem(null);
    setFormError('');
  }

  async function handleSubmit() {
    if (!form.name.trim() || !form.code.trim()) {
      setFormError('Ad ve kod zorunludur.');
      return;
    }
    try {
      if (editItem) {
        await updateFaculty.mutateAsync({ id: editItem.id, ...form });
        showSuccess('Fakülte güncellendi.');
      } else {
        await createFaculty.mutateAsync(form);
        showSuccess('Fakülte eklendi.');
      }
      closeDialog();
    } catch (e: unknown) {
      setFormError(extractErrorMessage(e, 'Kaydedilemedi.'));
    }
  }

  async function handleDelete() {
    if (!deleteItem) return;
    const target = deleteItem;
    try {
      await deleteFaculty.mutateAsync(target.id);
      setDeleteItem(null);
      showSuccess('Fakülte silindi.');
    } catch (e: unknown) {
      setDeleteItem(null);
      setActionError(extractErrorMessage(e, `"${target.name}" silinemedi.`));
    }
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
          searchPlaceholder="Ad veya kod ara..."
          search={list.search}
          onSearchChange={list.setSearch}
          hasAnyConstraint={list.hasAnyConstraint}
          onClear={list.clearAll}
          successMessage={successMessage}
          actions={
            <>
              <Button variant="outline" size="sm" className="h-8 shrink-0" onClick={() => setBulkOpen(true)}>
                <Upload className="h-4 w-4 mr-1" /> Toplu Ekle
              </Button>
              <Button size="sm" className="h-8 shrink-0" onClick={openCreate}>
                <Plus className="h-4 w-4 mr-1" /> Fakülte Ekle
              </Button>
            </>
          }
        />

        <div className="flex-1 min-h-0 overflow-auto">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 border-b border-slate-200 sticky top-0 z-10">
            <tr>
              <th className="text-left px-4 py-3 font-semibold text-slate-600">
                <SortHeader
                  label="Ad"
                  active={list.sortKey ==='name'}
                  dir={list.sortDir}
                  onClick={() => list.toggleSort('name')}
                />
              </th>
              <th className="text-center px-4 py-3 font-semibold text-slate-600 w-28">
                <SortHeader
                  label="Kod"
                  active={list.sortKey ==='code'}
                  dir={list.sortDir}
                  onClick={() => list.toggleSort('code')}
                />
              </th>
              <th className="text-center px-4 py-3 font-semibold text-slate-600 w-28">
                <SortHeader
                  label="Bölüm"
                  active={list.sortKey ==='departmentCount'}
                  dir={list.sortDir}
                  onClick={() => list.toggleSort('departmentCount')}
                />
              </th>
              <th className="text-center px-4 py-3 font-semibold text-slate-600 w-28">
                <SortHeader
                  label="Program"
                  active={list.sortKey ==='programCount'}
                  dir={list.sortDir}
                  onClick={() => list.toggleSort('programCount')}
                />
              </th>
              <th className="text-center px-4 py-3 font-semibold text-slate-600 w-28">
                <SortHeader
                  label="Öğrenci"
                  active={list.sortKey ==='studentCount'}
                  dir={list.sortDir}
                  onClick={() => list.toggleSort('studentCount')}
                />
              </th>
              <th className="px-4 py-3 w-44" />
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
                      <p className="font-medium text-foreground">Fakülte listesi yüklenemedi.</p>
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
                        {list.hasAnyConstraint ? 'Kriterle eşleşen fakülte bulunamadı.' : 'Henüz fakülte tanımlı değil.'}
                      </p>
                      <p className="text-xs text-muted-foreground mt-1">
                        {list.hasAnyConstraint
                          ? 'Arama veya filtre kriterini değiştirebilirsiniz.'
                          : 'İlk fakülteyi ekleyerek akademik yapıya başlayın.'}
                      </p>
                    </div>
                    {!list.hasAnyConstraint && (
                      <Button size="sm" className="h-8" onClick={openCreate}>
                        <Plus className="h-4 w-4 mr-1" /> İlk Fakülteyi Ekle
                      </Button>
                    )}
                  </div>
                </td>
              </tr>
            ) : (
              list.paged.map(f => {
                const isEmpty = f.departmentCount === 0;
                return (
                  <tr
                    key={f.id}
                    role="button"
                    tabIndex={0}
                    onClick={() => setDetailItem(f)}
                    onKeyDown={e => {
                      if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        setDetailItem(f);
                      }
                    }}
                    title={`${f.name} — detayları göster`}
                    className={`group cursor-pointer transition-colors border-b border-slate-200 border-l-2 focus:outline-none focus:bg-slate-50/80 ${
                      isEmpty
                        ? 'border-l-amber-300/70 bg-amber-50/20 hover:bg-amber-50/40'
                        : 'border-l-transparent hover:bg-slate-50/60'
                    }`}
                  >
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <span className={`inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-lg ${isEmpty ? 'bg-amber-50 text-amber-600' : 'bg-blue-50 text-blue-600'}`}>
                          <Building2 className="h-4 w-4" />
                        </span>
                        <span className="font-medium text-foreground">{f.name}</span>
                        {isEmpty && (
                          <span title="Bu fakülteye henüz bölüm eklenmemiş">
                            <AlertTriangle className="h-3.5 w-3.5 text-amber-500 shrink-0 animate-pulse" />
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-center">
                      <Badge
                        variant="outline"
                        className="font-mono text-xs tabular-nums uppercase tracking-wide"
                      >
                        {f.code}
                      </Badge>
                    </td>
                    <td className="px-4 py-3 text-center"><Metric value={f.departmentCount} /></td>
                    <td className="px-4 py-3 text-center"><Metric value={f.programCount} /></td>
                    <td className="px-4 py-3 text-center"><Metric value={f.studentCount} /></td>
                    <td className="px-4 py-3">
                      <div
                        className="flex items-center gap-2 justify-end"
                        onClick={e => e.stopPropagation()}
                      >
                        <Button
                          size="sm"
                          variant="ghost"
                          className="h-8 w-8 p-0 transition-colors hover:bg-blue-50 hover:text-blue-600"
                          title="Düzenle"
                          onClick={() => openEdit(f)}
                        >
                          <Edit2 className="h-3.5 w-3.5" />
                        </Button>
                        <div className="w-px h-5 bg-slate-200" />
                        <Button
                          size="sm"
                          variant="ghost"
                          className="h-8 w-8 p-0 transition-colors hover:bg-red-50 hover:text-red-600"
                          title="Sil"
                          onClick={() => setDeleteItem(f)}
                        >
                          <Trash2 className="h-3.5 w-3.5 text-destructive" />
                        </Button>
                        <ChevronRight className="h-4 w-4 ml-1 shrink-0 text-muted-foreground/60 transition-all duration-150 group-hover:translate-x-0.5 group-hover:text-foreground" />
                      </div>
                    </td>
                  </tr>
                );
              })
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
            itemLabel="fakülte"
            pageSizeOptions={PAGE_SIZE_OPTIONS}
            size="md"
          />
        )}
      </div>

      {/* Create / Edit Dialog (merged) */}
      <Dialog open={dialogOpen} onOpenChange={o => !o && closeDialog()}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>{editItem ? 'Fakülte Düzenle' : 'Fakülte Ekle'}</DialogTitle>
          </DialogHeader>
          <FacultyFormFields
            form={form}
            onChange={f => { setForm(f); setFormError(''); }}
          />
          {formError && <p className="text-sm text-destructive">{formError}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={closeDialog}>İptal</Button>
            <Button onClick={handleSubmit} disabled={isSavePending}>
              {isSavePending ? 'Kaydediliyor...' : 'Kaydet'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <FacultyDeleteDialog
        faculty={deleteItem}
        onClose={() => setDeleteItem(null)}
        onConfirm={handleDelete}
        isPending={deleteFaculty.isPending}
      />

      <BulkAddDialog<{ name: string; code: string }>
        open={bulkOpen}
        onClose={() => setBulkOpen(false)}
        title="Toplu Fakülte Ekle"
        description={
          <>
            Her satıra bir fakülte yazın: <strong>Kod, Ad</strong> (virgül veya Excel'den tab ile).
            Örn: <code>MF, Mühendislik Fakültesi</code>
          </>
        }
        placeholder={'MF, Mühendislik Fakültesi\nTF, Tıp Fakültesi'}
        parseColumns={cols => {
          const code = (cols[0] ?? '').toUpperCase();
          const name = cols.slice(1).join(', ').trim();
          if (!code || !name) return { label: code || name || '', item: null, error: 'Kod ve ad gerekli' };
          return { label: code, item: { name, code }, error: null };
        }}
        submit={items => bulkCreateFaculties.mutateAsync(items)}
      />

      <Dialog open={!!detailItem} onOpenChange={o => !o && setDetailItem(null)}>
        <ResizableDialogContent defaultWidth={1100} minWidth={560} minHeight={420}>
          {detailItem && (
            <FacultyDetailPanel
              faculty={detailItem}
              onEdit={() => {
                const target = detailItem;
                setDetailItem(null);
                openEdit(target);
              }}
              onDelete={() => {
                const target = detailItem;
                setDetailItem(null);
                setDeleteItem(target);
              }}
            />
          )}
        </ResizableDialogContent>
      </Dialog>
    </div>
  );
}
