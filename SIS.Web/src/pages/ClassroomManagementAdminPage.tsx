import { useMemo, useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem,
  DropdownMenuSeparator, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import {
  AlertCircle, AlertTriangle, BarChart3, Building2, Edit2, MoreHorizontal,
  Plus, Search, Trash2, X,
} from 'lucide-react';
import { useClassrooms, useCreateClassroom, useUpdateClassroom, useDeleteClassroom } from '../hooks/useClassrooms';
import type { ClassroomDto, CreateClassroomDto, ClassroomScheduleImpact } from '../api/classroom.api';
import { toApiError } from '../lib/apiError';
import { TablePagination } from '@/components/ui/table-pagination';
import ClassroomAnalyticsDialog from '../components/classroom/ClassroomAnalyticsDialog';
import {
  CLASSROOM_TYPE_CODES,
  getClassroomTypeLabel,
  getClassroomTypeBadgeClass,
  getClassroomTypeBarClass,
} from '../lib/classroomTypeUtils';

const emptyForm: CreateClassroomDto = {
  name: '',
  buildingName: '',
  floor: 0,
  capacity: 30,
  type: 'Lecture',
};

// Sayfa başına varsayılan 25 derslik; sonrası pagination ile (UI ayarı, iş kuralı değil).
const DEFAULT_PAGE_SIZE = 25;
const PAGE_SIZE_OPTIONS = [25, 50, 100];

function formatLocation(buildingName: string | null | undefined, floor: number | null | undefined): string {
  const building = buildingName?.trim();
  const hasFloor = floor !== null && floor !== undefined;
  const floorLabel = hasFloor ? (floor === 0 ? 'Zemin Kat' : `Kat ${floor}`) : null;

  if (building && floorLabel) return `${building} · ${floorLabel}`;
  if (building) return building;
  if (floorLabel) return floorLabel;
  return '—';
}

// ── Page ───────────────────────────────────────────────────────────────────

export default function ClassroomManagementAdminPage() {
  const { data: classrooms, isLoading, isError } = useClassrooms();
  const createMutation = useCreateClassroom();
  const updateMutation = useUpdateClassroom();
  const deleteMutation = useDeleteClassroom();

  const [filterType, setFilterType] = useState<string>('all');
  const [filterBuilding, setFilterBuilding] = useState<string>('all');
  const [filterFloor, setFilterFloor] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [analyticsOpen, setAnalyticsOpen] = useState(false);

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<ClassroomDto | null>(null);
  const [deleteError, setDeleteError] = useState('');
  const [editingClassroom, setEditingClassroom] = useState<ClassroomDto | null>(null);
  const [form, setForm] = useState<CreateClassroomDto>(emptyForm);
  const [error, setError] = useState('');

  // Tip/kapasite değişimi mevcut programları etkiliyorsa backend 409 + etki listesi döner.
  // Listeyi onay diyaloğunda gösterip kullanıcı "Yine de Kaydet" derse confirm flag'iyle tekrar göndeririz.
  const [impact, setImpact] = useState<ClassroomScheduleImpact[] | null>(null);

  const openCreate = () => {
    setEditingClassroom(null);
    setForm(emptyForm);
    setError('');
    setDialogOpen(true);
  };

  const openEdit = (classroom: ClassroomDto) => {
    setEditingClassroom(classroom);
    setForm({
      name: classroom.name,
      buildingName: classroom.buildingName,
      floor: classroom.floor,
      capacity: classroom.capacity,
      type: classroom.type,
    });
    setError('');
    setDialogOpen(true);
  };

  const confirmDelete = (classroom: ClassroomDto) => {
    setDeleteTarget(classroom);
    setDeleteError('');
    setDeleteDialogOpen(true);
  };

  const handleDelete = () => {
    if (!deleteTarget) return;
    setDeleteError('');
    deleteMutation.mutate(deleteTarget.id, {
      onSuccess: () => { setDeleteDialogOpen(false); setDeleteTarget(null); },
      onError: (e: unknown) => setDeleteError(toApiError(e, 'Derslik silinemedi.').message),
    });
  };

  // Backend 409 ScheduleImpact gövdesinden etki listesini çıkarır (toApiError bu alanı taşımaz).
  const extractImpact = (e: unknown): ClassroomScheduleImpact[] | null => {
    const res = (e as { response?: { status?: number; data?: { code?: string; affected?: ClassroomScheduleImpact[] } } })?.response;
    if (res?.status === 409 && res.data?.code === 'ScheduleImpact' && Array.isArray(res.data.affected)) {
      return res.data.affected;
    }
    return null;
  };

  const runUpdate = (id: number, confirmScheduleImpact: boolean) => {
    updateMutation.mutate(
      { id, dto: { ...form, confirmScheduleImpact } },
      {
        onSuccess: () => { setDialogOpen(false); setImpact(null); },
        onError: (e: unknown) => {
          const imp = extractImpact(e);
          if (imp) { setImpact(imp); return; }
          setError(toApiError(e, 'Güncelleme başarısız.').message);
        },
      },
    );
  };

  const handleSubmit = () => {
    setError('');
    if (!form.name.trim()) { setError('Derslik adı zorunludur.'); return; }
    if (form.capacity <= 0) { setError('Kapasite sıfırdan büyük olmalıdır.'); return; }

    if (editingClassroom) {
      runUpdate(editingClassroom.id, false);
    } else {
      createMutation.mutate(form, {
        onSuccess: () => setDialogOpen(false),
        onError: (e: unknown) => setError(toApiError(e, 'Oluşturma başarısız.').message),
      });
    }
  };

  const all = useMemo(() => classrooms ?? [], [classrooms]);

  const typeCounts = useMemo(() => {
    return all.reduce((acc, c) => {
      acc[c.type] = (acc[c.type] || 0) + 1;
      return acc;
    }, {} as Record<string, number>);
  }, [all]);

  // Tip dropdown'u DB'den gelen tiplerden türetilir; backend enum'una yeni tip
  // eklendiğinde o tipte derslik oluşturulur oluşturulmaz dropdown'da görünür.
  // Bilinen tipleri enum sırasında, bilinmeyenleri sonuna ekler.
  const sortedTypes = useMemo(() => {
    const known = (CLASSROOM_TYPE_CODES as readonly string[]).filter(t => (typeCounts[t] || 0) > 0);
    const knownSet = new Set<string>(CLASSROOM_TYPE_CODES);
    const extras = Object.keys(typeCounts)
      .filter(t => !knownSet.has(t))
      .sort((a, b) => getClassroomTypeLabel(a).localeCompare(getClassroomTypeLabel(b), 'tr'));
    return [...known, ...extras];
  }, [typeCounts]);

  const buildings = useMemo(() => {
    const set = new Set<string>();
    all.forEach(c => { const b = c.buildingName?.trim(); if (b) set.add(b); });
    return Array.from(set).sort((a, b) => a.localeCompare(b, 'tr'));
  }, [all]);

  const floors = useMemo(() => {
    const set = new Set<number>();
    all.forEach(c => { if (c.floor !== null && c.floor !== undefined) set.add(c.floor); });
    return Array.from(set).sort((a, b) => a - b);
  }, [all]);

  const maxCapacity = useMemo(() => all.reduce((m, c) => Math.max(m, c.capacity || 0), 0), [all]);

  const filtered = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    return all.filter(c => {
      if (filterType !== 'all' && c.type !== filterType) return false;
      if (filterBuilding !== 'all' && (c.buildingName?.trim() ?? '') !== filterBuilding) return false;
      if (filterFloor !== 'all' && String(c.floor ?? '') !== filterFloor) return false;
      if (q) {
        const haystack = `${c.name} ${c.buildingName ?? ''}`.toLowerCase();
        if (!haystack.includes(q)) return false;
      }
      return true;
    });
  }, [all, filterType, filterBuilding, filterFloor, searchQuery]);

  const isFiltered = filterType !== 'all' || filterBuilding !== 'all' || filterFloor !== 'all' || searchQuery.trim().length > 0;

  // Filtre/arama değişince ilk sayfaya dön (TablePagination safePage'i clamp'lese de,
  // kullanıcı listeyi daralttığında 1. sayfa beklenir).
  const onFilterType = (v: string) => { setFilterType(v); setPage(1); };
  const onFilterBuilding = (v: string) => { setFilterBuilding(v); setPage(1); };
  const onFilterFloor = (v: string) => { setFilterFloor(v); setPage(1); };
  const onSearch = (v: string) => { setSearchQuery(v); setPage(1); };

  const clearFilters = () => {
    setFilterType('all');
    setFilterBuilding('all');
    setFilterFloor('all');
    setSearchQuery('');
    setPage(1);
  };

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const safePage = Math.min(Math.max(1, page), totalPages);
  const paged = useMemo(
    () => filtered.slice((safePage - 1) * pageSize, safePage * pageSize),
    [filtered, safePage, pageSize],
  );

  const floorLabel = (f: number) => (f === 0 ? 'Zemin Kat' : `Kat ${f}`);

  // ── Render ────────────────────────────────────────────────────────────

  return (
    <div className="space-y-4">
      {/* Toolbar */}
      <div className="rounded-xl border border-slate-200 bg-white shadow-sm">
        <div className="flex flex-wrap items-center gap-2 px-3 py-2.5">
          <div className="relative flex-1 min-w-[200px] max-w-[340px]">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-400" />
            <Input
              placeholder="Derslik adı veya bina ara..."
              value={searchQuery}
              onChange={e => onSearch(e.target.value)}
              className="h-9 pl-8 pr-8 text-sm"
            />
            {searchQuery && (
              <button
                type="button"
                onClick={() => onSearch('')}
                className="absolute right-2 top-1/2 -translate-y-1/2 rounded-full p-0.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
                aria-label="Aramayı temizle"
              >
                <X className="h-3.5 w-3.5" />
              </button>
            )}
          </div>

          <Select value={filterType} onValueChange={onFilterType}>
            <SelectTrigger className="h-9 w-[180px] text-xs">
              <SelectValue placeholder="Tüm Tipler" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Tüm Tipler ({all.length})</SelectItem>
              {sortedTypes.map(t => (
                <SelectItem key={t} value={t}>
                  <span className="inline-flex items-center gap-2">
                    <span className={`h-2 w-2 rounded-full ${getClassroomTypeBarClass(t)}`} />
                    {getClassroomTypeLabel(t)} ({typeCounts[t] || 0})
                  </span>
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select value={filterBuilding} onValueChange={onFilterBuilding}>
            <SelectTrigger className="h-9 w-[180px] text-xs">
              <SelectValue placeholder="Tüm Binalar" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Tüm Binalar ({buildings.length})</SelectItem>
              {buildings.map(b => (
                <SelectItem key={b} value={b}>{b}</SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select value={filterFloor} onValueChange={onFilterFloor}>
            <SelectTrigger className="h-9 w-[150px] text-xs">
              <SelectValue placeholder="Tüm Katlar" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Tüm Katlar ({floors.length})</SelectItem>
              {floors.map(f => (
                <SelectItem key={f} value={String(f)}>{floorLabel(f)}</SelectItem>
              ))}
            </SelectContent>
          </Select>

          {isFiltered && (
            <button
              type="button"
              onClick={clearFilters}
              className="text-xs font-medium text-slate-500 underline underline-offset-2 hover:text-slate-700"
            >
              Filtreleri temizle
            </button>
          )}

          <div className="ml-auto flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setAnalyticsOpen(true)}
              className="h-9 gap-1.5 border-slate-200 hover:border-violet-300 hover:bg-violet-50/60 hover:text-violet-700"
              disabled={all.length === 0}
            >
              <BarChart3 className="h-4 w-4" /> Analiz
            </Button>
            <Button
              onClick={openCreate}
              size="sm"
              className="h-9 gap-1.5 bg-gradient-to-br from-[#003366] to-[#004080] text-white shadow-sm hover:from-[#002a52] hover:to-[#003366] hover:shadow-md"
            >
              <Plus className="h-4 w-4" /> Yeni Derslik
            </Button>
          </div>
        </div>

      </div>

      {/* Table */}
      {isLoading ? (
        <Skeleton className="h-48 rounded-xl" />
      ) : isError ? (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Derslikler yüklenemedi.</AlertDescription>
        </Alert>
      ) : all.length === 0 ? (
        <div className="rounded-xl border border-slate-200 bg-gradient-to-br from-white to-blue-50/30 py-16 text-center">
          <div className="mx-auto mb-3 flex h-14 w-14 items-center justify-center rounded-2xl bg-blue-100 text-blue-600">
            <Building2 className="h-7 w-7" />
          </div>
          <p className="text-slate-700">Henüz derslik tanımlanmamış.</p>
          <Button
            variant="outline"
            className="mt-4 border-blue-200 hover:border-blue-300 hover:bg-blue-50"
            onClick={openCreate}
          >
            <Plus className="mr-1 h-4 w-4" /> İlk Dersliği Ekle
          </Button>
        </div>
      ) : filtered.length === 0 ? (
        <div className="rounded-xl border border-slate-200 bg-white py-12 text-center">
          <Search className="mx-auto mb-3 h-10 w-10 text-slate-300" />
          <p className="text-sm text-slate-600">Filtrelere uyan derslik bulunamadı.</p>
          <Button variant="ghost" size="sm" className="mt-3" onClick={clearFilters}>
            Filtreleri Temizle
          </Button>
        </div>
      ) : (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-200 bg-gradient-to-r from-slate-50 to-slate-50/40">
                <th className="px-4 py-3 text-left font-semibold text-slate-700">Derslik Adı</th>
                <th className="px-4 py-3 text-left font-semibold text-slate-700">Konum</th>
                <th className="px-4 py-3 text-left font-semibold text-slate-700">Kapasite</th>
                <th className="px-4 py-3 text-left font-semibold text-slate-700">Derslik Tipi</th>
                <th className="w-12 px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {paged.map(c => {
                const location = formatLocation(c.buildingName, c.floor);
                const barClass = getClassroomTypeBarClass(c.type);
                const widthPct = maxCapacity > 0 ? Math.max(8, Math.round((c.capacity / maxCapacity) * 100)) : 0;
                return (
                  <tr
                    key={c.id}
                    className="group border-l-2 border-l-transparent transition-colors duration-150 hover:border-l-[#003366]/40 hover:bg-slate-50/60"
                  >
                    <td className="px-4 py-3 font-medium text-slate-900">{c.name}</td>
                    <td className="px-4 py-3 text-sm text-slate-600">{location}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2.5">
                        <div className="h-1.5 w-20 overflow-hidden rounded-full bg-slate-100">
                          <div
                            className={`h-full ${barClass} transition-all duration-300`}
                            style={{ width: `${widthPct}%` }}
                          />
                        </div>
                        <span className="font-mono text-xs font-semibold tabular-nums text-slate-700">{c.capacity}</span>
                        <span className="text-[11px] text-slate-400">kişi</span>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs ${getClassroomTypeBadgeClass(c.type)}`}>
                        <span className={`h-1.5 w-1.5 rounded-full ${barClass}`} />
                        {getClassroomTypeLabel(c.type)}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right">
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button
                            size="icon"
                            variant="ghost"
                            className="h-8 w-8 opacity-0 transition-opacity group-hover:opacity-100 data-[state=open]:opacity-100"
                            aria-label={`${c.name} işlemleri`}
                          >
                            <MoreHorizontal className="h-4 w-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end" className="w-40">
                          <DropdownMenuItem onClick={() => openEdit(c)}>
                            <Edit2 className="mr-2 h-3.5 w-3.5" /> Düzenle
                          </DropdownMenuItem>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem
                            onClick={() => confirmDelete(c)}
                            className="text-destructive focus:text-destructive"
                          >
                            <Trash2 className="mr-2 h-3.5 w-3.5" /> Sil
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          <TablePagination
            totalCount={filtered.length}
            page={safePage}
            pageSize={pageSize}
            onPageChange={setPage}
            onPageSizeChange={(size) => { setPageSize(size); setPage(1); }}
            itemLabel="derslik"
            pageSizeOptions={PAGE_SIZE_OPTIONS}
          />
        </div>
      )}

      {/* Create/Edit Dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-blue-100 text-blue-700">
                <Building2 className="h-4 w-4" />
              </span>
              {editingClassroom ? 'Derslik Düzenle' : 'Yeni Derslik Ekle'}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <label className="text-sm font-medium">Derslik Adı *</label>
              <Input
                value={form.name}
                onChange={e => setForm({ ...form, name: e.target.value })}
                placeholder="Örn: C 018, AMFİ 1, L139 Ceng Lab-1"
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <label className="text-sm font-medium">Bina</label>
                <Input
                  value={form.buildingName}
                  onChange={e => setForm({ ...form, buildingName: e.target.value })}
                  placeholder="Örn: C Blok, Ana Bina"
                  list="classroom-building-suggestions"
                />
                {/* Mevcut bina adlarını öner — "C Blok"/"C blok" gibi tutarsız yazımları azaltır. */}
                <datalist id="classroom-building-suggestions">
                  {buildings.map(b => <option key={b} value={b} />)}
                </datalist>
              </div>
              <div className="space-y-1.5">
                <label className="text-sm font-medium">Kat</label>
                <Input
                  type="number"
                  value={form.floor}
                  onChange={e => setForm({ ...form, floor: Number(e.target.value) })}
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <label className="text-sm font-medium">Kapasite *</label>
                <Input
                  type="number"
                  min={1}
                  value={form.capacity}
                  onChange={e => setForm({ ...form, capacity: Number(e.target.value) })}
                />
              </div>
              <div className="space-y-1.5">
                <label className="text-sm font-medium">Tip *</label>
                <Select value={form.type} onValueChange={v => setForm({ ...form, type: v })}>
                  <SelectTrigger className="h-9 text-sm">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {CLASSROOM_TYPE_CODES.map(t => (
                      <SelectItem key={t} value={t}>
                        <span className="inline-flex items-center gap-2">
                          <span className={`h-2 w-2 rounded-full ${getClassroomTypeBarClass(t)}`} />
                          {getClassroomTypeLabel(t)}
                        </span>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            {error && (
              <Alert variant="destructive">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)}>İptal</Button>
            <Button
              onClick={handleSubmit}
              disabled={createMutation.isPending || updateMutation.isPending}
              className="bg-gradient-to-br from-[#003366] to-[#004080] text-white hover:from-[#002a52] hover:to-[#003366]"
            >
              {createMutation.isPending || updateMutation.isPending ? 'Kaydediliyor...' : editingClassroom ? 'Güncelle' : 'Oluştur'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete confirmation dialog */}
      <Dialog open={deleteDialogOpen} onOpenChange={setDeleteDialogOpen}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-red-100 text-red-600">
                <Trash2 className="h-4 w-4" />
              </span>
              Derslik Sil
            </DialogTitle>
          </DialogHeader>
          <p className="text-sm text-slate-600">
            <strong className="text-slate-900">{deleteTarget?.name}</strong> dersliğini silmek istediğinize emin misiniz? Bu işlem geri alınamaz.
          </p>
          {deleteError && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>{deleteError}</AlertDescription>
            </Alert>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteDialogOpen(false)}>İptal</Button>
            <Button variant="destructive" onClick={handleDelete} disabled={deleteMutation.isPending}>
              {deleteMutation.isPending ? 'Siliniyor...' : 'Sil'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Schedule-impact confirmation (Type/Capacity change affects existing schedules) */}
      <AlertDialog open={impact !== null} onOpenChange={open => { if (!open) setImpact(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle className="flex items-center gap-2">
              <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-amber-100 text-amber-600">
                <AlertTriangle className="h-4 w-4" />
              </span>
              Mevcut programları etkiliyor
            </AlertDialogTitle>
            <AlertDialogDescription>
              Bu değişiklik aşağıdaki {impact?.length ?? 0} ders programı atamasını geçersiz kılabilir.
              Yine de kaydetmek programları gözden geçirmenizi gerektirir.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <div className="max-h-60 space-y-1.5 overflow-y-auto rounded-md border border-slate-200 bg-slate-50/60 p-2">
            {impact?.map((a, i) => (
              <div key={`${a.courseCode}-${a.sectionNumber}-${i}`} className="rounded bg-white px-3 py-2 text-sm shadow-sm">
                <div className="font-medium text-slate-900">
                  {a.courseCode} · {a.courseName} <span className="text-slate-400">(Şube {a.sectionNumber})</span>
                </div>
                <div className="text-xs text-amber-700">{a.reason}</div>
              </div>
            ))}
          </div>
          <AlertDialogFooter>
            <AlertDialogCancel>Vazgeç</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => { if (editingClassroom) runUpdate(editingClassroom.id, true); }}
              className="bg-amber-600 text-white hover:bg-amber-700"
            >
              Yine de Kaydet
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Analytics dialog */}
      <ClassroomAnalyticsDialog
        open={analyticsOpen}
        onOpenChange={setAnalyticsOpen}
        classrooms={all}
      />
    </div>
  );
}
