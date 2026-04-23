import { useState } from 'react';
import { Plus, Edit2, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import {
  useAdminFaculties,
  useAdminDepartmentsByStructure,
  useCreateDepartment,
  useUpdateDepartment,
  useDeleteDepartment,
  type DepartmentDto,
} from '../hooks/useAdminAcademicStructure';
import {
  DepartmentFormFields,
  DeleteConfirmDialog,
  DataTable,
  PageHeader,
} from '../components/academic-structure/shared';
import {
  SELECT_CLS,
  emptyDepartmentForm,
  type DepartmentFormState,
} from '../components/academic-structure/shared-constants';

export default function DepartmentDefinitionsPage() {
  const { data: faculties = [] } = useAdminFaculties();
  const [facultyFilter, setFacultyFilter] = useState<number | undefined>(undefined);
  const { data: departments = [], isLoading } = useAdminDepartmentsByStructure(facultyFilter);
  const createDept = useCreateDepartment();
  const updateDept = useUpdateDepartment();
  const deleteDept = useDeleteDepartment();

  const [createOpen, setCreateOpen] = useState(false);
  const [editItem, setEditItem] = useState<DepartmentDto | null>(null);
  const [deleteItem, setDeleteItem] = useState<DepartmentDto | null>(null);
  const [form, setForm] = useState<DepartmentFormState>(emptyDepartmentForm());
  const [error, setError] = useState('');

  function openCreate() {
    setForm(emptyDepartmentForm(facultyFilter ?? faculties[0]?.id ?? 0));
    setError('');
    setCreateOpen(true);
  }
  function openEdit(d: DepartmentDto) {
    setForm({ name: d.name, code: d.code, facultyId: d.facultyId });
    setError('');
    setEditItem(d);
  }

  async function handleCreate() {
    if (!form.name.trim() || !form.code.trim() || !form.facultyId) { setError('Tüm alanlar zorunludur.'); return; }
    try { await createDept.mutateAsync(form); setCreateOpen(false); }
    catch (e: unknown) { setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.'); }
  }

  async function handleUpdate() {
    if (!editItem) return;
    if (!form.name.trim() || !form.code.trim() || !form.facultyId) { setError('Tüm alanlar zorunludur.'); return; }
    try { await updateDept.mutateAsync({ id: editItem.id, ...form }); setEditItem(null); }
    catch (e: unknown) { setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.'); }
  }

  async function handleDelete() {
    if (!deleteItem) return;
    try { await deleteDept.mutateAsync(deleteItem.id); setDeleteItem(null); }
    catch (e: unknown) { alert((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Silinemedi.'); }
  }

  return (
    <div className="p-6 max-w-5xl mx-auto">
      <PageHeader title="Bölüm Tanımları" description="Fakültelere bağlı bölümleri tanımlayın ve yönetin." />

      <div className="flex items-center justify-between mb-4 gap-4">
        <div className="flex items-center gap-3 flex-1">
          <label className="text-sm text-muted-foreground whitespace-nowrap">Fakülte:</label>
          <select
            className={SELECT_CLS + ' max-w-xs'}
            value={facultyFilter ?? 0}
            onChange={e => setFacultyFilter(Number(e.target.value) || undefined)}
          >
            <option value={0}>Tümü</option>
            {faculties.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}
          </select>
          <p className="text-sm text-muted-foreground">{isLoading ? 'Yükleniyor...' : `${departments.length} bölüm`}</p>
        </div>
        <Button size="sm" onClick={openCreate} disabled={faculties.length === 0}>
          <Plus className="h-4 w-4 mr-1" /> Bölüm Ekle
        </Button>
      </div>

      <DataTable>
        <thead className="bg-muted/40 border-b border-border">
          <tr>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Fakülte</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ad</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Kod</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Program</th>
            <th className="px-4 py-3" />
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {isLoading ? (
            <tr><td colSpan={5} className="px-4 py-8 text-center text-muted-foreground">Yükleniyor...</td></tr>
          ) : departments.length === 0 ? (
            <tr><td colSpan={5} className="px-4 py-8 text-center text-muted-foreground">Henüz bölüm yok.</td></tr>
          ) : departments.map(d => (
            <tr key={d.id} className="hover:bg-muted/20 transition-colors">
              <td className="px-4 py-3 text-muted-foreground">{d.facultyName}</td>
              <td className="px-4 py-3 font-medium text-foreground">{d.name}</td>
              <td className="px-4 py-3"><Badge variant="outline">{d.code}</Badge></td>
              <td className="px-4 py-3 text-muted-foreground">{d.programCount} program</td>
              <td className="px-4 py-3">
                <div className="flex gap-1 justify-end">
                  <Button size="sm" variant="ghost" onClick={() => openEdit(d)}><Edit2 className="h-3.5 w-3.5" /></Button>
                  <Button size="sm" variant="ghost" onClick={() => setDeleteItem(d)}><Trash2 className="h-3.5 w-3.5 text-destructive" /></Button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </DataTable>

      <Dialog open={createOpen} onOpenChange={o => !o && setCreateOpen(false)}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>Bölüm Ekle</DialogTitle></DialogHeader>
          <DepartmentFormFields form={form} onChange={f => { setForm(f); setError(''); }} faculties={faculties} />
          {error && <p className="text-sm text-destructive">{error}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button onClick={handleCreate} disabled={createDept.isPending}>{createDept.isPending ? 'Kaydediliyor...' : 'Kaydet'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!editItem} onOpenChange={o => !o && setEditItem(null)}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>Bölüm Düzenle</DialogTitle></DialogHeader>
          <DepartmentFormFields form={form} onChange={f => { setForm(f); setError(''); }} faculties={faculties} />
          {error && <p className="text-sm text-destructive">{error}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditItem(null)}>İptal</Button>
            <Button onClick={handleUpdate} disabled={updateDept.isPending}>{updateDept.isPending ? 'Kaydediliyor...' : 'Kaydet'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <DeleteConfirmDialog open={!!deleteItem} onClose={() => setDeleteItem(null)} onConfirm={handleDelete} name={deleteItem?.name ?? ''} isPending={deleteDept.isPending} />
    </div>
  );
}
