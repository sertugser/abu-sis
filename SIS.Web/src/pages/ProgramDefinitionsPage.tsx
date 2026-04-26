import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  AlertTriangle,
  ArrowDown,
  ArrowUp,
  ArrowUpDown,
  CheckCircle2,
  ChevronRight,
  Edit2,
  Link2,
  Plus,
  Search,
  SlidersHorizontal,
  Trash2,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import {
  useAdminDepartmentsByStructure,
  useAdminFaculties,
  useAdminProgramsByStructure,
  useCreateProgram,
  useDeleteProgram,
  useUpdateProgram,
  type DepartmentDto,
  type ProgramDto,
  type ProgramUpsertDto,
} from '../hooks/useAdminAcademicStructure';
import { DeleteConfirmDialog, ProgramFormFields } from '../components/academic-structure/shared';
import {
  DEGREE_LEVELS,
  EDUCATION_TYPES,
  emptyProgramForm,
  type ProgramFormState,
} from '../components/academic-structure/shared-constants';
import { ROUTES } from '@/lib/constants';

type SortKey = 'name' | 'code' | 'degreeLevel' | 'totalRequiredEcts' | 'curriculumCount';
type SortDir = 'asc' | 'desc';
type CurriculumFilter = 'all' | 'has' | 'none';

interface FilterState {
  degreeLevel: string | 'all';
  educationType: string | 'all';
  curriculumStatus: CurriculumFilter;
}

interface ProgramDetailPanelProps {
  program: ProgramDto;
  faculty: string;
  onEdit: () => void;
  onDelete: () => void;
}

interface SortHeaderProps {
  label: string;
  active: boolean;
  dir: SortDir;
  onClick: () => void;
}

interface SummaryCardProps {
  label: string;
  value: number;
  tone: 'default' | 'warning' | 'success';
  helper: string;
}

const EMPTY_FILTERS: FilterState = {
  degreeLevel: 'all',
  educationType: 'all',
  curriculumStatus: 'all',
};

const SELECT_CLS = 'h-8 shrink-0 rounded-md border border-slate-200 bg-white px-2.5 text-sm text-slate-700';

function extractErrorMessage(e: unknown, fallback: string): string {
  const resp = (e as { response?: { data?: { error?: string; message?: string; title?: string } } })?.response?.data;
  return resp?.error ?? resp?.message ?? resp?.title ?? (e instanceof Error ? e.message : fallback);
}

function countActiveFilters(
  filters: FilterState,
  facultyId: number | undefined,
  departmentId: number | undefined,
): number {
  let count = 0;
  if (facultyId !== undefined) count++;
  if (departmentId !== undefined) count++;
  if (filters.degreeLevel !== 'all') count++;
  if (filters.educationType !== 'all') count++;
  if (filters.curriculumStatus !== 'all') count++;
  return count;
}

function degreeLabel(value: string): string {
  return DEGREE_LEVELS.find(item => item.value === value)?.label ?? value;
}

function educationLabel(value: string): string {
  return EDUCATION_TYPES.find(item => item.value === value)?.label ?? value;
}

function buildProgramPayload(form: ProgramFormState): ProgramUpsertDto {
  const externalSystemCode = form.externalSystemCode.trim();

  return {
    name: form.name.trim(),
    code: form.code.trim(),
    externalSystemCode: externalSystemCode ? externalSystemCode : null,
    departmentId: form.departmentId,
    degreeLevel: form.degreeLevel,
    educationType: form.educationType,
    maxStudyDurationSemesters: form.maxStudyDurationSemesters,
    totalRequiredEcts: form.totalRequiredEcts,
  };
}

function CurriculumCountBadge({ count }: { count: number }) {
  if (count === 0) {
    return (
      <Badge className="border border-amber-200 bg-amber-50 text-amber-800 hover:bg-amber-50">
        Müfredat yok
      </Badge>
    );
  }

  return (
    <Badge className="border border-blue-200 bg-blue-50 text-blue-700 hover:bg-blue-50">
      {count} müfredat
    </Badge>
  );
}

