import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  AlertTriangle,
  CheckCircle2,
  ChevronRight,
  Edit2,
  Plus,
  Trash2,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import {
  useAdminDepartmentsByStructure,
  useAdminProgramsByStructure,
  useCreateProgram,
  useDeleteProgram,
  useUpdateProgram,
  type DepartmentDto,
  type ProgramDto,
  type ProgramUpsertDto,
} from '../hooks/useAdminAcademicStructure';
import { ProgramFormFields, SortHeader } from '../components/academic-structure/shared';
import { ProgramDeleteDialog } from '../components/academic-structure/ProgramDeleteDialog';
import { ListToolbar } from '../components/academic-structure/ListToolbar';
import { useListController } from '../hooks/useListController';
import {
  DEGREE_LEVELS,
  EDUCATION_TYPES,
  emptyProgramForm,
  extractErrorMessage,
  formatDateTime,
  type ProgramFormState,
} from '../components/academic-structure/shared-constants';
import { TablePagination } from '@/components/ui/table-pagination';
import { ROUTES } from '@/lib/constants';

// Sayfa başına varsayılan 15 program; sonrası pagination ile.
const DEFAULT_PAGE_SIZE = 15;
const PAGE_SIZE_OPTIONS = [15, 30, 50];

type SortKey = 'name' | 'code' | 'degreeLevel' | 'totalRequiredEcts' | 'curriculumCount';

interface ProgramDetailPanelProps {
  program: ProgramDto;
  faculty: string;
  onEdit: () => void;
  onDelete: () => void;
}

function matchesProgramSearch(p: ProgramDto, q: string): boolean {
  return p.name.toLowerCase().includes(q)
    || p.code.toLowerCase().includes(q)
    || (p.externalSystemCode ?? '').toLowerCase().includes(q);
}

