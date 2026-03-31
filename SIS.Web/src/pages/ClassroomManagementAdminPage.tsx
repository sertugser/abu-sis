import { useState } from 'react';
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
import { AlertCircle, Edit2, Plus, Trash2, Building2, Users, Layers } from 'lucide-react';
import { useClassrooms, useCreateClassroom, useUpdateClassroom, useDeleteClassroom } from '../hooks/useClassrooms';
import type { ClassroomDto, CreateClassroomDto } from '../api/classroom.api';

const CLASSROOM_TYPES = ['Lecture', 'Lab', 'Computer', 'Seminar', 'Amphitheater'] as const;

const TYPE_LABELS: Record<string, string> = {
  Lecture: 'Normal Sınıf',
  Lab: 'Laboratuvar',
  Computer: 'Bilgisayar Lab',
  Seminar: 'Seminer',
  Amphitheater: 'Amfi',
};

const TYPE_COLORS: Record<string, string> = {
  Lecture: 'bg-blue-100 text-blue-800 border-blue-200',
  Lab: 'bg-emerald-100 text-emerald-800 border-emerald-200',
  Computer: 'bg-violet-100 text-violet-800 border-violet-200',
  Seminar: 'bg-amber-100 text-amber-800 border-amber-200',
  Amphitheater: 'bg-rose-100 text-rose-800 border-rose-200',
};

const emptyForm: CreateClassroomDto = {
  name: '',
  buildingName: '',
  floor: 0,
  capacity: 30,
  type: 'Lecture',
};

export default function ClassroomManagementAdminPage() {
  const [filterType, setFilterType] = useState<string | undefined>();
  const { data: classrooms, isLoading, isError } = useClassrooms(filterType);
  const createMutation = useCreateClassroom();
  const updateMutation = useUpdateClassroom();
  const deleteMutation = useDeleteClassroom();

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
        { onSuccess: () => setDialogOpen(false), onError: (e: any) => setError(e?.response?.data?.error || 'Güncelleme başarısız.') },
      );
    } else {
      createMutation.mutate(form, {
        onSuccess: () => setDialogOpen(false),
        onError: (e: any) => setError(e?.response?.data?.error || 'Oluşturma başarısız.'),
      });
    }
  };

  // Stats
  const all = classrooms ?? [];
  const totalCapacity = all.reduce((sum, c) => sum + c.capacity, 0);
  const typeGroups = all.reduce((acc, c) => {
    acc[c.type] = (acc[c.type] || 0) + 1;
    return acc;
  }, {} as Record<string, number>);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-[#003366]">Derslik Yönetimi</h1>
          <p className="text-sm text-muted-foreground mt-1">Derslikleri ekleyin, düzenleyin ve yönetin</p>
        </div>
        <Button onClick={openCreate} className="gap-1.5">
          <Plus className="h-4 w-4" /> Yeni Derslik
        </Button>
      </div>

      {/* Summary cards */}
      {!isLoading && !isError && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div className="bg-white border rounded-xl p-4 flex items-center gap-3">
            <div className="bg-blue-50 p-2.5 rounded-lg">
              <Building2 className="h-5 w-5 text-blue-600" />
            </div>
            <div>
              <p className="text-2xl font-bold">{all.length}</p>
              <p className="text-xs text-muted-foreground">Toplam Derslik</p>
            </div>
          </div>
          <div className="bg-white border rounded-xl p-4 flex items-center gap-3">
            <div className="bg-emerald-50 p-2.5 rounded-lg">
              <Users className="h-5 w-5 text-emerald-600" />
            </div>
            <div>
              <p className="text-2xl font-bold">{totalCapacity}</p>
              <p className="text-xs text-muted-foreground">Toplam Kapasite</p>
            </div>
          </div>
          <div className="bg-white border rounded-xl p-4 flex items-center gap-3">
            <div className="bg-violet-50 p-2.5 rounded-lg">
              <Layers className="h-5 w-5 text-violet-600" />
            </div>
            <div>
              <p className="text-2xl font-bold">{Object.keys(typeGroups).length}</p>
              <p className="text-xs text-muted-foreground">Derslik Tipi</p>
            </div>
          </div>
          <div className="bg-white border rounded-xl p-4">
            <div className="flex flex-wrap gap-1.5">
              {Object.entries(typeGroups).map(([type, count]) => (
                <span key={type} className={`text-xs px-2 py-0.5 rounded-full border ${TYPE_COLORS[type] || 'bg-gray-100'}`}>
                  {TYPE_LABELS[type] || type}: {count}
                </span>
              ))}
            </div>
            <p className="text-xs text-muted-foreground mt-2">Tip Dağılımı</p>
          </div>
        </div>
      )}

      {/* Filter */}
      <div className="flex items-center gap-2">
        <button
          onClick={() => setFilterType(undefined)}
          className={`px-3 py-1.5 text-xs rounded-full border transition ${!filterType ? 'bg-[#003366] text-white border-[#003366]' : 'bg-white text-gray-600 hover:bg-gray-50'}`}
        >
          Tümü ({all.length})
        </button>
        {CLASSROOM_TYPES.map(t => {
          const count = typeGroups[t] || 0;
          return (
            <button
              key={t}
              onClick={() => setFilterType(filterType === t ? undefined : t)}
              className={`px-3 py-1.5 text-xs rounded-full border transition ${filterType === t ? 'bg-[#003366] text-white border-[#003366]' : 'bg-white text-gray-600 hover:bg-gray-50'}`}
            >
              {TYPE_LABELS[t]} {count > 0 && `(${count})`}
            </button>
          );
        })}
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
      ) : (
        <div className="bg-white border rounded-xl overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow className="bg-gray-50/50">
                <TableHead className="font-semibold">Derslik Adı</TableHead>
                <TableHead className="font-semibold">Bina</TableHead>
                <TableHead className="font-semibold text-center">Kat</TableHead>
                <TableHead className="font-semibold text-center">Kapasite</TableHead>
                <TableHead className="font-semibold">Tip</TableHead>
                <TableHead className="font-semibold text-right w-24">İşlem</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {all.map(c => (
                <TableRow key={c.id} className="group hover:bg-gray-50/50">
                  <TableCell className="font-medium">{c.name}</TableCell>
                  <TableCell className="text-muted-foreground">{c.buildingName || '—'}</TableCell>
                  <TableCell className="text-center font-mono text-muted-foreground">{c.floor || '—'}</TableCell>
                  <TableCell className="text-center">
                    <span className="font-mono font-semibold">{c.capacity}</span>
                    <span className="text-xs text-muted-foreground ml-1">kişi</span>
                  </TableCell>
                  <TableCell>
                    <span className={`text-xs px-2.5 py-1 rounded-full border ${TYPE_COLORS[c.type] || 'bg-gray-100'}`}>
                      {TYPE_LABELS[c.type] || c.type}
                    </span>
                  </TableCell>
                  <TableCell className="text-right">
                    <div className="flex gap-0.5 justify-end opacity-0 group-hover:opacity-100 transition-opacity">
                      <Button size="icon" variant="ghost" className="h-7 w-7" onClick={() => openEdit(c)}>
                        <Edit2 className="h-3.5 w-3.5" />
                      </Button>
                      <Button
                        size="icon" variant="ghost"
                        className="h-7 w-7 text-destructive hover:text-destructive"
                        onClick={() => confirmDelete(c)}
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
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
                  {CLASSROOM_TYPES.map(t => (
                    <option key={t} value={t}>{TYPE_LABELS[t]}</option>
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
    </div>
  );
}