function ExternalCodeBadge({ code }: { code: string | null }) {
  if (!code) {
    return (
      <Badge variant="outline" className="border-slate-200 text-slate-500">
        Tanımlı değil
      </Badge>
    );
  }

  return (
    <Badge variant="outline" className="border-emerald-200 bg-emerald-50 font-mono text-emerald-700">
      {code}
    </Badge>
  );
}

function SummaryCard({ label, value, tone, helper }: SummaryCardProps) {
  const toneMap = {
    default: 'border-slate-200 bg-white text-slate-900',
    warning: 'border-amber-200 bg-amber-50/60 text-amber-900',
    success: 'border-emerald-200 bg-emerald-50/60 text-emerald-900',
  } as const;

  return (
    <div className={`rounded-xl border px-4 py-3 ${toneMap[tone]}`}>
      <div className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-2 text-3xl font-semibold tabular-nums">{value}</div>
      <p className="mt-1 text-xs text-slate-500">{helper}</p>
    </div>
  );
}

export default function ProgramDefinitionsPage() {
  const { data: faculties = [] } = useAdminFaculties();
  const { data: allDepartments = [] } = useAdminDepartmentsByStructure(undefined);
  const { data: programs = [], isLoading } = useAdminProgramsByStructure(undefined);

  const createProgram = useCreateProgram();
  const updateProgram = useUpdateProgram();
  const deleteProgram = useDeleteProgram();

  const [facultyFilter, setFacultyFilter] = useState<number | undefined>(undefined);
  const [departmentFilter, setDepartmentFilter] = useState<number | undefined>(undefined);
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

  const visibleDepartments = useMemo(() => {
    if (!facultyFilter) return allDepartments;
    return allDepartments.filter(item => item.facultyId === facultyFilter);
  }, [allDepartments, facultyFilter]);

  const departmentMap = useMemo(() => {
    const map = new Map<number, DepartmentDto>();
    allDepartments.forEach(item => map.set(item.id, item));
    return map;
  }, [allDepartments]);

  const visiblePrograms = useMemo(() => {
    const query = search.trim().toLowerCase();
    let result = programs;

    if (facultyFilter) {
      const departmentIds = new Set(visibleDepartments.map(item => item.id));
      result = result.filter(item => departmentIds.has(item.departmentId));
    }

    if (departmentFilter) {
      result = result.filter(item => item.departmentId === departmentFilter);
    }

    if (query) {
      result = result.filter(item =>
        item.name.toLowerCase().includes(query) ||
        item.code.toLowerCase().includes(query) ||
        (item.externalSystemCode ?? '').toLowerCase().includes(query),
      );
    }

    if (filters.degreeLevel !== 'all') {
      result = result.filter(item => item.degreeLevel === filters.degreeLevel);
    }

    if (filters.educationType !== 'all') {
      result = result.filter(item => item.educationType === filters.educationType);
    }

    if (filters.curriculumStatus === 'has') {
      result = result.filter(item => item.curriculumCount > 0);
    } else if (filters.curriculumStatus === 'none') {
      result = result.filter(item => item.curriculumCount === 0);
    }

    return [...result].sort((a, b) => {
      let comparison = 0;

      if (sortKey === 'name') comparison = a.name.localeCompare(b.name, 'tr');
      else if (sortKey === 'code') comparison = a.code.localeCompare(b.code, 'tr');
      else if (sortKey === 'degreeLevel') comparison = a.degreeLevel.localeCompare(b.degreeLevel, 'tr');
      else if (sortKey === 'totalRequiredEcts') comparison = a.totalRequiredEcts - b.totalRequiredEcts;
      else comparison = a.curriculumCount - b.curriculumCount;

      return sortDir === 'asc' ? comparison : -comparison;
    });
  }, [departmentFilter, facultyFilter, filters, programs, search, sortDir, sortKey, visibleDepartments]);

  const summary = useMemo(() => {
    const missingCurriculum = visiblePrograms.filter(item => item.curriculumCount === 0).length;
    const missingExternalCode = visiblePrograms.filter(item => !item.externalSystemCode).length;
    const fullyReady = visiblePrograms.filter(item => item.curriculumCount > 0 && item.externalSystemCode).length;

    return {
      total: visiblePrograms.length,
      missingCurriculum,
      missingExternalCode,
      fullyReady,
    };
  }, [visiblePrograms]);

  function toggleSort(key: SortKey) {
    if (sortKey === key) {
      setSortDir(current => (current === 'asc' ? 'desc' : 'asc'));
      return;
    }

    setSortKey(key);
    setSortDir('asc');
  }

  function showSuccess(message: string) {
    setSuccessMessage(message);
    setTimeout(() => setSuccessMessage(''), 3000);
  }

  function openCreate() {
    const fallbackDepartmentId = departmentFilter ?? visibleDepartments[0]?.id ?? allDepartments[0]?.id ?? 0;
    setForm(emptyProgramForm(fallbackDepartmentId));
    setFormError('');
    setEditItem(null);
    setCreateOpen(true);
  }

  function openEdit(program: ProgramDto) {
    setForm({
      name: program.name,
      code: program.code,
      externalSystemCode: program.externalSystemCode ?? '',
      departmentId: program.departmentId,
      degreeLevel: program.degreeLevel,
      educationType: program.educationType,
      maxStudyDurationSemesters: program.maxStudyDurationSemesters,
      totalRequiredEcts: program.totalRequiredEcts,
    });
    setFormError('');
    setCreateOpen(false);
    setEditItem(program);
  }

  function closeDialog() {
    setCreateOpen(false);
    setEditItem(null);
    setFormError('');
  }

  async function handleSubmit() {
    const payload = buildProgramPayload(form);

    if (!payload.name || !payload.code || !payload.departmentId) {
      setFormError('Program adı, kod ve bölüm alanları zorunludur.');
      return;
    }

    try {
      if (editItem) {
        await updateProgram.mutateAsync({ id: editItem.id, ...payload });
        showSuccess('Program güncellendi.');
      } else {
        await createProgram.mutateAsync(payload);
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
    <div className="space-y-4">
      {actionError && (
        <div className="flex items-start gap-2 rounded-lg border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
          <span className="shrink-0 font-semibold">Hata:</span>
          <span className="flex-1">{actionError}</span>
          <button
            type="button"
            onClick={() => setActionError('')}
            className="shrink-0 font-bold text-destructive hover:text-destructive/80"
            aria-label="Hatayı kapat"
          >
            &times;
          </button>
        </div>
      )}

      <div className="grid gap-3 md:grid-cols-4">
        <SummaryCard label="Görünen Program" value={summary.total} tone="default" helper="Mevcut filtrelerle listelenen toplam kayıt." />
        <SummaryCard label="Müfredatı Eksik" value={summary.missingCurriculum} tone="warning" helper="Bu programlarda sonraki ders akışları eksik kalabilir." />
        <SummaryCard label="Dış Kodu Eksik" value={summary.missingExternalCode} tone="default" helper="Şimdilik zorunlu değil, ama entegrasyon hazırlığı için tamamlanabilir." />
        <SummaryCard label="Tam Hazır" value={summary.fullyReady} tone="success" helper="Müfredatı ve dış sistem kodu birlikte tanımlı olan programlar." />
      </div>

      <div className="rounded-xl border border-slate-200 bg-slate-50/70 px-4 py-3">
        <div className="flex items-start gap-3">
          <div className="rounded-full bg-white p-2 text-slate-700">
            <Link2 className="h-4 w-4" />
          </div>
          <div className="space-y-1">
            <p className="text-sm font-medium text-slate-900">Program tanımı sonrası gizli kalan adımları görünür tutuyoruz.</p>
            <p className="text-sm text-slate-600">
              Kılavuzlardaki en büyük sorun, kullanıcıların programı ekledikten sonra neden ilerleyemediğini sonradan fark etmesiydi.
              Bu ekranda artık müfredat eksikliği ve dış sistem kodu hazırlığı doğrudan görünüyor.
            </p>
          </div>
        </div>
      </div>

      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
        <div className="flex flex-wrap items-center gap-3 border-b border-slate-200 bg-slate-50/40 px-4 py-2">
          <div className="relative shrink-0">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Ad, kod veya dış sistem kodu ara..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="h-8 w-72 pl-8 text-sm"
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
              <Badge className="ml-0.5 h-4 bg-primary px-1.5 text-[10px] text-primary-foreground">
                {activeFilterCount}
              </Badge>
            )}
          </Button>

          {hasAnyConstraint && (
            <button
              type="button"
              className="shrink-0 text-xs text-muted-foreground underline underline-offset-2 hover:text-foreground"
              onClick={clearAll}
            >
              Temizle
            </button>
          )}

          <div className="flex-1" />

          {successMessage && <span className="shrink-0 text-xs font-medium text-emerald-600">{successMessage}</span>}

          <Button size="sm" className="h-8 shrink-0" onClick={openCreate} disabled={allDepartments.length === 0}>
            <Plus className="mr-1 h-4 w-4" />
            Program Ekle
          </Button>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="border-b border-slate-200 bg-slate-50">
              <tr>
                <th className="px-4 py-3 text-left font-semibold text-slate-600">
                  <SortHeader label="Ad" active={sortKey === 'name'} dir={sortDir} onClick={() => toggleSort('name')} />
                </th>
                <th className="w-28 px-4 py-3 text-left font-semibold text-slate-600">
                  <SortHeader label="Kod" active={sortKey === 'code'} dir={sortDir} onClick={() => toggleSort('code')} />
                </th>
                <th className="w-40 px-4 py-3 text-left font-semibold text-slate-600">Dış Kod</th>
                <th className="w-60 px-4 py-3 text-left font-semibold text-slate-600">Fakülte / Bölüm</th>
                <th className="w-36 px-4 py-3 text-left font-semibold text-slate-600">
                  <SortHeader label="Derece" active={sortKey === 'degreeLevel'} dir={sortDir} onClick={() => toggleSort('degreeLevel')} />
                </th>
                <th className="w-28 px-4 py-3 text-left font-semibold text-slate-600">Öğretim</th>
                <th className="w-28 px-4 py-3 text-left font-semibold text-slate-600">Azami Süre</th>
                <th className="w-24 px-4 py-3 text-left font-semibold text-slate-600">
                  <SortHeader label="AKTS" active={sortKey === 'totalRequiredEcts'} dir={sortDir} onClick={() => toggleSort('totalRequiredEcts')} />
                </th>
                <th className="w-36 px-4 py-3 text-left font-semibold text-slate-600">
                  <SortHeader label="Müfredat" active={sortKey === 'curriculumCount'} dir={sortDir} onClick={() => toggleSort('curriculumCount')} />
                </th>
                <th className="w-44 px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {isLoading ? (
                <tr>
                  <td colSpan={10} className="px-4 py-10 text-center text-muted-foreground">
                    Yükleniyor...
                  </td>
                </tr>
              ) : visiblePrograms.length === 0 ? (
                <tr>
                  <td colSpan={10} className="px-4 py-14">
                    <div className="flex flex-col items-center gap-3 text-center">
                      <div>
                        <p className="font-medium text-foreground">
                          {hasAnyConstraint ? 'Kriterle eşleşen program bulunamadı.' : 'Henüz program tanımlı değil.'}
                        </p>
                        <p className="mt-1 text-xs text-muted-foreground">
                          {hasAnyConstraint
                            ? 'Arama veya filtre kriterini değiştirerek yeniden deneyebilirsiniz.'
                            : 'İlk programı ekleyerek akademik yapıyı oluşturmaya başlayın.'}
                        </p>
                      </div>
                      {!hasAnyConstraint && (
                        <Button size="sm" className="h-8" onClick={openCreate} disabled={allDepartments.length === 0}>
                          <Plus className="mr-1 h-4 w-4" />
                          İlk Programı Ekle
                        </Button>
                      )}
                    </div>
                  </td>
                </tr>
              ) : (
                visiblePrograms.map(program => {
                  const department = departmentMap.get(program.departmentId);
                  const facultyName = department?.facultyName ?? '—';
                  const hasCurriculum = program.curriculumCount > 0;

                  return (
                    <tr
                      key={program.id}
                      role="button"
                      tabIndex={0}
                      title={`${program.name} - detayları göster`}
                      className={`group cursor-pointer border-l-2 transition-colors focus:bg-slate-50/80 focus:outline-none ${
                        hasCurriculum
                          ? 'border-l-transparent hover:bg-slate-50/60'
                          : 'border-l-amber-300/70 bg-amber-50/20 hover:bg-amber-50/40'
                      }`}
                      onClick={() => setDetailItem(program)}
                      onKeyDown={e => {
                        if (e.key === 'Enter' || e.key === ' ') {
                          e.preventDefault();
                          setDetailItem(program);
                        }
                      }}
                    >
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-2 font-medium text-foreground">
                          {program.name}
                          {!hasCurriculum && (
                            <span title="Bu program için henüz müfredat tanımlı değil.">
                              <AlertTriangle className="h-3.5 w-3.5 shrink-0 text-amber-500" />
                            </span>
                          )}
                        </div>
                      </td>
                      <td className="px-4 py-3">
                        <Badge variant="outline" className="font-mono text-xs uppercase tracking-wide">
                          {program.code}
                        </Badge>
                      </td>
                      <td className="px-4 py-3">
                        <ExternalCodeBadge code={program.externalSystemCode} />
                      </td>
                      <td className="truncate px-4 py-3 text-xs text-muted-foreground">
                        {facultyName} / {program.departmentName}
                      </td>
                      <td className="px-4 py-3 text-muted-foreground">{degreeLabel(program.degreeLevel)}</td>
                      <td className="px-4 py-3 text-muted-foreground">{educationLabel(program.educationType)}</td>
                      <td className="px-4 py-3 tabular-nums text-muted-foreground">{program.maxStudyDurationSemesters} dönem</td>
                      <td className="px-4 py-3 tabular-nums text-muted-foreground">{program.totalRequiredEcts}</td>
                      <td className="px-4 py-3">
                        <CurriculumCountBadge count={program.curriculumCount} />
                      </td>
                      <td className="px-4 py-3">
                        <div
                          className="flex items-center justify-end gap-2 opacity-0 transition-opacity group-hover:opacity-100 focus-within:opacity-100"
                          onClick={e => e.stopPropagation()}
                        >
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-8 w-8 p-0 hover:bg-slate-200/60"
                            title="Düzenle"
                            onClick={() => openEdit(program)}
                          >
                            <Edit2 className="h-3.5 w-3.5" />
                          </Button>
                          <div className="h-5 w-px bg-slate-200" />
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-8 w-8 p-0 hover:bg-destructive/10"
                            title="Sil"
                            onClick={() => setDeleteItem(program)}
                          >
                            <Trash2 className="h-3.5 w-3.5 text-destructive" />
                          </Button>
                          <ChevronRight className="ml-1 h-4 w-4 shrink-0 text-muted-foreground/60" />
                        </div>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 bg-slate-50/60 px-4 py-3">
          <span className="text-xs tabular-nums text-muted-foreground">
            {isLoading
              ? 'Yükleniyor...'
              : hasAnyConstraint
                ? `${programs.length} programdan ${visiblePrograms.length} kayıt gösteriliyor`
                : `${programs.length} program`}
          </span>
          <p className="text-sm italic text-muted-foreground">
            Program sonrası zorunlu adım:
            {' '}
            <Link to={ROUTES.CURRICULUM_MANAGEMENT} className="not-italic text-blue-600 underline">
              Müfredat İşlemleri
            </Link>
          </p>
        </div>
      </div>

      <Dialog open={filtersOpen} onOpenChange={setFiltersOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Filtreler</DialogTitle>
          </DialogHeader>

          <div className="grid grid-cols-1 gap-4 pt-1 sm:grid-cols-2">
            <div className="space-y-1.5">
              <label className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Fakülte</label>
              <select
                className={`${SELECT_CLS} w-full`}
                value={facultyFilter ?? ''}
                onChange={e => {
                  setFacultyFilter(e.target.value ? Number(e.target.value) : undefined);
                  setDepartmentFilter(undefined);
                }}
              >
                <option value="">Tüm Fakülteler</option>
                {faculties.map(item => (
                  <option key={item.id} value={item.id}>
                    {item.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="space-y-1.5">
              <label className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Bölüm</label>
              <select
                className={`${SELECT_CLS} w-full`}
                value={departmentFilter ?? ''}
                onChange={e => setDepartmentFilter(e.target.value ? Number(e.target.value) : undefined)}
              >
                <option value="">Tüm Bölümler</option>
                {visibleDepartments.map(item => (
                  <option key={item.id} value={item.id}>
                    {item.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="space-y-2">
              <label className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Derece Düzeyi</label>
              <div className="flex flex-col gap-0.5 rounded-md border border-slate-200 bg-slate-50/40 p-1.5">
                <label className="flex cursor-pointer items-center gap-2 rounded px-2 py-1 text-sm hover:bg-white">
                  <input
                    type="radio"
                    name="degree-filter"
                    checked={filters.degreeLevel === 'all'}
                    onChange={() => setFilters({ ...filters, degreeLevel: 'all' })}
                    className="accent-primary"
                  />
                  <span>Tümü</span>
                </label>
                {DEGREE_LEVELS.map(item => (
                  <label key={item.value} className="flex cursor-pointer items-center gap-2 rounded px-2 py-1 text-sm hover:bg-white">
                    <input
                      type="radio"
                      name="degree-filter"
                      checked={filters.degreeLevel === item.value}
                      onChange={() => setFilters({ ...filters, degreeLevel: item.value })}
                      className="accent-primary"
                    />
                    <span>{item.label}</span>
                  </label>
                ))}
              </div>
            </div>

            <div className="space-y-2">
              <label className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Eğitim Türü</label>
              <div className="flex flex-col gap-0.5 rounded-md border border-slate-200 bg-slate-50/40 p-1.5">
                <label className="flex cursor-pointer items-center gap-2 rounded px-2 py-1 text-sm hover:bg-white">
                  <input
                    type="radio"
                    name="education-filter"
                    checked={filters.educationType === 'all'}
                    onChange={() => setFilters({ ...filters, educationType: 'all' })}
                    className="accent-primary"
                  />
                  <span>Tümü</span>
                </label>
                {EDUCATION_TYPES.map(item => (
                  <label key={item.value} className="flex cursor-pointer items-center gap-2 rounded px-2 py-1 text-sm hover:bg-white">
                    <input
                      type="radio"
                      name="education-filter"
                      checked={filters.educationType === item.value}
                      onChange={() => setFilters({ ...filters, educationType: item.value })}
                      className="accent-primary"
                    />
                    <span>{item.label}</span>
                  </label>
                ))}
              </div>
            </div>

            <div className="space-y-2 sm:col-span-2">
              <label className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Müfredat Durumu</label>
              <div className="grid grid-cols-3 gap-1.5">
                {([
                  { value: 'all', label: 'Tümü' },
                  { value: 'has', label: 'Müfredatı olan' },
                  { value: 'none', label: 'Müfredatı olmayan' },
                ] as { value: CurriculumFilter; label: string }[]).map(option => (
                  <label
                    key={option.value}
                    className={`flex cursor-pointer items-center gap-2 rounded-md border px-3 py-2 text-sm transition-colors ${
                      filters.curriculumStatus === option.value
                        ? 'border-primary bg-primary/5'
                        : 'border-slate-200 bg-white hover:bg-slate-50'
                    }`}
                  >
                    <input
                      type="radio"
                      name="curriculum-filter"
                      checked={filters.curriculumStatus === option.value}
                      onChange={() => setFilters({ ...filters, curriculumStatus: option.value })}
                      className="accent-primary"
                    />
                    <span>{option.label}</span>
                  </label>
                ))}
              </div>
            </div>
          </div>

          <DialogFooter className="mt-2 gap-2 border-t border-slate-200 pt-4 sm:justify-between">
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

      <Dialog open={dialogOpen} onOpenChange={open => !open && closeDialog()}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{editItem ? 'Program Düzenle' : 'Program Ekle'}</DialogTitle>
          </DialogHeader>
          <ProgramFormFields
            form={form}
            onChange={nextForm => {
              setForm(nextForm);
              setFormError('');
            }}
            departments={editItem ? allDepartments : (visibleDepartments.length > 0 ? visibleDepartments : allDepartments)}
          />
          {formError && <p className="text-sm text-destructive">{formError}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={closeDialog}>
              İptal
            </Button>
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

      <Dialog open={!!detailItem} onOpenChange={open => !open && setDetailItem(null)}>
        <DialogContent className="max-h-[90vh] max-w-4xl overflow-y-auto">
          {detailItem && (
            <ProgramDetailPanel
              program={detailItem}
              faculty={departmentMap.get(detailItem.departmentId)?.facultyName ?? '—'}
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

function ProgramDetailPanel({ program, faculty, onEdit, onDelete }: ProgramDetailPanelProps) {
  const hasCurriculum = program.curriculumCount > 0;
  const hasExternalCode = Boolean(program.externalSystemCode);

  return (
    <>
      <DialogHeader className="border-b border-slate-200 pb-4 pr-10">
        <div className="flex flex-wrap items-center gap-3">
          <DialogTitle className="text-2xl">{program.name}</DialogTitle>
          <Badge variant="outline" className="font-mono text-xs uppercase tracking-wide">
            {program.code}
          </Badge>
          {!hasCurriculum && (
            <span className="inline-flex items-center gap-1 rounded border border-amber-200 bg-amber-50 px-2 py-0.5 text-xs text-amber-700">
              <AlertTriangle className="h-3 w-3" />
              Müfredat tanımlı değil
            </span>
          )}
        </div>
        <p className="mt-1 text-sm text-muted-foreground">
          {faculty} · {program.departmentName}
        </p>
      </DialogHeader>

      <div className="grid grid-cols-1 gap-5 pt-2 md:grid-cols-5">
        <section className="space-y-5 md:col-span-3">
          <div>
            <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Genel Bilgiler</h3>
            <dl className="divide-y divide-slate-100 rounded-lg border border-slate-200 bg-slate-50/40">
              <DetailRow label="Program Adı" value={program.name} />
              <DetailRow label="Kod" value={<span className="font-mono uppercase tracking-wide">{program.code}</span>} />
              <DetailRow
                label="ÖSYM / Dış Sistem Kodu"
                value={
                  program.externalSystemCode ? (
                    <span className="font-mono uppercase tracking-wide">{program.externalSystemCode}</span>
                  ) : (
                    <span className="text-muted-foreground">Henüz tanımlı değil</span>
                  )
                }
              />
              <DetailRow label="Fakülte" value={faculty} />
              <DetailRow label="Bölüm" value={program.departmentName} />
              <DetailRow label="Derece Düzeyi" value={degreeLabel(program.degreeLevel)} />
              <DetailRow label="Eğitim Türü" value={educationLabel(program.educationType)} />
              <DetailRow label="ID" value={<span className="font-mono text-muted-foreground">#{program.id}</span>} />
            </dl>
          </div>
        </section>

        <section className="space-y-5 md:col-span-2">
          <div>
            <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Hızlı İstatistikler</h3>
            <div className="grid grid-cols-2 gap-2">
              <div className="rounded-lg border border-slate-200 bg-white px-3 py-2.5">
                <div className="text-xs text-muted-foreground">Azami Süre</div>
                <div className="mt-0.5 text-2xl font-semibold tabular-nums">{program.maxStudyDurationSemesters}</div>
                <div className="-mt-0.5 text-[11px] text-muted-foreground">dönem</div>
              </div>
              <div className="rounded-lg border border-slate-200 bg-white px-3 py-2.5">
                <div className="text-xs text-muted-foreground">Toplam AKTS</div>
                <div className="mt-0.5 text-2xl font-semibold tabular-nums">{program.totalRequiredEcts}</div>
              </div>
              <div className="col-span-2 rounded-lg border border-slate-200 bg-white px-3 py-2.5">
                <div className="text-xs text-muted-foreground">Müfredat Sayısı</div>
                <div className="mt-0.5 text-2xl font-semibold tabular-nums">{program.curriculumCount}</div>
              </div>
            </div>
          </div>

          <div>
            <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Hazırlık Durumu</h3>
            <div className="space-y-2 rounded-lg border border-slate-200 bg-white p-3">
              <ReadinessRow
                ready={hasCurriculum}
                label="Müfredat tanımı"
                helper={
                  hasCurriculum
                    ? 'Ders ve müfredat akışlarına geçilebilir.'
                    : 'Bu program için müfredat eklenmeden aşağı akışlar eksik kalır.'
                }
              />
              <ReadinessRow
                ready={hasExternalCode}
                optional
                label="Dış sistem kodu"
                helper={
                  hasExternalCode
                    ? 'İlerideki entegrasyonlar için temel bilgi hazır.'
                    : 'Şimdilik zorunlu değil, ama entegrasyon öncesi tamamlanabilir.'
                }
              />
            </div>
          </div>

          <div>
            <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground">İlgili Sayfa</h3>
            {hasCurriculum ? (
              <Link
                to={ROUTES.CURRICULUM_MANAGEMENT}
                className="block rounded-lg border border-slate-200 bg-white px-3 py-2.5 text-sm transition-colors hover:border-slate-300"
              >
                <p className="font-medium text-foreground">Müfredat İşlemleri →</p>
                <p className="mt-0.5 text-xs text-muted-foreground">{program.curriculumCount} müfredatı yönet</p>
              </Link>
            ) : (
              <div className="rounded-lg border border-dashed border-amber-300 bg-amber-50/30 p-3 text-sm text-amber-900">
                Bu programa henüz müfredat eklenmemiş.
                {' '}
                <Link to={ROUTES.CURRICULUM_MANAGEMENT} className="font-medium underline">
                  Müfredat İşlemleri
                </Link>
                {' '}
                sayfasından devam edebilirsiniz.
              </div>
            )}
          </div>
        </section>
      </div>

      <DialogFooter className="mt-6 gap-2 border-t border-slate-200 pt-4 sm:justify-between">
        <Button
          variant="outline"
          className="text-destructive hover:bg-destructive/10 hover:text-destructive"
          onClick={onDelete}
        >
          <Trash2 className="mr-1.5 h-4 w-4" />
          Sil
        </Button>
        <Button onClick={onEdit}>
          <Edit2 className="mr-1.5 h-4 w-4" />
          Düzenle
        </Button>
      </DialogFooter>
    </>
  );
}

function DetailRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex justify-between gap-4 px-3 py-2.5 text-sm">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="text-right font-medium text-foreground">{value}</dd>
    </div>
  );
}

function ReadinessRow({
  ready,
  label,
  helper,
  optional = false,
}: {
  ready: boolean;
  label: string;
  helper: string;
  optional?: boolean;
}) {
  return (
    <div className="flex items-start gap-2 rounded-md border border-slate-100 bg-slate-50/70 px-3 py-2">
      <div className={`mt-0.5 ${ready ? 'text-emerald-600' : optional ? 'text-slate-400' : 'text-amber-600'}`}>
        <CheckCircle2 className="h-4 w-4" />
      </div>
      <div className="space-y-0.5">
        <div className="flex items-center gap-2">
          <p className="text-sm font-medium text-foreground">{label}</p>
          {optional && (
            <Badge variant="outline" className="text-[10px] uppercase tracking-wide text-slate-500">
              İsteğe bağlı
            </Badge>
          )}
        </div>
        <p className="text-xs text-muted-foreground">{helper}</p>
      </div>
    </div>
  );
}

function SortHeader({ label, active, dir, onClick }: SortHeaderProps) {
  const Icon = active ? (dir === 'asc' ? ArrowUp : ArrowDown) : ArrowUpDown;

  return (
    <button
      type="button"
      onClick={onClick}
      className={`inline-flex items-center gap-1 transition-colors hover:text-foreground ${active ? 'text-foreground' : ''}`}
    >
      {label}
      <Icon className="h-3.5 w-3.5" />
    </button>
  );
}
