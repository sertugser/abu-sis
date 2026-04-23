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
  useAdminProgramsByStructure,
  useCreateProgram,
  useUpdateProgram,
  useDeleteProgram,
  type DepartmentDto,
  type ProgramDto,
} from '../hooks/useAdminAcademicStructure';
import {
  ProgramFormFields,
  DeleteConfirmDialog,
} from '../components/academic-structure/shared';
import {
  DEGREE_LEVELS,
  EDUCATION_TYPES,
  emptyProgramForm,
  type ProgramFormState,
} from '../components/academic-structure/shared-constants';
import { ROUTES } from '@/lib/constants';

// ── Types ────────────────────────────────────────────────────────────────────

type SortKey = 'name' | 'code' | 'degreeLevel' | 'totalRequiredEcts' | 'curriculumCount';
type SortDir = 'asc' | 'desc';
type CurriculumFilter = 'all' | 'has' | 'none';

interface FilterState {
  degreeLevel: string | 'all';
  educationType: string | 'all';
  curriculumStatus: CurriculumFilter;
}

const EMPTY_FILTERS: FilterState = {
  degreeLevel: 'all',
  educationType: 'all',
  curriculumStatus: 'all',
};

const SELECT_CLS = 'h-8 rounded-md border border-slate-200 bg-white px-2.5 text-sm text-slate-700 shrink-0';

// ── Helpers ──────────────────────────────────────────────────────────────────

function extractErrorMessage(e: unknown, fallback: string): string {
  const resp = (e as { response?: { data?: { error?: string; message?: string; title?: string } } })?.response?.data;
  return resp?.error ?? resp?.message ?? resp?.title ?? (e instanceof Error ? e.message : fallback);
}

function countActiveFilters(
  f: FilterState,
  facultyId: number | undefined,
  departmentId: number | undefined,
): number {
  let n = 0;
  if (facultyId !== undefined) n++;
  if (departmentId !== undefined) n++;
  if (f.degreeLevel !== 'all') n++;
  if (f.educationType !== 'all') n++;
  if (f.curriculumStatus !== 'all') n++;
  return n;
}

function degreeLabel(value: string): string {
  return DEGREE_LEVELS.find(d => d.value === value)?.label ?? value;
}

function educationLabel(value: string): string {
  return EDUCATION_TYPES.find(t => t.value === value)?.label ?? value;
}

function CurriculumCountBadge({ count }: { count: number }) {
  if (count === 0) {
    return (
      <Badge className="bg-slate-100 text-slate-500 border border-slate-200 hover:bg-slate-100 tabular-nums font-normal">
        0 müfredat
      </Badge>
    );
  }
  if (count >= 3) {
    return (
      <Badge className="bg-blue-100 text-blue-800 border border-blue-300 hover:bg-blue-100 tabular-nums">
        {count} müfredat
      </Badge>
    );
  }
  return (
    <Badge className="bg-blue-50 text-blue-700 border border-blue-200 hover:bg-blue-50 tabular-nums">
      {count} müfredat
    </Badge>
  );
}

// ── Main Page ────────────────────────────────────────────────────────────────

