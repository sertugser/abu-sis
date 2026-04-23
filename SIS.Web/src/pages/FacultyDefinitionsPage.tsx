import { useState } from 'react';
import { Plus, Edit2, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import {
  useAdminFaculties,
  useCreateFaculty,
  useUpdateFaculty,
  useDeleteFaculty,
  type FacultyDto,
} from '../hooks/useAdminAcademicStructure';
import {
  FacultyFormFields,
  DeleteConfirmDialog,
  DataTable,
  PageHeader,
} from '../components/academic-structure/shared';
import {
  emptyFacultyForm,
  type FacultyFormState,
} from '../components/academic-structure/shared-constants';

export default function FacultyDefinitionsPage() {
  const { data: faculties = [], isLoading } = useAdminFaculties();
  const createFaculty = useCreateFaculty();
  const updateFaculty = useUpdateFaculty();
  const deleteFaculty = useDeleteFaculty();

  const [createOpen, setCreateOpen] = useState(false);
  const [editItem, setEditItem] = useState<FacultyDto | null>(null);
  const [deleteItem, setDeleteItem] = useState<FacultyDto | null>(null);
  const [form, setForm] = useState<FacultyFormState>(emptyFacultyForm());
  const [error, setError] = useState('');

  function openCreate() { setForm(emptyFacultyForm()); setError(''); setCreateOpen(true); }
  function openEdit(f: FacultyDto) { setForm({ name: f.name, code: f.code }); setError(''); setEditItem(f); }

  async function handleCreate() {
    if (!form.name.trim() || !form.code.trim()) { setError('Ad ve kod zorunludur.'); return; }
    try { await createFaculty.mutateAsync(form); setCreateOpen(false); }
    catch (e: unknown) { setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.'); }
  }

  async function handleUpdate() {
    if (!editItem) return;
    if (!form.name.trim() || !form.code.trim()) { setError('Ad ve kod zorunludur.'); return; }
    try { await updateFaculty.mutateAsync({ id: editItem.id, ...form }); setEditItem(null); }
    catch (e: unknown) { setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.'); }
  }

  async function handleDelete() {
    if (!deleteItem) return;
    try { await deleteFaculty.mutateAsync(deleteItem.id); setDeleteItem(null); }
    catch (e: unknown) { alert((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Silinemedi.'); }
  }

  return (
    <div className="p-6 max-w-5xl mx-auto">
      <PageHeader title="Fakülte Tanımları" description="Üniversitenin fakültelerini tanımlayın ve yönetin." />

      <div className="flex items-center justify-between mb-4">
        <p className="text-sm text-muted-foreground">{isLoading ? 'Yükleniyor...' : `${faculties.length} fakülte`}</p>
        <Button size="sm" onClick={openCreate}><Plus className="h-4 w-4 mr-1" /> Fakülte Ekle</Button>
      </div>

      <DataTable>
        <thead className="bg-muted/40 border-b border-border">
          <tr>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ad</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Kod</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Bölüm</th>
            <th className="px-4 py-3" />
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {isLoading ? (
            <tr><td colSpan={4} className="px-4 py-8 text-center text-muted-foreground">Yükleniyor...</td></tr>
          ) : faculties.length === 0 ? (
            <tr><td colSpan={4} className="px-4 py-8 text-center text-muted-foreground">Henüz fakülte yok.</td></tr>
          ) : faculties.map(f => (
            <tr key={f.id} className="hover:bg-muted/20 transition-colors">
              <td className="px-4 py-3 font-medium text-foreground">{f.name}</td>
              <td className="px-4 py-3"><Badge variant="outline">{f.code}</Badge></td>
              <td className="px-4 py-3 text-muted-foreground">{f.departmentCount} bölüm</td>
              <td className="px-4 py-3">
                <div className="flex gap-1 justify-end">
                  <Button size="sm" variant="ghost" onClick={() => openEdit(f)}><Edit2 className="h-3.5 w-3.5" /></Button>
                  <Button size="sm" variant="ghost" onClick={() => setDeleteItem(f)}><Trash2 className="h-3.5 w-3.5 text-destructive" /></Button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </DataTable>

      <Dialog open={createOpen} onOpenChange={o => !o && setCreateOpen(false)}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>Fakülte Ekle</DialogTitle></DialogHeader>
          <FacultyFormFields form={form} onChange={f => { setForm(f); setError(''); }} />
          {error && <p className="text-sm text-destructive">{error}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button onClick={handleCreate} disabled={createFaculty.isPending}>{createFaculty.isPending ? 'Kaydediliyor...' : 'Kaydet'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!editItem} onOpenChange={o => !o && setEditItem(null)}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>Fakülte Düzenle</DialogTitle></DialogHeader>
          <FacultyFormFields form={form} onChange={f => { setForm(f); setError(''); }} />
          {error && <p className="text-sm text-destructive">{error}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditItem(null)}>İptal</Button>
            <Button onClick={handleUpdate} disabled={updateFaculty.isPending}>{updateFaculty.isPending ? 'Kaydediliyor...' : 'Kaydet'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <DeleteConfirmDialog open={!!deleteItem} onClose={() => setDeleteItem(null)} onConfirm={handleDelete} name={deleteItem?.name ?? ''} isPending={deleteFaculty.isPending} />
    </div>
  );
}
