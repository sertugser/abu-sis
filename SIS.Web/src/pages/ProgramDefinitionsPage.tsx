import { useState, useMemo } from 'react';
import { Plus, Edit2, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import {
  useAdminFaculties,
  useAdminDepartmentsByStructure,
  useAdminProgramsByStructure,
  useCreateProgram,
  useUpdateProgram,
  useDeleteProgram,
  type ProgramDto,
} from '../hooks/useAdminAcademicStructure';
import {
  ProgramFormFields,
  DeleteConfirmDialog,
  DataTable,
  PageHeader,
} from '../components/academic-structure/shared';
import {
  SELECT_CLS,
  DEGREE_LEVELS,
  EDUCATION_TYPES,
  emptyProgramForm,
  type ProgramFormState,
} from '../components/academic-structure/shared-constants';

export default function ProgramDefinitionsPage() {
  const { data: faculties = [] } = useAdminFaculties();
  const [facultyFilter, setFacultyFilter] = useState<number | undefined>(undefined);
  // Fetch all departments; faculty filter narrows the dropdown client-side
  const { data: allDepartments = [] } = useAdminDepartmentsByStructure(undefined);
  const [departmentFilter, setDepartmentFilter] = useState<number | undefined>(undefined);
  // Hook only supports departmentId filter — faculty filter is client-side only
  const { data: programs = [], isLoading } = useAdminProgramsByStructure(departmentFilter);

  const createProgram = useCreateProgram();
  const updateProgram = useUpdateProgram();
  const deleteProgram = useDeleteProgram();

  const [createOpen, setCreateOpen] = useState(false);
  const [editItem, setEditItem] = useState<ProgramDto | null>(null);
  const [deleteItem, setDeleteItem] = useState<ProgramDto | null>(null);
  const [form, setForm] = useState<ProgramFormState>(emptyProgramForm());
  const [error, setError] = useState('');

  // Filter departments by selected faculty for the dropdown
  const visibleDepartments = useMemo(() => {
    if (!facultyFilter) return allDepartments;
    return allDepartments.filter(d => d.facultyId === facultyFilter);
  }, [allDepartments, facultyFilter]);

  function openCreate() {
    setForm(emptyProgramForm(departmentFilter ?? visibleDepartments[0]?.id ?? 0));
    setError('');
    setCreateOpen(true);
  }
  function openEdit(p: ProgramDto) {
    setForm({
      name: p.name, code: p.code, departmentId: p.departmentId,
      degreeLevel: p.degreeLevel, educationType: p.educationType,
      maxStudyDurationSemesters: p.maxStudyDurationSemesters,
      totalRequiredEcts: p.totalRequiredEcts,
    });
    setError('');
    setEditItem(p);
  }

  async function handleCreate() {
    if (!form.name.trim() || !form.code.trim() || !form.departmentId) { setError('Tüm alanlar zorunludur.'); return; }
    try { await createProgram.mutateAsync(form); setCreateOpen(false); }
    catch (e: unknown) { setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.'); }
  }

  async function handleUpdate() {
    if (!editItem) return;
    if (!form.name.trim() || !form.code.trim() || !form.departmentId) { setError('Tüm alanlar zorunludur.'); return; }
    try { await updateProgram.mutateAsync({ id: editItem.id, ...form }); setEditItem(null); }
    catch (e: unknown) { setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.'); }
  }

  async function handleDelete() {
    if (!deleteItem) return;
    try { await deleteProgram.mutateAsync(deleteItem.id); setDeleteItem(null); }
    catch (e: unknown) { alert((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Silinemedi.'); }
  }

  const degreeLabel = (v: string) => DEGREE_LEVELS.find(d => d.value === v)?.label ?? v;
  const eduLabel = (v: string) => EDUCATION_TYPES.find(e => e.value === v)?.label ?? v;

  return (
    <div className="p-6 max-w-6xl mx-auto">
      <PageHeader title="Program Tanımları" description="Bölümlere bağlı programları tanımlayın ve yönetin." />

      <div className="flex items-center justify-between mb-4 gap-4">
        <div className="flex items-center gap-3 flex-1 flex-wrap">
          <div className="flex items-center gap-2">
            <label className="text-sm text-muted-foreground whitespace-nowrap">Fakülte:</label>
            <select
              className={SELECT_CLS + ' max-w-xs'}
              value={facultyFilter ?? 0}
              onChange={e => {
                setFacultyFilter(Number(e.target.value) || undefined);
                setDepartmentFilter(undefined);
              }}
            >
              <option value={0}>Tümü</option>
              {faculties.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}
            </select>
          </div>
          <div className="flex items-center gap-2">
            <label className="text-sm text-muted-foreground whitespace-nowrap">Bölüm:</label>
            <select
              className={SELECT_CLS + ' max-w-xs'}
              value={departmentFilter ?? 0}
              onChange={e => setDepartmentFilter(Number(e.target.value) || undefined)}
            >
              <option value={0}>Tümü</option>
              {visibleDepartments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </div>
          <p className="text-sm text-muted-foreground">{isLoading ? 'Yükleniyor...' : `${programs.length} program`}</p>
        </div>
        <Button size="sm" onClick={openCreate} disabled={visibleDepartments.length === 0}>
          <Plus className="h-4 w-4 mr-1" /> Program Ekle
        </Button>
      </div>

      <DataTable>
        <thead className="bg-muted/40 border-b border-border">
          <tr>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Bölüm</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ad</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Kod</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Derece</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Eğitim Türü</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Süre</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">AKTS</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Müfredat</th>
            <th className="px-4 py-3" />
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {isLoading ? (
            <tr><td colSpan={9} className="px-4 py-8 text-center text-muted-foreground">Yükleniyor...</td></tr>
          ) : programs.length === 0 ? (
            <tr><td colSpan={9} className="px-4 py-8 text-center text-muted-foreground">Henüz program yok.</td></tr>
          ) : programs.map(p => (
            <tr key={p.id} className="hover:bg-muted/20 transition-colors">
              <td className="px-4 py-3 text-muted-foreground">{p.departmentName}</td>
              <td className="px-4 py-3 font-medium text-foreground">{p.name}</td>
              <td className="px-4 py-3"><Badge variant="outline">{p.code}</Badge></td>
              <td className="px-4 py-3 text-muted-foreground">{degreeLabel(p.degreeLevel)}</td>
              <td className="px-4 py-3 text-muted-foreground">{eduLabel(p.educationType)}</td>
              <td className="px-4 py-3 text-muted-foreground">{p.maxStudyDurationSemesters} dönem</td>
              <td className="px-4 py-3 text-muted-foreground">{p.totalRequiredEcts}</td>
              <td className="px-4 py-3 text-muted-foreground">{p.curriculumCount} müfredat</td>
              <td className="px-4 py-3">
                <div className="flex gap-1 justify-end">
                  <Button size="sm" variant="ghost" onClick={() => openEdit(p)}><Edit2 className="h-3.5 w-3.5" /></Button>
                  <Button size="sm" variant="ghost" onClick={() => setDeleteItem(p)}><Trash2 className="h-3.5 w-3.5 text-destructive" /></Button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </DataTable>

      <Dialog open={createOpen} onOpenChange={o => !o && setCreateOpen(false)}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>Program Ekle</DialogTitle></DialogHeader>
          <ProgramFormFields form={form} onChange={f => { setForm(f); setError(''); }} departments={visibleDepartments} />
          {error && <p className="text-sm text-destructive">{error}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button onClick={handleCreate} disabled={createProgram.isPending}>{createProgram.isPending ? 'Kaydediliyor...' : 'Kaydet'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!editItem} onOpenChange={o => !o && setEditItem(null)}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>Program Düzenle</DialogTitle></DialogHeader>
          <ProgramFormFields form={form} onChange={f => { setForm(f); setError(''); }} departments={allDepartments} />
          {error && <p className="text-sm text-destructive">{error}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditItem(null)}>İptal</Button>
            <Button onClick={handleUpdate} disabled={updateProgram.isPending}>{updateProgram.isPending ? 'Kaydediliyor...' : 'Kaydet'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <DeleteConfirmDialog open={!!deleteItem} onClose={() => setDeleteItem(null)} onConfirm={handleDelete} name={deleteItem?.name ?? ''} isPending={deleteProgram.isPending} />
    </div>
  );
}
