import { useMemo, useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
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
  AlertCircle, BarChart3, Building2, Edit2, MoreHorizontal,
  Plus, Search, Trash2,
} from 'lucide-react';
import { useClassrooms, useCreateClassroom, useUpdateClassroom, useDeleteClassroom } from '../hooks/useClassrooms';
import type { ClassroomDto, CreateClassroomDto } from '../api/classroom.api';
import ClassroomAnalyticsSheet from '../components/classroom/ClassroomAnalyticsSheet';
import {
  CLASSROOM_TYPE_CODES,
  getClassroomTypeLabel,
  getClassroomTypeBadgeClass,
} from '../lib/classroomTypeUtils';

const emptyForm: CreateClassroomDto = {
  name: '',
  buildingName: '',
  floor: 0,
  capacity: 30,
  type: 'Lecture',
};

function formatLocation(buildingName: string | null | undefined, floor: number | null | undefined): string {
  const building = buildingName?.trim();
  const hasFloor = floor !== null && floor !== undefined;
  const floorLabel = hasFloor ? (floor === 0 ? 'Zemin Kat' : `Kat ${floor}`) : null;

  if (building && floorLabel) return `${building} · ${floorLabel}`;
  if (building) return building;
  if (floorLabel) return floorLabel;
  return '—';
}

