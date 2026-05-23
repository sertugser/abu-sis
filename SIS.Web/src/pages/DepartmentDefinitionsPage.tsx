import { useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Plus,
  Edit2,
  Trash2,
  ChevronRight,
  AlertTriangle,
  ArrowRight,
  Upload,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import {
  useAdminDepartmentsByStructure,
  useAdminFaculties,
  useCreateDepartment,
  useDeleteDepartment,
  useUpdateDepartment,
  useBulkCreateDepartments,
  type DepartmentDto,
} from '../hooks/useAdminAcademicStructure';
import {
  DepartmentFormFields,
  SortHeader,
} from '../components/academic-structure/shared';
import { DepartmentDeleteDialog } from '../components/academic-structure/DepartmentDeleteDialog';
import { DepartmentDetailPanel } from '../components/academic-structure/DepartmentDetailPanel';
import { BulkAddDialog } from '../components/academic-structure/BulkAddDialog';
import { ListToolbar } from '../components/academic-structure/ListToolbar';
import { useListController } from '../hooks/useListController';
import {
  emptyDepartmentForm,
  extractErrorMessage,
  type DepartmentFormState,
} from '../components/academic-structure/shared-constants';
import { TablePagination } from '@/components/ui/table-pagination';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { ROUTES } from '@/lib/constants';

// Sayfa başına varsayılan 15 bölüm; sonrası pagination ile.
const DEFAULT_PAGE_SIZE = 15;
const PAGE_SIZE_OPTIONS = [15, 30, 50];

// ── Types ────────────────────────────────────────────────────────────────────

type SortKey =
  | 'facultyName'
  | 'name'
  | 'code'
  | 'studentCount'
  | 'instructorCount'
  | 'programCount';

// ── Liste mantığı (useListController'a verilen saf fonksiyonlar) ──────────────

function matchesSearch(d: DepartmentDto, q: string): boolean {
  return d.name.toLowerCase().includes(q)
    || d.code.toLowerCase().includes(q)
    || d.facultyName.toLowerCase().includes(q);
}

function compareDepartment(a: DepartmentDto, b: DepartmentDto, key: SortKey): number {
  if (key === 'facultyName') {
    const c = a.facultyName.localeCompare(b.facultyName, 'tr');
    return c !== 0 ? c : a.name.localeCompare(b.name, 'tr');
  }
  if (key === 'name') return a.name.localeCompare(b.name, 'tr');
  if (key === 'code') return a.code.localeCompare(b.code, 'tr');
  if (key === 'studentCount') return a.studentCount - b.studentCount;
  if (key === 'instructorCount') return a.instructorCount - b.instructorCount;
  return a.programCount - b.programCount;
}

function ProgramCountBadge({ count }: { count: number | undefined | null }) {
  const n = count ?? 0;
  if (n === 0) {
    return (
      <Badge className="bg-slate-100 text-slate-500 border border-slate-200 hover:bg-slate-100 tabular-nums font-normal">
        0
      </Badge>
    );
  }
  if (n >= 5) {
    return (
      <Badge className="bg-blue-100 text-blue-800 border border-blue-300 hover:bg-blue-100 tabular-nums">
        {n}
      </Badge>
    );
  }
  return (
    <Badge className="bg-blue-50 text-blue-700 border border-blue-200 hover:bg-blue-50 tabular-nums">
      {n}
    </Badge>
  );
}

function InstructorCountBadge({ count }: { count: number | undefined | null }) {
  const n = count ?? 0;
  if (n === 0) {
    return (
      <Badge className="bg-amber-50 text-amber-700 border border-amber-200 hover:bg-amber-50 tabular-nums">
        0
      </Badge>
    );
  }
  if (n >= 10) {
    return (
      <Badge className="bg-blue-100 text-blue-800 border border-blue-300 hover:bg-blue-100 tabular-nums">
        {n}
      </Badge>
    );
  }
  return (
    <Badge className="bg-blue-50 text-blue-700 border border-blue-200 hover:bg-blue-50 tabular-nums">
      {n}
    </Badge>
  );
}

function StudentCountBadge({ count }: { count: number | undefined | null }) {
  const n = count ?? 0;
  if (n === 0) {
    return (
      <Badge className="bg-slate-100 text-slate-500 border border-slate-200 hover:bg-slate-100 tabular-nums font-normal">
        0
      </Badge>
    );
  }
  if (n >= 100) {
    return (
      <Badge className="bg-emerald-100 text-emerald-800 border border-emerald-300 hover:bg-emerald-100 tabular-nums">
        {n}
      </Badge>
    );
  }
  return (
    <Badge className="bg-emerald-50 text-emerald-700 border border-emerald-200 hover:bg-emerald-50 tabular-nums">
      {n}
    </Badge>
  );
}

// ── Main Page ────────────────────────────────────────────────────────────────

export default function DepartmentDefinitionsPage() {
  const { data: faculties = [] } = useAdminFaculties();
  const { data: departments = [], isLoading, isError, refetch } = useAdminDepartmentsByStructure(undefined);

  const createDept = useCreateDepartment();
  const updateDept = useUpdateDepartment();
  const deleteDept = useDeleteDepartment();
  const bulkCreateDepts = useBulkCreateDepartments();
  const [bulkOpen, setBulkOpen] = useState(false);

  const [createOpen, setCreateOpen] = useState(false);
  const [editItem, setEditItem] = useState<DepartmentDto | null>(null);
  const [deleteItem, setDeleteItem] = useState<DepartmentDto | null>(null);
  const [detailItem, setDetailItem] = useState<DepartmentDto | null>(null);
  const [form, setForm] = useState<DepartmentFormState>(emptyDepartmentForm());
  const [formError, setFormError] = useState('');

  const [successMessage, setSuccessMessage] = useState('');
  const [actionError, setActionError] = useState('');

  const list = useListController<DepartmentDto, SortKey>({
    items: departments,
    initialSortKey: 'facultyName',
    defaultPageSize: DEFAULT_PAGE_SIZE,
    matchesSearch,
    compare: compareDepartment,
  });

  const dialogOpen = createOpen || !!editItem;
  const isSavePending = createDept.isPending || updateDept.isPending;
  const hasFaculties = faculties.length > 0;

  function showSuccess(msg: string) {
    setSuccessMessage(msg);
    setTimeout(() => setSuccessMessage(''), 3000);
  }

  function openCreate() {
    setForm(emptyDepartmentForm(faculties[0]?.id ?? 0));
    setFormError('');
    setEditItem(null);
    setCreateOpen(true);
  }

  function openEdit(d: DepartmentDto) {
    setForm({ name: d.name, code: d.code, facultyId: d.facultyId });
    setFormError('');
    setCreateOpen(false);
    setEditItem(d);
  }

  function closeDialog() {
    setCreateOpen(false);
    setEditItem(null);
    setFormError('');
  }

  async function handleSubmit() {
    if (!form.name.trim() || !form.code.trim() || !form.facultyId) {
      setFormError('Tüm alanlar zorunludur.');
      return;
    }
    try {
      if (editItem) {
        await updateDept.mutateAsync({ id: editItem.id, ...form });
        showSuccess('Bölüm güncellendi.');
      } else {
        await createDept.mutateAsync(form);
        showSuccess('Bölüm eklendi.');
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
      await deleteDept.mutateAsync(target.id);
      setDeleteItem(null);
      showSuccess('Bölüm silindi.');
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

      {!hasFaculties && (
        <div className="p-3 bg-amber-50 border border-amber-200 rounded-lg text-sm text-amber-900 flex items-center justify-between gap-3 flex-wrap">
          <span>Bölüm tanımlamak için önce en az bir fakülte eklemelisiniz.</span>
          <Button variant="outline" size="sm" asChild>
            <Link to={ROUTES.FACULTY_DEFINITIONS}>
              Fakülte Tanımları <ArrowRight className="h-4 w-4 ml-1" />
            </Link>
          </Button>
        </div>
      )}

      {/* ── Merged card: toolbar + table + footer ── */}
      <div className="bg-white border border-slate-200 rounded-xl overflow-hidden flex flex-1 flex-col min-h-0">
        <ListToolbar
          searchPlaceholder="Bölüm, kod veya fakülte ara..."
          search={list.search}
          onSearchChange={list.setSearch}
          hasAnyConstraint={list.hasAnyConstraint}
          onClear={list.clearAll}
          successMessage={successMessage}
          actions={
            <>
              <Button variant="outline" size="sm" className="h-8 shrink-0" onClick={() => setBulkOpen(true)} disabled={!hasFaculties}>
                <Upload className="h-4 w-4 mr-1" /> Toplu Ekle
              </Button>
              <Button size="sm" className="h-8 shrink-0" onClick={openCreate} disabled={!hasFaculties}>
                <Plus className="h-4 w-4 mr-1" /> Bölüm Ekle
              </Button>
            </>
          }
        />

        {/* Table */}
        <div className="flex-1 min-h-0 overflow-auto">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 border-b border-slate-200 sticky top-0 z-10">
            <tr>
              <th className="text-left px-4 py-3 font-semibold text-slate-600 w-48">
                <SortHeader label="Fakülte" active={list.sortKey ==='facultyName'} dir={list.sortDir} onClick={() => list.toggleSort('facultyName')} />
              </th>
              <th className="text-left px-4 py-3 font-semibold text-slate-600">
                <SortHeader label="Ad" active={list.sortKey ==='name'} dir={list.sortDir} onClick={() => list.toggleSort('name')} />
              </th>
              <th className="text-center px-4 py-3 font-semibold text-slate-600 w-36">
                <SortHeader label="Kod" active={list.sortKey ==='code'} dir={list.sortDir} onClick={() => list.toggleSort('code')} />
              </th>
              <th
                className="text-center px-4 py-3 font-semibold text-slate-600 w-36"
                title="Bölümün programlarına kayıtlı öğrenci sayısı"
              >
                <SortHeader label="Öğrenci" active={list.sortKey ==='studentCount'} dir={list.sortDir} onClick={() => list.toggleSort('studentCount')} />
              </th>
              <th
                className="text-center px-4 py-3 font-semibold text-slate-600 w-36"
                title="Bölüme atanmış öğretim üyesi (instructor) sayısı"
              >
                <SortHeader label="Öğretim Üyesi" active={list.sortKey ==='instructorCount'} dir={list.sortDir} onClick={() => list.toggleSort('instructorCount')} />
              </th>
              <th
                className="text-center px-4 py-3 font-semibold text-slate-600 w-36"
                title="Bölüme bağlı tanımlı program sayısı"
              >
                <SortHeader label="Program" active={list.sortKey ==='programCount'} dir={list.sortDir} onClick={() => list.toggleSort('programCount')} />
              </th>
              <th className="px-4 py-3 w-36" />
            </tr>
          </thead>
          <tbody>
            {isLoading ? (
              <tr>
                <td colSpan={7} className="px-4 py-10 text-center text-muted-foreground">Yükleniyor...</td>
              </tr>
            ) : isError ? (
              <tr>
                <td colSpan={7} className="px-4 py-14">
                  <div className="flex flex-col items-center gap-3 text-center">
                    <AlertTriangle className="h-6 w-6 text-destructive" />
                    <div>
                      <p className="font-medium text-foreground">Bölüm listesi yüklenemedi.</p>
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
                <td colSpan={7} className="px-4 py-14">
                  <div className="flex flex-col items-center gap-3 text-center">
                    <div>
                      <p className="font-medium text-foreground">
                        {list.hasAnyConstraint ? 'Kriterle eşleşen bölüm bulunamadı.' : 'Henüz bölüm tanımlı değil.'}
                      </p>
                      <p className="text-xs text-muted-foreground mt-1">
                        {list.hasAnyConstraint
                          ? 'Arama veya filtre kriterini değiştirebilirsiniz.'
                          : hasFaculties
                            ? 'İlk bölümü ekleyerek akademik yapıya başlayın.'
                            : 'Önce fakülte tanımlamalısınız.'}
                      </p>
                    </div>
                    {!list.hasAnyConstraint && hasFaculties && (
                      <Button size="sm" className="h-8" onClick={openCreate}>
                        <Plus className="h-4 w-4 mr-1" /> İlk Bölümü Ekle
                      </Button>
                    )}
                  </div>
                </td>
              </tr>
            ) : (
              list.paged.map(d => {
                const hasProgramGap = d.programCount === 0 && !d.isServiceUnit;
                const hasInstructorGap = d.instructorCount === 0;
                return (
                  <tr
                    key={d.id}
                    role="button"
                    tabIndex={0}
                    onClick={() => setDetailItem(d)}
                    onKeyDown={e => {
                      if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        setDetailItem(d);
                      }
                    }}
                    title={`${d.name} — detayları göster`}
                    className={`group cursor-pointer transition-colors border-b border-slate-200 border-l-2 focus:outline-none focus:bg-slate-50/80 ${
                      hasProgramGap
                        ? 'border-l-amber-300/70 bg-amber-50/20 hover:bg-amber-50/40'
                        : 'border-l-transparent hover:bg-slate-50/60'
                    }`}
                  >
                    <td className="px-4 py-3 text-xs text-muted-foreground">{d.facultyName}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2 font-medium text-foreground">
                        <span className="truncate">{d.name}</span>
                        {d.isServiceUnit && (
                          <Badge className="bg-violet-50 text-violet-700 border border-violet-200 hover:bg-violet-50 text-[10px] font-normal shrink-0">
                            Koordinatörlük
                          </Badge>
                        )}
                        {hasProgramGap && (
                          <span title="Bu bölüme henüz program eklenmemiş">
                            <AlertTriangle className="h-3.5 w-3.5 text-amber-500 shrink-0 animate-pulse" />
                          </span>
                        )}
                        {hasInstructorGap && (
                          <span title="Bu bölüme atanmış öğretim üyesi yok">
                            <AlertTriangle className="h-3.5 w-3.5 text-amber-500 shrink-0 animate-pulse" />
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-center">
                      <Badge variant="outline" className="font-mono text-xs tabular-nums uppercase tracking-wide">
                        {d.code}
                      </Badge>
                    </td>
                    <td className="px-4 py-3 text-center"><StudentCountBadge count={d.studentCount} /></td>
                    <td className="px-4 py-3 text-center"><InstructorCountBadge count={d.instructorCount} /></td>
                    <td className="px-4 py-3 text-center"><ProgramCountBadge count={d.programCount} /></td>
                    <td className="px-4 py-3">
                      <div
                        className="flex items-center gap-2 justify-end"
                        onClick={e => e.stopPropagation()}
                      >
                        <Button size="sm" variant="ghost" className="h-8 w-8 p-0 transition-colors hover:bg-blue-50 hover:text-blue-600" title="Düzenle" onClick={() => openEdit(d)}>
                          <Edit2 className="h-3.5 w-3.5" />
                        </Button>
                        <div className="w-px h-5 bg-slate-200" />
                        <Button size="sm" variant="ghost" className="h-8 w-8 p-0 transition-colors hover:bg-red-50 hover:text-red-600" title="Sil" onClick={() => setDeleteItem(d)}>
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
            itemLabel="bölüm"
            pageSizeOptions={PAGE_SIZE_OPTIONS}
            size="md"
          />
        )}
      </div>

      {/* Create / Edit Dialog (merged) */}
      <Dialog open={dialogOpen} onOpenChange={o => !o && closeDialog()}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>{editItem ? 'Bölüm Düzenle' : 'Bölüm Ekle'}</DialogTitle>
          </DialogHeader>
          <DepartmentFormFields
            form={form}
            onChange={f => { setForm(f); setFormError(''); }}
            faculties={faculties}
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

      <DepartmentDeleteDialog
        department={deleteItem}
        onClose={() => setDeleteItem(null)}
        onConfirm={handleDelete}
        isPending={deleteDept.isPending}
      />

      <BulkAddDialog<{ name: string; code: string; facultyId: number }>
        open={bulkOpen}
        onClose={() => setBulkOpen(false)}
        title="Toplu Bölüm Ekle"
        description={
          <>
            Her satıra bir bölüm yazın: <strong>Fakülte Kodu, Bölüm Kodu, Bölüm Adı</strong> (virgül veya Excel'den tab ile).
            Örn: <code>MF, BM, Bilgisayar Mühendisliği</code>
          </>
        }
        placeholder={'MF, BM, Bilgisayar Mühendisliği\nMF, EE, Elektrik-Elektronik Mühendisliği'}
        parseColumns={cols => {
          const facCode = (cols[0] ?? '').toUpperCase();
          const code = (cols[1] ?? '').toUpperCase();
          const name = cols.slice(2).join(', ').trim();
          if (!facCode || !code || !name) {
            return { label: code || name || '', item: null, error: 'Fakülte kodu, bölüm kodu ve ad gerekli' };
          }
          const faculty = faculties.find(f => f.code.toUpperCase() === facCode);
          if (!faculty) return { label: code, item: null, error: `Fakülte bulunamadı: ${facCode}` };
          return { label: code, item: { name, code, facultyId: faculty.id }, error: null };
        }}
        submit={items => bulkCreateDepts.mutateAsync(items)}
      />

      {/* Detail Modal (resizable + tam ekran) */}
      <Dialog open={!!detailItem} onOpenChange={o => !o && setDetailItem(null)}>
        <ResizableDialogContent defaultWidth={1100} minWidth={560} minHeight={420}>
          {detailItem && (
            <DepartmentDetailPanel
              department={detailItem}
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
