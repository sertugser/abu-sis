import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { AlertCircle, Edit2, Plus, Trash2 } from 'lucide-react';
import { useClassrooms, useCreateClassroom, useUpdateClassroom, useDeleteClassroom } from '../hooks/useClassrooms';
import type { ClassroomDto, CreateClassroomDto } from '../api/classroom.api';

const CLASSROOM_TYPES = ['Lecture', 'Lab', 'Computer', 'Seminar', 'Amphitheater'] as const;

const TYPE_LABELS: Record<string, string> = {
  Lecture: 'Normal Sinif',
  Lab: 'Laboratuvar',
  Computer: 'Bilgisayar Lab',
  Seminar: 'Seminer',
  Amphitheater: 'Amfi',
};

const TYPE_VARIANTS: Record<string, 'default' | 'secondary' | 'outline'> = {
  Lecture: 'default',
  Lab: 'secondary',
  Computer: 'secondary',
  Seminar: 'outline',
  Amphitheater: 'default',
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

  const handleSubmit = () => {
    setError('');
    if (!form.name.trim()) {
      setError('Derslik adi zorunludur.');
      return;
    }
    if (!form.buildingName.trim()) {
      setError('Bina adi zorunludur.');
      return;
    }
    if (form.capacity <= 0) {
      setError('Kapasite sifirdan buyuk olmalidir.');
      return;
    }

    if (editingClassroom) {
      updateMutation.mutate(
        { id: editingClassroom.id, dto: form },
        {
          onSuccess: () => setDialogOpen(false),
          onError: () => setError('Guncelleme basarisiz oldu.'),
        },
      );
    } else {
      createMutation.mutate(form, {
        onSuccess: () => setDialogOpen(false),
        onError: () => setError('Olusturma basarisiz oldu.'),
      });
    }
  };

  const handleDelete = (id: number) => {
    if (confirm('Bu dersligi silmek istediginize emin misiniz?')) {
      deleteMutation.mutate(id);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold">Derslik Yonetimi</h1>
        <Button size="sm" onClick={openCreate}>
          <Plus className="h-4 w-4 mr-1" /> Ekle
        </Button>
      </div>

      {/* Type filter */}
      <div className="flex items-center gap-3">
        <label className="text-sm font-medium">Tip:</label>
        <select
          className="h-9 rounded-md border border-input bg-background px-3 text-sm min-w-[200px]"
          value={filterType ?? ''}
          onChange={e => setFilterType(e.target.value || undefined)}
        >
          <option value="">Tumu</option>
          {CLASSROOM_TYPES.map(t => (
            <option key={t} value={t}>{TYPE_LABELS[t]}</option>
          ))}
        </select>
      </div>

      {/* Classrooms table */}
      {isLoading ? (
        <Skeleton className="h-48 rounded-xl" />
      ) : isError ? (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Derslikler yuklenemedi.</AlertDescription>
        </Alert>
      ) : (
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm">
              {(classrooms ?? []).length} derslik
            </CardTitle>
          </CardHeader>
          <CardContent>
            {(classrooms ?? []).length === 0 ? (
              <div className="text-center py-8 text-muted-foreground">
                Henuz derslik tanimlanmamis.
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Derslik Adi</TableHead>
                    <TableHead>Bina</TableHead>
                    <TableHead>Kat</TableHead>
                    <TableHead>Kapasite</TableHead>
                    <TableHead>Tip</TableHead>
                    <TableHead className="w-24">Islemler</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {(classrooms ?? []).map(c => (
                    <TableRow key={c.id}>
                      <TableCell className="font-medium">{c.name}</TableCell>
                      <TableCell>{c.buildingName}</TableCell>
                      <TableCell className="font-mono">{c.floor}</TableCell>
                      <TableCell className="font-mono">{c.capacity}</TableCell>
                      <TableCell>
                        <Badge variant={TYPE_VARIANTS[c.type] ?? 'outline'}>
                          {TYPE_LABELS[c.type] ?? c.type}
                        </Badge>
                      </TableCell>
                      <TableCell>
                        <div className="flex gap-1">
                          <Button size="icon" variant="ghost" className="h-7 w-7" onClick={() => openEdit(c)}>
                            <Edit2 className="h-4 w-4" />
                          </Button>
                          <Button
                            size="icon"
                            variant="ghost"
                            className="h-7 w-7 text-destructive"
                            onClick={() => handleDelete(c.id)}
                            disabled={deleteMutation.isPending}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      )}

      {/* Create/Edit Dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editingClassroom ? 'Derslik Duzenle' : 'Yeni Derslik Ekle'}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-1">
              <label className="text-xs font-medium">Derslik Adi</label>
              <Input
                value={form.name}
                onChange={e => setForm({ ...form, name: e.target.value })}
                placeholder="orn: A-101"
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <label className="text-xs font-medium">Bina</label>
                <Input
                  value={form.buildingName}
                  onChange={e => setForm({ ...form, buildingName: e.target.value })}
                  placeholder="orn: A Blok"
                />
              </div>
              <div className="space-y-1">
                <label className="text-xs font-medium">Kat</label>
                <Input
                  type="number"
                  value={form.floor}
                  onChange={e => setForm({ ...form, floor: Number(e.target.value) })}
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <label className="text-xs font-medium">Kapasite</label>
                <Input
                  type="number"
                  min={1}
                  value={form.capacity}
                  onChange={e => setForm({ ...form, capacity: Number(e.target.value) })}
                />
              </div>
              <div className="space-y-1">
                <label className="text-xs font-medium">Tip</label>
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
            <Button variant="outline" onClick={() => setDialogOpen(false)}>Iptal</Button>
            <Button
              onClick={handleSubmit}
              disabled={createMutation.isPending || updateMutation.isPending}
            >
              {editingClassroom ? 'Guncelle' : 'Olustur'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