export default function ClassroomManagementAdminPage() {
  const { data: classrooms, isLoading, isError } = useClassrooms();
  const createMutation = useCreateClassroom();
  const updateMutation = useUpdateClassroom();
  const deleteMutation = useDeleteClassroom();

  const [filterType, setFilterType] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [analyticsOpen, setAnalyticsOpen] = useState(false);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<ClassroomDto | null>(null);
  const [editingClassroom, setEditingClassroom] = useState<ClassroomDto | null>(null);
  const [form, setForm] = useState<CreateClassroomDto>(emptyForm);
  const [error, setError] = useState('');

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
    setDeleteDialogOpen(true);
  };

  const handleDelete = () => {
    if (!deleteTarget) return;
    deleteMutation.mutate(deleteTarget.id, {
      onSuccess: () => { setDeleteDialogOpen(false); setDeleteTarget(null); },
    });
  };

  const handleSubmit = () => {
    setError('');
    if (!form.name.trim()) { setError('Derslik adı zorunludur.'); return; }
    if (form.capacity <= 0) { setError('Kapasite sıfırdan büyük olmalıdır.'); return; }

    if (editingClassroom) {
      updateMutation.mutate(
        { id: editingClassroom.id, dto: form },
        { onSuccess: () => setDialogOpen(false), onError: (e: unknown) => setError((e as { response?: { data?: { error?: string } } })?.response?.data?.error || 'Güncelleme başarısız.') },
      );
    } else {
      createMutation.mutate(form, {
        onSuccess: () => setDialogOpen(false),
        onError: (e: unknown) => setError((e as { response?: { data?: { error?: string } } })?.response?.data?.error || 'Oluşturma başarısız.'),
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

  const filtered = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    return all.filter(c => {
      if (filterType !== 'all' && c.type !== filterType) return false;
      if (q) {
        const haystack = `${c.name} ${c.buildingName ?? ''}`.toLowerCase();
        if (!haystack.includes(q)) return false;
      }
      return true;
    });
  }, [all, filterType, searchQuery]);

  const isFiltered = filterType !== 'all' || searchQuery.trim().length > 0;

  return (
    <div className="space-y-4">
      {/* Compact toolbar: count · filter · search · analiz · yeni */}
      <div className="flex flex-wrap items-center gap-2 bg-white border rounded-xl px-3 py-2">
        <div className="flex items-center gap-2 pr-3 mr-1 border-r">
          <Building2 className="h-4 w-4 text-[#003366]" />
          <span className="text-sm font-semibold tabular-nums">
            {isFiltered ? `${filtered.length} / ${all.length}` : all.length}
          </span>
          <span className="text-xs text-muted-foreground">derslik</span>
        </div>

        <Select value={filterType} onValueChange={setFilterType}>
          <SelectTrigger className="h-8 w-[180px] text-xs">
            <SelectValue placeholder="Tüm Tipler" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Tüm Tipler ({all.length})</SelectItem>
            {CLASSROOM_TYPE_CODES.map(t => (
              <SelectItem key={t} value={t}>
                {getClassroomTypeLabel(t)} ({typeCounts[t] || 0})
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <div className="relative flex-1 min-w-[160px] max-w-[280px]">
          <Search className="h-3.5 w-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder="Derslik adı veya bina ara..."
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            className="h-8 pl-8 text-xs"
          />
        </div>

        <div className="ml-auto flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setAnalyticsOpen(true)}
            className="gap-1.5 h-8"
            disabled={all.length === 0}
          >
            <BarChart3 className="h-4 w-4" /> Analiz
          </Button>
          <Button onClick={openCreate} size="sm" className="gap-1.5 h-8">
            <Plus className="h-4 w-4" /> Yeni Derslik
          </Button>
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
        <div className="text-center py-16 bg-white border rounded-xl">
          <Building2 className="h-12 w-12 mx-auto text-gray-300 mb-3" />
          <p className="text-muted-foreground">Henüz derslik tanımlanmamış.</p>
          <Button variant="outline" className="mt-4" onClick={openCreate}>
            <Plus className="h-4 w-4 mr-1" /> İlk Dersliği Ekle
          </Button>
        </div>
      ) : filtered.length === 0 ? (
        <div className="text-center py-12 bg-white border rounded-xl">
          <Search className="h-10 w-10 mx-auto text-gray-300 mb-3" />
          <p className="text-sm text-muted-foreground">Filtrelere uyan derslik bulunamadı.</p>
          <Button
            variant="ghost"
            size="sm"
            className="mt-3"
            onClick={() => { setFilterType('all'); setSearchQuery(''); }}
          >
            Filtreleri Temizle
          </Button>
        </div>
      ) : (
        <div className="bg-white border rounded-xl overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow className="bg-gray-50/50">
                <TableHead className="font-semibold">Derslik Adı</TableHead>
                <TableHead className="font-semibold">Konum</TableHead>
                <TableHead className="font-semibold text-center w-32">Kapasite</TableHead>
                <TableHead className="font-semibold">Derslik Tipi</TableHead>
                <TableHead className="font-semibold text-right w-12"></TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.map(c => {
                const location = formatLocation(c.buildingName, c.floor);
                return (
                  <TableRow key={c.id} className="group hover:bg-gray-50/50">
                    <TableCell className="font-medium">{c.name}</TableCell>
                    <TableCell className="text-sm text-muted-foreground">{location}</TableCell>
                    <TableCell className="text-center">
                      <span className="font-mono font-semibold">{c.capacity}</span>
                      <span className="text-xs text-muted-foreground ml-1">kişi</span>
                    </TableCell>
                    <TableCell>
                      <span className={`text-xs px-2.5 py-1 rounded-full border ${getClassroomTypeBadgeClass(c.type)}`}>
                        {getClassroomTypeLabel(c.type)}
                      </span>
                    </TableCell>
                    <TableCell className="text-right">
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button
                            size="icon"
                            variant="ghost"
                            className="h-8 w-8 opacity-0 group-hover:opacity-100 data-[state=open]:opacity-100 transition-opacity"
                            aria-label={`${c.name} işlemleri`}
                          >
                            <MoreHorizontal className="h-4 w-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end" className="w-40">
                          <DropdownMenuItem onClick={() => openEdit(c)}>
                            <Edit2 className="h-3.5 w-3.5 mr-2" /> Düzenle
                          </DropdownMenuItem>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem
                            onClick={() => confirmDelete(c)}
                            className="text-destructive focus:text-destructive"
                          >
                            <Trash2 className="h-3.5 w-3.5 mr-2" /> Sil
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>
      )}

      {/* Create/Edit Dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{editingClassroom ? 'Derslik Düzenle' : 'Yeni Derslik Ekle'}</DialogTitle>
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
                />
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
                  type="number" min={1}
                  value={form.capacity}
                  onChange={e => setForm({ ...form, capacity: Number(e.target.value) })}
                />
              </div>
              <div className="space-y-1.5">
                <label className="text-sm font-medium">Tip *</label>
                <select
                  className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                  value={form.type}
                  onChange={e => setForm({ ...form, type: e.target.value })}
                >
                  {CLASSROOM_TYPE_CODES.map(t => (
                    <option key={t} value={t}>{getClassroomTypeLabel(t)}</option>
                  ))}
                </select>
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
            <DialogTitle>Derslik Sil</DialogTitle>
          </DialogHeader>
          <p className="text-sm text-muted-foreground">
            <strong>{deleteTarget?.name}</strong> dersliğini silmek istediğinize emin misiniz? Bu işlem geri alınamaz.
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteDialogOpen(false)}>İptal</Button>
            <Button variant="destructive" onClick={handleDelete} disabled={deleteMutation.isPending}>
              {deleteMutation.isPending ? 'Siliniyor...' : 'Sil'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Analytics sheet */}
      <ClassroomAnalyticsSheet
        open={analyticsOpen}
        onOpenChange={setAnalyticsOpen}
        classrooms={all}
      />
    </div>
  );
}
