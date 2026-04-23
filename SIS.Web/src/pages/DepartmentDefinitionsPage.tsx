import { useState, useMemo } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
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
  ArrowRight,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import {
  useAdminDepartmentsByStructure,
  useAdminFaculties,
  useAdminProgramsByStructure,
  useCreateDepartment,
  useDeleteDepartment,
  useUpdateDepartment,
  type DepartmentDto,
} from '../hooks/useAdminAcademicStructure';
import {
  DeleteConfirmDialog,
  DepartmentFormFields,
} from '../components/academic-structure/shared';
import {
  SELECT_CLS,
  emptyDepartmentForm,
  type DepartmentFormState,
} from '../components/academic-structure/shared-constants';
import { ROUTES } from '@/lib/constants';

// ── Types ────────────────────────────────────────────────────────────────────

type SortKey =
  | 'facultyName'
  | 'name'
  | 'code'
  | 'studentCount'
  | 'instructorCount'
  | 'programCount';
type SortDir = 'asc' | 'desc';
type ProgramStatus = 'all' | 'withPrograms' | 'empty';
type InstructorStatus = 'all' | 'withInstructors' | 'empty';
type StudentStatus = 'all' | 'withStudents' | 'empty';
type UnitType = 'all' | 'academic' | 'service';

interface FilterState {
  facultyId?: number;
  programStatus: ProgramStatus;
  instructorStatus: InstructorStatus;
  studentStatus: StudentStatus;
  unitType: UnitType;
}

const EMPTY_FILTERS: FilterState = {
  facultyId: undefined,
  programStatus: 'all',
  instructorStatus: 'all',
  studentStatus: 'all',
  unitType: 'all',
};

// ── Helpers ──────────────────────────────────────────────────────────────────

function extractErrorMessage(e: unknown, fallback: string): string {
  const resp = (e as { response?: { data?: { error?: string; message?: string; title?: string } } })?.response?.data;
  return resp?.error ?? resp?.message ?? resp?.title ?? (e instanceof Error ? e.message : fallback);
}