export default function ProgramDefinitionsPage() {
  const { data: faculties = [] } = useAdminFaculties();
  const { data: allDepartments = [] } = useAdminDepartmentsByStructure(undefined);

  const [facultyFilter, setFacultyFilter] = useState<number | undefined>(undefined);
  const [departmentFilter, setDepartmentFilter] = useState<number | undefined>(undefined);

  // Server narrows by department; everything else client-side
  const { data: programs = [], isLoading } = useAdminProgramsByStructure(departmentFilter);

  const createProgram = useCreateProgram();
  const updateProgram = useUpdateProgram();
  const deleteProgram = useDeleteProgram();

  const [createOpen, setCreateOpen] = useState(false);
  const [editItem, setEditItem] = useState<ProgramDto | null>(null);
  const [deleteItem, setDeleteItem] = useState<ProgramDto | null>(null);
  const [detailItem, setDetailItem] = useState<ProgramDto | null>(null);
  const [form, setForm] = useState<ProgramFormState>(emptyProgramForm());
  const [formError, setFormError] = useState('');

  const [search, setSearch] = useState('');
  const [filters, setFilters] = useState<FilterState>(EMPTY_FILTERS);
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [sortKey, setSortKey] = useState<SortKey>('name');
  const [sortDir, setSortDir] = useState<SortDir>('asc');

  const [successMessage, setSuccessMessage] = useState('');
  const [actionError, setActionError] = useState('');

  const dialogOpen = createOpen || !!editItem;
  const isSavePending = createProgram.isPending || updateProgram.isPending;
  const activeFilterCount = countActiveFilters(filters, facultyFilter, departmentFilter);
  const hasAnyConstraint = search.trim().length > 0 || activeFilterCount > 0;

  // Department dropdown narrows by selected faculty
  const visibleDepartments = useMemo(() => {
    if (!facultyFilter) return allDepartments;
    return allDepartments.filter(d => d.facultyId === facultyFilter);
  }, [allDepartments, facultyFilter]);

  // Department → DepartmentDto lookup (for faculty breadcrumb in rows/details)
  const deptById = useMemo(() => {
    const m = new Map<number, DepartmentDto>();
    allDepartments.forEach(d => m.set(d.id, d));
    return m;
  }, [allDepartments]);

  const visiblePrograms = useMemo(() => {
    const q = search.trim().toLowerCase();
    let result = programs;

    // Client-side faculty filter (backend doesn't support it)
    if (facultyFilter) {
      const deptIds = new Set(visibleDepartments.map(d => d.id));
      result = result.filter(p => deptIds.has(p.departmentId));
    }

    if (q) {
      result = result.filter(p =>
        p.name.toLowerCase().includes(q) || p.code.toLowerCase().includes(q),
      );
    }

    if (filters.degreeLevel !== 'all') {
      result = result.filter(p => p.degreeLevel === filters.degreeLevel);
    }
    if (filters.educationType !== 'all') {
      result = result.filter(p => p.educationType === filters.educationType);
    }
    if (filters.curriculumStatus === 'has') {
      result = result.filter(p => p.curriculumCount > 0);
    } else if (filters.curriculumStatus === 'none') {
      result = result.filter(p => p.curriculumCount === 0);
    }

    const sorted = [...result].sort((a, b) => {
      let cmp = 0;
      if (sortKey === 'name') cmp = a.name.localeCompare(b.name, 'tr');
      else if (sortKey === 'code') cmp = a.code.localeCompare(b.code, 'tr');
      else if (sortKey === 'degreeLevel') cmp = a.degreeLevel.localeCompare(b.degreeLevel);
      else if (sortKey === 'totalRequiredEcts') cmp = a.totalRequiredEcts - b.totalRequiredEcts;
      else cmp = a.curriculumCount - b.curriculumCount;
      return sortDir === 'asc' ? cmp : -cmp;
    });
    return sorted;
  }, [programs, facultyFilter, visibleDepartments, search, filters, sortKey, sortDir]);

  function toggleSort(key: SortKey) {
    if (sortKey === key) setSortDir(d => (d === 'asc' ? 'desc' : 'asc'));
    else { setSortKey(key); setSortDir('asc'); }
  }

  function showSuccess(msg: string) {
    setSuccessMessage(msg);
    setTimeout(() => setSuccessMessage(''), 3000);
  }

  function openCreate() {
    setForm(emptyProgramForm(departmentFilter ?? visibleDepartments[0]?.id ?? 0));
    setFormError('');
    setEditItem(null);
    setCreateOpen(true);
  }

  function openEdit(p: ProgramDto) {
    setForm({
      name: p.name,
      code: p.code,
      departmentId: p.departmentId,
      degreeLevel: p.degreeLevel,
      educationType: p.educationType,
      maxStudyDurationSemesters: p.maxStudyDurationSemesters,
      totalRequiredEcts: p.totalRequiredEcts,
    });
    setFormError('');
    setCreateOpen(false);
    setEditItem(p);
  }

  function closeDialog() {
    setCreateOpen(false);
    setEditItem(null);
    setFormError('');
  }

  async function handleSubmit() {
    if (!form.name.trim() || !form.code.trim() || !form.departmentId) {
      setFormError('Tüm alanlar zorunludur.');
      return;
    }
    try {
      if (editItem) {
        await updateProgram.mutateAsync({ id: editItem.id, ...form });
        showSuccess('Program güncellendi.');
      } else {
        await createProgram.mutateAsync(form);
        showSuccess('Program eklendi.');
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
      await deleteProgram.mutateAsync(target.id);
      setDeleteItem(null);
      showSuccess('Program silindi.');
    } catch (e: unknown) {
      setDeleteItem(null);
      setActionError(extractErrorMessage(e, `"${target.name}" silinemedi.`));
    }
  }

  function clearAll() {
    setSearch('');
    setFilters(EMPTY_FILTERS);
    setFacultyFilter(undefined);
    setDepartmentFilter(undefined);
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
              className="pl-8 h-8 w-64 text-sm"
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

          <Button size="sm" className="h-8 shrink-0" onClick={openCreate} disabled={visibleDepartments.length === 0}>
            <Plus className="h-4 w-4 mr-1" /> Program Ekle
          </Button>
        </div>

        <table className="w-full text-sm">
          <thead className="bg-slate-50 border-b border-slate-200">
            <tr>
              <th className="text-left px-4 py-3 font-semibold text-slate-600">
                <SortHeader label="Ad" active={sortKey === 'name'} dir={sortDir} onClick={() => toggleSort('name')} />
              </th>
              <th className="text-left px-4 py-3 font-semibold text-slate-600 w-28">
                <SortHeader label="Kod" active={sortKey === 'code'} dir={sortDir} onClick={() => toggleSort('code')} />
              </th>
              <th className="text-left px-4 py-3 font-semibold text-slate-600 w-60">Fakülte / Bölüm</th>
              <th className="text-left px-4 py-3 font-semibold text-slate-600 w-36">
                <SortHeader label="Derece" active={sortKey === 'degreeLevel'} dir={sortDir} onClick={() => toggleSort('degreeLevel')} />
              </th>
              <th className="text-left px-4 py-3 font-semibold text-slate-600 w-28">Öğretim</th>
              <th className="text-left px-4 py-3 font-semibold text-slate-600 w-24">Süre</th>
              <th className="text-left px-4 py-3 font-semibold text-slate-600 w-24">
                <SortHeader label="AKTS" active={sortKey === 'totalRequiredEcts'} dir={sortDir} onClick={() => toggleSort('totalRequiredEcts')} />
              </th>
              <th className="text-left px-4 py-3 font-semibold text-slate-600 w-36">
                <SortHeader label="Müfredat" active={sortKey === 'curriculumCount'} dir={sortDir} onClick={() => toggleSort('curriculumCount')} />
              </th>
              <th className="px-4 py-3 w-44" />
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {isLoading ? (
              <tr>
                <td colSpan={9} className="px-4 py-10 text-center text-muted-foreground">Yükleniyor...</td>
              </tr>
            ) : visiblePrograms.length === 0 ? (
              <tr>
                <td colSpan={9} className="px-4 py-14">
                  <div className="flex flex-col items-center gap-3 text-center">
                    <div>
                      <p className="font-medium text-foreground">
                        {hasAnyConstraint ? 'Kriterle eşleşen program bulunamadı.' : 'Henüz program tanımlı değil.'}
                      </p>
                      <p className="text-xs text-muted-foreground mt-1">
                        {hasAnyConstraint
                          ? 'Arama veya filtre kriterini değiştirebilirsiniz.'
                          : 'İlk programı ekleyerek akademik yapıyı tamamlayın.'}
                      </p>
                    </div>
                    {!hasAnyConstraint && (
                      <Button size="sm" className="h-8" onClick={openCreate} disabled={visibleDepartments.length === 0}>
                        <Plus className="h-4 w-4 mr-1" /> İlk Programı Ekle
                      </Button>
                    )}
                  </div>
                </td>
              </tr>
            ) : (
              visiblePrograms.map(p => {
                const dept = deptById.get(p.departmentId);
                const facultyName = dept?.facultyName ?? '—';
                const isEmpty = p.curriculumCount === 0;
                return (
                  <tr
                    key={p.id}
                    role="button"
                    tabIndex={0}
                    onClick={() => setDetailItem(p)}
                    onKeyDown={e => {
                      if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        setDetailItem(p);
                      }
                    }}
                    title={`${p.name} — detayları göster`}
                    className={`group cursor-pointer transition-colors border-l-2 focus:outline-none focus:bg-slate-50/80 ${
                      isEmpty
                        ? 'border-l-amber-300/70 bg-amber-50/20 hover:bg-amber-50/40'
                        : 'border-l-transparent hover:bg-slate-50/60'
                    }`}
                  >
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2 font-medium text-foreground">
                        {p.name}
                        {isEmpty && (
                          <span title="Bu program için henüz müfredat eklenmemiş">
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
                        {p.code}
                      </Badge>
                    </td>
                    <td className="px-4 py-3 text-muted-foreground text-xs truncate">
                      {facultyName} / {p.departmentName}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">{degreeLabel(p.degreeLevel)}</td>
                    <td className="px-4 py-3 text-muted-foreground">{educationLabel(p.educationType)}</td>
                    <td className="px-4 py-3 text-muted-foreground tabular-nums">{p.maxStudyDurationSemesters} dönem</td>
                    <td className="px-4 py-3 text-muted-foreground tabular-nums">{p.totalRequiredEcts}</td>
                    <td className="px-4 py-3">
                      <CurriculumCountBadge count={p.curriculumCount} />
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
                          onClick={() => openEdit(p)}
                        >
                          <Edit2 className="h-3.5 w-3.5" />
                        </Button>
                        <div className="w-px h-5 bg-slate-200" />
                        <Button
                          size="sm"
                          variant="ghost"
                          className="h-8 w-8 p-0 hover:bg-destructive/10"
                          title="Sil"
                          onClick={() => setDeleteItem(p)}
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
                ? `${programs.length} programdan ${visiblePrograms.length}'i gösteriliyor`
                : `${programs.length} program`}
          </span>
          <p className="text-sm text-muted-foreground italic">
            Programlara bağlı müfredatları yönetmek için →{' '}
            <Link to={ROUTES.CURRICULUM_MANAGEMENT} className="underline text-blue-600 not-italic">
              Müfredat İşlemleri
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
            {/* Faculty */}
            <div className="space-y-1.5">
              <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                Fakülte
              </label>
              <select
                className={SELECT_CLS + ' w-full'}
                value={facultyFilter ?? ''}
                onChange={e => {
                  setFacultyFilter(e.target.value ? Number(e.target.value) : undefined);
                  setDepartmentFilter(undefined);
                }}
              >
                <option value="">Tüm Fakülteler</option>
                {faculties.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}
              </select>
            </div>

            {/* Department */}
            <div className="space-y-1.5">
              <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                Bölüm
              </label>
              <select
                className={SELECT_CLS + ' w-full'}
                value={departmentFilter ?? ''}
                onChange={e => setDepartmentFilter(e.target.value ? Number(e.target.value) : undefined)}
              >
                <option value="">Tüm Bölümler</option>
                {visibleDepartments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
              </select>
            </div>

            {/* Degree level */}
            <div className="space-y-2">
              <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                Derece Düzeyi
              </label>
              <div className="flex flex-col gap-0.5 rounded-md border border-slate-200 bg-slate-50/40 p-1.5">
                <label className="flex items-center gap-2 text-sm cursor-pointer rounded px-2 py-1 hover:bg-white">
                  <input
                    type="radio"
                    name="degree-filter"
                    checked={filters.degreeLevel === 'all'}
                    onChange={() => setFilters({ ...filters, degreeLevel: 'all' })}
                    className="accent-primary"
                  />
                  <span>Tümü</span>
                </label>
                {DEGREE_LEVELS.map(d => (
                  <label
                    key={d.value}
                    className="flex items-center gap-2 text-sm cursor-pointer rounded px-2 py-1 hover:bg-white"
                  >
                    <input
                      type="radio"
                      name="degree-filter"
                      checked={filters.degreeLevel === d.value}
                      onChange={() => setFilters({ ...filters, degreeLevel: d.value })}
                      className="accent-primary"
                    />
                    <span>{d.label}</span>
                  </label>
                ))}
              </div>
            </div>

            {/* Education type */}
            <div className="space-y-2">
              <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                Eğitim Türü
              </label>
              <div className="flex flex-col gap-0.5 rounded-md border border-slate-200 bg-slate-50/40 p-1.5">
                <label className="flex items-center gap-2 text-sm cursor-pointer rounded px-2 py-1 hover:bg-white">
                  <input
                    type="radio"
                    name="edu-filter"
                    checked={filters.educationType === 'all'}
                    onChange={() => setFilters({ ...filters, educationType: 'all' })}
                    className="accent-primary"
                  />
                  <span>Tümü</span>
                </label>
                {EDUCATION_TYPES.map(t => (
                  <label
                    key={t.value}
                    className="flex items-center gap-2 text-sm cursor-pointer rounded px-2 py-1 hover:bg-white"
                  >
                    <input
                      type="radio"
                      name="edu-filter"
                      checked={filters.educationType === t.value}
                      onChange={() => setFilters({ ...filters, educationType: t.value })}
                      className="accent-primary"
                    />
                    <span>{t.label}</span>
                  </label>
                ))}
              </div>
            </div>

            {/* Curriculum status — full width on sm+ */}
            <div className="space-y-2 sm:col-span-2">
              <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                Müfredat Durumu
              </label>
              <div className="grid grid-cols-3 gap-1.5">
                {([
                  { value: 'all', label: 'Tümü' },
                  { value: 'has', label: 'Müfredatı olan' },
                  { value: 'none', label: 'Müfredatı olmayan' },
                ] as { value: CurriculumFilter; label: string }[]).map(opt => (
                  <label
                    key={opt.value}
                    className={`flex items-center gap-2 text-sm cursor-pointer rounded-md border px-3 py-2 transition-colors ${
                      filters.curriculumStatus === opt.value
                        ? 'border-primary bg-primary/5'
                        : 'border-slate-200 bg-white hover:bg-slate-50'
                    }`}
                  >
                    <input
                      type="radio"
                      name="curriculum-filter"
                      checked={filters.curriculumStatus === opt.value}
                      onChange={() => setFilters({ ...filters, curriculumStatus: opt.value })}
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
              onClick={() => {
                setFilters(EMPTY_FILTERS);
                setFacultyFilter(undefined);
                setDepartmentFilter(undefined);
              }}
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
            <DialogTitle>{editItem ? 'Program Düzenle' : 'Program Ekle'}</DialogTitle>
          </DialogHeader>
          <ProgramFormFields
            form={form}
            onChange={f => { setForm(f); setFormError(''); }}
            departments={editItem ? allDepartments : (visibleDepartments.length > 0 ? visibleDepartments : allDepartments)}
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
        isPending={deleteProgram.isPending}
      />

      <Dialog open={!!detailItem} onOpenChange={o => !o && setDetailItem(null)}>
        <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
          {detailItem && (
            <ProgramDetailPanel
              program={detailItem}
              faculty={deptById.get(detailItem.departmentId)?.facultyName ?? '—'}
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

// ── Program Detail Panel ─────────────────────────────────────────────────────

interface ProgramDetailPanelProps {
  program: ProgramDto;
  faculty: string;
  onEdit: () => void;
  onDelete: () => void;
}

function ProgramDetailPanel({ program, faculty, onEdit, onDelete }: ProgramDetailPanelProps) {
  const isEmpty = program.curriculumCount === 0;

  return (
    <>
      <DialogHeader className="pr-10 border-b border-slate-200 pb-4">
        <div className="flex flex-wrap items-center gap-3">
          <DialogTitle className="text-2xl">{program.name}</DialogTitle>
          <Badge variant="outline" className="font-mono text-xs tabular-nums uppercase tracking-wide">
            {program.code}
          </Badge>
          {isEmpty && (
            <span className="inline-flex items-center gap-1 text-xs text-amber-700 bg-amber-50 border border-amber-200 px-2 py-0.5 rounded">
              <AlertTriangle className="h-3 w-3" />
              Müfredat tanımlı değil
            </span>
          )}
        </div>
        <p className="text-sm text-muted-foreground mt-1">{faculty} · {program.departmentName}</p>
      </DialogHeader>

      <div className="grid grid-cols-1 md:grid-cols-5 gap-5 pt-2">
        {/* Left — General info */}
        <section className="md:col-span-3 space-y-5">
          <div>
            <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-3">
              Genel Bilgiler
            </h3>
            <dl className="divide-y divide-slate-100 rounded-lg border border-slate-200 bg-slate-50/40">
              <div className="flex justify-between px-3 py-2.5 text-sm">
                <dt className="text-muted-foreground">Program Adı</dt>
                <dd className="font-medium text-foreground text-right">{program.name}</dd>
              </div>
              <div className="flex justify-between px-3 py-2.5 text-sm">
                <dt className="text-muted-foreground">Kod</dt>
                <dd className="font-mono uppercase tracking-wide">{program.code}</dd>
              </div>
              <div className="flex justify-between px-3 py-2.5 text-sm">
                <dt className="text-muted-foreground">Fakülte</dt>
                <dd className="text-foreground text-right">{faculty}</dd>
              </div>
              <div className="flex justify-between px-3 py-2.5 text-sm">
                <dt className="text-muted-foreground">Bölüm</dt>
                <dd className="text-foreground text-right">{program.departmentName}</dd>
              </div>
              <div className="flex justify-between px-3 py-2.5 text-sm">
                <dt className="text-muted-foreground">Derece Düzeyi</dt>
                <dd className="text-foreground text-right">{degreeLabel(program.degreeLevel)}</dd>
              </div>
              <div className="flex justify-between px-3 py-2.5 text-sm">
                <dt className="text-muted-foreground">Eğitim Türü</dt>
                <dd className="text-foreground text-right">{educationLabel(program.educationType)}</dd>
              </div>
              <div className="flex justify-between px-3 py-2.5 text-sm">
                <dt className="text-muted-foreground">ID</dt>
                <dd className="font-mono tabular-nums text-muted-foreground">#{program.id}</dd>
              </div>
            </dl>
          </div>
        </section>

        {/* Right — Stats + curriculum link */}
        <section className="md:col-span-2 space-y-5">
          <div>
            <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-3">
              Hızlı İstatistikler
            </h3>
            <div className="grid grid-cols-2 gap-2">
              <div className="rounded-lg border border-slate-200 bg-white px-3 py-2.5">
                <div className="text-xs text-muted-foreground">Maks. Süre</div>
                <div className="text-2xl font-semibold tabular-nums mt-0.5">
                  {program.maxStudyDurationSemesters}
                </div>
                <div className="text-[11px] text-muted-foreground -mt-0.5">dönem</div>
              </div>
              <div className="rounded-lg border border-slate-200 bg-white px-3 py-2.5">
                <div className="text-xs text-muted-foreground">Toplam AKTS</div>
                <div className="text-2xl font-semibold tabular-nums mt-0.5">
                  {program.totalRequiredEcts}
                </div>
              </div>
              <div className="col-span-2 rounded-lg border border-slate-200 bg-white px-3 py-2.5">
                <div className="text-xs text-muted-foreground">Müfredat Sayısı</div>
                <div className="text-2xl font-semibold tabular-nums mt-0.5">
                  {program.curriculumCount}
                </div>
              </div>
            </div>
          </div>

          <div>
            <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-3">
              İlgili Sayfa
            </h3>
            {isEmpty ? (
              <div className="rounded-lg border border-dashed border-amber-300 bg-amber-50/30 p-3 text-sm text-amber-900">
                Bu programa henüz müfredat eklenmemiş.{' '}
                <Link to={ROUTES.CURRICULUM_MANAGEMENT} className="underline font-medium">
                  Müfredat İşlemleri
                </Link>{' '}
                sayfasından müfredat tanımlayabilirsiniz.
              </div>
            ) : (
              <Link
                to={ROUTES.CURRICULUM_MANAGEMENT}
                className="block rounded-lg border border-slate-200 bg-white px-3 py-2.5 text-sm hover:border-slate-300 transition-colors"
              >
                <p className="font-medium text-foreground">Müfredat İşlemleri →</p>
                <p className="text-xs text-muted-foreground mt-0.5">
                  {program.curriculumCount} müfredatı yönet
                </p>
              </Link>
            )}
          </div>
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