function compareProgram(a: ProgramDto, b: ProgramDto, key: SortKey): number {
  if (key === 'name') return a.name.localeCompare(b.name, 'tr');
  if (key === 'code') return a.code.localeCompare(b.code, 'tr');
  if (key === 'degreeLevel') return a.degreeLevel.localeCompare(b.degreeLevel, 'tr');
  if (key === 'totalRequiredEcts') return a.totalRequiredEcts - b.totalRequiredEcts;
  return a.curriculumCount - b.curriculumCount;
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

export default function ProgramDefinitionsPage() {
  const { data: allDepartments = [] } = useAdminDepartmentsByStructure(undefined);
  const { data: programs = [], isLoading, isError, refetch } = useAdminProgramsByStructure(undefined);

  const createProgram = useCreateProgram();
  const updateProgram = useUpdateProgram();
  const deleteProgram = useDeleteProgram();

  const [createOpen, setCreateOpen] = useState(false);
  const [editItem, setEditItem] = useState<ProgramDto | null>(null);
  const [deleteItem, setDeleteItem] = useState<ProgramDto | null>(null);
  const [detailItem, setDetailItem] = useState<ProgramDto | null>(null);
  const [form, setForm] = useState<ProgramFormState>(emptyProgramForm());
  const [formError, setFormError] = useState('');
  const [successMessage, setSuccessMessage] = useState('');
  const [actionError, setActionError] = useState('');

  const departmentMap = useMemo(() => {
    const map = new Map<number, DepartmentDto>();
    allDepartments.forEach(item => map.set(item.id, item));
    return map;
  }, [allDepartments]);

  const list = useListController<ProgramDto, SortKey>({
    items: programs,
    initialSortKey: 'name',
    defaultPageSize: DEFAULT_PAGE_SIZE,
    matchesSearch: matchesProgramSearch,
    compare: compareProgram,
  });

  const dialogOpen = createOpen || !!editItem;
  const isSavePending = createProgram.isPending || updateProgram.isPending;

  function showSuccess(message: string) {
    setSuccessMessage(message);
    setTimeout(() => setSuccessMessage(''), 3000);
  }

  function openCreate() {
    const fallbackDepartmentId = allDepartments[0]?.id ?? 0;
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

  return (
    <div className="flex flex-1 flex-col gap-3 min-h-0">
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

      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white flex flex-1 flex-col min-h-0">
        <ListToolbar
          searchPlaceholder="Ad, kod veya dış sistem kodu ara..."
          search={list.search}
          onSearchChange={list.setSearch}
          hasAnyConstraint={list.hasAnyConstraint}
          onClear={list.clearAll}
          successMessage={successMessage}
          actions={
            <Button size="sm" className="h-8 shrink-0" onClick={openCreate} disabled={allDepartments.length === 0}>
              <Plus className="mr-1 h-4 w-4" />
              Program Ekle
            </Button>
          }
        />

        <div className="flex-1 min-h-0 overflow-auto">
          <table className="w-full text-sm">
            <thead className="border-b border-slate-200 bg-slate-50 sticky top-0 z-10">
              <tr>
                <th className="px-4 py-3 text-left font-semibold text-slate-600">
                  <SortHeader label="Ad" active={list.sortKey ==='name'} dir={list.sortDir} onClick={() => list.toggleSort('name')} />
                </th>
                <th className="w-32 px-4 py-3 text-center font-semibold text-slate-600">
                  <SortHeader label="Kod" active={list.sortKey ==='code'} dir={list.sortDir} onClick={() => list.toggleSort('code')} />
                </th>
                <th className="w-32 px-4 py-3 text-center font-semibold text-slate-600">Dış Kod</th>
                <th className="w-56 px-4 py-3 text-left font-semibold text-slate-600">Fakülte / Bölüm</th>
                <th className="w-32 px-4 py-3 text-center font-semibold text-slate-600">
                  <SortHeader label="Derece" active={list.sortKey ==='degreeLevel'} dir={list.sortDir} onClick={() => list.toggleSort('degreeLevel')} />
                </th>
                <th className="w-32 px-4 py-3 text-center font-semibold text-slate-600">Öğretim</th>
                <th className="w-32 px-4 py-3 text-center font-semibold text-slate-600">Azami Süre</th>
                <th className="w-32 px-4 py-3 text-center font-semibold text-slate-600">
                  <SortHeader label="AKTS" active={list.sortKey ==='totalRequiredEcts'} dir={list.sortDir} onClick={() => list.toggleSort('totalRequiredEcts')} />
                </th>
                <th className="w-32 px-4 py-3 text-center font-semibold text-slate-600">
                  <SortHeader label="Müfredat" active={list.sortKey ==='curriculumCount'} dir={list.sortDir} onClick={() => list.toggleSort('curriculumCount')} />
                </th>
                <th className="w-32 px-4 py-3" />
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                <tr>
                  <td colSpan={10} className="px-4 py-10 text-center text-muted-foreground">
                    Yükleniyor...
                  </td>
                </tr>
              ) : isError ? (
                <tr>
                  <td colSpan={10} className="px-4 py-14">
                    <div className="flex flex-col items-center gap-3 text-center">
                      <AlertTriangle className="h-6 w-6 text-destructive" />
                      <div>
                        <p className="font-medium text-foreground">Program listesi yüklenemedi.</p>
                        <p className="mt-1 text-xs text-muted-foreground">Bağlantı veya sunucu hatası olabilir.</p>
                      </div>
                      <Button size="sm" variant="outline" className="h-8" onClick={() => refetch()}>
                        Tekrar dene
                      </Button>
                    </div>
                  </td>
                </tr>
              ) : list.visible.length === 0 ? (
                <tr>
                  <td colSpan={10} className="px-4 py-14">
                    <div className="flex flex-col items-center gap-3 text-center">
                      <div>
                        <p className="font-medium text-foreground">
                          {list.hasAnyConstraint ? 'Kriterle eşleşen program bulunamadı.' : 'Henüz program tanımlı değil.'}
                        </p>
                        <p className="mt-1 text-xs text-muted-foreground">
                          {list.hasAnyConstraint
                            ? 'Arama veya filtre kriterini değiştirerek yeniden deneyebilirsiniz.'
                            : 'İlk programı ekleyerek akademik yapıyı oluşturmaya başlayın.'}
                        </p>
                      </div>
                      {!list.hasAnyConstraint && (
                        <Button size="sm" className="h-8" onClick={openCreate} disabled={allDepartments.length === 0}>
                          <Plus className="mr-1 h-4 w-4" />
                          İlk Programı Ekle
                        </Button>
                      )}
                    </div>
                  </td>
                </tr>
              ) : (
                list.paged.map(program => {
                  const department = departmentMap.get(program.departmentId);
                  const facultyName = department?.facultyName ?? '—';
                  const hasCurriculum = program.curriculumCount > 0;

                  return (
                    <tr
                      key={program.id}
                      role="button"
                      tabIndex={0}
                      title={`${program.name} - detayları göster`}
                      className={`group cursor-pointer border-b border-slate-200 border-l-2 transition-colors focus:bg-slate-50/80 focus:outline-none ${
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
                              <AlertTriangle className="h-3.5 w-3.5 shrink-0 text-amber-500 animate-pulse" />
                            </span>
                          )}
                        </div>
                      </td>
                      <td className="px-4 py-3 text-center">
                        <Badge variant="outline" className="font-mono text-xs uppercase tracking-wide">
                          {program.code}
                        </Badge>
                      </td>
                      <td className="px-4 py-3 text-center">
                        <ExternalCodeBadge code={program.externalSystemCode} />
                      </td>
                      <td className="truncate px-4 py-3 text-xs text-muted-foreground">
                        {facultyName} / {program.departmentName}
                      </td>
                      <td className="px-4 py-3 text-center text-muted-foreground">{degreeLabel(program.degreeLevel)}</td>
                      <td className="px-4 py-3 text-center text-muted-foreground">{educationLabel(program.educationType)}</td>
                      <td className="px-4 py-3 text-center tabular-nums text-muted-foreground">{program.maxStudyDurationSemesters} dönem</td>
                      <td className="px-4 py-3 text-center tabular-nums text-muted-foreground">{program.totalRequiredEcts}</td>
                      <td className="px-4 py-3 text-center">
                        <CurriculumCountBadge count={program.curriculumCount} />
                      </td>
                      <td className="px-4 py-3">
                        <div
                          className="flex items-center justify-end gap-2"
                          onClick={e => e.stopPropagation()}
                        >
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-8 w-8 p-0 transition-colors hover:bg-blue-50 hover:text-blue-600"
                            title="Düzenle"
                            onClick={() => openEdit(program)}
                          >
                            <Edit2 className="h-3.5 w-3.5" />
                          </Button>
                          <div className="h-5 w-px bg-slate-200" />
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-8 w-8 p-0 transition-colors hover:bg-red-50 hover:text-red-600"
                            title="Sil"
                            onClick={() => setDeleteItem(program)}
                          >
                            <Trash2 className="h-3.5 w-3.5 text-destructive" />
                          </Button>
                          <ChevronRight className="ml-1 h-4 w-4 shrink-0 text-muted-foreground/60 transition-all duration-150 group-hover:translate-x-0.5 group-hover:text-foreground" />
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
            itemLabel="program"
            pageSizeOptions={PAGE_SIZE_OPTIONS}
            size="md"
          />
        )}
      </div>

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
            departments={allDepartments}
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

      <ProgramDeleteDialog
        program={deleteItem}
        onClose={() => setDeleteItem(null)}
        onConfirm={handleDelete}
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
              <DetailRow label="Oluşturulma" value={<span className="text-muted-foreground">{formatDateTime(program.createdAt)}</span>} />
              <DetailRow label="Son Güncelleme" value={<span className="text-muted-foreground">{formatDateTime(program.updatedAt)}</span>} />
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