function countActiveFilters(f: FilterState): number {
  let n = 0;
  if (f.facultyId !== undefined) n++;
  if (f.programStatus !== 'all') n++;
  if (f.instructorStatus !== 'all') n++;
  if (f.studentStatus !== 'all') n++;
  if (f.unitType !== 'all') n++;
  return n;
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
  const [searchParams] = useSearchParams();
  const initialFacultyId = (() => {
    const raw = searchParams.get('facultyId');
    const n = raw ? Number(raw) : NaN;
    return Number.isFinite(n) && n > 0 ? n : undefined;
  })();

  const [filters, setFilters] = useState<FilterState>({
    ...EMPTY_FILTERS,
    facultyId: initialFacultyId,
  });

  const { data: departments = [], isLoading } = useAdminDepartmentsByStructure(filters.facultyId);

  const createDept = useCreateDepartment();
  const updateDept = useUpdateDepartment();
  const deleteDept = useDeleteDepartment();

  const [createOpen, setCreateOpen] = useState(false);
  const [editItem, setEditItem] = useState<DepartmentDto | null>(null);
  const [deleteItem, setDeleteItem] = useState<DepartmentDto | null>(null);
  const [detailItem, setDetailItem] = useState<DepartmentDto | null>(null);
  const [form, setForm] = useState<DepartmentFormState>(emptyDepartmentForm());
  const [formError, setFormError] = useState('');

  const [search, setSearch] = useState('');
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [sortKey, setSortKey] = useState<SortKey>('facultyName');
  const [sortDir, setSortDir] = useState<SortDir>('asc');

  const [successMessage, setSuccessMessage] = useState('');
  const [actionError, setActionError] = useState('');

  const dialogOpen = createOpen || !!editItem;
  const isSavePending = createDept.isPending || updateDept.isPending;
  const activeFilterCount = countActiveFilters(filters);
  const hasAnyConstraint = search.trim().length > 0 || activeFilterCount > 0;
  const hasFaculties = faculties.length > 0;

  const visibleDepartments = useMemo(() => {
    const q = search.trim().toLowerCase();
    let result = departments;

    if (q) {
      result = result.filter(d =>
        d.name.toLowerCase().includes(q) ||
        d.code.toLowerCase().includes(q) ||
        d.facultyName.toLowerCase().includes(q),
      );
    }

    if (filters.programStatus === 'withPrograms') result = result.filter(d => d.programCount > 0);
    else if (filters.programStatus === 'empty') result = result.filter(d => d.programCount === 0);

    if (filters.instructorStatus === 'withInstructors') result = result.filter(d => d.instructorCount > 0);
    else if (filters.instructorStatus === 'empty') result = result.filter(d => d.instructorCount === 0);

    if (filters.studentStatus === 'withStudents') result = result.filter(d => d.studentCount > 0);
    else if (filters.studentStatus === 'empty') result = result.filter(d => d.studentCount === 0);

    if (filters.unitType === 'academic') result = result.filter(d => !d.isServiceUnit);
    else if (filters.unitType === 'service') result = result.filter(d => d.isServiceUnit);

    const sorted = [...result].sort((a, b) => {
      let cmp = 0;
      if (sortKey === 'facultyName') {
        cmp = a.facultyName.localeCompare(b.facultyName, 'tr');
        if (cmp === 0) cmp = a.name.localeCompare(b.name, 'tr');
      } else if (sortKey === 'name') cmp = a.name.localeCompare(b.name, 'tr');
      else if (sortKey === 'code') cmp = a.code.localeCompare(b.code, 'tr');
      else if (sortKey === 'studentCount') cmp = a.studentCount - b.studentCount;
      else if (sortKey === 'instructorCount') cmp = a.instructorCount - b.instructorCount;
      else cmp = a.programCount - b.programCount;
      return sortDir === 'asc' ? cmp : -cmp;
    });
    return sorted;
  }, [departments, search, filters, sortKey, sortDir]);

  function toggleSort(key: SortKey) {
    if (sortKey === key) setSortDir(d => (d === 'asc' ? 'desc' : 'asc'));
    else { setSortKey(key); setSortDir('asc'); }
  }

  function showSuccess(msg: string) {
    setSuccessMessage(msg);
    setTimeout(() => setSuccessMessage(''), 3000);
  }

  function openCreate() {
    setForm(emptyDepartmentForm(filters.facultyId ?? faculties[0]?.id ?? 0));
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
      <div className="bg-white border border-slate-200 rounded-xl overflow-hidden">
        {/* Toolbar */}
        <div className="flex items-center gap-3 px-4 py-2 border-b border-slate-200 bg-slate-50/40 flex-wrap">
          <div className="relative shrink-0">
            <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground pointer-events-none" />
            <Input
              placeholder="Bölüm, kod veya fakülte ara..."
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

          <Button size="sm" className="h-8 shrink-0" onClick={openCreate} disabled={!hasFaculties}>
            <Plus className="h-4 w-4 mr-1" /> Bölüm Ekle
          </Button>
        </div>

        {/* Table */}
        <table className="w-full text-sm">
          <thead className="bg-slate-50 border-b border-slate-200">
            <tr>
              <th className="text-left px-4 py-3 font-semibold text-slate-600 w-48">
                <SortHeader label="Fakülte" active={sortKey === 'facultyName'} dir={sortDir} onClick={() => toggleSort('facultyName')} />
              </th>
              <th className="text-left px-4 py-3 font-semibold text-slate-600">
                <SortHeader label="Ad" active={sortKey === 'name'} dir={sortDir} onClick={() => toggleSort('name')} />
              </th>
              <th className="text-left px-4 py-3 font-semibold text-slate-600 w-28">
                <SortHeader label="Kod" active={sortKey === 'code'} dir={sortDir} onClick={() => toggleSort('code')} />
              </th>
              <th
                className="text-left px-4 py-3 font-semibold text-slate-600 w-28"
                title="Bölümün programlarına kayıtlı öğrenci sayısı"
              >
                <SortHeader label="Öğrenci" active={sortKey === 'studentCount'} dir={sortDir} onClick={() => toggleSort('studentCount')} />
              </th>
              <th
                className="text-left px-4 py-3 font-semibold text-slate-600 w-36"
                title="Bölüme atanmış öğretim üyesi (instructor) sayısı"
              >
                <SortHeader label="Öğretim Üyesi" active={sortKey === 'instructorCount'} dir={sortDir} onClick={() => toggleSort('instructorCount')} />
              </th>
              <th
                className="text-left px-4 py-3 font-semibold text-slate-600 w-28"
                title="Bölüme bağlı tanımlı program sayısı"
              >
                <SortHeader label="Program" active={sortKey === 'programCount'} dir={sortDir} onClick={() => toggleSort('programCount')} />
              </th>
              <th className="px-4 py-3 w-40" />
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {isLoading ? (
              <tr>
                <td colSpan={7} className="px-4 py-10 text-center text-muted-foreground">Yükleniyor...</td>
              </tr>
            ) : visibleDepartments.length === 0 ? (
              <tr>
                <td colSpan={7} className="px-4 py-14">
                  <div className="flex flex-col items-center gap-3 text-center">
                    <div>
                      <p className="font-medium text-foreground">
                        {hasAnyConstraint ? 'Kriterle eşleşen bölüm bulunamadı.' : 'Henüz bölüm tanımlı değil.'}
                      </p>
                      <p className="text-xs text-muted-foreground mt-1">
                        {hasAnyConstraint
                          ? 'Arama veya filtre kriterini değiştirebilirsiniz.'
                          : hasFaculties
                            ? 'İlk bölümü ekleyerek akademik yapıya başlayın.'
                            : 'Önce fakülte tanımlamalısınız.'}
                      </p>
                    </div>
                    {!hasAnyConstraint && hasFaculties && (
                      <Button size="sm" className="h-8" onClick={openCreate}>
                        <Plus className="h-4 w-4 mr-1" /> İlk Bölümü Ekle
                      </Button>
                    )}
                  </div>
                </td>
              </tr>
            ) : (
              visibleDepartments.map(d => {
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
                    className={`group cursor-pointer transition-colors border-l-2 focus:outline-none focus:bg-slate-50/80 ${
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
                            <AlertTriangle className="h-3.5 w-3.5 text-amber-500 shrink-0" />
                          </span>
                        )}
                        {hasInstructorGap && (
                          <span title="Bu bölüme atanmış öğretim üyesi yok">
                            <AlertTriangle className="h-3.5 w-3.5 text-amber-500 shrink-0" />
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <Badge variant="outline" className="font-mono text-xs tabular-nums uppercase tracking-wide">
                        {d.code}
                      </Badge>
                    </td>
                    <td className="px-4 py-3"><StudentCountBadge count={d.studentCount} /></td>
                    <td className="px-4 py-3"><InstructorCountBadge count={d.instructorCount} /></td>
                    <td className="px-4 py-3"><ProgramCountBadge count={d.programCount} /></td>
                    <td className="px-4 py-3">
                      <div
                        className="flex items-center gap-2 justify-end opacity-0 group-hover:opacity-100 focus-within:opacity-100 transition-opacity"
                        onClick={e => e.stopPropagation()}
                      >
                        <Button size="sm" variant="ghost" className="h-8 w-8 p-0 hover:bg-slate-200/60" title="Düzenle" onClick={() => openEdit(d)}>
                          <Edit2 className="h-3.5 w-3.5" />
                        </Button>
                        <div className="w-px h-5 bg-slate-200" />
                        <Button size="sm" variant="ghost" className="h-8 w-8 p-0 hover:bg-destructive/10" title="Sil" onClick={() => setDeleteItem(d)}>
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
                ? `${departments.length} bölümden ${visibleDepartments.length}'i gösteriliyor`
                : `${departments.length} bölüm`}
          </span>
          <p className="text-sm text-muted-foreground italic">
            Bölümlere bağlı programları tanımlamak için →{' '}
            <Link to={ROUTES.PROGRAM_DEFINITIONS} className="underline text-blue-600 not-italic">
              Program Tanımları
            </Link>
          </p>
        </div>
      </div>

      {/* Filter Dialog */}
      <Dialog open={filtersOpen} onOpenChange={setFiltersOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Filtreler</DialogTitle>
          </DialogHeader>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-1">
            {/* Faculty — full width */}
            <div className="space-y-1.5 sm:col-span-2">
              <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                Fakülte
              </label>
              <select
                className={SELECT_CLS}
                value={filters.facultyId ?? 0}
                onChange={e => setFilters({ ...filters, facultyId: Number(e.target.value) || undefined })}
              >
                <option value={0}>Tüm fakülteler</option>
                {faculties.map(f => (
                  <option key={f.id} value={f.id}>
                    {f.name} ({f.departmentCount})
                  </option>
                ))}
              </select>
            </div>

            {/* Birim Türü */}
            <div className="space-y-2">
              <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                Birim Türü
              </label>
              <div className="grid grid-cols-3 gap-1.5">
                {([
                  { value: 'all', label: 'Tümü' },
                  { value: 'academic', label: 'Akademik' },
                  { value: 'service', label: 'Koordinatörlük' },
                ] as { value: UnitType; label: string }[]).map(opt => (
                  <label
                    key={opt.value}
                    className={`flex items-center gap-2 text-sm cursor-pointer rounded-md border px-3 py-2 transition-colors ${
                      filters.unitType === opt.value
                        ? 'border-primary bg-primary/5'
                        : 'border-slate-200 bg-white hover:bg-slate-50'
                    }`}
                  >
                    <input
                      type="radio"
                      name="unit-type"
                      checked={filters.unitType === opt.value}
                      onChange={() => setFilters({ ...filters, unitType: opt.value })}
                      className="accent-primary"
                    />
                    <span className="truncate">{opt.label}</span>
                  </label>
                ))}
              </div>
            </div>

            {/* Program Durumu */}
            <div className="space-y-2">
              <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                Program Durumu
              </label>
              <div className="grid grid-cols-3 gap-1.5">
                {([
                  { value: 'all', label: 'Tümü' },
                  { value: 'withPrograms', label: 'Programı olan' },
                  { value: 'empty', label: 'Programı olmayan' },
                ] as { value: ProgramStatus; label: string }[]).map(opt => (
                  <label
                    key={opt.value}
                    className={`flex items-center gap-2 text-sm cursor-pointer rounded-md border px-3 py-2 transition-colors ${
                      filters.programStatus === opt.value
                        ? 'border-primary bg-primary/5'
                        : 'border-slate-200 bg-white hover:bg-slate-50'
                    }`}
                  >
                    <input
                      type="radio"
                      name="program-status"
                      checked={filters.programStatus === opt.value}
                      onChange={() => setFilters({ ...filters, programStatus: opt.value })}
                      className="accent-primary"
                    />
                    <span className="truncate">{opt.label}</span>
                  </label>
                ))}
              </div>
            </div>

            {/* Öğrenci Durumu */}
            <div className="space-y-2">
              <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                Öğrenci Durumu
              </label>
              <div className="grid grid-cols-3 gap-1.5">
                {([
                  { value: 'all', label: 'Tümü' },
                  { value: 'withStudents', label: 'Öğrencisi olan' },
                  { value: 'empty', label: 'Öğrencisi olmayan' },
                ] as { value: StudentStatus; label: string }[]).map(opt => (
                  <label
                    key={opt.value}
                    className={`flex items-center gap-2 text-sm cursor-pointer rounded-md border px-3 py-2 transition-colors ${
                      filters.studentStatus === opt.value
                        ? 'border-primary bg-primary/5'
                        : 'border-slate-200 bg-white hover:bg-slate-50'
                    }`}
                  >
                    <input
                      type="radio"
                      name="student-status"
                      checked={filters.studentStatus === opt.value}
                      onChange={() => setFilters({ ...filters, studentStatus: opt.value })}
                      className="accent-primary"
                    />
                    <span className="truncate">{opt.label}</span>
                  </label>
                ))}
              </div>
            </div>

            {/* Kadro Durumu */}
            <div className="space-y-2">
              <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                Kadro Durumu
              </label>
              <div className="grid grid-cols-3 gap-1.5">
                {([
                  { value: 'all', label: 'Tümü' },
                  { value: 'withInstructors', label: 'Hocası olan' },
                  { value: 'empty', label: 'Hocası olmayan' },
                ] as { value: InstructorStatus; label: string }[]).map(opt => (
                  <label
                    key={opt.value}
                    className={`flex items-center gap-2 text-sm cursor-pointer rounded-md border px-3 py-2 transition-colors ${
                      filters.instructorStatus === opt.value
                        ? 'border-primary bg-primary/5'
                        : 'border-slate-200 bg-white hover:bg-slate-50'
                    }`}
                  >
                    <input
                      type="radio"
                      name="instructor-status"
                      checked={filters.instructorStatus === opt.value}
                      onChange={() => setFilters({ ...filters, instructorStatus: opt.value })}
                      className="accent-primary"
                    />
                    <span className="truncate">{opt.label}</span>
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

      <DeleteConfirmDialog
        open={!!deleteItem}
        onClose={() => setDeleteItem(null)}
        onConfirm={handleDelete}
        name={deleteItem?.name ?? ''}
        isPending={deleteDept.isPending}
      />

      {/* Detail Modal (page-like) */}
      <Dialog open={!!detailItem} onOpenChange={o => !o && setDetailItem(null)}>
        <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
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
        </DialogContent>
      </Dialog>
    </div>
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

// ── Department Detail Panel (Dialog content, page-like) ──────────────────────

interface DepartmentDetailPanelProps {
  department: DepartmentDto;
  onEdit: () => void;
  onDelete: () => void;
}

function DepartmentDetailPanel({ department, onEdit, onDelete }: DepartmentDetailPanelProps) {
  const { data: programs = [], isLoading } = useAdminProgramsByStructure(department.id);
  const hasProgramGap = department.programCount === 0 && !department.isServiceUnit;
  const hasInstructorGap = department.instructorCount === 0;
  const totalCurricula = programs.reduce((sum, p) => sum + (p.curriculumCount ?? 0), 0);
  const studentPerInstructor = department.instructorCount > 0
    ? (department.studentCount / department.instructorCount).toFixed(1)
    : '—';

  return (
    <>
      <DialogHeader className="pr-10 border-b border-slate-200 pb-4">
        <div className="flex flex-wrap items-center gap-3">
          <DialogTitle className="text-2xl">{department.name}</DialogTitle>
          <Badge variant="outline" className="font-mono text-xs tabular-nums uppercase tracking-wide">
            {department.code}
          </Badge>
          {department.isServiceUnit && (
            <Badge className="bg-violet-50 text-violet-700 border border-violet-200 hover:bg-violet-50 text-xs font-normal">
              Koordinatörlük
            </Badge>
          )}
          <span className="text-sm text-muted-foreground">· {department.facultyName}</span>
        </div>
        <p className="text-sm text-muted-foreground mt-1">Bölüm detayları, kadro ve bağlı programlar</p>
        {(hasProgramGap || hasInstructorGap) && (
          <div className="mt-3 flex flex-wrap gap-2">
            {hasProgramGap && (
              <span className="inline-flex items-center gap-1 text-xs text-amber-700 bg-amber-50 border border-amber-200 px-2 py-0.5 rounded">
                <AlertTriangle className="h-3 w-3" />
                Program tanımlı değil
              </span>
            )}
            {hasInstructorGap && (
              <span className="inline-flex items-center gap-1 text-xs text-amber-700 bg-amber-50 border border-amber-200 px-2 py-0.5 rounded">
                <AlertTriangle className="h-3 w-3" />
                Öğretim üyesi atanmamış
              </span>
            )}
          </div>
        )}
      </DialogHeader>

      <div className="grid grid-cols-1 md:grid-cols-5 gap-5 pt-2">
        {/* Sol kolon — Genel bilgiler + Kadro + Başkan placeholder */}
        <section className="md:col-span-2 space-y-5">
          <div>
            <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-3">
              Genel Bilgiler
            </h3>
            <dl className="divide-y divide-slate-100 rounded-lg border border-slate-200 bg-slate-50/40">
              <div className="flex justify-between px-3 py-2.5 text-sm">
                <dt className="text-muted-foreground">Bölüm Adı</dt>
                <dd className="font-medium text-foreground text-right">{department.name}</dd>
              </div>
              <div className="flex justify-between px-3 py-2.5 text-sm">
                <dt className="text-muted-foreground">Kod</dt>
                <dd className="font-mono uppercase tracking-wide">{department.code}</dd>
              </div>
              <div className="flex justify-between px-3 py-2.5 text-sm">
                <dt className="text-muted-foreground">Fakülte</dt>
                <dd className="text-right">{department.facultyName}</dd>
              </div>
              <div className="flex justify-between px-3 py-2.5 text-sm">
                <dt className="text-muted-foreground">Birim Türü</dt>
                <dd className="text-right">{department.isServiceUnit ? 'Koordinatörlük' : 'Akademik bölüm'}</dd>
              </div>
              <div className="flex justify-between px-3 py-2.5 text-sm">
                <dt className="text-muted-foreground">ID</dt>
                <dd className="font-mono tabular-nums text-muted-foreground">#{department.id}</dd>
              </div>
            </dl>
          </div>

          <div>
            <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-3">
              Kadro ve İstatistikler
            </h3>
            <div className="grid grid-cols-2 gap-2">
              <StatCard label="Öğrenci" value={department.studentCount} tone={department.studentCount === 0 ? 'muted' : 'emerald'} />
              <StatCard label="Öğretim Üyesi" value={department.instructorCount} tone={department.instructorCount === 0 ? 'amber' : 'blue'} />
              <StatCard label="Program" value={department.programCount} tone={department.programCount === 0 && !department.isServiceUnit ? 'amber' : 'blue'} />
              <StatCard label="Müfredat" value={isLoading ? '—' : totalCurricula} tone="blue" />
            </div>
            <p className="text-xs text-muted-foreground mt-2 px-1">
              Öğrenci / Hoca oranı:{' '}
              <span className="font-semibold text-foreground tabular-nums">{studentPerInstructor}</span>
            </p>
          </div>

          <div>
            <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-3">
              Bölüm Başkanı
            </h3>
            <div className="rounded-lg border border-dashed border-slate-300 bg-slate-50/50 p-3 text-sm text-muted-foreground">
              Başkan atama özelliği yakında eklenecek.
            </div>
          </div>
        </section>

        {/* Sağ kolon — Bağlı programlar */}
        <section className="md:col-span-3 space-y-3">
          <div className="flex items-center justify-between">
            <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
              Bağlı Programlar
              <span className="ml-1.5 text-muted-foreground/80 tabular-nums">
                ({department.programCount})
              </span>
            </h3>
            <Link
              to={`${ROUTES.PROGRAM_DEFINITIONS}?departmentId=${department.id}`}
              className="text-xs text-blue-600 hover:underline"
            >
              Program sayfasında aç →
            </Link>
          </div>

          {isLoading ? (
            <p className="text-sm text-muted-foreground py-4 text-center">Yükleniyor...</p>
          ) : programs.length === 0 ? (
            <div className="rounded-lg border border-dashed border-amber-300 bg-amber-50/30 p-4 text-sm text-amber-900">
              {department.isServiceUnit
                ? 'Koordinatörlük birimlerinde program bulunmaz. Yalnızca ders ve kadro taşır.'
                : (
                  <>
                    Bu bölüme henüz program eklenmemiş. Program eklemek için{' '}
                    <Link to={ROUTES.PROGRAM_DEFINITIONS} className="underline font-medium">
                      Program Tanımları
                    </Link>{' '}
                    sayfasına gidin.
                  </>
                )}
            </div>
          ) : (
            <ul className="space-y-1.5 max-h-96 overflow-y-auto pr-1">
              {programs.map(p => (
                <li
                  key={p.id}
                  className="flex items-center justify-between rounded-md border border-slate-200 bg-white px-3 py-2 text-sm hover:border-slate-300 transition-colors gap-3"
                >
                  <div className="min-w-0">
                    <p className="font-medium text-foreground truncate">{p.name}</p>
                    <div className="flex items-center gap-1.5 mt-0.5 flex-wrap">
                      <span className="text-xs text-muted-foreground font-mono uppercase tracking-wide">
                        {p.code}
                      </span>
                      <span className="text-xs text-muted-foreground">·</span>
                      <span className="text-xs text-muted-foreground">{p.degreeLevel}</span>
                      <span className="text-xs text-muted-foreground">·</span>
                      <span className="text-xs text-muted-foreground">{p.educationType}</span>
                    </div>
                  </div>
                  <Badge
                    className={
                      (p.curriculumCount ?? 0) > 0
                        ? 'bg-blue-50 text-blue-700 border border-blue-200 hover:bg-blue-50 tabular-nums shrink-0'
                        : 'bg-slate-100 text-slate-500 border border-slate-200 hover:bg-slate-100 tabular-nums font-normal shrink-0'
                    }
                  >
                    {p.curriculumCount ?? 0} müfredat
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

// ── Stat card ────────────────────────────────────────────────────────────────

type StatTone = 'blue' | 'emerald' | 'amber' | 'muted';

function StatCard({ label, value, tone }: { label: string; value: number | string; tone: StatTone }) {
  const toneClass: Record<StatTone, string> = {
    blue: 'border-slate-200 bg-white',
    emerald: 'border-emerald-200 bg-emerald-50/40',
    amber: 'border-amber-200 bg-amber-50/40',
    muted: 'border-slate-200 bg-slate-50',
  };
  const valueToneClass: Record<StatTone, string> = {
    blue: 'text-foreground',
    emerald: 'text-emerald-800',
    amber: 'text-amber-800',
    muted: 'text-slate-500',
  };
  return (
    <div className={`rounded-lg border px-3 py-2.5 ${toneClass[tone]}`}>
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className={`text-2xl font-semibold tabular-nums mt-0.5 ${valueToneClass[tone]}`}>{value}</div>
    </div>
  );
}
