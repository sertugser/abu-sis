import { useState, useMemo } from 'react';
import { Link } from 'react-router-dom';
import {
  Plus,
  Edit2,
  Trash2,
  Search,
  SlidersHorizontal,
  ArrowUpDown,
  ArrowUp,
  ArrowDown,
  ChevronRight,
  AlertTriangle,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import {
  useAdminFaculties,
  useAdminDepartmentsByStructure,
  useCreateFaculty,
  useUpdateFaculty,
  useDeleteFaculty,
  type FacultyDto,
} from '../hooks/useAdminAcademicStructure';
import {
  FacultyFormFields,
  DeleteConfirmDialog,
} from '../components/academic-structure/shared';
import {
  emptyFacultyForm,
  type FacultyFormState,
} from '../components/academic-structure/shared-constants';
import { ROUTES } from '@/lib/constants';

// ── Types ────────────────────────────────────────────────────────────────────

type SortKey = 'name' | 'code' | 'departmentCount';
type SortDir = 'asc' | 'desc';
type DeptFilter = 'all' | 'has' | 'none';

interface FilterState {
  departmentStatus: DeptFilter;
}

const EMPTY_FILTERS: FilterState = { departmentStatus: 'all' };

// ── Helpers ──────────────────────────────────────────────────────────────────

function extractErrorMessage(e: unknown, fallback: string): string {
  const resp = (e as { response?: { data?: { error?: string; message?: string; title?: string } } })?.response?.data;
  return resp?.error ?? resp?.message ?? resp?.title ?? (e instanceof Error ? e.message : fallback);
}

function countActiveFilters(f: FilterState): number {
  let n = 0;
  if (f.departmentStatus !== 'all') n++;
  return n;
}

function DepartmentCountBadge({ count }: { count: number }) {
  if (count === 0) {
    return (
      <Badge className="bg-slate-100 text-slate-500 border border-slate-200 hover:bg-slate-100 tabular-nums font-normal">
        0 bölüm
      </Badge>
    );
  }
  if (count >= 5) {
    return (
      <Badge className="bg-blue-100 text-blue-800 border border-blue-300 hover:bg-blue-100 tabular-nums">
        {count} bölüm
      </Badge>
    );
  }
  return (
    <Badge className="bg-blue-50 text-blue-700 border border-blue-200 hover:bg-blue-50 tabular-nums">
      {count} bölüm
    </Badge>
  );
}

// ── Main Page ────────────────────────────────────────────────────────────────

export default function FacultyDefinitionsPage() {
  const { data: faculties = [], isLoading } = useAdminFaculties();
  const createFaculty = useCreateFaculty();
  const updateFaculty = useUpdateFaculty();
  const deleteFaculty = useDeleteFaculty();

  const [createOpen, setCreateOpen] = useState(false);
  const [editItem, setEditItem] = useState<FacultyDto | null>(null);
  const [deleteItem, setDeleteItem] = useState<FacultyDto | null>(null);
  const [detailItem, setDetailItem] = useState<FacultyDto | null>(null);
  const [form, setForm] = useState<FacultyFormState>(emptyFacultyForm());
  const [formError, setFormError] = useState('');

  const [search, setSearch] = useState('');
  const [filters, setFilters] = useState<FilterState>(EMPTY_FILTERS);
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [sortKey, setSortKey] = useState<SortKey>('name');
  const [sortDir, setSortDir] = useState<SortDir>('asc');

  const [successMessage, setSuccessMessage] = useState('');
  const [actionError, setActionError] = useState('');

  const dialogOpen = createOpen || !!editItem;
  const isSavePending = createFaculty.isPending || updateFaculty.isPending;
  const activeFilterCount = countActiveFilters(filters);
  const hasAnyConstraint = search.trim().length > 0 || activeFilterCount > 0;

  const visibleFaculties = useMemo(() => {
    const q = search.trim().toLowerCase();
    let result = faculties;

    if (q) {
      result = result.filter(f =>
        f.name.toLowerCase().includes(q) || f.code.toLowerCase().includes(q),
      );
    }

    if (filters.departmentStatus === 'has') {
      result = result.filter(f => f.departmentCount > 0);
    } else if (filters.departmentStatus === 'none') {
      result = result.filter(f => f.departmentCount === 0);
    }

    const sorted = [...result].sort((a, b) => {
      let cmp = 0;
      if (sortKey === 'name') cmp = a.name.localeCompare(b.name, 'tr');
      else if (sortKey === 'code') cmp = a.code.localeCompare(b.code, 'tr');
      else cmp = a.departmentCount - b.departmentCount;
      return sortDir === 'asc' ? cmp : -cmp;
    });
    return sorted;
  }, [faculties, search, filters, sortKey, sortDir]);

  function toggleSort(key: SortKey) {
    if (sortKey === key) setSortDir(d => (d === 'asc' ? 'desc' : 'asc'));
    else { setSortKey(key); setSortDir('asc'); }
  }

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

  function clearAll() {
    setSearch('');
    setFilters(EMPTY_FILTERS);
  }

  return (
    <div className="space-y-3">
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
      <div className="bg-white border border-slate-200 rounded-xl overflow-hidden">
        {/* Toolbar */}
        <div className="flex items-center gap-3 px-4 py-2 border-b border-slate-200 bg-slate-50/40 flex-wrap">
        <div className="relative shrink-0">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground pointer-events-none" />
          <Input
            placeholder="Ad veya kod ara..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="pl-8 h-8 w-72 text-sm"
          />
        </div>

        <Button
          variant="outline"
          size="sm"
          className="h-8 gap-1.5 shrink-0"
          onClick={() => setFiltersOpen(true)}
        >
          <SlidersHorizontal className="h-3.5 w-3.5" />
          Filtreler
          {activeFilterCount > 0 && (
            <Badge className="ml-0.5 h-4 px-1.5 text-[10px] bg-primary text-primary-foreground tabular-nums">
              {activeFilterCount}
            </Badge>
          )}
        </Button>

        {hasAnyConstraint && (
          <button
            type="button"
            className="text-xs text-muted-foreground hover:text-foreground underline underline-offset-2 shrink-0"
            onClick={clearAll}
          >
            Temizle
          </button>
        )}

        <div className="flex-1" />

        {successMessage && (
          <span className="text-xs font-medium text-emerald-600 shrink-0">{successMessage}</span>
        )}

        <Button size="sm" className="h-8 shrink-0" onClick={openCreate}>
          <Plus className="h-4 w-4 mr-1" /> Fakülte Ekle
        </Button>
      </div>

        <table className="w-full text-sm">
          <thead className="bg-slate-50 border-b border-slate-200">
            <tr>
              <th className="text-left px-4 py-3 font-semibold text-slate-600">
                <SortHeader
                  label="Ad"
                  active={sortKey === 'name'}
                  dir={sortDir}
                  onClick={() => toggleSort('name')}
                />
              </th>
              <th className="text-left px-4 py-3 font-semibold text-slate-600 w-32">
                <SortHeader
                  label="Kod"
                  active={sortKey === 'code'}
                  dir={sortDir}
                  onClick={() => toggleSort('code')}
                />
              </th>
              <th className="text-left px-4 py-3 font-semibold text-slate-600 w-40">
                <SortHeader
                  label="Bölüm Sayısı"
                  active={sortKey === 'departmentCount'}
                  dir={sortDir}
                  onClick={() => toggleSort('departmentCount')}
                />
              </th>
              <th className="px-4 py-3 w-44" />
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {isLoading ? (
              <tr>
                <td colSpan={4} className="px-4 py-10 text-center text-muted-foreground">Yükleniyor...</td>
              </tr>
            ) : visibleFaculties.length === 0 ? (
              <tr>
                <td colSpan={4} className="px-4 py-14">
                  <div className="flex flex-col items-center gap-3 text-center">
                    <div>
                      <p className="font-medium text-foreground">
                        {hasAnyConstraint ? 'Kriterle eşleşen fakülte bulunamadı.' : 'Henüz fakülte tanımlı değil.'}
                      </p>
                      <p className="text-xs text-muted-foreground mt-1">
                        {hasAnyConstraint
                          ? 'Arama veya filtre kriterini değiştirebilirsiniz.'
                          : 'İlk fakülteyi ekleyerek akademik yapıya başlayın.'}
                      </p>
                    </div>
                    {!hasAnyConstraint && (
                      <Button size="sm" className="h-8" onClick={openCreate}>
                        <Plus className="h-4 w-4 mr-1" /> İlk Fakülteyi Ekle
                      </Button>
                    )}
                  </div>
                </td>
              </tr>
            ) : (
              visibleFaculties.map(f => {
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
                    className={`group cursor-pointer transition-colors border-l-2 focus:outline-none focus:bg-slate-50/80 ${
                      isEmpty
                        ? 'border-l-amber-300/70 bg-amber-50/20 hover:bg-amber-50/40'
                        : 'border-l-transparent hover:bg-slate-50/60'
                    }`}
                  >
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2 font-medium text-foreground">
                        {f.name}
                        {isEmpty && (
                          <span title="Bu fakülteye henüz bölüm eklenmemiş">
                            <AlertTriangle className="h-3.5 w-3.5 text-amber-500 shrink-0" />
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <Badge
                        variant="outline"
                        className="font-mono text-xs tabular-nums uppercase tracking-wide"
                      >
                        {f.code}
                      </Badge>
                    </td>
                    <td className="px-4 py-3">
                      <DepartmentCountBadge count={f.departmentCount} />
                    </td>
                    <td className="px-4 py-3">
                      <div
                        className="flex items-center gap-2 justify-end opacity-0 group-hover:opacity-100 focus-within:opacity-100 transition-opacity"
                        onClick={e => e.stopPropagation()}
                      >
                        <Button
                          size="sm"
                          variant="ghost"
                          className="h-8 w-8 p-0 hover:bg-slate-200/60"
                          title="Düzenle"
                          onClick={() => openEdit(f)}
                        >
                          <Edit2 className="h-3.5 w-3.5" />
                        </Button>
                        <div className="w-px h-5 bg-slate-200" />
                        <Button
                          size="sm"
                          variant="ghost"
                          className="h-8 w-8 p-0 hover:bg-destructive/10"
                          title="Sil"
                          onClick={() => setDeleteItem(f)}
                        >
                          <Trash2 className="h-3.5 w-3.5 text-destructive" />
                        </Button>
                        <ChevronRight className="h-4 w-4 text-muted-foreground/60 ml-1 shrink-0" />
                      </div>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>

        <div className="px-4 py-3 border-t border-slate-200 bg-slate-50/60 flex items-center justify-between flex-wrap gap-3">
          <span className="text-xs text-muted-foreground tabular-nums">
            {isLoading
              ? 'Yükleniyor...'
              : hasAnyConstraint
                ? `${faculties.length} fakülteden ${visibleFaculties.length}'i gösteriliyor`
                : `${faculties.length} fakülte`}
          </span>
          <p className="text-sm text-muted-foreground italic">
            Fakültelere bağlı bölümleri tanımlamak için →{' '}
            <Link to={ROUTES.DEPARTMENT_DEFINITIONS} className="underline text-blue-600 not-italic">
              Bölüm Tanımları
            </Link>
          </p>
        </div>
      </div>

      {/* Filter Dialog */}
      <Dialog open={filtersOpen} onOpenChange={setFiltersOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Filtreler</DialogTitle>
          </DialogHeader>

          <div className="space-y-4 pt-1">
            <div className="space-y-2">
              <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                Bölüm Durumu
              </label>
              <div className="grid grid-cols-3 gap-1.5">
                {([
                  { value: 'all', label: 'Tümü' },
                  { value: 'has', label: 'Bölümü olan' },
                  { value: 'none', label: 'Bölümü olmayan' },
                ] as { value: DeptFilter; label: string }[]).map(opt => (
                  <label
                    key={opt.value}
                    className={`flex items-center gap-2 text-sm cursor-pointer rounded-md border px-3 py-2 transition-colors ${
                      filters.departmentStatus === opt.value
                        ? 'border-primary bg-primary/5'
                        : 'border-slate-200 bg-white hover:bg-slate-50'
                    }`}
                  >
                    <input
                      type="radio"
                      name="dept-filter"
                      checked={filters.departmentStatus === opt.value}
                      onChange={() => setFilters({ ...filters, departmentStatus: opt.value })}
                      className="accent-primary"
                    />
                    <span>{opt.label}</span>
                  </label>
                ))}
              </div>
            </div>
          </div>

          <DialogFooter className="sm:justify-between gap-2 border-t border-slate-200 pt-4 mt-2">
            <Button
              variant="ghost"
              disabled={activeFilterCount === 0}
              onClick={() => setFilters(EMPTY_FILTERS)}
            >
              Sıfırla
            </Button>
            <Button onClick={() => setFiltersOpen(false)}>Kapat</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

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

      <DeleteConfirmDialog
        open={!!deleteItem}
        onClose={() => setDeleteItem(null)}
        onConfirm={handleDelete}
        name={deleteItem?.name ?? ''}
        isPending={deleteFaculty.isPending}
      />

      <Dialog open={!!detailItem} onOpenChange={o => !o && setDetailItem(null)}>
        <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
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
        </DialogContent>
      </Dialog>
    </div>
  );
}

// ── Faculty Detail Panel (Sheet content) ──────────────────────────────────────

interface FacultyDetailPanelProps {
  faculty: FacultyDto;
  onEdit: () => void;
  onDelete: () => void;
}

function FacultyDetailPanel({ faculty, onEdit, onDelete }: FacultyDetailPanelProps) {
  const { data: departments = [], isLoading } = useAdminDepartmentsByStructure(faculty.id);
  const isEmpty = faculty.departmentCount === 0;
  const totalPrograms = departments.reduce((sum, d) => sum + d.programCount, 0);

  return (
    <>
      <DialogHeader className="pr-10 border-b border-slate-200 pb-4">
        <div className="flex flex-wrap items-center gap-3">
          <DialogTitle className="text-2xl">{faculty.name}</DialogTitle>
          <Badge
            variant="outline"
            className="font-mono text-xs tabular-nums uppercase tracking-wide"
          >
            {faculty.code}
          </Badge>
          {isEmpty && (
            <span className="inline-flex items-center gap-1 text-xs text-amber-700 bg-amber-50 border border-amber-200 px-2 py-0.5 rounded">
              <AlertTriangle className="h-3 w-3" />
              Bölüm tanımlı değil
            </span>
          )}
        </div>
        <p className="text-sm text-muted-foreground mt-1">Fakülte detayları ve bağlı bölümler</p>
      </DialogHeader>

      <div className="grid grid-cols-1 md:grid-cols-5 gap-5 pt-2">
        {/* Sol kolon — Genel bilgiler + istatistikler */}
        <section className="md:col-span-2 space-y-5">
          <div>
            <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-3">
              Genel Bilgiler
            </h3>
            <dl className="divide-y divide-slate-100 rounded-lg border border-slate-200 bg-slate-50/40">
              <div className="flex justify-between px-3 py-2.5 text-sm">
                <dt className="text-muted-foreground">Fakülte Adı</dt>
                <dd className="font-medium text-foreground text-right">{faculty.name}</dd>
              </div>
              <div className="flex justify-between px-3 py-2.5 text-sm">
                <dt className="text-muted-foreground">Kod</dt>
                <dd className="font-mono uppercase tracking-wide">{faculty.code}</dd>
              </div>
              <div className="flex justify-between px-3 py-2.5 text-sm">
                <dt className="text-muted-foreground">ID</dt>
                <dd className="font-mono tabular-nums text-muted-foreground">#{faculty.id}</dd>
              </div>
            </dl>
          </div>

          <div>
            <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-3">
              Hızlı İstatistikler
            </h3>
            <div className="grid grid-cols-2 gap-2">
              <div className="rounded-lg border border-slate-200 bg-white px-3 py-2.5">
                <div className="text-xs text-muted-foreground">Bölüm</div>
                <div className="text-2xl font-semibold tabular-nums mt-0.5">{faculty.departmentCount}</div>
              </div>
              <div className="rounded-lg border border-slate-200 bg-white px-3 py-2.5">
                <div className="text-xs text-muted-foreground">Program</div>
                <div className="text-2xl font-semibold tabular-nums mt-0.5">
                  {isLoading ? '—' : totalPrograms}
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* Sağ kolon — Bağlı bölümler */}
        <section className="md:col-span-3 space-y-3">
          <div className="flex items-center justify-between">
            <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
              Bağlı Bölümler
              <span className="ml-1.5 text-muted-foreground/80 tabular-nums">
                ({faculty.departmentCount})
              </span>
            </h3>
            <Link
              to={`${ROUTES.DEPARTMENT_DEFINITIONS}?facultyId=${faculty.id}`}
              className="text-xs text-blue-600 hover:underline"
            >
              Bölüm sayfasında aç →
            </Link>
          </div>

          {isLoading ? (
            <p className="text-sm text-muted-foreground py-4 text-center">Yükleniyor...</p>
          ) : departments.length === 0 ? (
            <div className="rounded-lg border border-dashed border-amber-300 bg-amber-50/30 p-4 text-sm text-amber-900">
              Bu fakülteye henüz bölüm eklenmemiş. Bölüm eklemek için{' '}
              <Link to={ROUTES.DEPARTMENT_DEFINITIONS} className="underline font-medium">
                Bölüm Tanımları
              </Link>{' '}
              sayfasına gidin.
            </div>
          ) : (
            <ul className="space-y-1.5 max-h-96 overflow-y-auto pr-1">
              {departments.map(d => (
                <li
                  key={d.id}
                  className="flex items-center justify-between rounded-md border border-slate-200 bg-white px-3 py-2 text-sm hover:border-slate-300 transition-colors"
                >
                  <div className="min-w-0">
                    <p className="font-medium text-foreground truncate">{d.name}</p>
                    <p className="text-xs text-muted-foreground font-mono uppercase tracking-wide mt-0.5">
                      {d.code}
                    </p>
                  </div>
                  <Badge
                    className={
                      d.programCount > 0
                        ? 'bg-blue-50 text-blue-700 border border-blue-200 hover:bg-blue-50 tabular-nums shrink-0'
                        : 'bg-slate-100 text-slate-500 border border-slate-200 hover:bg-slate-100 tabular-nums font-normal shrink-0'
                    }
                  >
                    {d.programCount} program
                  </Badge>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      <DialogFooter className="mt-6 sm:justify-between gap-2 border-t border-slate-200 pt-4">
        <Button variant="outline" className="text-destructive hover:bg-destructive/10 hover:text-destructive" onClick={onDelete}>
          <Trash2 className="h-4 w-4 mr-1.5" /> Sil
        </Button>
        <Button onClick={onEdit}>
          <Edit2 className="h-4 w-4 mr-1.5" /> Düzenle
        </Button>
      </DialogFooter>
    </>
  );
}

// ── Sort Header Button ───────────────────────────────────────────────────────

interface SortHeaderProps {
  label: string;
  active: boolean;
  dir: SortDir;
  onClick: () => void;
}

function SortHeader({ label, active, dir, onClick }: SortHeaderProps) {
  const Icon = active ? (dir === 'asc' ? ArrowUp : ArrowDown) : ArrowUpDown;
  return (
    <button
      type="button"
      onClick={onClick}
      className={`inline-flex items-center gap-1 hover:text-foreground transition-colors ${active ? 'text-foreground' : ''}`}
    >
      {label}
      <Icon className="h-3.5 w-3.5" />
    </button>
  );
}
